using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Models;
using Smp.Helpers;
using Smp.Data;

namespace Smp.Forms;

public class GlobalGuaranteesReportForm : Form
{
    private readonly string _connectionString;
    private DataGridView dgvGuarantees = null!;
    private Button btnExport = null!, btnPrint = null!;
    private ModernStatCard cardCount = null!, cardTotal = null!, cardSoon = null!, cardExpired = null!;
    private static readonly Font _boldFont = ModernTheme.GetFont(11f, FontStyle.Bold);

    private PrintDocument printDoc = null!;
    private int printRowIndex = 0;
    private bool isPrintingHeader = true;
    private decimal _totalAmount = 0;
    private Image? _companyLogo = null;

    public GlobalGuaranteesReportForm(string connectionString)
    {
        _connectionString = connectionString;

        BuildUI();

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnExport);
        ShowInTaskbar = false;

        Shown += async (s, e) =>
        {
            LoadCompanyLogo();
            await LoadDataSafeAsync();
        };

        // ✅ FIX: Dispose وسایل چاپ هنگام بسته شدن فرم
        FormClosed += (s, e) =>
        {
            try { printDoc?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "GlobalGuaranteesReportForm"); }
            try { _companyLogo?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "GlobalGuaranteesReportForm"); }
        };
    }

    private void BuildUI()
    {
        ModernForm.Setup(this, "گزارش جامع ضمانت‌نامه‌ها", new Size(1200, 750));
        this.Padding = new Padding(15);

        // ── کارت‌های خلاصه ──
        var strip = new StatStrip(4, 76);
        cardCount = strip.Add("تعداد ضمانت‌نامه‌ها", ModernTheme.Dark);
        cardTotal = strip.Add("جمع مبالغ (ریال)", ModernTheme.Primary);
        cardSoon = strip.Add("سررسید تا ۳۰ روز آینده", ModernTheme.Orange);
        cardExpired = strip.Add("گذشته از سررسید", ModernTheme.Danger);
        cardCount.SetCount(0);
        cardTotal.SetAmount(0m);
        cardSoon.SetCount(0);
        cardExpired.SetCount(0);
        strip.Margin = new Padding(10, 4, 10, 4);

        // ── جدول ──
        dgvGuarantees = new DataGridView();
        dgvGuarantees.ApplyModernStyle();
        dgvGuarantees.FillColumns();
        dgvGuarantees.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgvGuarantees.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        dgvGuarantees.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        dgvGuarantees.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractNumber", HeaderText = "شماره قرارداد", FillWeight = 11 });
        dgvGuarantees.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractorName", HeaderText = "نام پیمانکار", FillWeight = 17,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
        dgvGuarantees.Columns.Add(new DataGridViewTextBoxColumn { Name = "GuaranteeNumber", HeaderText = "شماره ضمانت‌نامه", FillWeight = 12 });
        dgvGuarantees.Columns.Add(new DataGridViewTextBoxColumn { Name = "BankName", HeaderText = "بانک", FillWeight = 11 });
        dgvGuarantees.Columns.Add(new DataGridViewTextBoxColumn { Name = "GuaranteeType", HeaderText = "نوع", FillWeight = 10 });
        dgvGuarantees.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 12,
            DefaultCellStyle = new DataGridViewCellStyle { Font = _boldFont } });
        dgvGuarantees.Columns.Add(new DataGridViewTextBoxColumn { Name = "IssueDate", HeaderText = "تاریخ صدور", FillWeight = 10 });
        dgvGuarantees.Columns.Add(new DataGridViewTextBoxColumn { Name = "MaturityDate", HeaderText = "تاریخ سررسید", FillWeight = 10 });
        dgvGuarantees.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "وضعیت", FillWeight = 9 });

        // وضعیت به‌صورت قرص رنگی (وضعیت‌های دیگر خاکستری)
        dgvGuarantees.UseBadges("Status", new Dictionary<string, Color> { ["فعال"] = ModernTheme.Success });

        var gridHost = ModernReport.WrapGrid(dgvGuarantees);
        gridHost.Margin = new Padding(16, 8, 16, 8);

        // ── دکمه‌های پایین ──
        btnExport = ModernButton.Success("📥 خروجی اکسل", 150, 38);
        btnExport.Click += BtnExportExcel_Click;

        btnPrint = ModernButton.Outline("🖨️ پرینت گزارش", ModernTheme.Primary, 150, 38);
        btnPrint.Click += BtnPrint_Click;

        var bottomBar = ModernForm.CreateButtonBar(btnExport, btnPrint);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        root.Controls.Add(strip, 0, 0);
        root.Controls.Add(gridHost, 0, 1);
        root.Controls.Add(bottomBar, 0, 2);
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
            LogHelper.LogWarning("خطا در بارگذاری لوگو: " + ex.Message, "GlobalGuaranteesReportForm");
        }
    }

    private async Task LoadDataSafeAsync()
    {
        try
        {
            var (success, list) = await AsyncHelper.RunWithLoading(
                this,
                () => Task.Run(() => new ContractorGuaranteeRepository(_connectionString).GetAll()),
                "در حال بارگذاری گزارش جامع...");

            if (!success || list is null || IsDisposed) return;

            dgvGuarantees.Rows.Clear();
            _totalAmount = 0;
            int soonCount = 0, expiredCount = 0;

            foreach (var g in list)
            {
                int rowIndex = dgvGuarantees.Rows.Add(
                    g.Id,
                    g.ContractNumber,
                    g.ContractorName,
                    g.GuaranteeNumber,
                    g.BankName,
                    g.GuaranteeType,
                    NumberHelper.FormatThousands(g.Amount, usePersianDigits: true),
                    DateHelper.ToPersianDate(g.IssueDate),
                    DateHelper.ToPersianDate(g.MaturityDate),
                    g.Status
                );

                _totalAmount += g.Amount;

                if (g.Status == "فعال")
                {
                    int daysLeft = (g.MaturityDate - DateTime.Today).Days;

                    if (daysLeft < 0)
                    {
                        expiredCount++;
                        dgvGuarantees.Rows[rowIndex].DefaultCellStyle.BackColor = ModernTheme.DangerSoft;
                        dgvGuarantees.Rows[rowIndex].DefaultCellStyle.ForeColor = ModernTheme.DangerText;
                    }
                    else if (daysLeft <= 30)
                    {
                        soonCount++;
                        dgvGuarantees.Rows[rowIndex].DefaultCellStyle.BackColor = ModernTheme.WarningSoft;
                        dgvGuarantees.Rows[rowIndex].DefaultCellStyle.ForeColor = ModernTheme.WarningText;
                    }
                }
            }

            cardCount.SetCount(list.Count);
            cardTotal.SetAmount(_totalAmount);
            cardSoon.SetCount(soonCount);
            cardExpired.SetCount(expiredCount);
        }
        catch (ObjectDisposedException) { /* نادیده بگیر */ }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "GlobalGuaranteesReportForm.LoadDataSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا:\n" + ex.Message);
        }
    }

    private void BtnExportExcel_Click(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        if (dgvGuarantees.Rows.Count == 0)
        {
            ModernMessageBox.ShowWarning("داده‌ای برای خروجی وجود ندارد.");
            return;
        }

        using var sfd = new SaveFileDialog { Filter = "Excel Files|*.xlsx", FileName = "گزارش جامع ضمانت‌نامه‌ها.xlsx" };
        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                ExcelExportHelper.ExportDataGridViewToExcel(dgvGuarantees, sfd.FileName, "ضمانت‌نامه‌ها");
                ToastHelper.Show("فایل اکسل با موفقیت ذخیره شد.", isSuccess: true);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "GlobalGuaranteesReportForm.ExportExcel");
                ModernMessageBox.ShowError("خطا در ذخیره فایل:\n" + ex.Message);
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
        if (dgvGuarantees.Rows.Count == 0)
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

        // فونت‌های UIHelper.PersianFont کش شده‌اند و نیاز به using ندارند.
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
                catch (ArgumentException) { /* تصویر Dispose شده */ }
            }

            g.DrawString("گزارش جامع ضمانت‌نامه‌های بانکی", fontTitle, Brushes.Black, new RectangleF(margin, yPos + 110, pageWidth - margin * 2, 40), titleFormat);
            yPos += 150;

            g.DrawString($"تاریخ گزارش: {DateHelper.ToPersianDate(DateTime.Now)}", fontHeader, Brushes.Black, new RectangleF(margin, yPos, pageWidth - margin * 2, 25), rtlFormatRight);
            yPos += 30;

            string totalCount = StringHelper.ConvertToPersianDigits(dgvGuarantees.Rows.Count.ToString());
            string totalAmount = NumberHelper.FormatThousands(_totalAmount, usePersianDigits: true);

            g.DrawString($"تعداد کل ضمانت‌نامه‌ها: {totalCount} | مجموع مبالغ ضمانت‌نامه‌ها: {totalAmount} ریال", fontHeader, Brushes.Black, new RectangleF(margin, yPos, pageWidth - margin * 2, 25), rtlFormatCenter);
            yPos += 35;

            isPrintingHeader = false;
        }
        else
        {
            g.DrawString("ادامه گزارش جامع ضمانت‌نامه‌ها...", fontHeader, Brushes.Black, new RectangleF(margin, yPos, pageWidth - margin * 2, 20), rtlFormatRight);
            yPos += 30;
        }

        float[] colWidths = { 40, 100, 150, 110, 90, 90, 110, 100, 100, 80 };
        float tableWidth = colWidths.Sum();
        float startX = (pageWidth - tableWidth) / 2;
        float rowHeight = 28f;

        string[] headers = { "ردیف", "شماره قرارداد", "نام پیمانکار", "شماره ضمانت‌نامه", "بانک", "نوع", "مبلغ (ریال)", "تاریخ صدور", "تاریخ سررسید", "وضعیت" };

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

        while (printRowIndex < dgvGuarantees.Rows.Count)
        {
            if (yPos + rowHeight > pageHeight - 100)
            {
                e.HasMorePages = true;
                return;
            }

            var row = dgvGuarantees.Rows[printRowIndex];
            currentX = startX + tableWidth;

            currentX -= colWidths[0];
            RectangleF rect0 = new RectangleF(currentX, yPos, colWidths[0], rowHeight);
            g.DrawRectangle(Pens.Black, rect0.X, rect0.Y, rect0.Width, rect0.Height);
            g.DrawString(StringHelper.ConvertToPersianDigits((printRowIndex + 1).ToString()), fontBody, Brushes.Black, rect0, rtlFormatCenter);

            for (int i = 1; i <= 9; i++)
            {
                currentX -= colWidths[i];
                string cellValue = row.Cells[i].Value?.ToString() ?? "";

                RectangleF rect = new RectangleF(currentX, yPos, colWidths[i], rowHeight);
                g.DrawRectangle(Pens.Black, rect.X, rect.Y, rect.Width, rect.Height);
                g.DrawString(cellValue, fontBody, Brushes.Black, rect, rtlFormatCenter);
            }

            yPos += rowHeight;
            printRowIndex++;
        }

        if (printRowIndex >= dgvGuarantees.Rows.Count)
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
