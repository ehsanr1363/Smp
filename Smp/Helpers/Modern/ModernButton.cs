using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Smp.Helpers;

public enum ModernButtonStyle
{
    /// <summary>پُر و رنگی (دکمه‌ی اصلی)</summary>
    Filled,
    /// <summary>زمینه‌ی کم‌رنگ با متن رنگی (دکمه‌ی ثانویه)</summary>
    Soft,
    /// <summary>فقط حاشیه‌ی رنگی</summary>
    Outline
}

/// <summary>
/// دکمه‌ی مدرن: گوشه گرد، سایه‌ی نرم، حالت هاور/کلیک، آیکن اختیاری، حالت «در حال انجام».
/// جایگزین مستقیم UIHelper.CreateButton (امضای Create یکی است).
/// </summary>
public class ModernButton : Button
{
    private bool _hover, _down, _busy;
    private Color _accent = ModernTheme.Primary;
    private ModernButtonStyle _variant = ModernButtonStyle.Filled;
    private Image? _icon;

    [DefaultValue(typeof(Color), "59, 130, 246")]
    public Color Accent { get => _accent; set { _accent = value; Invalidate(); } }

    [DefaultValue(ModernButtonStyle.Filled)]
    public ModernButtonStyle Variant { get => _variant; set { _variant = value; Invalidate(); } }

    [DefaultValue(10)]
    public int CornerRadius { get; set; } = 10;

    /// <summary>آیکن کنار متن (سمت شروع؛ در راست‌به‌چپ سمت راست متن)</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image? Icon { get => _icon; set { _icon = value; Invalidate(); } }

    [DefaultValue(18)]
    public int IconSize { get; set; } = 18;

    /// <summary>در حال انجام: دکمه خاکستری می‌شود و کلیک نادیده گرفته می‌شود</summary>
    [DefaultValue(false)]
    public bool Busy { get => _busy; set { _busy = value; Invalidate(); } }

    /// <summary>
    /// برای سازگاری با کدهای قدیمی (btn.BackColor = رنگ): تنظیم BackColor همان Accent را عوض می‌کند.
    /// </summary>
    public override Color BackColor
    {
        get => base.BackColor;
        set
        {
            base.BackColor = value;
            if (value.A == 255 && value != SystemColors.Control && value != Color.Transparent) _accent = value;
            Invalidate();
        }
    }

    public ModernButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Font = ModernTheme.GetFont(11f, FontStyle.Bold);
        Margin = new Padding(5);
        Size = new Size(130, 40);
    }

    // ───────────── سازنده‌های آماده ─────────────
    public static ModernButton Create(string text, Color accent, int width = 130, int height = 40,
                                      ModernButtonStyle style = ModernButtonStyle.Filled)
    {
        var b = new ModernButton { Text = text, Accent = accent, Variant = style, Width = width, Height = height };
        b.MakeCompact();   // همه‌ی دکمه‌های مدرن هم‌اندازه‌ی دکمه‌های ایمپورت (جمع‌وجور)
        return b;
    }

    /// <summary>(قدیمی) دکمه‌های اکسل/ایمپورت/قالب؛ اکنون همه‌ی دکمه‌ها جمع‌وجور ساخته می‌شوند</summary>
    public static bool IsExcelAction(string? text)
        => !string.IsNullOrEmpty(text) && (text.Contains("اکسل") || text.Contains("ایمپورت") || text.Contains("قالب"));

    /// <summary>فونت کوچک‌تر، ارتفاع حداکثر ۳۴ و عرض متناسب با متن (بدون بزرگ‌تر شدن از عرض درخواستی)</summary>
    public void MakeCompact()
    {
        Font = ModernTheme.GetFont(9.5f, FontStyle.Bold);
        IconSize = 16;
        Height = Math.Min(Height, 34);
        int fit = TextRenderer.MeasureText(Text ?? string.Empty, Font).Width + 40;
        Width = Math.Max(96, Math.Min(Width, fit));
    }

    public static ModernButton Primary(string text, int width = 130, int height = 40) => Create(text, ModernTheme.Primary, width, height);
    public static ModernButton Save(string text = "ذخیره", int width = 130, int height = 40) => Create(text, ModernTheme.Brand, width, height);
    public static ModernButton Success(string text, int width = 130, int height = 40) => Create(text, ModernTheme.Success, width, height);
    public static ModernButton Danger(string text, int width = 130, int height = 40) => Create(text, ModernTheme.Danger, width, height);
    public static ModernButton Soft(string text, Color accent, int width = 130, int height = 40) => Create(text, accent, width, height, ModernButtonStyle.Soft);
    public static ModernButton Outline(string text, Color accent, int width = 130, int height = 40) => Create(text, accent, width, height, ModernButtonStyle.Outline);
    public static ModernButton Cancel(string text = "انصراف", int width = 110, int height = 40) => Create(text, ModernTheme.Neutral, width, height, ModernButtonStyle.Outline);

    // ───────────── رویدادها ─────────────
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _down = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnClick(EventArgs e) { if (_busy) return; base.OnClick(e); }

    // ───────────── رسم ─────────────
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(ModernPaint.BehindColor(this));

        bool active = Enabled && !_busy;
        bool filled = Variant == ModernButtonStyle.Filled;
        int shadow = (filled && active && !_down && Height >= 34) ? 2 : 0;
        int top = (_down && filled) ? 1 : 0;
        var rect = new Rectangle(0, top, Width - 1, Height - 1 - 2 - top + (shadow == 0 ? 2 : 0));
        if (rect.Width < 4 || rect.Height < 4) return;

        Color text;
        Color border = Color.Empty;
        Color fillTop, fillBottom;

        if (!active)
        {
            fillTop = fillBottom = filled ? Color.FromArgb(226, 232, 240) : Color.FromArgb(246, 245, 243);
            text = Color.FromArgb(148, 163, 184);
            if (!filled) border = Color.FromArgb(226, 232, 240);
        }
        else if (Variant == ModernButtonStyle.Filled)
        {
            Color b = _down ? ModernPaint.Darken(Accent, 28) : _hover ? ModernPaint.Darken(Accent, 14) : Accent;
            fillTop = ModernPaint.Lighten(b, 0.10f);
            fillBottom = b;
            text = Color.White;
        }
        else if (Variant == ModernButtonStyle.Soft)
        {
            fillTop = fillBottom = ModernPaint.Blend(Color.White, Accent, _down ? 0.26f : _hover ? 0.20f : 0.12f);
            text = ModernPaint.Darken(Accent, 40);
        }
        else // Outline
        {
            fillTop = fillBottom = ModernPaint.Blend(Color.White, Accent, _down ? 0.16f : _hover ? 0.08f : 0f);
            border = Accent;
            text = ModernPaint.Darken(Accent, 20);
        }

        using (var path = ModernPaint.RoundedRect(rect, CornerRadius))
        {
            if (shadow > 0)
            {
                var sr = new Rectangle(rect.X + 1, rect.Y + 2, rect.Width - 2, rect.Height);
                using var sp = ModernPaint.RoundedRect(sr, CornerRadius);
                using var sb = new SolidBrush(Color.FromArgb(_hover ? 70 : 45, ModernPaint.Darken(Accent, 60)));
                g.FillPath(sb, sp);
            }

            if (fillTop == fillBottom)
            {
                using var br = new SolidBrush(fillTop);
                g.FillPath(br, path);
            }
            else
            {
                using var br = new LinearGradientBrush(rect, fillTop, fillBottom, 90f);
                g.FillPath(br, path);
            }

            if (border != Color.Empty)
            {
                using var pen = new Pen(border, Variant == ModernButtonStyle.Outline ? 1.5f : 1f);
                g.DrawPath(pen, path);
            }
        }

        if (Focused && ShowFocusCues && active)
        {
            var fr = Rectangle.Inflate(rect, -3, -3);
            if (fr.Width > 6 && fr.Height > 6)
            {
                using var fp = ModernPaint.RoundedRect(fr, Math.Max(3, CornerRadius - 3));
                using var pen = new Pen(Color.FromArgb(filled ? 150 : 120, filled ? Color.White : Accent), 1.5f) { DashStyle = DashStyle.Dot };
                g.DrawPath(pen, fp);
            }
        }

        bool rtl = RightToLeft == RightToLeft.Yes;
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        if (rtl) flags |= TextFormatFlags.RightToLeft;

        string caption = _busy ? "…" : (Text ?? string.Empty);
        bool hasIcon = _icon is not null && !_busy;
        int iconW = hasIcon ? IconSize : 0;
        int gap = hasIcon && caption.Length > 0 ? 8 : 0;

        int maxTextW = Math.Max(10, rect.Width - 16 - iconW - gap);
        int tw = caption.Length == 0 ? 0
               : Math.Min(maxTextW, TextRenderer.MeasureText(g, caption, Font, new Size(int.MaxValue, rect.Height), flags | TextFormatFlags.NoPadding).Width);
        int total = tw + gap + iconW;
        int startX = rect.X + (rect.Width - total) / 2;

        int iconX, textX;
        if (rtl) { iconX = startX + total - iconW; textX = startX; }
        else     { iconX = startX;                 textX = startX + iconW + gap; }

        if (hasIcon && _icon is not null)
            g.DrawImage(_icon, new Rectangle(iconX, rect.Y + (rect.Height - IconSize) / 2, IconSize, IconSize));

        if (caption.Length > 0)
            TextRenderer.DrawText(g, caption, Font, new Rectangle(textX, rect.Y, tw + 2, rect.Height), text,
                flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
    }
}
