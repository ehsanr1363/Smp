using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace SMP.Forms
{
    public class ProjectProfitLossDashboardForm : Form
    {
        private readonly string _connectionString;
        private readonly ClientContractRepository _contractRepo;

        private TextBox txtSearch = null!;
        private Button btnRefresh = null!;
        private FlowLayoutPanel pnlCards = null!;

        private List<ProjectProfitLossDto> _allProjects = new();
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public ProjectProfitLossDashboardForm(string connectionString)
        {
            _connectionString = connectionString;
            _contractRepo = new ClientContractRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("داشبورد سود و زیان پروژه‌ها", new Size(1350, 850));

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnRefresh);

            Shown += async (s, e) => await LoadDataAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _loadLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var pnlTop = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(15, 10, 15, 10) };
            var topFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };

            var lblTitle = new Label
            {
                Text = "📊 تحلیل سود و زیان قراردادهای کارفرما",
                AutoSize = true,
                Font = UIHelper.PersianFont(14f, FontStyle.Bold),
                ForeColor = UIHelper.SectionBlue,
                Margin = new Padding(0, 8, 20, 5)
            };

            btnRefresh = UIHelper.CreateButton("🔄 بروزرسانی", UIHelper.SectionGray, 120, 35);
            btnRefresh.Margin = new Padding(10, 3, 10, 5);
            btnRefresh.Click += async (s, e) => await LoadDataAsync();

            txtSearch = new TextBox
            {
                Width = 250,
                Font = UIHelper.PersianFont(11f),
                Margin = new Padding(10, 8, 5, 5),
                PlaceholderText = "جستجوی شماره یا موضوع قرارداد...",
                BackColor = UIHelper.WarmInputBackground,
                BorderStyle = BorderStyle.FixedSingle
            };
            txtSearch.TextChanged += (s, e) => ApplyFilter();

            topFlow.Controls.Add(lblTitle);
            topFlow.Controls.Add(btnRefresh);
            topFlow.Controls.Add(txtSearch);
            pnlTop.Controls.Add(topFlow);

            pnlCards = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(15)
            };

            pnlCards.SizeChanged += (s, e) =>
            {
                foreach (Control c in pnlCards.Controls) c.Width = pnlCards.ClientSize.Width - 40;
            };

            mainLayout.Controls.Add(pnlTop, 0, 0);
            mainLayout.Controls.Add(pnlCards, 0, 1);
            this.Controls.Add(mainLayout);
        }

        private async Task LoadDataAsync()
        {
            if (IsDisposed) return;
            if (!await _loadLock.WaitAsync(0)) return;

            try
            {
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    // ✅ اصلاح شد: اضافه کردن هزینه‌های اداری که به پروژه متصل شده‌اند
                    string query = @"
                        SELECT 
                            c.Id AS ContractId,
                            c.ContractNumber,
                            c.Subject,
                            c.Amount AS ContractAmount,
                            
                            (ISNULL((SELECT SUM(Amount) FROM CashTransactions 
                                     WHERE ContractId = c.Id AND Direction = N'پرداختی' AND PaymentScope = N'DirectPurchase'), 0) +
                             ISNULL((SELECT SUM(Amount) FROM CashChecks 
                                     WHERE ContractId = c.Id AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND PaymentScope = N'DirectPurchase'), 0)
                            ) AS DirectPurchase,

                            (ISNULL((SELECT SUM(Amount) FROM CashTransactions 
                                     WHERE ContractId = c.Id AND Direction = N'پرداختی' AND PaymentScope = N'InformalContractor'), 0) +
                             ISNULL((SELECT SUM(Amount) FROM CashChecks 
                                     WHERE ContractId = c.Id AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND PaymentScope = N'InformalContractor'), 0)
                            ) AS InformalContractor,

                            (ISNULL((SELECT SUM(Amount) FROM CashTransactions 
                                     WHERE ContractId = c.Id AND Direction = N'پرداختی' AND PaymentScope = N'ContractorContract'), 0) +
                             ISNULL((SELECT SUM(Amount) FROM CashChecks 
                                     WHERE ContractId = c.Id AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND PaymentScope = N'ContractorContract'), 0)
                            ) AS FormalContractor,

                            ISNULL((SELECT SUM(Amount) FROM ProjectExpenses WHERE ClientContractId = c.Id), 0) AS InvoiceExpenses,

                            -- ✅ هزینه‌های اداری که به این پروژه متصل شده‌اند
                            (ISNULL((SELECT SUM(Amount) FROM CashTransactions 
                                     WHERE ContractId = c.Id AND Direction = N'پرداختی' AND PaymentScope = N'Administrative'), 0) +
                             ISNULL((SELECT SUM(Amount) FROM CashChecks 
                                     WHERE ContractId = c.Id AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND PaymentScope = N'Administrative'), 0)
                            ) AS AdminExpenses
                        FROM ClientContracts c
                        ORDER BY c.Id DESC";

                    var dt = await Task.Run(() => DbHelper.ExecuteQuery(_connectionString, query));

                    if (IsDisposed) return;

                    _allProjects.Clear();

                    foreach (DataRow row in dt.Rows)
                    {
                        _allProjects.Add(new ProjectProfitLossDto
                        {
                            ContractId = Convert.ToInt32(row["ContractId"]),
                            ContractNumber = row["ContractNumber"].ToString() ?? "",
                            Subject = row["Subject"].ToString() ?? "",
                            ContractAmount = Convert.ToDecimal(row["ContractAmount"]),
                            DirectPurchase = Convert.ToDecimal(row["DirectPurchase"]),
                            InformalContractor = Convert.ToDecimal(row["InformalContractor"]),
                            FormalContractor = Convert.ToDecimal(row["FormalContractor"]),
                            InvoiceExpenses = Convert.ToDecimal(row["InvoiceExpenses"]),
                            AdminExpenses = Convert.ToDecimal(row["AdminExpenses"])
                        });
                    }
                }, "در حال محاسبه سود و زیان پروژه‌ها...");

                if (!success || IsDisposed) return;

                ApplyFilter();
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ProjectProfitLossDashboardForm.LoadDataAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری اطلاعات:\n" + ex.Message);
            }
            finally
            {
                try { _loadLock.Release(); } catch { }
            }
        }

        private void ApplyFilter()
        {
            if (IsDisposed) return;

            string search = txtSearch.Text.Trim();

            var filtered = _allProjects.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(p =>
                    p.ContractNumber.Contains(search) ||
                    (p.Subject != null && p.Subject.Contains(search)));
            }

            var displayList = filtered.OrderBy(p => p.Profit).ToList();

            pnlCards.SuspendLayout();
            pnlCards.Controls.Clear();

            if (displayList.Count == 0)
            {
                var lblEmpty = new Label
                {
                    Text = "هیچ پروژه‌ای با این مشخصات یافت نشد.",
                    AutoSize = true,
                    Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                    ForeColor = Color.Gray,
                    Margin = new Padding(20)
                };
                pnlCards.Controls.Add(lblEmpty);
            }
            else
            {
                foreach (var project in displayList)
                {
                    pnlCards.Controls.Add(CreateProjectCard(project));
                }
            }

            pnlCards.ResumeLayout();
        }

        private Panel CreateProjectCard(ProjectProfitLossDto dto)
        {
            var card = new Panel
            {
                Width = pnlCards.ClientSize.Width - 40,
                Height = 180,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 15),
                Cursor = Cursors.Hand
            };

            card.Paint += (s, e) =>
            {
                Color borderColor = dto.Profit >= 0 ? UIHelper.ActionGreen : UIHelper.ActionRed;
                ControlPaint.DrawBorder(e.Graphics, card.ClientRectangle, borderColor, 2, ButtonBorderStyle.Solid, borderColor, 2, ButtonBorderStyle.Solid, borderColor, 2, ButtonBorderStyle.Solid, borderColor, 2, ButtonBorderStyle.Solid);
                using var brush = new SolidBrush(borderColor);
                e.Graphics.FillRectangle(brush, card.Width - 4, 0, 4, card.Height);
            };

            card.Click += (s, e) =>
            {
                var contract = _contractRepo.GetById(dto.ContractId);
                if (contract != null)
                {
                    using var form = new ClientContractReportForm(_connectionString, contract);
                    form.ShowDialog(this);
                }
            };

            var lblTitle = new Label
            {
                Text = $"قرارداد {dto.ContractNumber} — {dto.Subject}",
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText,
                Location = new Point(20, 15),
                AutoSize = true
            };

            var lblStatus = new Label
            {
                Text = dto.Profit >= 0 ? $"سود: {NumberHelper.FormatThousands(dto.Profit, usePersianDigits: true)} ریال" : $"زیان: {NumberHelper.FormatThousands(Math.Abs(dto.Profit), usePersianDigits: true)} ریال",
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                ForeColor = dto.Profit >= 0 ? UIHelper.ActionGreen : UIHelper.ActionRed,
                Location = new Point(card.Width - 300, 15),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblAmounts = new Label
            {
                Text = $"مبلغ قرارداد: {NumberHelper.FormatThousands(dto.ContractAmount, usePersianDigits: true)} ریال   |   جمع هزینه‌ها: {NumberHelper.FormatThousands(dto.TotalCosts, usePersianDigits: true)} ریال",
                Font = UIHelper.PersianFont(10f, FontStyle.Bold),
                ForeColor = Color.Gray,
                Location = new Point(20, 45),
                AutoSize = true
            };

            var lblBreakdown = new Label
            {
                Text = $"🛒 خرید مستقیم: {NumberHelper.FormatThousands(dto.DirectPurchase, usePersianDigits: true)}  |  " +
                       $"👷 پیمانکار غیررسمی: {NumberHelper.FormatThousands(dto.InformalContractor, usePersianDigits: true)}  |  " +
                       $"📋 پیمانکار رسمی: {NumberHelper.FormatThousands(dto.FormalContractor, usePersianDigits: true)}  |  " +
                       $"🧾 فاکتورها: {NumberHelper.FormatThousands(dto.InvoiceExpenses, usePersianDigits: true)}  |  " +
                       $"🏢 اداری: {NumberHelper.FormatThousands(dto.AdminExpenses, usePersianDigits: true)}",
                Font = UIHelper.PersianFont(9f),
                ForeColor = Color.DimGray,
                Location = new Point(20, 80),
                AutoSize = true
            };

            var pnlProgressBg = new Panel
            {
                Location = new Point(20, 115),
                Width = card.Width - 40,
                Height = 24,
                BackColor = Color.FromArgb(230, 230, 230)
            };
            pnlProgressBg.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, pnlProgressBg.ClientRectangle, Color.LightGray, 1, ButtonBorderStyle.Solid, Color.LightGray, 1, ButtonBorderStyle.Solid, Color.LightGray, 1, ButtonBorderStyle.Solid, Color.LightGray, 1, ButtonBorderStyle.Solid);
            };

            var pnlProgressFill = new Panel
            {
                Location = new Point(0, 0),
                Height = 22,
                Width = 0,
                BackColor = dto.ProgressPercent > 100 ? UIHelper.ActionRed : (dto.ProgressPercent > 80 ? UIHelper.ActionAmber : UIHelper.ActionGreen)
            };

            int fillWidth = (int)Math.Min(100, dto.ProgressPercent);
            pnlProgressFill.Width = (int)((card.Width - 42) * (fillWidth / 100.0));

            pnlProgressBg.Controls.Add(pnlProgressFill);

            var lblProgressText = new Label
            {
                Text = $"مصرف بودجه: {StringHelper.ConvertToPersianDigits(dto.ProgressPercent.ToString("N1"))}%",
                Font = UIHelper.PersianFont(9f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText,
                Location = new Point(20, 145),
                AutoSize = true
            };

            card.Controls.Add(lblTitle);
            card.Controls.Add(lblStatus);
            card.Controls.Add(lblAmounts);
            card.Controls.Add(lblBreakdown);
            card.Controls.Add(pnlProgressBg);
            card.Controls.Add(lblProgressText);

            return card;
        }

        private class ProjectProfitLossDto
        {
            public int ContractId { get; set; }
            public string ContractNumber { get; set; } = "";
            public string Subject { get; set; } = "";
            public decimal ContractAmount { get; set; }
            public decimal DirectPurchase { get; set; }
            public decimal InformalContractor { get; set; }
            public decimal FormalContractor { get; set; }
            public decimal InvoiceExpenses { get; set; }
            public decimal AdminExpenses { get; set; } // ✅ فیلد جدید

            // ✅ حالا هزینه‌های اداری هم به جمع هزینه‌ها اضافه می‌شوند
            public decimal TotalCosts => DirectPurchase + InformalContractor + FormalContractor + InvoiceExpenses + AdminExpenses;
            public decimal Profit => ContractAmount - TotalCosts;
            public double ProgressPercent => ContractAmount > 0 ? (double)(TotalCosts / ContractAmount) * 100 : 0;
        }
    }
}