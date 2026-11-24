using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Models;
using Smp.Helpers;
using Smp.Data;

namespace Smp.Forms;

public class GlobalContractsReportForm : Form
{
    private readonly string _connectionString;
    private DataGridView dgvSummaries = null!;
    private TextBox txtSearch = null!;
    private System.Windows.Forms.Timer _searchTimer = null!;
    private ModernStatCard cardCount = null!, cardTotal = null!, cardWork = null!, cardPaid = null!, cardDebt = null!;
    private static readonly Font _boldFont = ModernTheme.GetFont(11f, FontStyle.Bold);
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

        // ✅ FIX: Dispose وسایل چاپ هنگام بسته شدن فرم
        FormClosed += (s, e) =>
        {
            try { _searchTimer?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "GlobalContractsReportForm"); }
            try { printDoc?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "GlobalContractsReportForm"); }
            try { _companyLogo?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "GlobalContractsReportForm"); }
        };
    }

    private void BuildUI()
    {
        ModernForm.Setup(this, "داشبورد جامع قراردادهای پیمانکاری", new Size(1400, 800));
        this.Padding = new Padding(15);
        this.MaximizeBox = true;

        // ── نوار جستجو (هم‌سبک گزارش‌های مدرن) ──
        var filter = ModernReport.CreateFilterBar();

        txtSearch = new TextBox { PlaceholderText = "شماره قرارداد یا نام پیمانکار..." };
        _searchTimer = new System.Windows.Forms.Timer { Interval = 300 };
        _searchTimer.Tick += async (s, e) => { _searchTimer.Stop(); await LoadDataSafeAsync(); };
        txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };
        ModernReport.AddFilter(filter, "جستجو", txtSearch, 360);

        // ── کارت‌های خلاصه (جایگزین برچسب متنی پایین صفحه) ──
        var strip = new StatStrip(5, 76);
        cardCount = strip.Add("تعداد قراردادها", ModernTheme.Dark);
        cardTotal = strip.Add("جمع قراردادها (ریال)", ModernTheme.Primary);
        cardWork = strip.Add("جمع کارکرد (ریال)", ModernTheme.Teal);
        cardPaid = strip.Add("جمع پرداختی (ریال)", ModernTheme.Success);
        cardDebt = strip.Add("جمع بدهی (ریال)", ModernTheme.Danger);
        cardCount.SetCount(0);
        cardTotal.SetAmount(0m); cardWork.SetAmount(0m); cardPaid.SetAmount(0m); cardDebt.SetAmount(0m);
        strip.Margin = new Padding(10, 4, 10, 4);

        // ── جدول قراردادها ──
        dgvSummaries = new DataGridView();
        dgvSummaries.ApplyModernStyle();
        dgvSummaries.FillColumns();
        dgvSummaries.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvSummaries.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        dgvSummaries.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractId", Visible = false });
        dgvSummaries.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractNumber", HeaderText = "شماره قرارداد", FillWeight = 15 });
        dgvSummaries.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractorName", HeaderText = "نام پیمانکار", FillWeight = 25,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
        dgvSummaries.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalAmount", HeaderText = "مبلغ کل قرارداد", FillWeight = 15 });
        dgvSummaries.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalWorkDone", HeaderText = "مجموع کارکرد", FillWeight = 15 });
        dgvSummaries.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalPaid", HeaderText = "مجموع پرداختی", FillWeight = 15 });
        dgvSummaries.Columns.Add(new DataGridViewTextBoxColumn { Name = "RemainingDebt", HeaderText = "مانده بدهی", FillWeight = 15 });

        dgvSummaries.CellDoubleClick += DgvSummaries_CellDoubleClick;

        var gridHost = ModernReport.WrapGrid(dgvSummaries);
        gridHost.Margin = new Padding(8, 8, 16, 8);

        // ── نمودار داخل کارت ──
        chartsPanel = new ContractChartsPanel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        var chartCard = ModernUI.SurfacePanel();
        chartCard.BackColor = ModernTheme.Surface;
        chartCard.Padding = new Padding(10);
        chartCard.Margin = new Padding(16, 8, 8, 8);
        chartCard.Controls.Add(chartsPanel);

        var splitLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        splitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        splitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        splitLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        splitLayout.Controls.Add(gridHost, 0, 0);
        splitLayout.Controls.Add(chartCard, 1, 0);

        // ── دکمه‌های پایین ──
        btnExportExcel = ModernButton.Success("📥 خروجی اکسل", 150, 38);
        btnExportExcel.Click += BtnExportExcel_Click;

        btnPrint = ModernButton.Outline("🖨️ پرینت گزارش", ModernTheme.Primary, 150, 38);
        btnPrint.Click += BtnPrint_Click;

        var bottomBar = ModernForm.CreateButtonBar(btnExportExcel, btnPrint);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        root.Controls.Add(filter, 0, 0);
        root.Controls.Add(strip, 0, 1);
        root.Controls.Add(splitLayout, 0, 2);
        root.Controls.Add(bottomBar, 0, 3);
        Controls.Add(root);
    }

    private void LoadCompanyLogo()
    {
        try
        {
            var companyRepo = new CompanyRepository(_connectionString);
            var company = companyRepo.GetAll().FirstOrDefault();

            if (company is not null && company.Logo is not null && company.Logo.Length > 0)
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
        try
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

            if (!success || summaries is null || IsDisposed) return;

            dgvSummaries.Rows.Clear();

            _totalContractAmount = 0;
            _totalWorkDone = 0;
            _totalPaid = 0;
            _totalDebt = 0;

            foreach (var item in summaries)
            {
                int rowIdx = dgvSummaries.Rows.Add(
                    item.ContractId,
                    item.ContractNumber,
                    item.ContractorName,
                    NumberHelper.FormatThousands(item.TotalAmount, usePersianDigits: true),
                    NumberHelper.FormatThousands(item.TotalWorkDone, usePersianDigits: true),
                    NumberHelper.FormatThousands(item.TotalPaid, usePersianDigits: true),
                    NumberHelper.FormatThousands(item.RemainingDebt, usePersianDigits: true)
                );

                // مانده بدهی: قرمز و پررنگ اگر بدهی دارد، وگرنه کم‌رنگ
                var debtCell = dgvSummaries.Rows[rowIdx].Cells["RemainingDebt"];
                if (item.RemainingDebt > 0) { debtCell.Style.ForeColor = ModernTheme.Danger; debtCell.Style.Font = _boldFont; }
                else debtCell.Style.ForeColor = ModernTheme.TextMuted;

                _totalContractAmount += item.TotalAmount;
                _totalWorkDone += item.TotalWorkDone;
                _totalPaid += item.TotalPaid;
                _totalDebt += item.RemainingDebt;
            }

            cardCount.SetCount(summaries.Count);
            cardTotal.SetAmount(_totalContractAmount);
            cardWork.SetAmount(_totalWorkDone);
            cardPaid.SetAmount(_totalPaid);
            cardDebt.SetAmount(_totalDebt);

            chartsPanel.UpdateData(summaries);
        }
        catch (ObjectDisposedException) { /* نادیده بگیر */ }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "GlobalContractsReportForm.LoadDataSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
    }

    private async void DgvSummaries_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        try
        {
            if (e.RowIndex < 0 || dgvSummaries.CurrentRow is null || IsDisposed) return;

            int contractId = Convert.ToInt32(dgvSummaries.CurrentRow.Cells["ContractId"].Value);

            ContractorContract? contract = null;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var contractRepo = new ContractorContractRepository(_connectionString);
                contract = await Task.Run(() => contractRepo.GetById(contractId));
            }, "در حال دریافت اطلاعات قرارداد...");

            if (!success || contract is null || IsDisposed) return;

            using var reportForm = new ContractorStatementReportForm(_connectionString, contract);
            reportForm.ShowDialog(this);
            await LoadDataSafeAsync();
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "GlobalContractsReportForm.CellDoubleClick");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
    }

    private void BtnExportExcel_Click(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
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
        if (IsDisposed) return;
        if (dgvSummaries.Rows.Count == 0)
        {
            ModernMessageBox.ShowWarning("داده‌ای برای پرینت وجود ندارد.");
            return;
        }

        if (printDoc is null) SetupPrintDocument();

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
        // ✅ جبران حاشیه‌ی سخت‌افزاری چاپگر (فقط در چاپ واقعی؛ در پیش‌نمایش لازم نیست)
        if (!((sender as PrintDocument)?.PrintController?.IsPreview ?? false))
            g.TranslateTransform(-e.PageSettings.HardMarginX, -e.PageSettings.HardMarginY);
        float pageWidth = e.PageBounds.Width;
        float pageHeight = e.PageBounds.Height;
        float margin = 40f;
        float yPos = margin;

        // توجه: فونت‌های UIHelper.PersianFont به صورت Cache در حافظه هستند و نباید Dispose شوند (با using نیاورید).
        var fontTitle = ModernTheme.GetFont(18, FontStyle.Bold);
        var fontHeader = ModernTheme.GetFont(10, FontStyle.Bold);
        var fontBody = ModernTheme.GetFont(9.5f);
        var fontFooter = ModernTheme.GetFont(12, FontStyle.Bold);

        using StringFormat rtlFormatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
        using StringFormat rtlFormatRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
        using StringFormat titleFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft };

        if (isPrintingHeader)
        {
            if (_companyLogo is not null)
            {
                try
                {
                    float logoWidth = 100;
                    float logoHeight = 100;
                    g.DrawImage(_companyLogo, pageWidth - margin - logoWidth, yPos, logoWidth, logoHeight);
                }
                catch (ArgumentException) { /* تصویر Dispose شده است */ }
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

                using (var brushPaid = new SolidBrush(ModernTheme.Success))
                    g.FillPie(brushPaid, pieRect, 0, anglePaid);

                using (var brushUnpaid = new SolidBrush(ModernTheme.WarningText))
                    g.FillPie(brushUnpaid, pieRect, anglePaid, angleUnpaid);

                using (var brushRemaining = new SolidBrush(Color.LightGray))
                    g.FillPie(brushRemaining, pieRect, anglePaid + angleUnpaid, angleRemaining);

                using (var pen = new Pen(Color.White, 2))
                    g.DrawEllipse(pen, pieRect);
            }

            float legendX = pieRect.Right + 30;
            float legendY = pieRect.Top + 10;

            using (var brushPaid = new SolidBrush(ModernTheme.Success))
                g.FillRectangle(brushPaid, legendX, legendY, 15, 15);
            g.DrawString($"پرداخت شده: {NumberHelper.FormatThousands(_totalPaid, usePersianDigits: true)} ریال", fontHeader, Brushes.Black, new RectangleF(legendX + 20, legendY - 5, 300, 25), rtlFormatRight);
            legendY += 30;

            using (var brushUnpaid = new SolidBrush(ModernTheme.WarningText))
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

                using (var brushProgress = new SolidBrush(ModernTheme.Primary))
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
