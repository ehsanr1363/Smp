using System;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using System.Collections.Generic;
using Smp.Helpers;

namespace Smp.Helpers;

public class PayslipPrinter
{
    private readonly DataTable _dt;
    private readonly string _yearMonth;
    private readonly string _companyName;
    private readonly Image? _logo;

    private int _currentRowIndex = 0;

    public PayslipPrinter(DataTable dt, string yearMonth, string companyName, Image? logo)
    {
        _dt = dt;
        _yearMonth = yearMonth;
        _companyName = companyName;
        _logo = logo;
    }

    public void ShowPreview()
    {
        var printDoc = new PrintDocument();
        printDoc.DefaultPageSettings.Landscape = false;
        printDoc.DefaultPageSettings.Margins = new Margins(20, 20, 20, 20);

        printDoc.BeginPrint += (s, e) => _currentRowIndex = 0;
        printDoc.PrintPage += PrintDoc_PrintPage;

        using var ppd = new PrintPreviewDialog
        {
            Document = printDoc,
            Width = 850,
            Height = 950,
            UseAntiAlias = true,
            Text = "پیش‌نمایش فیش حقوقی (دو نفر در یک برگ A4)",
            RightToLeft = RightToLeft.Yes,
            RightToLeftLayout = true
        };
        ppd.ShowDialog();
    }

    // ═══════════════════════════════════════════════════════════════
    // ✅ هر نیم‌صفحه برای یک نفر متفاوت است
    //    نیم‌بالا = نفر جاری، نیم‌پایین = نفر بعدی
    // ═══════════════════════════════════════════════════════════════
    private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
    {
        if (_currentRowIndex >= _dt.Rows.Count)
        {
            e.HasMorePages = false;
            return;
        }

        Graphics g = e.Graphics!;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        var bounds = e.MarginBounds;
        float halfHeight = bounds.Height / 2f;

        // ─── نسخه بالایی: نفر جاری ───
        var topRow = _dt.Rows[_currentRowIndex];
        RectangleF rectTop = new RectangleF(bounds.Left, bounds.Top, bounds.Width, halfHeight - 10);
        DrawPayslip(g, rectTop, topRow);
        _currentRowIndex++;

        // ─── خط‌چین برش در وسط ───
        using (var pen = new Pen(Color.Gray, 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
        {
            g.DrawLine(pen, bounds.Left, bounds.Top + halfHeight, bounds.Right, bounds.Top + halfHeight);
        }

        // ─── نسخه پایینی: نفر بعدی (اگر وجود دارد) ───
        if (_currentRowIndex < _dt.Rows.Count)
        {
            var bottomRow = _dt.Rows[_currentRowIndex];
            RectangleF rectBottom = new RectangleF(bounds.Left, bounds.Top + halfHeight + 10, bounds.Width, halfHeight - 10);
            DrawPayslip(g, rectBottom, bottomRow);
            _currentRowIndex++;
        }

        e.HasMorePages = _currentRowIndex < _dt.Rows.Count;
    }

    private string FormatToTimeHHMM(decimal totalHours)
    {
        if (totalHours <= 0) return "00:00";
        int hours = (int)totalHours;
        int minutes = (int)Math.Round((totalHours - hours) * 60);
        if (minutes == 60) { minutes = 0; hours++; }
        return $"{hours:00}:{minutes:00}";
    }

    private void DrawPayslip(Graphics g, RectangleF bounds, DataRow row)
    {
        float startX = bounds.Left;
        float startY = bounds.Top;
        float width = bounds.Width;

        float yPos = startY;

        Font fontMainTitle = ModernTheme.GetFont(12f, FontStyle.Bold);
        Font fontHeader = ModernTheme.GetFont(9f, FontStyle.Bold);
        Font fontBody = ModernTheme.GetFont(8.5f, FontStyle.Regular);
        Font fontWords = ModernTheme.GetFont(8f, FontStyle.Bold);

        Pen borderPen = new Pen(Color.DarkGreen, 1.5f);
        Pen innerPen = new Pen(Color.FromArgb(150, 150, 150), 0.5f);
        Brush bgHeader = new SolidBrush(Color.FromArgb(240, 248, 240));

        var rtlCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft };
        var rtlRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft };

        float margin = 5;
        float innerWidth = width - (margin * 2);

        float headerHeight = 105;

        if (_logo is not null)
        {
            try
            {
                float ratio = (float)_logo.Width / _logo.Height;
                float drawWidth = 45;
                float drawHeight = drawWidth / ratio;
                if (drawHeight > 45) { drawHeight = 45; drawWidth = drawHeight * ratio; }
                g.DrawImage(_logo, startX + innerWidth - drawWidth, yPos + 5, drawWidth, drawHeight);
            }
            catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "PayslipPrinter"); }
        }

        g.DrawString($"{_companyName}", fontHeader, Brushes.Gray, new RectangleF(startX, yPos + 5, width, 15), rtlCenter);
        g.DrawString($"فیش حقوق و مزایای {_yearMonth}", fontMainTitle, Brushes.Black, new RectangleF(startX, yPos + 25, width, 20), rtlCenter);

        string empName = row["EmployeeName"]?.ToString() ?? "";
        string empCode = row["PersonnelCode"]?.ToString() ?? "";
        string insNum = row.Table.Columns.Contains("InsuranceNumber") ? row["InsuranceNumber"]?.ToString() ?? "" : "";
        string sheba = row.Table.Columns.Contains("ShebaNumber") ? row["ShebaNumber"]?.ToString() ?? "---" : "---";
        string natCode = row.Table.Columns.Contains("NationalCode") ? row["NationalCode"]?.ToString() ?? "---" : "---";
        string position = row.Table.Columns.Contains("Position") ? row["Position"]?.ToString() ?? "---" : "---";

        g.DrawString($"نام و نام خانوادگی: {empName}", fontHeader, Brushes.Black, new RectangleF(startX + innerWidth - 300, yPos + 60, 280, 15), rtlRight);
        g.DrawString($"کد پرسنلی: {empCode}", fontBody, Brushes.Black, new RectangleF(startX + innerWidth - 300, yPos + 75, 280, 15), rtlRight);

        g.DrawString($"کد ملی: {natCode}", fontBody, Brushes.Black, new RectangleF(startX + (width / 2) - 100, yPos + 60, 200, 15), rtlCenter);
        g.DrawString($"عنوان شغل: {position}", fontBody, Brushes.Black, new RectangleF(startX + (width / 2) - 100, yPos + 75, 200, 15), rtlCenter);

        g.DrawString($"شماره شبا: {sheba}", fontBody, Brushes.Black, new RectangleF(startX + margin + 10, yPos + 60, 200, 15), rtlRight);
        g.DrawString($"شماره بیمه: {insNum}", fontBody, Brushes.Black, new RectangleF(startX + margin + 10, yPos + 75, 150, 15), rtlRight);

        yPos += headerHeight;

        var earnings = new List<(string Title, decimal Amount)>();
        void AddEarning(string key, string title) { if (row.Table.Columns.Contains(key)) { decimal v = row[key] != DBNull.Value ? Convert.ToDecimal(row[key]) : 0; if (v != 0 || key == "BaseSalary") earnings.Add((title, v)); } }
        AddEarning("BaseSalary", "حقوق مبنا");
        AddEarning("YearsBonus", "پایه سنوات");
        AddEarning("ChildAllowance", "حق اولاد");
        AddEarning("HousingAllowance", "مسکن");
        AddEarning("GroceryAllowance", "مزایای رفاهی");
        AddEarning("OvertimePay", "مبلغ اضافه کار");
        AddEarning("AttractionAllowance", "حق جذب");
        AddEarning("ResponsibilityAllowance", "حق مسئولیت");
        AddEarning("ManagementAllowance", "حق مدیریت");
        AddEarning("TechnicalAllowance", "حق فنی");
        AddEarning("MarriageAllowance", "حق تاهل");
        AddEarning("TransportationAllowance", "حق ایاب و ذهاب");
        AddEarning("OtherAllowances", "سایر مزایا");

        var deductions = new List<(string Title, decimal Amount)>();
        void AddDeduction(string key, string title) { if (row.Table.Columns.Contains(key)) { decimal v = row[key] != DBNull.Value ? Convert.ToDecimal(row[key]) : 0; if (v != 0 || key == "InsuranceEmployee" || key == "IncomeTax") deductions.Add((title, v)); } }
        AddDeduction("AbsenceDeduction", "کسر غیبت");
        AddDeduction("InsuranceEmployee", "بیمه کارمند");
        AddDeduction("IncomeTax", "مالیات");
        AddDeduction("LoanDeduction", "کسر وام");
        AddDeduction("AdvanceDeduction", "کسر مساعده");
        AddDeduction("SupplementaryInsuranceDeduction", "بیمه تکمیلی");
        AddDeduction("CarInsuranceDeduction", "بیمه خودرو");
        AddDeduction("OtherDeductions", "سایر کسورات");

        int maxRows = Math.Max(earnings.Count, deductions.Count);
        if (maxRows < 6) maxRows = 6;

        float fixedBottomHeight = 18 + 15 + 25;
        float availableGridHeight = bounds.Height - headerHeight - fixedBottomHeight;
        float rowHeight = Math.Min(16f, availableGridHeight / maxRows);
        float headerRowHeight = 18f;
        float colWidth = innerWidth / 6f;

        g.FillRectangle(bgHeader, startX + margin, yPos, innerWidth, headerRowHeight);
        g.DrawRectangle(innerPen, startX + margin, yPos, innerWidth, headerRowHeight);

        for (int i = 1; i < 6; i++)
            g.DrawLine(innerPen, startX + margin + (i * colWidth), yPos, startX + margin + (i * colWidth), yPos + headerRowHeight);

        g.DrawString("حقوق و مزایا", fontHeader, Brushes.Black, new RectangleF(startX + margin + colWidth * 5, yPos, colWidth, headerRowHeight), rtlCenter);
        g.DrawString("معوقه", fontHeader, Brushes.Black, new RectangleF(startX + margin + colWidth * 4, yPos, colWidth, headerRowHeight), rtlCenter);
        g.DrawString("ماه جاری", fontHeader, Brushes.Black, new RectangleF(startX + margin + colWidth * 3, yPos, colWidth, headerRowHeight), rtlCenter);
        g.DrawString("کسورات", fontHeader, Brushes.Black, new RectangleF(startX + margin + colWidth * 2, yPos, colWidth, headerRowHeight), rtlCenter);
        g.DrawString("معوقه/مانده", fontHeader, Brushes.Black, new RectangleF(startX + margin + colWidth * 1, yPos, colWidth, headerRowHeight), rtlCenter);
        g.DrawString("ماه جاری", fontHeader, Brushes.Black, new RectangleF(startX + margin, yPos, colWidth, headerRowHeight), rtlCenter);

        yPos += headerRowHeight;

        for (int i = 0; i < maxRows; i++)
        {
            g.DrawRectangle(innerPen, startX + margin, yPos, innerWidth, rowHeight);
            for (int j = 1; j < 6; j++)
                g.DrawLine(innerPen, startX + margin + (j * colWidth), yPos, startX + margin + (j * colWidth), yPos + rowHeight);

            if (i < earnings.Count)
            {
                g.DrawString(earnings[i].Title, fontBody, Brushes.Black, new RectangleF(startX + margin + colWidth * 5, yPos, colWidth, rowHeight), rtlCenter);
                g.DrawString("0", fontBody, Brushes.Black, new RectangleF(startX + margin + colWidth * 4, yPos, colWidth, rowHeight), rtlCenter);
                g.DrawString(NumberHelper.FormatThousands(earnings[i].Amount, usePersianDigits: true), fontBody, Brushes.Black, new RectangleF(startX + margin + colWidth * 3, yPos, colWidth, rowHeight), rtlCenter);
            }

            if (i < deductions.Count)
            {
                g.DrawString(deductions[i].Title, fontBody, Brushes.Black, new RectangleF(startX + margin + colWidth * 2, yPos, colWidth, rowHeight), rtlCenter);
                g.DrawString("0", fontBody, Brushes.Black, new RectangleF(startX + margin + colWidth * 1, yPos, colWidth, rowHeight), rtlCenter);
                g.DrawString(NumberHelper.FormatThousands(deductions[i].Amount, usePersianDigits: true), fontBody, Brushes.Black, new RectangleF(startX + margin, yPos, colWidth, rowHeight), rtlCenter);
            }

            yPos += rowHeight;
        }

        // ═══════════ جدول خلاصه ═══════════
        float summaryColWidth = innerWidth / 7f;
        g.FillRectangle(bgHeader, startX + margin, yPos, innerWidth, headerRowHeight);
        g.DrawRectangle(innerPen, startX + margin, yPos, innerWidth, headerRowHeight);

        for (int i = 1; i < 7; i++)
            g.DrawLine(innerPen, startX + margin + (i * summaryColWidth), yPos, startX + margin + (i * summaryColWidth), yPos + headerRowHeight);

        g.DrawString("کارکرد", fontHeader, Brushes.Black, new RectangleF(startX + margin + summaryColWidth * 6, yPos, summaryColWidth, headerRowHeight), rtlCenter);
        g.DrawString("اضافه کار", fontHeader, Brushes.Black, new RectangleF(startX + margin + summaryColWidth * 5, yPos, summaryColWidth, headerRowHeight), rtlCenter);
        g.DrawString("کسر کار", fontHeader, Brushes.Black, new RectangleF(startX + margin + summaryColWidth * 4, yPos, summaryColWidth, headerRowHeight), rtlCenter);
        g.DrawString("بیمه کارفرما", fontHeader, Brushes.Black, new RectangleF(startX + margin + summaryColWidth * 3, yPos, summaryColWidth, headerRowHeight), rtlCenter);
        g.DrawString("جمع مزایا", fontHeader, Brushes.Black, new RectangleF(startX + margin + summaryColWidth * 2, yPos, summaryColWidth, headerRowHeight), rtlCenter);
        g.DrawString("جمع کسور", fontHeader, Brushes.Black, new RectangleF(startX + margin + summaryColWidth * 1, yPos, summaryColWidth, headerRowHeight), rtlCenter);
        g.DrawString("مانده پرداخت", fontHeader, Brushes.Black, new RectangleF(startX + margin, yPos, summaryColWidth, headerRowHeight), rtlCenter);

        yPos += headerRowHeight;

        g.DrawRectangle(innerPen, startX + margin, yPos, innerWidth, rowHeight);
        for (int i = 1; i < 7; i++)
            g.DrawLine(innerPen, startX + margin + (i * summaryColWidth), yPos, startX + margin + (i * summaryColWidth), yPos + rowHeight);

        decimal totalEarnings = row["TotalEarnings"] != DBNull.Value ? Convert.ToDecimal(row["TotalEarnings"]) : 0;
        decimal totalDeductions = row["TotalDeductions"] != DBNull.Value ? Convert.ToDecimal(row["TotalDeductions"]) : 0;
        decimal netPay = row["NetPay"] != DBNull.Value ? Convert.ToDecimal(row["NetPay"]) : 0;
        decimal insEmployer = row.Table.Columns.Contains("InsuranceEmployer") && row["InsuranceEmployer"] != DBNull.Value ? Convert.ToDecimal(row["InsuranceEmployer"]) : 0;

        decimal overtimeHours = row.Table.Columns.Contains("OvertimeHours") && row["OvertimeHours"] != DBNull.Value ? Convert.ToDecimal(row["OvertimeHours"]) : 0;
        decimal absenceHours = row.Table.Columns.Contains("AbsenceHours") && row["AbsenceHours"] != DBNull.Value ? Convert.ToDecimal(row["AbsenceHours"]) : 0;
        decimal workingDays = row.Table.Columns.Contains("WorkingDays") && row["WorkingDays"] != DBNull.Value ? Convert.ToDecimal(row["WorkingDays"]) : 30;
        string workingDaysText = StringHelper.ConvertToPersianDigits(workingDays.ToString("0.##"));

        g.DrawString(workingDaysText, fontBody, Brushes.Black, new RectangleF(startX + margin + summaryColWidth * 6, yPos, summaryColWidth, rowHeight), rtlCenter);
        g.DrawString(FormatToTimeHHMM(overtimeHours), fontBody, Brushes.Black, new RectangleF(startX + margin + summaryColWidth * 5, yPos, summaryColWidth, rowHeight), rtlCenter);
        g.DrawString(FormatToTimeHHMM(absenceHours), fontBody, Brushes.Black, new RectangleF(startX + margin + summaryColWidth * 4, yPos, summaryColWidth, rowHeight), rtlCenter);
        g.DrawString(NumberHelper.FormatThousands(insEmployer, usePersianDigits: true), fontBody, Brushes.Black, new RectangleF(startX + margin + summaryColWidth * 3, yPos, summaryColWidth, rowHeight), rtlCenter);
        g.DrawString(NumberHelper.FormatThousands(totalEarnings, usePersianDigits: true), fontBody, Brushes.Black, new RectangleF(startX + margin + summaryColWidth * 2, yPos, summaryColWidth, rowHeight), rtlCenter);
        g.DrawString(NumberHelper.FormatThousands(totalDeductions, usePersianDigits: true), fontBody, Brushes.Black, new RectangleF(startX + margin + summaryColWidth * 1, yPos, summaryColWidth, rowHeight), rtlCenter);
        g.DrawString(NumberHelper.FormatThousands(netPay, usePersianDigits: true), fontHeader, Brushes.Black, new RectangleF(startX + margin, yPos, summaryColWidth, rowHeight), rtlCenter);

        yPos += rowHeight + 8;

        // کادر مبلغ به حروف
        g.DrawRectangle(innerPen, startX + margin, yPos, innerWidth, 22);
        string netPayInWords = NumberHelper.ToPersianWords(netPay) + " ریال";
        g.DrawString($"مانده قابل پرداخت به حروف: {netPayInWords}", fontWords, Brushes.Black, new RectangleF(startX + margin + 5, yPos, innerWidth - 10, 22), rtlRight);

        // کشیدن کادر بیرونی تا انتهای نیم‌صفحه
        yPos = startY + bounds.Height - 2;
        float actualHeight = yPos - startY;
        g.DrawRectangle(borderPen, startX, startY, width, actualHeight);
    }
}
