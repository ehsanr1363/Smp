using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PersonnelApp.Models;
using PersonnelApp.Helpers;
using PersonnelApp.Data;

namespace PersonnelApp.Forms
{
    public class GlobalContractsReportForm : Form
    {
        private readonly string _connectionString;
        private DataGridView dgvSummaries = null!;
        private TextBox txtSearch = null!;
        private System.Windows.Forms.Timer _searchTimer = null!;
        private Label lblTotals = null!;
        private Button btnExportExcel = null!, btnPrint = null!;

        private ContractChartsPanel chartsPanel = null!;
        private Image? _companyLogo = null;

        private PrintDocument printDoc = null!;
        private int printRowIndex = 0;
        private bool isPrintingHeader = true;

        private decimal _totalContractAmount = 0;
        private decimal _totalWorkDone = 0;
        private decimal _totalPaid = 0;
        private decimal _totalDebt = 0;

        public GlobalContractsReportForm(string connectionString)
        {
            _connectionString = connectionString;

            BuildUI();

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnExportExcel);
            ShowInTaskbar = false;

            Shown += async (s, e) =>
            {
                LoadCompanyLogo();
                await LoadDataSafeAsync();
            };

            FormClosed += (s, e) =>
            {
                printDoc?.Dispose();
                _companyLogo?.Dispose();
            };
        }

        private void BuildUI()
        {
            UIHelper.SetupForm(this, "داشبورد جامع قراردادهای پیمانکاری", new Size(1400, 800));
            this.MaximizeBox = true;

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

            var pnlTop = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(10) };

            var topFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            var lblSearch = new Label
            {
                Text = "جستجو (شماره قرارداد یا نام پیمانکار):",
                AutoSize = true,
                Font = UIHelper.PersianFont(11f),
                ForeColor = UIHelper.WarmText,
                Margin = new Padding(5, 12, 5, 5)
            };

            txtSearch = new TextBox
            {
                Width = 250,
                Font = UIHelper.PersianFont(11f),
                BackColor = UIHelper.WarmInputBackground,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(5, 8, 5, 5)
            };

            _searchTimer = new System.Windows.Forms.Timer { Interval = 300 };
            _searchTimer.Tick += async (s, e) => { _searchTimer.Stop(); await LoadDataSafeAsync(); };
            txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };

            topFlow.Controls.Add(lblSearch);
            topFlow.Controls.Add(txtSearch);
            pnlTop.Controls.Add(topFlow);

            var splitLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            splitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            splitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));

            dgvSummaries = new DataGridView { Dock = DockStyle.Fill };
            dgvSummaries.ApplyModernStyle();

            dgvSummaries.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractId", Visible = false });
            dgvSummaries.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractNumber", HeaderText = "شماره قرارداد", FillWeight = 15 });
            dgvSummaries.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractorName", HeaderText = "نام پیمانکار", FillWeight = 25 });
            dgvSummaries.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalAmount", HeaderText = "مبلغ کل قرارداد", FillWeight = 15 });
            dgvSummaries.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalWorkDone", HeaderText = "مجموع کارکرد", FillWeight = 15 });
            dgvSummaries.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalPaid", HeaderText = "مجموع پرداختی", FillWeight = 15 });
            dgvSummaries.Columns.Add(new DataGridViewTextBoxColumn { Name = "RemainingDebt", HeaderText = "مانده بدهی", FillWeight = 15 });

            dgvSummaries.CellDoubleClick += DgvSummaries_CellDoubleClick;

            chartsPanel = new ContractChartsPanel { Dock = DockStyle.Fill, Margin = new Padding(10) };

            splitLayout.Controls.Add(dgvSummaries, 0, 0);
            splitLayout.Controls.Add(chartsPanel, 1, 0);

            var pnlBottom = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground };

            var flow = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 400, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(10) };

            btnExportExcel = UIHelper.CreateButton("📥 خروجی اکسل", UIHelper.ActionGreen, 150, 40);
            btnExportExcel.Click += BtnExportExcel_Click;

            btnPrint = UIHelper.CreateButton("🖨️ پرینت گزارش", UIHelper.SectionBlue, 150, 40);
            btnPrint.Click += BtnPrint_Click;

            flow.Controls.Add(btnExportExcel);
            flow.Controls.Add(btnPrint);

            lblTotals = new Label
            {
                Dock = DockStyle.Right,
                Width = 700,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UIHelper.PersianFont(11.5f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText,
                Padding = new Padding(0, 0, 20, 0)
            };

            pnlBottom.Controls.Add(flow);
            pnlBottom.Controls.Add(lblTotals);

            mainLayout.Controls.Add(pnlTop, 0, 0);
            mainLayout.Controls.Add(splitLayout, 0, 1);
            mainLayout.Controls.Add(pnlBottom, 0, 2);

            Controls.Add(mainLayout);
        }

        private void LoadCompanyLogo()
        {
            try
            {
                var companyRepo = new CompanyRepository(_connectionString);
                var company = companyRepo.GetAll().FirstOrDefault();

                if (company != null && company.Logo != null && company.Logo.Length > 0)
                {
                    _companyLogo = ImageHelper.ByteArrayToImage(company.Logo);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogWarning("خطا در بارگذاری لوگو برای پرینت: " + ex.Message, "GlobalContractsReportForm");
            }
        }

        private async Task LoadDataSafeAsync()
        {
            string search = txtSearch.Text.Trim();

            var (success, summaries) = await AsyncHelper.RunWithLoading(
                this,
                () => Task.Run(() =>
                {
                    var repo = new ContractSummaryRepository(_connectionString);
                    return repo.GetAllSummaries(search);
                }),
                "در حال بارگذاری داشبورد...");

            if (!success || summaries == null) return;

            dgvSummaries.Rows.Clear();

            _totalContractAmount = 0;
            _totalWorkDone = 0;
            _totalPaid = 0;
            _totalDebt = 0;

            foreach (var item in summaries)
            {
                dgvSummaries.Rows.Add(
                    item.ContractId,
                    item.ContractNumber,
                    item.ContractorName,
                    NumberHelper.FormatThousands(item.TotalAmount, usePersianDigits: true),
                    NumberHelper.FormatThousands(item.TotalWorkDone, usePersianDigits: true),
                    NumberHelper.FormatThousands(item.TotalPaid, usePersianDigits: true),
                    NumberHelper.FormatThousands(item.RemainingDebt, usePersianDigits: true)
                );

                _totalContractAmount += item.TotalAmount;
                _totalWorkDone += item.TotalWorkDone;
                _totalPaid += item.TotalPaid;
                _totalDebt += item.RemainingDebt;
            }

            string formattedTotal = NumberHelper.FormatThousands(_totalContractAmount, usePersianDigits: true);
            string formattedDebt = NumberHelper.FormatThousands(_totalDebt, usePersianDigits: true);
            lblTotals.Text = $"جمع کل قراردادها: {formattedTotal} ریال  |  جمع بدهی: {formattedDebt} ریال";

            chartsPanel.UpdateData(summaries);
        }

        private async void DgvSummaries_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || dgvSummaries.CurrentRow == null) return;

            int contractId = Convert.ToInt32(dgvSummaries.CurrentRow.Cells["ContractId"].Value);

            ContractorContract? contract = null;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var contractRepo = new ContractorContractRepository(_connectionString);
                contract = await Task.Run(() => contractRepo.GetById(contractId));
            }, "در حال دریافت اطلاعات قرارداد...");

            if (!success || contract == null) return;

            using var reportForm = new ContractorStatementReportForm(_connectionString, contract);
            reportForm.ShowDialog(this);
            await LoadDataSafeAsync();
        }

        private void BtnExportExcel_Click(object? sender, EventArgs e)
        {
            if (dgvSummaries.Rows.Count == 0)
            {
                ModernMessageBox.ShowWarning("داده‌ای برای خروجی وجود ندارد.");
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                FileName = "داشبورد جامع قراردادها.xlsx",
                Title = "ذخیره فایل اکسل"
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    ExcelExportHelper.ExportDataGridViewToExcel(dgvSummaries, sfd.FileName, "داشبورد قراردادها");
                    ToastHelper.Show("فایل اکسل با موفقیت ذخیره شد.", isSuccess: true);
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "GlobalContractsReportForm.ExportExcel");
                    ModernMessageBox.ShowError("خطا در ذخیره فایل اکسل:\n" + ex.Message);
                }
            }
        }

        private void SetupPrintDocument()
        {
            printDoc = new PrintDocument();
            printDoc.DefaultPageSettings.Landscape = true;
            printDoc.PrintPage += PrintDoc_PrintPage;
        }

        private void BtnPrint_Click(object? sender, EventArgs e)
        {
            if (dgvSummaries.Rows.Count == 0)
            {
                ModernMessageBox.ShowWarning("داده‌ای برای پرینت وجود ندارد.");
                return;
            }

            if (printDoc == null) SetupPrintDocument();

            printRowIndex = 0;
            isPrintingHeader = true;

            using var preview = new PrintPreviewDialog
            {
                Document = printDoc,
                Width = 1100,
                Height = 800,
                WindowState = FormWindowState.Maximized,
                UseAntiAlias = true,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true
            };

            preview.ShowDialog(this);
        }

        private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics!;
            float pageWidth = e.PageBounds.Width;
            float pageHeight = e.PageBounds.Height;
            float margin = 40f;
            float yPos = margin;

            // ✅ FIX: فونت‌های کش‌شده نباید Dispose شوند
            var fontTitle = UIHelper.PersianFont(18, FontStyle.Bold);
            var fontHeader = UIHelper.PersianFont(10, FontStyle.Bold);
            var fontBody = UIHelper.PersianFont(9.5f);
            var fontFooter = UIHelper.PersianFont(12, FontStyle.Bold);

            using StringFormat rtlFormatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
            using StringFormat rtlFormatRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
            using StringFormat titleFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft };

            if (isPrintingHeader)
            {
                if (_companyLogo != null)
                {
                    float logoWidth = 100;
                    float logoHeight = 100;
                    g.DrawImage(_companyLogo, pageWidth - margin - logoWidth, yPos, logoWidth, logoHeight);
                }

                g.DrawString("گزارش جامع قراردادهای پیمانکاری", fontTitle, Brushes.Black, new RectangleF(margin, yPos + 110, pageWidth - margin * 2, 40), titleFormat);
                yPos += 150;

                g.DrawString($"تاریخ گزارش: {DateHelper.ToPersianDate(DateTime.Now)}", fontHeader, Brushes.Black, new RectangleF(margin, yPos, pageWidth - margin * 2, 25), rtlFormatRight);
                yPos += 30;

                string formattedTotal = NumberHelper.FormatThousands(_totalContractAmount, usePersianDigits: true);
                string formattedDebt = NumberHelper.FormatThousands(_totalDebt, usePersianDigits: true);
                g.DrawString($"تعداد قراردادها: {StringHelper.ConvertToPersianDigits(dgvSummaries.Rows.Count.ToString())} | مبلغ کل قراردادها: {formattedTotal} ریال | مجموع بدهی به پیمانکاران: {formattedDebt} ریال", fontHeader, Brushes.Black, new RectangleF(margin, yPos, pageWidth - margin * 2, 25), rtlFormatCenter);
                yPos += 40;

                float pieSize = 150;
                Rectangle pieRect = new Rectangle((int)(pageWidth / 2 - pieSize / 2), (int)yPos, (int)pieSize, (int)pieSize);
                decimal remainingWork = _totalContractAmount - _totalWorkDone;
                decimal unpaidAmount = _totalWorkDone - _totalPaid;
                if (unpaidAmount < 0) unpaidAmount = 0;

                if (_totalContractAmount > 0)
                {
                    float anglePaid = (float)(_totalPaid / _totalContractAmount) * 360f;
                    float angleUnpaid = (float)(unpaidAmount / _totalContractAmount) * 360f;
                    float angleRemaining = (float)(remainingWork / _totalContractAmount) * 360f;

                    if (anglePaid + angleUnpaid + angleRemaining > 360f)
                        angleRemaining = 360f - (anglePaid + angleUnpaid);

                    using (var brushPaid = new SolidBrush(UIHelper.ActionGreen))
                        g.FillPie(brushPaid, pieRect, 0, anglePaid);

                    using (var brushUnpaid = new SolidBrush(UIHelper.WarmWarningText))
                        g.FillPie(brushUnpaid, pieRect, anglePaid, angleUnpaid);

                    using (var brushRemaining = new SolidBrush(Color.LightGray))
                        g.FillPie(brushRemaining, pieRect, anglePaid + angleUnpaid, angleRemaining);

                    using (var pen = new Pen(Color.White, 2))
                        g.DrawEllipse(pen, pieRect);
                }

                float legendX = pieRect.Right + 30;
                float legendY = pieRect.Top + 10;

                using (var brushPaid = new SolidBrush(UIHelper.ActionGreen))
                    g.FillRectangle(brushPaid, legendX, legendY, 15, 15);
                g.DrawString($"پرداخت شده: {NumberHelper.FormatThousands(_totalPaid, usePersianDigits: true)} ریال", fontHeader, Brushes.Black, new RectangleF(legendX + 20, legendY - 5, 300, 25), rtlFormatRight);
                legendY += 30;

                using (var brushUnpaid = new SolidBrush(UIHelper.WarmWarningText))
                    g.FillRectangle(brushUnpaid, legendX, legendY, 15, 15);
                g.DrawString($"کارکرد پرداخت نشده: {NumberHelper.FormatThousands(unpaidAmount, usePersianDigits: true)} ریال", fontHeader, Brushes.Black, new RectangleF(legendX + 20, legendY - 5, 300, 25), rtlFormatRight);
                legendY += 30;

                using (var brushRemaining = new SolidBrush(Color.LightGray))
                    g.FillRectangle(brushRemaining, legendX, legendY, 15, 15);
                g.DrawString($"مانده قرارداد: {NumberHelper.FormatThousands(remainingWork, usePersianDigits: true)} ریال", fontHeader, Brushes.Black, new RectangleF(legendX + 20, legendY - 5, 300, 25), rtlFormatRight);

                float barY = pieRect.Bottom + 20;
                float barW = pieRect.Width;
                float barH = 15;
                float barX = pieRect.X;

                using (var brushGray = new SolidBrush(Color.LightGray))
                    g.FillRectangle(brushGray, barX, barY, barW, barH);

                if (_totalContractAmount > 0)
                {
                    float progressWidth = (float)(_totalWorkDone / _totalContractAmount) * barW;
                    if (progressWidth > barW) progressWidth = barW;

                    using (var brushProgress = new SolidBrush(UIHelper.SectionBlue))
                        g.FillRectangle(brushProgress, barX, barY, progressWidth, barH);

                    double percent = (double)(_totalWorkDone / _totalContractAmount) * 100;
                    string formattedPercent = StringHelper.ConvertToPersianDigits(percent.ToString("N1"));
                    g.DrawString($"پیشرفت کل: {formattedPercent}%", fontHeader, Brushes.Black, new RectangleF(barX, barY + barH + 5, barW, 20), rtlFormatCenter);
                }

                yPos = barY + barH + 40;
                isPrintingHeader = false;
            }
            else
            {
                g.DrawString("ادامه گزارش جامع قراردادها...", fontHeader, Brushes.Black, new RectangleF(margin, yPos, pageWidth - margin * 2, 20), rtlFormatRight);
                yPos += 30;
            }

            float[] colWidths = { 40, 120, 200, 130, 130, 130, 130 };
            float tableWidth = colWidths.Sum();
            float startX = (pageWidth - tableWidth) / 2;
            float rowHeight = 28f;

            string[] headers = { "ردیف", "شماره قرارداد", "نام پیمانکار", "مبلغ کل قرارداد", "مجموع کارکرد", "مجموع پرداختی", "مانده بدهی" };

            float currentX = startX + tableWidth;
            using (var brushHeaderBg = new SolidBrush(Color.LightGray))
                g.FillRectangle(brushHeaderBg, startX, yPos, tableWidth, rowHeight);

            for (int i = 0; i < headers.Length; i++)
            {
                currentX -= colWidths[i];
                RectangleF rect = new RectangleF(currentX, yPos, colWidths[i], rowHeight);
                g.DrawRectangle(Pens.Black, rect.X, rect.Y, rect.Width, rect.Height);
                g.DrawString(headers[i], fontHeader, Brushes.Black, rect, rtlFormatCenter);
            }
            yPos += rowHeight;

            int rowCounter = printRowIndex + 1;
            while (printRowIndex < dgvSummaries.Rows.Count)
            {
                if (yPos + rowHeight > pageHeight - 100)
                {
                    e.HasMorePages = true;
                    return;
                }

                var row = dgvSummaries.Rows[printRowIndex];
                currentX = startX + tableWidth;

                currentX -= colWidths[0];
                RectangleF rect0 = new RectangleF(currentX, yPos, colWidths[0], rowHeight);
                g.DrawRectangle(Pens.Black, rect0.X, rect0.Y, rect0.Width, rect0.Height);
                g.DrawString(StringHelper.ConvertToPersianDigits(rowCounter.ToString()), fontBody, Brushes.Black, rect0, rtlFormatCenter);

                for (int i = 1; i <= 6; i++)
                {
                    currentX -= colWidths[i];
                    string cellValue = row.Cells[i].Value?.ToString() ?? "";

                    RectangleF rect = new RectangleF(currentX, yPos, colWidths[i], rowHeight);
                    g.DrawRectangle(Pens.Black, rect.X, rect.Y, rect.Width, rect.Height);
                    g.DrawString(cellValue, fontBody, Brushes.Black, rect, rtlFormatCenter);
                }

                yPos += rowHeight;
                printRowIndex++;
                rowCounter++;
            }

            if (printRowIndex >= dgvSummaries.Rows.Count)
            {
                yPos += 50;
                float signWidth = 150f; float gap = (pageWidth - (signWidth * 3) - margin * 2) / 2;

                g.DrawString("مهر و امضای مدیر مالی", fontFooter, Brushes.Black, new RectangleF(pageWidth - margin - signWidth, yPos, signWidth, 20), rtlFormatCenter);
                g.DrawLine(Pens.Black, pageWidth - margin - signWidth, yPos + 60, pageWidth - margin, yPos + 60);

                g.DrawString("مهر و امضای مدیرعامل", fontFooter, Brushes.Black, new RectangleF(pageWidth - margin - signWidth - gap - signWidth, yPos, signWidth, 20), rtlFormatCenter);
                g.DrawLine(Pens.Black, pageWidth - margin - signWidth - gap - signWidth, yPos + 60, pageWidth - margin - gap - signWidth, yPos + 60);

                g.DrawString("مهر و امضای حسابرس", fontFooter, Brushes.Black, new RectangleF(margin, yPos, signWidth, 20), rtlFormatCenter);
                g.DrawLine(Pens.Black, margin, yPos + 60, margin + signWidth, yPos + 60);
            }

            e.HasMorePages = false;
        }
    }
}