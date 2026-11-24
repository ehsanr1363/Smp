using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ClosedXML.Excel;

namespace Smp.Helpers;

/// <summary>
/// ساخت خروجی اکسل «گزارش‌ساز» با همان ظاهر فایل نمونه (گزارش_پرسنل_*.xlsx):
/// فونت B Nazanin، راست‌به‌چپ، تیتر + خط تاریخ/تعداد، هدر کرم با متن قهوه‌ای،
/// ردیف‌های داده با پس‌زمینه‌ی کرم روشن، حاشیه‌ی نازک و تنظیمات چاپ.
/// </summary>
public static class ReportExcelStyler
{
    // ───── مشخصات استخراج‌شده از فایل نمونه ─────
    private const string FontName = "B Nazanin";

    private static readonly XLColor HeaderFill = XLColor.FromArgb(0xF8, 0xED, 0xE3);
    private static readonly XLColor HeaderFont = XLColor.FromArgb(0x5A, 0x3C, 0x28);
    private static readonly XLColor RowFill    = XLColor.FromArgb(0xFD, 0xFA, 0xF5);
    private static readonly XLColor SubtitleFont = XLColor.FromArgb(0x64, 0x74, 0x8B);

    private const double TitleRowHeight  = 24;
    private const double HeaderRowHeight = 24;
    private const double DataRowHeight   = 21;

    // ردیف‌ها: 1 = تیتر، 2 = تاریخ/تعداد، 3 = خالی، 4 = هدر، 5.. = داده
    private const int TitleRow = 1;
    private const int SubtitleRow = 2;
    private const int HeaderRow = 4;
    private const int FirstDataRow = 5;

    /// <summary>
    /// یک شیت با ظاهر نمونه به workbook اضافه می‌کند.
    /// </summary>
    /// <param name="title">مثلاً «گزارش پرسنل - قراردادی / ماهانه»</param>
    /// <param name="persianDate">مثلاً «1405/09/01»</param>
    /// <param name="headers">عنوان ستون‌ها (بدون «ردیف»، خودکار اضافه می‌شود)</param>
    /// <param name="rows">هر عنصر = یک ردیف از مقادیر، به ترتیب headers</param>
    /// <param name="textColumns">اندیس ستون‌هایی (از صفر، نسبت به headers) که باید متن باشند، مثل کد ملی</param>
    public static IXLWorksheet AddReportSheet(
        XLWorkbook workbook,
        string sheetName,
        string title,
        string persianDate,
        IList<string> headers,
        IList<object?[]> rows,
        ISet<int>? textColumns = null)
    {
        var ws = workbook.Worksheets.Add(SafeSheetName(sheetName, workbook));
        ws.RightToLeft = true;
        ws.Style.Font.FontName = FontName;

        int colCount = headers.Count + 1;   // +1 برای ستون «ردیف»
        int lastRow = FirstDataRow + rows.Count - 1;

        // ───── تیتر (در نمونه از ستون B تا آخر ادغام شده) ─────
        var titleRange = ws.Range(TitleRow, Math.Min(2, colCount), TitleRow, colCount).Merge();
        titleRange.Value = title;
        titleRange.Style.Font.FontName = FontName;
        titleRange.Style.Font.FontSize = 14;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontColor = XLColor.Black;
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Row(TitleRow).Height = TitleRowHeight;

        // ───── خط تاریخ و تعداد رکورد ─────
        var subRange = ws.Range(SubtitleRow, Math.Min(2, colCount), SubtitleRow, colCount).Merge();
        subRange.Value = $"تاریخ: {persianDate}  —  تعداد رکورد: {rows.Count}";
        subRange.Style.Font.FontName = FontName;
        subRange.Style.Font.FontSize = 10;
        subRange.Style.Font.Bold = false;
        subRange.Style.Font.FontColor = SubtitleFont;
        subRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // ───── هدر جدول ─────
        ws.Cell(HeaderRow, 1).Value = "ردیف";
        for (int c = 0; c < headers.Count; c++)
            ws.Cell(HeaderRow, c + 2).Value = headers[c];

        var headerRange = ws.Range(HeaderRow, 1, HeaderRow, colCount);
        headerRange.Style.Font.FontName = FontName;
        headerRange.Style.Font.FontSize = 12;
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = HeaderFont;
        headerRange.Style.Fill.BackgroundColor = HeaderFill;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        SetThinBorders(headerRange);
        ws.Row(HeaderRow).Height = HeaderRowHeight;

        // ───── ردیف‌های داده ─────
        for (int r = 0; r < rows.Count; r++)
        {
            int excelRow = FirstDataRow + r;
            ws.Cell(excelRow, 1).Value = r + 1;

            for (int c = 0; c < headers.Count; c++)
            {
                var cell = ws.Cell(excelRow, c + 2);
                object? v = c < rows[r].Length ? rows[r][c] : null;

                if (textColumns is not null && textColumns.Contains(c))
                {
                    cell.Style.NumberFormat.Format = "@";          // کد ملی با صفر ابتدایی حفظ شود
                    cell.Value = v?.ToString() ?? "";
                }
                else
                {
                    SetCellValue(cell, v);
                }
            }
            ws.Row(excelRow).Height = DataRowHeight;
        }

        if (rows.Count > 0)
        {
            var dataRange = ws.Range(FirstDataRow, 1, lastRow, colCount);
            dataRange.Style.Font.FontName = FontName;
            dataRange.Style.Font.FontSize = 12;
            dataRange.Style.Font.Bold = true;
            dataRange.Style.Font.FontColor = XLColor.Black;
            dataRange.Style.Fill.BackgroundColor = RowFill;
            dataRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            SetThinBorders(dataRange);
        }

        // ───── فیلتر، عرض ستون‌ها، تنظیمات چاپ ─────
        if (rows.Count > 0)
            ws.Range(HeaderRow, 1, lastRow, colCount).SetAutoFilter();

        AutoSizeColumns(ws, colCount, HeaderRow, lastRow);

        ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        ws.PageSetup.FitToPages(1, 0);                 // عرض: ۱ صفحه، ارتفاع: آزاد
        ws.PageSetup.Margins.Left = 0.748;
        ws.PageSetup.Margins.Right = 0.748;
        ws.PageSetup.Margins.Top = 0.748;
        ws.PageSetup.Margins.Bottom = 0.512;
        ws.PageSetup.SetRowsToRepeatAtTop(HeaderRow, HeaderRow);

        return ws;
    }

    /// <summary>
    /// نسخه‌ی ساده برای DataTable: ستون‌ها با ColumnName به‌عنوان هدر.
    /// </summary>
    public static IXLWorksheet AddReportSheet(
        XLWorkbook workbook,
        string sheetName,
        string title,
        string persianDate,
        DataTable table,
        ISet<string>? textColumnNames = null)
    {
        var headers = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
        var rows = table.Rows.Cast<DataRow>().Select(r => r.ItemArray).ToList();

        ISet<int>? textIdx = null;
        if (textColumnNames is not null)
            textIdx = new HashSet<int>(headers
                .Select((h, i) => (h, i))
                .Where(x => textColumnNames.Contains(x.h))
                .Select(x => x.i));

        return AddReportSheet(workbook, sheetName, title, persianDate, headers, rows, textIdx);
    }

    // ───────────────────────── کمکی‌ها ─────────────────────────

    private static void SetThinBorders(IXLRange range)
    {
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.Black;
        range.Style.Border.InsideBorderColor = XLColor.Black;
    }

    private static void SetCellValue(IXLCell cell, object? v)
    {
        switch (v)
        {
            case null:
            case DBNull:
                cell.Value = "";
                break;
            case DateTime d:
                cell.Value = d;
                break;
            case bool b:
                cell.Value = b ? "بله" : "خیر";
                break;
            case int or long or short or byte or decimal or double or float:
                cell.Value = Convert.ToDouble(v);
                break;
            default:
                cell.Value = v.ToString();
                break;
        }
    }

    private static void AutoSizeColumns(IXLWorksheet ws, int colCount, int fromRow, int toRow)
    {
        for (int c = 1; c <= colCount; c++)
        {
            int maxLen = 0;
            for (int r = fromRow; r <= Math.Max(fromRow, toRow); r++)
            {
                var text = ws.Cell(r, c).GetFormattedString();
                if (text.Length > maxLen) maxLen = text.Length;
            }
            // ستون «ردیف» باریک، بقیه بر اساس طول محتوا (حداقل ۱۰، حداکثر ۴۵)
            double width = c == 1 ? 8.86 : Math.Min(45, Math.Max(10, maxLen * 1.3 + 3));
            ws.Column(c).Width = width;
        }
    }

    private static string SafeSheetName(string name, XLWorkbook wb)
    {
        foreach (var ch in new[] { ':', '\\', '/', '?', '*', '[', ']' })
            name = name.Replace(ch.ToString(), "");
        name = string.IsNullOrWhiteSpace(name) ? "Sheet" : name.Trim();
        if (name.Length > 31) name = name.Substring(0, 31);

        string candidate = name;
        int i = 2;
        while (wb.Worksheets.Any(w => w.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            string suffix = $" ({i++})";
            candidate = name.Substring(0, Math.Min(name.Length, 31 - suffix.Length)) + suffix;
        }
        return candidate;
    }
}
