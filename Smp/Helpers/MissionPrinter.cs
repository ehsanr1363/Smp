using ClosedXML.Excel;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Smp.Helpers;

/// <summary>یک برگه‌ی ماموریت به‌همراه اطلاعات شرکت (نام و لوگو) برای چاپ/خروجی.</summary>
public class MissionPrintItem
{
    public MissionRecord Record { get; set; } = new MissionRecord();
    public string CompanyName { get; set; } = "";
    public Image? Logo { get; set; }
}

/// <summary>
/// چاپ و خروجی اکسل «خلاصه محاسبات حق ماموریت داخل کشور» مطابق فرمت فایل اکسل نمونه.
/// هر برگه‌ی ماموریت = یک صفحه‌ی A4 افقی.
/// </summary>
public class MissionPrinter
{
    public const string ReportTitle = "خلاصه محاسبات حق ماموریت داخل کشور";
    private static readonly string[] SignatureTitles = { "حسابداری", "مدیر مالی", "مدیرعامل" };

    private readonly IList<MissionPrintItem> _items;
    private int _index;

    public MissionPrinter(IList<MissionPrintItem> items) => _items = items;

    // ═══════════════════════════ پیش‌نمایش و چاپ ═══════════════════════════
    public void ShowPreview()
    {
        if (_items.Count == 0) return;

        var doc = new PrintDocument();
        doc.DefaultPageSettings.Landscape = true;
        doc.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
        doc.DefaultPageSettings.Margins = new Margins(40, 40, 35, 35);
        doc.DocumentName = ReportTitle;

        doc.BeginPrint += (s, e) => _index = 0;
        doc.PrintPage += PrintPage;

        using var ppd = new PrintPreviewDialog
        {
            Document = doc,
            Width = 1100,
            Height = 800,
            UseAntiAlias = true,
            Text = "پیش‌نمایش چاپ برگه ماموریت",
            RightToLeft = RightToLeft.Yes,
            RightToLeftLayout = true
        };
        ppd.ShowDialog();
    }

    private void PrintPage(object sender, PrintPageEventArgs e)
    {
        if (_index >= _items.Count) { e.HasMorePages = false; return; }

        var g = e.Graphics!;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        DrawSheet(g, e.MarginBounds, _items[_index]);

        _index++;
        e.HasMorePages = _index < _items.Count;
    }

    // ═══════════════════════════ رسم یک برگه ═══════════════════════════
    // ستون‌ها مطابق اکسل: B(برچسب) C(روز) D(شب) E(اقامت) F(ایاب و ذهاب) G(نهار) H I J K (ساعت/تاریخ رفت و برگشت)
    private static readonly float[] ColWeights = { 26f, 20.7f, 13f, 13f, 12.4f, 13f, 11.4f, 13f, 13f, 13f };

    private static readonly Color Ink = Color.FromArgb(40, 40, 40);
    private static readonly Color Line = Color.FromArgb(90, 90, 90);
    private static readonly Color HeaderFill = Color.FromArgb(255, 236, 153);   // زرد ملایم (هدرهای جدول اکسل)
    private static readonly Color NameFill = Color.FromArgb(255, 224, 102);
    private static readonly Color LabelFill = Color.FromArgb(244, 244, 240);
    private static readonly Color TotalFill = Color.FromArgb(214, 238, 220);

    private void DrawSheet(Graphics g, Rectangle bounds, MissionPrintItem item)
    {
        var m = item.Record;
        float left = bounds.Left, right = bounds.Right, width = bounds.Width;
        float y = bounds.Top;

        // ─── سربرگ: لوگو + نام شرکت + عنوان ───
        float headerH = 92f;
        DrawLogoAndTitle(g, item, left, right, y, width, headerH);
        y += headerH + 6f;

        // ─── جدول ───
        float totalW = ColWeights.Sum();
        float scale = width / totalW;
        float[] edgeRight = new float[ColWeights.Length + 1];  // لبه‌ی راست هر ستون (RTL)
        edgeRight[0] = right;
        for (int i = 0; i < ColWeights.Length; i++) edgeRight[i + 1] = edgeRight[i] - ColWeights[i] * scale;

        RectangleF Cell(int c1, int c2, float top, float h) =>
            new RectangleF(edgeRight[c2 + 1], top, edgeRight[c1] - edgeRight[c2 + 1], h);

        float[] rh = { 42f, 46f, 38f, 40f, 52f, 40f, 40f, 58f };
        float[] ry = new float[rh.Length + 1];
        ry[0] = y;
        for (int i = 0; i < rh.Length; i++) ry[i + 1] = ry[i] + rh[i];

        var fTitleName = ModernTheme.GetFont(15f, FontStyle.Bold);
        var fHead = ModernTheme.GetFont(11.5f, FontStyle.Bold);
        var fBody = ModernTheme.GetFont(11f, FontStyle.Bold);
        var fLabel = ModernTheme.GetFont(10f, FontStyle.Bold);
        var fSmall = ModernTheme.GetFont(9f, FontStyle.Regular);

        string D(DateTime dt) => StringHelper.ConvertToPersianDigits(DateHelper.ToPersianDate(dt));
        string N(decimal v) => NumberHelper.FormatThousands(v, usePersianDigits: true);
        string Z(decimal v) => v == 0 ? "—" : N(v);
        string C(int v) => StringHelper.ConvertToPersianDigits(v.ToString());
        string T(string s) => string.IsNullOrWhiteSpace(s) ? "—" : StringHelper.ConvertToPersianDigits(s);

        // ردیف ۱ (هدرهای زرد)
        Box(g, Cell(0, 0, ry[0], rh[0] + rh[1] + rh[2]), m.EmployeeName, fTitleName, NameFill);
        Box(g, Cell(1, 1, ry[0], rh[0]), "استان ماموریت", fHead, HeaderFill);
        Box(g, Cell(2, 2, ry[0], rh[0]), "محل ماموریت", fHead, HeaderFill);
        Box(g, Cell(3, 5, ry[0], rh[0]), "موضوع ماموریت", fHead, HeaderFill);
        Box(g, Cell(6, 9, ry[0], rh[0]), "تاریخ و ساعت", fHead, HeaderFill);

        // ردیف ۲
        Box(g, Cell(1, 1, ry[1], rh[1]), m.Province, fBody, null);
        Box(g, Cell(2, 2, ry[1], rh[1]), m.Location, fBody, null);
        Box(g, Cell(3, 5, ry[1], rh[1]), m.Subject, ModernTheme.GetFont(10.5f, FontStyle.Bold), null);
        Box(g, Cell(6, 7, ry[1], rh[1]), "رفت", fHead, LabelFill);
        Box(g, Cell(8, 9, ry[1], rh[1]), "برگشت", fHead, LabelFill);

        // ردیف ۳ (عنوان ستون‌ها)
        string[] colTitles = { "روز", "شب", "اقامت", "هزینه ایاب و ذهاب", "حق نهار" };
        for (int i = 0; i < colTitles.Length; i++)
            Box(g, Cell(1 + i, 1 + i, ry[2], rh[2]), colTitles[i], fLabel, LabelFill);
        Box(g, Cell(6, 6, ry[2], rh[2]), "ساعت", fLabel, LabelFill);
        Box(g, Cell(7, 7, ry[2], rh[2]), "تاریخ", fLabel, LabelFill);
        Box(g, Cell(8, 8, ry[2], rh[2]), "ساعت", fLabel, LabelFill);
        Box(g, Cell(9, 9, ry[2], rh[2]), "تاریخ", fLabel, LabelFill);

        // ردیف ۴: تعداد
        Box(g, Cell(0, 0, ry[3], rh[3]), "تعداد روز", fLabel, LabelFill);
        Box(g, Cell(1, 1, ry[3], rh[3]), C(m.DaysCount), fBody, null);
        Box(g, Cell(2, 2, ry[3], rh[3]), m.NightsCount == 0 ? "—" : C(m.NightsCount), fBody, null);
        Box(g, Cell(3, 3, ry[3], rh[3]), m.StayCount == 0 ? "—" : C(m.StayCount), fBody, null);
        Box(g, Cell(4, 4, ry[3], rh[3]), "—", fBody, null);
        Box(g, Cell(5, 5, ry[3], rh[3]), m.LunchCount == 0 ? "—" : C(m.LunchCount), fBody, null);
        Box(g, Cell(6, 6, ry[3], rh[3]), T(m.DepartTime), fBody, null);
        Box(g, Cell(7, 7, ry[3], rh[3]), D(m.DepartDate), ModernTheme.GetFont(10f, FontStyle.Bold), null);
        Box(g, Cell(8, 8, ry[3], rh[3]), T(m.ReturnTime), fBody, null);
        Box(g, Cell(9, 9, ry[3], rh[3]), D(m.ReturnDate), ModernTheme.GetFont(10f, FontStyle.Bold), null);

        // ردیف ۵: جمع حقوق و مزایا + «توضیحات»
        Box(g, Cell(0, 0, ry[4], rh[4]), "جمع حقوق و مزایا / حق‌الزحمه مشاوره", fLabel, LabelFill);
        Box(g, Cell(1, 1, ry[4], rh[4]), N(m.SalaryBase), fBody, null);
        for (int c = 2; c <= 5; c++) Box(g, Cell(c, c, ry[4], rh[4]), "—", fBody, null);
        Box(g, Cell(6, 9, ry[4], rh[4]), "توضیحات", fHead, LabelFill);

        // ردیف ۶: مبلغ واحد
        string rateText = StringHelper.ConvertToPersianDigits(m.MissionRate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
        Box(g, Cell(0, 0, ry[5], rh[5]), $"مبلغ ماموریت ({rateText}٪) / شبانه‌روز / اقامت / نهاری", fLabel, LabelFill);
        Box(g, Cell(1, 1, ry[5], rh[5]), N(m.DailyAmount), fBody, null);
        Box(g, Cell(2, 2, ry[5], rh[5]), Z(m.NightUnit), fBody, null);
        Box(g, Cell(3, 3, ry[5], rh[5]), Z(m.StayUnit), fBody, null);
        Box(g, Cell(4, 4, ry[5], rh[5]), "—", fBody, null);
        Box(g, Cell(5, 5, ry[5], rh[5]), Z(m.LunchUnit), fBody, null);

        // ردیف ۷: جمع مبلغ
        Box(g, Cell(0, 0, ry[6], rh[6]), "جمع مبلغ حق ماموریت", fLabel, LabelFill);
        Box(g, Cell(1, 1, ry[6], rh[6]), N(m.DaysTotal), fBody, null);
        Box(g, Cell(2, 2, ry[6], rh[6]), Z(m.NightsTotal), fBody, null);
        Box(g, Cell(3, 3, ry[6], rh[6]), Z(m.StayTotal), fBody, null);
        Box(g, Cell(4, 4, ry[6], rh[6]), Z(m.TravelCost), fBody, null);
        Box(g, Cell(5, 5, ry[6], rh[6]), Z(m.LunchTotal), fBody, null);

        // ردیف ۸: جمع کل (+ به حروف)
        Box(g, Cell(0, 0, ry[7], rh[7]), "جمع کل (ریال)", ModernTheme.GetFont(12f, FontStyle.Bold), TotalFill);
        var totalRect = Cell(1, 5, ry[7], rh[7]);
        Box(g, totalRect, "", fBody, TotalFill);
        DrawText(g, N(m.GrandTotal), ModernTheme.GetFont(14f, FontStyle.Bold), new RectangleF(totalRect.Left, totalRect.Top + 2, totalRect.Width, totalRect.Height * 0.55f));
        string words = "";
        try { words = NumberHelper.ToPersianWordsWithUnit(m.GrandTotal, "ریال"); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "MissionPrinter"); }
        DrawText(g, words, fSmall, new RectangleF(totalRect.Left + 4, totalRect.Top + totalRect.Height * 0.55f, totalRect.Width - 8, totalRect.Height * 0.43f));

        // کادر توضیحات (H8:K10 در اکسل)
        var notesRect = new RectangleF(edgeRight[9 + 1], ry[5], edgeRight[6] - edgeRight[10], rh[5] + rh[6] + rh[7]);
        Box(g, notesRect, m.Notes ?? "", ModernTheme.GetFont(10f, FontStyle.Regular), null);

        // قاب بیرونی ضخیم‌تر
        using (var pen = new Pen(Ink, 2f))
            g.DrawRectangle(pen, edgeRight[10], ry[0], right - edgeRight[10], ry[rh.Length] - ry[0]);

        // ─── امضاها ───
        float sigTop = ry[rh.Length] + 28f;
        float sigH = 95f, gap = 14f;
        float sigW = (width - gap * (SignatureTitles.Length - 1)) / SignatureTitles.Length;
        for (int i = 0; i < SignatureTitles.Length; i++)
        {
            float sx = right - (i + 1) * sigW - i * gap;
            var rect = new RectangleF(sx, sigTop, sigW, sigH);
            using (var pen = new Pen(Line, 1f)) g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            g.FillRectangle(new SolidBrush(LabelFill), rect.X + 0.5f, rect.Y + 0.5f, rect.Width - 1, 24f);
            DrawText(g, SignatureTitles[i], fLabel, new RectangleF(rect.X, rect.Y, rect.Width, 24f));
            DrawText(g, "نام و امضا", fSmall, new RectangleF(rect.X, rect.Bottom - 22f, rect.Width, 20f), Brushes.Gray);
        }

        // ─── پاورقی ───
        string foot = "تاریخ چاپ: " + StringHelper.ConvertToPersianDigits(DateHelper.ToPersianDate(DateTime.Now));
        DrawText(g, foot, fSmall, new RectangleF(left, bounds.Bottom - 16f, 250f, 16f), Brushes.Gray, StringAlignment.Near);
    }

    private static void DrawLogoAndTitle(Graphics g, MissionPrintItem item, float left, float right, float y, float width, float headerH)
    {
        // لوگو سمت راست
        float logoBox = 78f;
        if (item.Logo is not null)
        {
            try
            {
                float ratio = (float)item.Logo.Width / item.Logo.Height;
                float w = logoBox, h = logoBox;
                if (ratio >= 1) h = w / ratio; else w = h * ratio;
                g.DrawImage(item.Logo, right - w - 4f, y + (headerH - h) / 2f, w, h);
            }
            catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "MissionPrinter"); }
        }

        var rect1 = new RectangleF(left + 100f, y + 8f, width - 200f, 38f);
        var rect2 = new RectangleF(left + 100f, y + 48f, width - 200f, 36f);
        DrawText(g, item.CompanyName, ModernTheme.GetFont(20f, FontStyle.Bold), rect1);
        DrawText(g, ReportTitle, ModernTheme.GetFont(17f, FontStyle.Bold), rect2);

        using var pen = new Pen(Ink, 1.5f);
        g.DrawLine(pen, left, y + headerH, right, y + headerH);
    }

    private static readonly StringFormat Center = new StringFormat
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center,
        FormatFlags = StringFormatFlags.DirectionRightToLeft,
        Trimming = StringTrimming.EllipsisCharacter
    };

    private static void DrawText(Graphics g, string text, Font font, RectangleF rect, Brush? brush = null, StringAlignment align = StringAlignment.Center)
    {
        if (string.IsNullOrEmpty(text)) return;
        using var sf = new StringFormat(Center) { Alignment = align };
        g.DrawString(text, font, brush ?? Brushes.Black, rect, sf);
    }

    /// <summary>سلول جدول: پس‌زمینه، حاشیه و متن وسط‌چین با کوچک‌شدن خودکار فونت در صورت طولانی بودن.</summary>
    private static void Box(Graphics g, RectangleF rect, string text, Font font, Color? fill)
    {
        if (fill.HasValue)
            using (var br = new SolidBrush(fill.Value)) g.FillRectangle(br, rect);
        using (var pen = new Pen(Line, 1f)) g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

        if (string.IsNullOrWhiteSpace(text)) return;

        var inner = RectangleF.Inflate(rect, -4f, -2f);
        var f = font;
        bool created = false;
        try
        {
            for (float size = font.Size; size >= 7f; size -= 0.5f)
            {
                if (created) f.Dispose();
                f = new Font(font.FontFamily, size, font.Style);
                created = true;
                var sz = g.MeasureString(text, f, (int)inner.Width, Center);
                if (sz.Height <= inner.Height && sz.Width <= inner.Width + 1) break;
            }
            g.DrawString(text, f, Brushes.Black, inner, Center);
        }
        finally { if (created) f.Dispose(); }
    }

    private static void MergeText(IXLWorksheet ws, string address, string text)
    {
        var first = address.Split(':')[0];
        ws.Cell(first).Value = text;
        ws.Range(address).Merge();
    }

    private static void MergeFormula(IXLWorksheet ws, string address, string formula)
    {
        var first = address.Split(':')[0];
        ws.Cell(first).FormulaA1 = formula;
        ws.Range(address).Merge();
    }

    // ═══════════════════════════ خروجی اکسل ═══════════════════════════
    /// <summary>هر برگه‌ی ماموریت یک Sheet؛ با همان چیدمان فایل اکسل نمونه و فرمول‌های زنده.</summary>
    public static void ExportExcel(IList<MissionPrintItem> items, string filePath)
    {
        using var wb = new XLWorkbook();
        int n = 0;
        foreach (var it in items)
        {
            n++;
            var m = it.Record;
            string sheetName = $"ماموریت {n}";
            var ws = wb.Worksheets.Add(sheetName);
            ws.RightToLeft = true;
            ws.ShowGridLines = false;
            ws.Style.Font.FontName = "B Nazanin";
            ws.Style.Font.FontSize = 13;
            ws.Style.Font.Bold = true;
            ws.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Style.Alignment.WrapText = true;

            double[] widths = { 3.5, 26, 20.7, 13, 13, 12.4, 13, 11.4, 13, 13, 13 };
            for (int i = 0; i < widths.Length; i++) ws.Column(i + 1).Width = widths[i];
            double[] heights = { 49.5, 40, 50, 50, 36, 40, 52, 40, 40, 52, 30, 60 };
            for (int i = 0; i < heights.Length; i++) ws.Row(i + 1).Height = heights[i];

            var yellow = XLColor.FromHtml("#FFEC99");
            var label = XLColor.FromHtml("#F4F4F0");
            var green = XLColor.FromHtml("#D6EEDC");

            // سربرگ
            MergeText(ws, "B1:K1", it.CompanyName);
            ws.Range("B1:K1").Style.Font.FontSize = 20;
            MergeText(ws, "B2:K2", ReportTitle);
            ws.Range("B2:K2").Style.Font.FontSize = 17;

            // لوگو (سمت راست)
            if (it.Logo is not null)
            {
                try
                {
                    using var ms = new MemoryStream();
                    it.Logo.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    double ratio = (double)it.Logo.Width / it.Logo.Height;
                    int h = 60, w = (int)(h * ratio);
                    ws.AddPicture(ms).MoveTo(ws.Cell("B1"), 6, 4).WithSize(w, h);
                }
                catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "MissionPrinter"); }
            }

            // ردیف ۳ تا ۵: هدرها
            MergeText(ws, "B3:B5", m.EmployeeName);
            ws.Range("B3:B5").Style.Font.FontSize = 20;
            ws.Range("B3:B5").Style.Fill.BackgroundColor = yellow;
            ws.Cell("C3").Value = "استان ماموریت";
            ws.Cell("D3").Value = "محل ماموریت";
            MergeText(ws, "E3:G3", "موضوع ماموریت");
            MergeText(ws, "H3:K3", "تاریخ و ساعت");
            ws.Range("C3:K3").Style.Fill.BackgroundColor = yellow;

            ws.Cell("C4").Value = m.Province;
            ws.Cell("D4").Value = m.Location;
            MergeText(ws, "E4:G4", m.Subject);
            ws.Range("E4:G4").Style.Font.FontSize = 12;
            MergeText(ws, "H4:I4", "رفت");
            MergeText(ws, "J4:K4", "برگشت");

            string[] t5 = { "روز", "شب", "اقامت", "هزینه ایاب و ذهاب", "حق نهار", "ساعت", "تاریخ", "ساعت", "تاریخ" };
            for (int i = 0; i < t5.Length; i++) ws.Cell(5, 3 + i).Value = t5[i];
            ws.Range("C4:K5").Style.Fill.BackgroundColor = label;

            // ردیف ۶: تعداد
            ws.Cell("B6").Value = "تعداد روز";
            ws.Cell("C6").Value = m.DaysCount;
            ws.Cell("D6").Value = m.NightsCount;
            ws.Cell("E6").Value = m.StayCount;
            ws.Cell("F6").Value = 0;
            ws.Cell("G6").Value = m.LunchCount;
            ws.Cell("H6").Value = m.DepartTime;
            ws.Cell("I6").Value = DateHelper.ToPersianDate(m.DepartDate);
            ws.Cell("J6").Value = m.ReturnTime;
            ws.Cell("K6").Value = DateHelper.ToPersianDate(m.ReturnDate);
            ws.Range("I6").Style.Font.FontSize = 11; ws.Range("K6").Style.Font.FontSize = 11;

            // ردیف ۷: مبنا
            ws.Cell("B7").Value = "جمع حقوق و مزایا / حق‌الزحمه مشاوره";
            ws.Cell("C7").Value = m.SalaryBase;
            MergeText(ws, "H7:K7", "توضیحات");
            ws.Range("H7:K7").Style.Fill.BackgroundColor = label;

            // ردیف ۸: مبلغ واحد (فرمول زنده: مبنا × درصد)
            string rate = m.MissionRate.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
            ws.Cell("B8").Value = $"مبلغ ماموریت ({rate}٪) / شبانه‌روز / اقامت / نهاری";
            ws.Cell("C8").FormulaA1 = $"ROUND(C7*{rate}/100,0)";
            ws.Cell("D8").Value = m.NightUnit;
            ws.Cell("E8").Value = m.StayUnit;
            ws.Cell("F8").Value = 0;
            ws.Cell("G8").Value = m.LunchUnit;

            // ردیف ۹: جمع مبلغ
            ws.Cell("B9").Value = "جمع مبلغ حق ماموریت";
            ws.Cell("C9").FormulaA1 = "C6*C8";
            ws.Cell("D9").FormulaA1 = "D6*D8";
            ws.Cell("E9").FormulaA1 = "E6*E8";
            ws.Cell("F9").Value = m.TravelCost;
            ws.Cell("G9").FormulaA1 = "G6*G8";

            // ردیف ۱۰: جمع کل
            ws.Cell("B10").Value = "جمع کل (ریال)";
            MergeFormula(ws, "C10:G10", "SUM(C9:G9)");
            ws.Range("B10:G10").Style.Fill.BackgroundColor = green;
            ws.Range("C10:G10").Style.Font.FontSize = 16;

            // توضیحات
            MergeText(ws, "H8:K10", m.Notes ?? "");
            ws.Range("H8:K10").Style.Font.Bold = false;

            // برچسب‌ها
            ws.Range("B6:B9").Style.Fill.BackgroundColor = label;
            ws.Range("B6:B9").Style.Font.FontSize = 12;

            // فرمت عدد
            ws.Range("C7:G10").Style.NumberFormat.Format = "#,##0";
            ws.Range("C6:G6").Style.NumberFormat.Format = "0";

            // کادرها
            var table = ws.Range("B3:K10");
            table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            table.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

            // امضاها
            ws.Cell("B12").Value = SignatureTitles[0];
            MergeText(ws, "C12:D12", SignatureTitles[1]);
            MergeText(ws, "F12:G12", SignatureTitles[2]);
            foreach (var addr in new[] { "B12", "C12:D12", "F12:G12" })
            {
                var r = ws.Range(addr);
                r.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                r.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                r.Style.Fill.BackgroundColor = label;
            }

            // تنظیمات چاپ: A4 افقی، یک صفحه
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.FitToPages(1, 1);
            ws.PageSetup.CenterHorizontally = true;
            ws.PageSetup.Margins.Left = ws.PageSetup.Margins.Right = 0.4;
            ws.PageSetup.PrintAreas.Add("A1:L12");
        }
        wb.SaveAs(filePath);
    }
}
