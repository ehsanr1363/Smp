using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;

namespace Smp.Helpers;

/// <summary>اطلاعات یک رسید پرداخت تنخواه</summary>
public class TankhahReceipt
{
    public string Number { get; set; } = "";
    public string DateText { get; set; } = "";
    public string Bank { get; set; } = "";
    public string Owner { get; set; } = "";
    public string ListTitle { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
}

/// <summary>
/// چاپ «رسید پرداخت وجه تنخواه» با لوگو و نام شرکت.
/// هر رسید یک صفحه‌ی A4 است و دو نسخه‌ی یکسان دارد (نسخه‌ی پرداخت‌کننده / نسخه‌ی تحویل‌گیرنده)
/// که با خط‌چین از هم جدا می‌شوند.
/// </summary>
public static class TankhahReceiptPrinter
{
    public static void ShowPreview(IList<TankhahReceipt> items, string companyName, Image? logo)
    {
        if (items is null || items.Count == 0) return;
        if (string.IsNullOrWhiteSpace(companyName)) companyName = "شرکت";

        using var pd = new PrintDocument { DocumentName = "رسید پرداخت تنخواه" };
        pd.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
        pd.DefaultPageSettings.Landscape = false;

        int index = 0;
        string printedAt = StringHelper.ConvertToPersianDigits(DateHelper.ToPersianDate(DateTime.Now));

        pd.BeginPrint += (s, e) => index = 0;
        pd.PrintPage += (s, e) =>
        {
            var g = e.Graphics;
            if (g is null) { e.HasMorePages = false; return; }
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            var mb = e.MarginBounds;
            float half = mb.Height / 2f;
            var item = items[index];

            var top = new RectangleF(mb.Left, mb.Top, mb.Width, half - 14);
            var bottom = new RectangleF(mb.Left, mb.Top + half + 14, mb.Width, half - 14);

            DrawReceipt(g, top, item, companyName, logo, "نسخه‌ی پرداخت‌کننده", printedAt);
            DrawReceipt(g, bottom, item, companyName, logo, "نسخه‌ی تحویل‌گیرنده", printedAt);

            // خط‌چین برش
            using (var pen = new Pen(Color.Gray, 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                g.DrawLine(pen, mb.Left, mb.Top + half, mb.Right, mb.Top + half);

            index++;
            e.HasMorePages = index < items.Count;
        };

        ModernPrint.ShowPreview(pd, "پیش‌نمایش رسید پرداخت تنخواه");
    }

    // ───────────────────────── رسم یک نسخه ─────────────────────────
    private static StringFormat Sf(StringAlignment align, bool wrap = false) => new()
    {
        Alignment = align,
        LineAlignment = StringAlignment.Center,
        FormatFlags = wrap
            ? StringFormatFlags.DirectionRightToLeft
            : StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap,
        Trimming = wrap ? StringTrimming.Word : StringTrimming.EllipsisCharacter
    };

    private static void DrawReceipt(Graphics g, RectangleF r, TankhahReceipt it, string companyName,
                                    Image? logo, string copyLabel, string printedAt)
    {
        // فونت‌ها از کش ModernTheme می‌آیند؛ نباید Dispose شوند
        var fCompany = ModernTheme.GetFont(15f, FontStyle.Bold);
        var fTitle = ModernTheme.GetFont(13f, FontStyle.Bold);
        var fLabel = ModernTheme.GetFont(10f, FontStyle.Bold);
        var fValue = ModernTheme.GetFont(10.5f);
        var fAmount = ModernTheme.GetFont(14f, FontStyle.Bold);
        var fSmall = ModernTheme.GetFont(8.5f);

        using var center = Sf(StringAlignment.Center);
        using var right = Sf(StringAlignment.Near);          // در RTL «Near» یعنی راست
        using var left = Sf(StringAlignment.Far);            // در RTL «Far» یعنی چپ
        using var rightWrap = Sf(StringAlignment.Near, wrap: true);

        // قاب بیرونی
        using (var pen = new Pen(Color.FromArgb(90, 90, 90), 1.3f))
            g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);

        float pad = 14f;
        float x0 = r.Left + pad, x1 = r.Right - pad, w = x1 - x0;
        float y = r.Top + 10f;

        // ── سربرگ: لوگو (راست)، نام شرکت و عنوان (وسط)، شماره/تاریخ (چپ) ──
        float logoBox = 56f, logoW = 0f;
        if (logo is not null)
        {
            try
            {
                float ratio = (float)logo.Width / logo.Height;
                float lw = logoBox, lh = logoBox;
                if (ratio >= 1f) lh = logoBox / ratio; else lw = logoBox * ratio;
                g.DrawImage(logo, x1 - lw, y + (logoBox - lh) / 2f, lw, lh);
                logoW = logoBox + 8f;
            }
            catch (Exception ex) { LogHelper.LogException(ex, "TankhahReceiptPrinter"); logoW = 0f; }
        }

        float sideW = 150f;
        var midRect = new RectangleF(x0 + sideW, y, w - sideW - Math.Max(logoW, sideW), 28);
        g.DrawString(companyName, fCompany, Brushes.Black, midRect, center);
        g.DrawString("رسید پرداخت وجه تنخواه", fTitle, Brushes.Black,
            new RectangleF(midRect.X, y + 28, midRect.Width, 24), center);

        g.DrawString("شماره: " + StringHelper.ConvertToPersianDigits(it.Number), fValue, Brushes.Black,
            new RectangleF(x0, y, sideW, 20), left);
        g.DrawString("تاریخ: " + StringHelper.ConvertToPersianDigits(it.DateText), fValue, Brushes.Black,
            new RectangleF(x0, y + 20, sideW, 20), left);
        g.DrawString(copyLabel, fSmall, Brushes.DimGray, new RectangleF(x0, y + 40, sideW, 16), left);

        y += logoBox + 8f;
        using (var pen = new Pen(Color.Black, 1.3f)) g.DrawLine(pen, x0, y, x1, y);
        y += 8f;

        // ── ردیف‌های اطلاعات ──
        const float labelW = 130f, rowH = 26f;
        void Row(string label, string value, bool wrap = false, float h = rowH)
        {
            g.DrawString(label, fLabel, Brushes.Black, new RectangleF(x1 - labelW, y, labelW, h), right);
            g.DrawString(value, fValue, Brushes.Black, new RectangleF(x0, y, w - labelW - 6f, h), wrap ? rightWrap : right);
            using var dot = new Pen(Color.LightGray, 0.8f);
            g.DrawLine(dot, x0, y + h, x1, y + h);
            y += h;
        }

        Row("پرداخت‌کننده:", companyName);
        Row("از حساب بانکی:", it.Bank);
        Row("تحویل‌گیرنده:", string.IsNullOrWhiteSpace(it.ListTitle) ? it.Owner : $"{it.Owner}  (تنخواه‌دار — {it.ListTitle})");

        // مبلغ: کادر برجسته
        y += 4f;
        var amountBox = new RectangleF(x0, y, w, 34f);
        using (var br = new SolidBrush(Color.FromArgb(240, 244, 248))) g.FillRectangle(br, amountBox);
        using (var pen = new Pen(Color.FromArgb(120, 120, 120), 1f)) g.DrawRectangle(pen, amountBox.X, amountBox.Y, amountBox.Width, amountBox.Height);
        g.DrawString("مبلغ (ریال):", fLabel, Brushes.Black,
            new RectangleF(amountBox.Right - labelW - 6f, y, labelW, amountBox.Height), right);
        g.DrawString(NumberHelper.FormatThousands(it.Amount, usePersianDigits: true), fAmount, Brushes.Black,
            new RectangleF(amountBox.X, y, amountBox.Width - labelW - 16f, amountBox.Height), center);
        y += amountBox.Height + 4f;

        Row("مبلغ به حروف:", NumberHelper.ToPersianWordsWithUnit(it.Amount, "ریال"), wrap: true, h: 34f);
        Row("بابت / توضیحات:", string.IsNullOrWhiteSpace(it.Description) ? "—" : it.Description, wrap: true, h: 34f);

        // ── امضاها (پایین قاب) ──
        float sigTop = r.Bottom - 56f;
        float sw = w / 3f;
        string[] titles = { "پرداخت‌کننده", "تحویل‌گیرنده (تنخواه‌دار)", "مدیر مالی" };
        for (int i = 0; i < 3; i++)
        {
            float sx = x1 - sw * (i + 1);
            g.DrawString(titles[i], fLabel, Brushes.Black, new RectangleF(sx, sigTop, sw, 18), center);
            g.DrawString("نام و امضاء", fSmall, Brushes.DimGray, new RectangleF(sx, sigTop + 18, sw, 16), center);
            using var pen = new Pen(Color.Gray, 0.8f);
            g.DrawLine(pen, sx + sw * 0.18f, sigTop + 46, sx + sw * 0.82f, sigTop + 46);
        }
    }
}
