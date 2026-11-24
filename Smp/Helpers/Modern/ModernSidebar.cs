using System;
using System.Drawing;
using System.Windows.Forms;

namespace Smp.Helpers;

// ═══════════════════════════════════════════════════════════════════════
//  ModernSidebar — اجزای آکاردئونی منوی کناری (جایگزین بخش سایدبار UIHelper)
// ═══════════════════════════════════════════════════════════════════════
public static class ModernSidebar
{
    // ── رنگ‌های منوی کناری ──
    public static readonly Color Back = Color.FromArgb(240, 244, 248);
    public static readonly Color ItemText = Color.FromArgb(30, 41, 59);

    // ==========================================================
    // 🔘 متدهای آکاردئون (نسخه جدید با فونت بزرگ)
    // ==========================================================

    /// <summary>
    /// هدر رنگی بخش‌های سایدبار. با کلیک روی آن، بخش زیرش باز/بسته می‌شود.
    /// </summary>
    public static Panel CreateAccordionHeader(string title, string icon, Color accentColor, bool isExpanded, out Label chevron)
    {
        // هدر روشن (تینت ملایم رنگ بخش) با نوار رنگی لبه‌ی راست؛ متن تیره برای خوانایی
        var baseBack = ModernPaint.Tint(accentColor, 14);
        var hoverBack = ModernPaint.Tint(accentColor, 26);
        var textColor = ModernPaint.Darken(accentColor, 80);

        var header = new Panel
        {
            Width = 240,
            Height = 42,
            BackColor = baseBack,
            Margin = new Padding(0, 10, 0, 0),
            Cursor = Cursors.Hand
        };

        var colorStrip = new Panel
        {
            Width = 4,
            Dock = DockStyle.Right,
            BackColor = accentColor
        };

        chevron = new Label
        {
            Text = isExpanded ? "▼" : "▶",
            Width = 30,
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(9f, FontStyle.Bold),
            ForeColor = accentColor,
            BackColor = Color.Transparent
        };

        var lblIcon = new Label
        {
            Text = icon,
            Width = 32,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(13f),
            ForeColor = textColor,
            BackColor = Color.Transparent
        };

        var lblTitle = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = ModernTheme.GetFont(11f, FontStyle.Bold),
            ForeColor = textColor,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 0, 5, 0)
        };

        EventHandler onEnter = (s, e) => header.BackColor = hoverBack;
        EventHandler onLeave = (s, e) => header.BackColor = baseBack;

        header.MouseEnter += onEnter; header.MouseLeave += onLeave;
        chevron.MouseEnter += onEnter; chevron.MouseLeave += onLeave;
        lblIcon.MouseEnter += onEnter; lblIcon.MouseLeave += onLeave;
        lblTitle.MouseEnter += onEnter; lblTitle.MouseLeave += onLeave;

        header.Controls.Add(lblTitle);
        header.Controls.Add(chevron);
        header.Controls.Add(lblIcon);
        header.Controls.Add(colorStrip);

        return header;
    }

    /// <summary>
    /// پنل سفید زیرمجموعه که دکمه‌های آکاردئون را نگه می‌دارد.
    /// </summary>
    public static FlowLayoutPanel CreateAccordionContainer(bool isExpanded)
    {
        return new FlowLayoutPanel
        {
            Width = 240,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Visible = isExpanded,
            BackColor = ModernTheme.Surface,
            Margin = new Padding(0, 0, 0, 5)
        };
    }

    /// <summary>
    /// دکمه‌ی زیرمجموعه با فونت بزرگ، بولد و رنگ تیره برای خوانایی بالا.
    /// </summary>
    public static Button CreateAccordionButton(string key, string icon, string text, Color accentColor)
    {
        var hoverTint = ModernPaint.Tint(accentColor, 12);
        var pressTint = ModernPaint.Tint(accentColor, 20);

        var btn = new Button
        {
            Text = $"    {icon}  {text}",
            Width = 240,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            BackColor = ModernTheme.Surface,
            ForeColor = ItemText,
            Font = ModernTheme.GetFont(11f, FontStyle.Bold), // فونت بزرگ و بولد
            TextAlign = ContentAlignment.MiddleRight,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 1, 0, 1),
            Tag = key,
            Padding = new Padding(10, 0, 5, 0)
        };

        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.BorderColor = ModernTheme.Border;
        btn.FlatAppearance.MouseOverBackColor = hoverTint;
        btn.FlatAppearance.MouseDownBackColor = pressTint;

        bool isHovering = false;
        btn.MouseEnter += (s, e) => { isHovering = true; btn.Invalidate(); };
        btn.MouseLeave += (s, e) => { isHovering = false; btn.Invalidate(); };

        btn.Paint += (s, e) => {
            if (isHovering)
            {
                using var brush = new SolidBrush(accentColor);
                e.Graphics.FillRectangle(brush, btn.Width - 4, 0, 4, btn.Height);
            }
        };

        return btn;
    }
}
