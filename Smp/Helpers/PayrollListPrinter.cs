using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Windows.Forms;

namespace Smp.Helpers;

public class PayrollListPrinter
{
    private readonly DataTable _dt;
    private readonly List<KeyValuePair<string, string>> _selectedCols;
    private readonly string _yearMonth;
    private readonly string _listTitle;
    private readonly string _companyName;
    private readonly Image? _logo;
    private readonly HashSet<string> _numericColumns;
    private readonly List<string>? _headerLines;
    private readonly string[] _signatureTitles;

    private readonly HashSet<string> _smallColumns = new HashSet<string>
    {
        "WorkingDays",
        "OvertimeHours",
        "AbsenceHours"
    };

    /// <summary>ساعت اعشاری → ساعت:دقیقه (مثلاً ۷٫۵ → 07:30؛ صفر → 00:00)</summary>
    private static string FormatHhMm(decimal hours)
    {
        int totalMinutes = (int)Math.Round(Math.Abs(hours) * 60m, MidpointRounding.AwayFromZero);
        return $"{totalMinutes / 60:00}:{totalMinutes % 60:00}";
    }

    public PayrollListPrinter(
        DataTable dt,
        List<KeyValuePair<string, string>> selectedCols,
        string yearMonth,
        string listTitle,
        string companyName,
        Image? logo,
        HashSet<string> numericColumns,
        List<string>? headerLines = null,
        string[]? signatureTitles = null)
    {
        _headerLines = headerLines;
        _signatureTitles = signatureTitles ?? new[] { "تهیه کننده", "بررسی کننده", "تایید کننده" };
        _dt = dt;
        _selectedCols = selectedCols;
        _yearMonth = yearMonth;
        _listTitle = listTitle;
        _companyName = companyName;
        _logo = logo;
        _numericColumns = numericColumns;
    }

    public void ShowPreview(string? printerName = null)
    {
        var printDoc = new PrintDocument();
        if (!string.IsNullOrEmpty(printerName))
        {
            printDoc.PrinterSettings.PrinterName = printerName;
            if (!printDoc.PrinterSettings.IsValid) printDoc.PrinterSettings = new PrinterSettings();
        }
        printDoc.DefaultPageSettings.Landscape = true;
        printDoc.DefaultPageSettings.Margins = new Margins(45, 45, 40, 40);

        int rowIndex = 0;
        int pageNum = 1;

        var totals = new Dictionary<string, decimal>();
        foreach (var col in _selectedCols)
        {
            if (_numericColumns.Contains(col.Key))
            {
                decimal sum = 0;
                foreach (DataRow row in _dt.Rows)
                {
                    if (decimal.TryParse(row[col.Key]?.ToString(), out decimal val))
                        sum += val;
                }
                totals[col.Key] = sum;
            }
        }

        printDoc.BeginPrint += (s, e) => { rowIndex = 0; pageNum = 1; };
        printDoc.PrintPage += (s, e) =>
        {
            Graphics g = e.Graphics!;
            var bounds = e.MarginBounds;
            int y = bounds.Top;

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            int totalCols = _selectedCols.Count + 1;
            float baseFontSize = 9.5f;

            if (totalCols >= 25) baseFontSize = 5.5f;
            else if (totalCols >= 20) baseFontSize = 6.5f;
            else if (totalCols >= 15) baseFontSize = 7.0f;
            else if (totalCols >= 12) baseFontSize = 8.0f;

            var fontCompany = ModernTheme.GetFont(16, FontStyle.Bold);
            var fontTitle = ModernTheme.GetFont(12, FontStyle.Bold);
            var fontHeader = ModernTheme.GetFont(baseFontSize, FontStyle.Bold);
            var fontBody = ModernTheme.GetFont(baseFontSize - 0.5f);
            var fontFooter = ModernTheme.GetFont(7f);

            var rtlCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft };
            var rtlRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft };

            if (pageNum == 1)
            {
                if (_logo is not null)
                {
                    try
                    {
                        float ratio = (float)_logo.Width / _logo.Height;
                        float maxWidth = 60;
                        float maxHeight = 60;
                        float drawWidth = maxWidth;
                        float drawHeight = drawWidth / ratio;
                        if (drawHeight > maxHeight) { drawHeight = maxHeight; drawWidth = drawHeight * ratio; }
                        g.DrawImage(_logo, bounds.Right - drawWidth, y, drawWidth, drawHeight);
                    }
                    catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "PayrollListPrinter"); }
                }

                g.DrawString(_companyName, fontCompany, Brushes.Black, new RectangleF(bounds.Left, y + 5, bounds.Width, 25), rtlCenter);

                if (_headerLines is { Count: > 0 })
                {
                    float hy = y + 30;
                    foreach (var line in _headerLines)
                    {
                        g.DrawString(line, fontTitle, Brushes.Black, new RectangleF(bounds.Left, hy, bounds.Width, 22), rtlCenter);
                        hy += 22;
                    }
                    y = (int)Math.Max(hy + 8, y + 70);
                }
                else
                {
                    // ✅ عنوان دقیق
                    g.DrawString($"{_listTitle} {_yearMonth}", fontTitle, Brushes.Black, new RectangleF(bounds.Left, y + 30, bounds.Width, 25), rtlCenter);
                    y += 60;
                }
            }

            float availableWidth = bounds.Width;
            float baseWidth = availableWidth / totalCols;

            // ✅ افزایش عرض ستون ردیف از 0.4f به 0.5f
            List<float> colWidths = new List<float> { baseWidth * 0.5f };
            float remainingWidth = availableWidth - (baseWidth * 0.5f);

            float totalWeight = 0;
            var colWeights = new Dictionary<string, float>();
            foreach (var col in _selectedCols)
            {
                float weight = 1.0f;
                if (_smallColumns.Contains(col.Key)) weight = 0.65f;

                colWeights[col.Key] = weight;
                totalWeight += weight;
            }

            foreach (var col in _selectedCols)
            {
                float width = (colWeights[col.Key] / totalWeight) * remainingWidth;
                colWidths.Add(width);
            }

            float tableWidth = colWidths.Sum();
            float startX = bounds.Left + (bounds.Width - tableWidth) / 2;
            float currentX = startX + tableWidth;

            // ✅ وسط‌چین عمودی: اگر کل جدول (سرستون + ردیف‌ها + جمع کل) در یک صفحه جا شود،
            //    بین عنوان و محل امضاها وسط صفحه قرار می‌گیرد؛ در غیر این صورت از بالا شروع می‌شود (چندصفحه‌ای)
            if (pageNum == 1 && rowIndex == 0)
            {
                float neededHeight = 22f + (_dt.Rows.Count + 1) * 18f;
                float availableHeight = (bounds.Bottom - 105) - y;
                if (neededHeight < availableHeight)
                    y += (int)((availableHeight - neededHeight) / 2f);
            }

            float headerHeight = 22f;
            g.FillRectangle(Brushes.LightGray, startX, y, tableWidth, headerHeight);
            g.DrawRectangle(Pens.Black, startX, y, tableWidth, headerHeight);

            currentX -= colWidths[0];
            g.DrawLine(Pens.Black, currentX, y, currentX, y + headerHeight);

            // ✅ اعمال GetFittedFont برای کلمه ردیف
            Font rowNumHeaderFont = GetFittedFont(g, "ردیف", fontHeader, colWidths[0] - 4, rtlCenter);
            g.DrawString("ردیف", rowNumHeaderFont, Brushes.Black, new RectangleF(currentX + 2, y, colWidths[0] - 4, headerHeight), rtlCenter);
            if (rowNumHeaderFont != fontHeader) rowNumHeaderFont.Dispose();

            for (int i = 0; i < _selectedCols.Count; i++)
            {
                currentX -= colWidths[i + 1];
                g.DrawLine(Pens.Black, currentX, y, currentX, y + headerHeight);

                Font cellFont = GetFittedFont(g, _selectedCols[i].Value, fontHeader, colWidths[i + 1] - 2, rtlCenter);
                g.DrawString(_selectedCols[i].Value, cellFont, Brushes.Black, new RectangleF(currentX + 1, y, colWidths[i + 1] - 2, headerHeight), rtlCenter);

                if (cellFont != fontHeader) cellFont.Dispose();
            }
            y += (int)headerHeight;

            float rowHeight = 18f;

            while (rowIndex < _dt.Rows.Count)
            {
                if (y + rowHeight > bounds.Bottom - 105)
                {
                    e.HasMorePages = true;
                    pageNum++;
                    return;
                }

                var row = _dt.Rows[rowIndex];
                currentX = startX + tableWidth;
                g.DrawRectangle(Pens.Black, startX, y, tableWidth, rowHeight);

                currentX -= colWidths[0];
                g.DrawLine(Pens.Black, currentX, y, currentX, y + rowHeight);

                Font rowNumFont = GetFittedFont(g, (rowIndex + 1).ToString(), fontBody, colWidths[0] - 2, rtlCenter);
                g.DrawString((rowIndex + 1).ToString(), rowNumFont, Brushes.Black, new RectangleF(currentX + 1, y, colWidths[0] - 2, rowHeight), rtlCenter);
                if (rowNumFont != fontBody) rowNumFont.Dispose();

                for (int i = 0; i < _selectedCols.Count; i++)
                {
                    currentX -= colWidths[i + 1];
                    g.DrawLine(Pens.Black, currentX, y, currentX, y + rowHeight);

                    string cellValue = row[_selectedCols[i].Key]?.ToString() ?? "";
                    if (_selectedCols[i].Key == "OvertimeHours")
                    {
                        decimal.TryParse(cellValue, out decimal otVal);
                        cellValue = FormatHhMm(otVal);
                    }
                    else if (_selectedCols[i].Key != "EmployeeName" && decimal.TryParse(cellValue, out decimal decVal))
                    {
                        cellValue = NumberHelper.FormatThousands(decVal);
                    }

                    Font cellFont = GetFittedFont(g, cellValue, fontBody, colWidths[i + 1] - 2, rtlCenter);
                    g.DrawString(cellValue, cellFont, Brushes.Black, new RectangleF(currentX + 1, y, colWidths[i + 1] - 2, rowHeight), rtlCenter);

                    if (cellFont != fontBody) cellFont.Dispose();
                }

                y += (int)rowHeight;
                rowIndex++;
            }

            if (rowIndex >= _dt.Rows.Count)
            {
                if (y + rowHeight > bounds.Bottom - 105)
                {
                    e.HasMorePages = true;
                    pageNum++;
                    return;
                }

                g.FillRectangle(Brushes.LightYellow, startX, y, tableWidth, rowHeight);
                g.DrawRectangle(Pens.Black, startX, y, tableWidth, rowHeight);

                currentX = startX + tableWidth;
                currentX -= colWidths[0];
                g.DrawLine(Pens.Black, currentX, y, currentX, y + rowHeight);

                for (int i = 0; i < _selectedCols.Count; i++)
                {
                    currentX -= colWidths[i + 1];
                    g.DrawLine(Pens.Black, currentX, y, currentX, y + rowHeight);

                    string cellValue = "";
                    if (i == 0) cellValue = "جمع کل";
                    else if (_selectedCols[i].Key == "OvertimeHours" && totals.ContainsKey("OvertimeHours")) cellValue = FormatHhMm(totals["OvertimeHours"]);
                    else if (totals.ContainsKey(_selectedCols[i].Key)) cellValue = NumberHelper.FormatThousands(totals[_selectedCols[i].Key], usePersianDigits: true);

                    Font cellFont = GetFittedFont(g, cellValue, fontHeader, colWidths[i + 1] - 2, rtlCenter);
                    g.DrawString(cellValue, cellFont, Brushes.Black, new RectangleF(currentX + 1, y, colWidths[i + 1] - 2, rowHeight), rtlCenter);

                    if (cellFont != fontHeader) cellFont.Dispose();
                }
                y += (int)rowHeight;
            }

            if (rowIndex >= _dt.Rows.Count)
            {
                float sigY = bounds.Bottom - 80;
                int n = _signatureTitles.Length;
                float sigWidth = (float)bounds.Width / n;
                string sigLine = "نام و امضاء:\n\n" + new string('_', n >= 4 ? 20 : 25);

                for (int i = 0; i < n; i++)
                {
                    // از راست به چپ
                    float x = bounds.Right - sigWidth * (i + 1);
                    g.DrawString(_signatureTitles[i], fontHeader, Brushes.Black, new RectangleF(x, sigY, sigWidth, 20), rtlCenter);
                    g.DrawString(sigLine, fontBody, Brushes.Black, new RectangleF(x, sigY + 18, sigWidth, 44), rtlCenter);
                }
            }

            g.DrawString($"صفحه {pageNum}", fontFooter, Brushes.Black, new RectangleF(bounds.Left, bounds.Bottom - 14, bounds.Width, 14), rtlCenter);
            e.HasMorePages = false;
        };

        using var ppd = new PrintPreviewDialog { Document = printDoc, Width = 1100, Height = 800, UseAntiAlias = true, Text = "پیش‌نمایش لیست حقوق" };
        ppd.ShowDialog();
    }

    private Font GetFittedFont(Graphics g, string text, Font originalFont, float maxWidth, StringFormat format)
    {
        if (string.IsNullOrEmpty(text)) return originalFont;
        SizeF textSize = g.MeasureString(text, originalFont, int.MaxValue, format);
        if (textSize.Width <= maxWidth) return originalFont;

        float currentSize = originalFont.Size - 0.5f;
        while (currentSize > 4f)
        {
            Font testFont = new Font(originalFont.FontFamily, currentSize, originalFont.Style);
            textSize = g.MeasureString(text, testFont, int.MaxValue, format);
            if (textSize.Width <= maxWidth) return testFont;
            testFont.Dispose();
            currentSize -= 0.5f;
        }
        return new Font(originalFont.FontFamily, 4f, originalFont.Style);
    }
}
