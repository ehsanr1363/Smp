using SMP.Data;
using SMP.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class UnbilledExpensesForm : Form
    {
        private readonly UnbilledExpensesRepository _repository;

        private DataGridView dgvExpenses = null!;
        private TextBox txtSearch = null!;
        private Label lblTotalAmount = null!, lblTotalCount = null!;

        private List<UnbilledExpenseItem> _allItems = new();
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public UnbilledExpensesForm(string connectionString)
        {
            _repository = new UnbilledExpensesRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("گزارش هزینه‌های صورت وضعیت نشده", new Size(1200, 700));

            ShortcutHelper.ApplyStandardShortcuts(this);

            Shown += async (s, e) => await LoadDataAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _loadLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));  // فیلتر
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // گرید
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));  // جمع کل

            // ═══════ ۱. فیلتر ═══════
            var pnlTop = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground };
            var topFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 12, 10, 10) };

            var lblSearch = new Label { Text = "جستجو (شماره/موضوع قرارداد یا توضیحات):", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Margin = new Padding(10, 8, 5, 5) };
            txtSearch = new TextBox { Width = 350, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 6, 5, 5), BackColor = Color.White };
            txtSearch.TextChanged += (s, e) => ApplyFilter();

            var btnRefresh = UIHelper.CreateButton("🔄 بروزرسانی", UIHelper.SectionGray, 120, 35);
            btnRefresh.Margin = new Padding(5, 3, 20, 5);
            btnRefresh.Click += async (s, e) => await LoadDataAsync();

            topFlow.Controls.Add(lblSearch);
            topFlow.Controls.Add(txtSearch);
            topFlow.Controls.Add(btnRefresh);
            pnlTop.Controls.Add(topFlow);

            // ═══════ ۲. گرید ═══════
            dgvExpenses = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                RightToLeft = RightToLeft.Yes
            };
            dgvExpenses.ApplyModernStyle();

            dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractTitle", HeaderText = "پروژه / قرارداد", FillWeight = 25 });
            dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "تاریخ", FillWeight = 12 });
            dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "نوع سند", FillWeight = 15 });
            dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "CostType", HeaderText = "ماهیت", FillWeight = 10 });
            dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "توضیحات", FillWeight = 25 });
            dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 15 });

            // ═══════ ۳. جمع کل ═══════
            var pnlFooter = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground };
            pnlFooter.Paint += (s, e) => { using var pen = new Pen(UIHelper.WarmBorder, 1); e.Graphics.DrawLine(pen, 0, 0, pnlFooter.Width, 0); };

            var footerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, RightToLeft = RightToLeft.Yes };
            footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300F));
            footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            lblTotalCount = new Label { Text = "تعداد اقلام: 0", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText, Padding = new Padding(20, 0, 0, 0) };
            lblTotalAmount = new Label { Text = "جمع کل هزینه‌های صورت وضعیت نشده: 0 ریال", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(13f, FontStyle.Bold), ForeColor = UIHelper.ActionRed };

            footerLayout.Controls.Add(lblTotalCount, 0, 0);
            footerLayout.Controls.Add(lblTotalAmount, 1, 0);
            pnlFooter.Controls.Add(footerLayout);

            mainLayout.Controls.Add(pnlTop, 0, 0);
            mainLayout.Controls.Add(dgvExpenses, 0, 1);
            mainLayout.Controls.Add(pnlFooter, 0, 2);

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
                    _allItems = await Task.Run(() => _repository.GetUnbilledExpenses());
                    if (IsDisposed) return;
                    ApplyFilter();
                }, "در حال دریافت هزینه‌های صورت وضعیت نشده...");
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "UnbilledExpensesForm.LoadDataAsync");
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
            var filtered = _allItems.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(x =>
                    x.ContractTitle.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    x.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var list = filtered.ToList();

            dgvExpenses.Rows.Clear();
            foreach (var item in list)
            {
                int rowIndex = dgvExpenses.Rows.Add(
                    item.ContractTitle,
                    DateHelper.ToPersianDate(item.Date),
                    item.Type,
                    item.CostType,
                    item.Description,
                    NumberHelper.FormatThousands(item.Amount, usePersianDigits: true)
                );

                // رنگ‌آمیزی بر اساس نوع (اختیاری برای زیبایی)
                if (item.SourceType == "Exp")
                    dgvExpenses.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 250, 240); // نارنجی خیلی کم‌رنگ
            }

            decimal total = list.Sum(x => x.Amount);
            lblTotalCount.Text = $"تعداد اقلام: {NumberHelper.FormatThousands(list.Count, usePersianDigits: true)}";
            lblTotalAmount.Text = $"جمع کل هزینه‌های صورت وضعیت نشده: {NumberHelper.FormatThousands(total, usePersianDigits: true)} ریال";
        }
    }
}