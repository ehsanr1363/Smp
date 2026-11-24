using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Smp.Helpers;

// ═══════════════════════════════════════════════════════════════════════
//  ModernTheme — تنها منبع رنگ، فونت و اندازه‌ها برای همه‌ی کنترل‌های مدرن
//  (جایگزین UIHelper.Warm* و UIHelper.PersianFont)
// ═══════════════════════════════════════════════════════════════════════
public static class ModernTheme
{
    // ── سطح‌ها ──
    public static readonly Color Background = Color.FromArgb(253, 251, 247);
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceAlt = Color.FromArgb(255, 252, 248);   // ردیف‌های زوج جدول
    public static readonly Color SurfaceMuted = Color.FromArgb(246, 241, 235);
    public static readonly Color InputFill = Color.FromArgb(255, 254, 252);
    public static readonly Color InputDisabled = Color.FromArgb(244, 242, 239);

    // ── خطوط ──
    public static readonly Color Border = Color.FromArgb(235, 225, 215);
    public static readonly Color InputBorder = Color.FromArgb(222, 212, 202);
    public static readonly Color InputBorderHover = Color.FromArgb(190, 200, 215);
    public static readonly Color InputBorderOff = Color.FromArgb(230, 226, 221);

    // ── متن ──
    public static readonly Color Text = Color.FromArgb(60, 50, 45);
    public static readonly Color TextStrong = Color.FromArgb(50, 45, 42);
    public static readonly Color TextMuted = Color.FromArgb(100, 116, 139);
    public static readonly Color Placeholder = Color.FromArgb(150, 150, 155);
    public static readonly Color TextDisabled = Color.FromArgb(150, 145, 140);

    // ── رنگ‌های معنایی ──
    public static readonly Color Primary = Color.FromArgb(59, 130, 246);    // آبی (فوکوس، تب‌ها)
    public static readonly Color Brand = Color.FromArgb(234, 124, 105);   // مرجانی (دکمه ذخیره)
    public static readonly Color Success = Color.FromArgb(34, 197, 94);
    public static readonly Color Danger = Color.FromArgb(239, 68, 68);
    public static readonly Color Warning = Color.FromArgb(245, 158, 11);
    public static readonly Color Neutral = Color.FromArgb(100, 116, 139);
    public static readonly Color Teal = Color.FromArgb(20, 184, 166);
    public static readonly Color Purple = Color.FromArgb(168, 85, 247);
    public static readonly Color Orange = Color.FromArgb(249, 115, 22);
    public static readonly Color Secondary = Color.FromArgb(107, 144, 128);   // سبز خاکستری
    public static readonly Color Dark = Color.FromArgb(30, 41, 59);

    public static readonly Color PrimarySoft = Color.FromArgb(225, 238, 254);
    public static readonly Color SuccessSoft = Color.FromArgb(220, 252, 231);
    public static readonly Color DangerSoft = Color.FromArgb(254, 226, 226);
    public static readonly Color WarningSoft = Color.FromArgb(255, 243, 205);
    public static readonly Color DangerText = Color.FromArgb(153, 27, 27);
    public static readonly Color WarningText = Color.FromArgb(133, 100, 4);

    // ── جدول ──
    public static readonly Color GridHeaderBack = Color.FromArgb(246, 239, 231);
    public static readonly Color GridHeaderText = Color.FromArgb(90, 60, 40);
    public static readonly Color GridLine = Color.FromArgb(240, 232, 224);
    public static readonly Color GridHover = Color.FromArgb(251, 246, 240);
    public static readonly Color GridSelection = Color.FromArgb(253, 229, 215);

    // ── اندازه‌ها ──
    public const int InputHeight = 36;
    public const int LabelWidth = 130;
    public const int RowHeight = 46;
    public const int CornerRadius = 10;

    // ── فونت (با کش؛ اولین فونت نصب‌شده از فهرست انتخاب می‌شود) ──
    private static readonly string[] FontFamilies = { "Vazirmatn", "IRANSans", "B Nazanin", "Tahoma" };
    private static readonly ConcurrentDictionary<(float Size, FontStyle Style), Font> _fonts = new();

    public static Font GetFont(float size = 11f, FontStyle style = FontStyle.Regular)
    {
        return _fonts.GetOrAdd((size, style), key =>
        {
            foreach (var name in FontFamilies)
            {
                try
                {
                    var f = new Font(name, key.Size, key.Style);
                    if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) return f;
                    f.Dispose();
                }
                catch (Exception __ex) { /* فونت نصب نیست */ Smp.Helpers.LogHelper.LogException(__ex, "ModernTheme"); }
            }
            return new Font("Tahoma", key.Size, key.Style);
        });
    }
}

// ═══════════════════════════════════════════════════════════════════════
//  ابزارهای رسم مشترک
// ═══════════════════════════════════════════════════════════════════════
public static class ModernPaint
{
    public static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath();

        // ۱. اگر عرض یا ارتفاع مستطیل صفر یا منفی باشد، از رسم کمان‌ها جلوگیری می‌کنیم تا خطای GDI+ رخ ندهد.
        if (r.Width <= 0 || r.Height <= 0)
        {
            return p;
        }

        int d = Math.Max(1, radius * 2);
        d = Math.Min(d, Math.Min(r.Width, r.Height));

        // ۲. اگر مقدار d صفر شد (مثلاً وقتی radius صفر است)، یک مستطیل ساده رسم می‌کنیم.
        if (d <= 0)
        {
            p.AddRectangle(r);
            return p;
        }

        // رسم مستطیل با گوشه‌های گرد
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();

        return p;
    }

    /// <summary>رنگ پشت گوشه‌های گرد = رنگ اولین والد غیرشفاف</summary>
    public static Color BehindColor(Control c)
    {
        for (Control? p = c.Parent; p is not null; p = p.Parent)
            if (p.BackColor.A == 255) return p.BackColor;
        return SystemColors.Control;
    }

    /// <summary>t=0 → a ، t=1 → b</summary>
    public static Color Blend(Color a, Color b, float t)
    {
        t = Math.Max(0f, Math.Min(1f, t));
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    public static Color Darken(Color c, int amount)
        => Color.FromArgb(Math.Max(0, c.R - amount), Math.Max(0, c.G - amount), Math.Max(0, c.B - amount));

    public static Color Lighten(Color c, float t) => Blend(c, Color.White, t);

    /// <summary>رنگ کم‌رنگ شده روی سفید؛ percent بین ۰ تا ۱۰۰</summary>
    public static Color Tint(Color c, int percent)
        => Blend(Color.White, c, Math.Max(0, Math.Min(100, percent)) / 100f);

    public static void EnableSmooth(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }
}

/// <summary>TableLayoutPanel بدون پرش تصویر هنگام رنگ‌آمیزی</summary>
public class BufferedTable : TableLayoutPanel
{
    public BufferedTable() { DoubleBuffered = true; }
}
