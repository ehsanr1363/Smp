using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Smp.Helpers;

// ═══════════════════════════════════════════════════════════════════════
//  ModernChoiceTile — رادیوی کاشی‌مانند (گوشه گرد، با حالت hover و انتخاب‌شده)
//  برای انتخاب «نوع پرداخت» و هر جای دیگری که گزینه‌ها باید به‌صورت کارت دیده شوند.
//  رفتار مثل RadioButton معمولی است (گروه‌بندی بر اساس والد، Checked، CheckedChanged).
//  نقطه‌ی رادیو در لبه‌ی راست و متن سمت چپ آن (راست‌چین) رسم می‌شود.
// ═══════════════════════════════════════════════════════════════════════
public class ModernChoiceTile : RadioButton
{
    private bool _hover;

    [DefaultValue(typeof(Color), "59, 130, 246")]
    public Color Accent { get; set; } = ModernTheme.Primary;

    public ModernChoiceTile()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Font = ModernTheme.GetFont(10.5f);
        ForeColor = ModernTheme.Text;
        AutoSize = false;
        Height = 44;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        ModernPaint.EnableSmooth(g);
        g.Clear(ModernPaint.BehindColor(this));

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        if (rect.Width < 24 || rect.Height < 16) return;

        bool on = Checked;
        bool enabled = Enabled;

        Color fill = !enabled ? ModernTheme.InputDisabled
                   : on ? ModernPaint.Tint(Accent, 10)
                   : _hover ? Color.White
                   : ModernTheme.SurfaceAlt;
        Color border = !enabled ? ModernTheme.InputBorderOff
                     : on ? Accent
                     : _hover ? ModernTheme.InputBorderHover
                     : ModernTheme.InputBorder;

        using (var path = ModernPaint.RoundedRect(rect, ModernTheme.CornerRadius))
        {
            using (var br = new SolidBrush(fill)) g.FillPath(br, path);
            using (var pen = new Pen(border, on ? 2f : 1f)) g.DrawPath(pen, path);
        }

        // ── نقطه‌ی رادیو (لبه‌ی راست) ──
        const int d = 18;
        var dot = new Rectangle(Width - 16 - d, (Height - d) / 2, d, d);
        if (on)
        {
            using (var br = new SolidBrush(enabled ? Accent : Color.FromArgb(190, 198, 208))) g.FillEllipse(br, dot);
            var inner = Rectangle.Inflate(dot, -6, -6);
            g.FillEllipse(Brushes.White, inner);
        }
        else
        {
            g.FillEllipse(Brushes.White, dot);
            using (var pen = new Pen(enabled ? Color.FromArgb(180, 190, 203) : ModernTheme.InputBorderOff, 1.6f))
                g.DrawEllipse(pen, dot);
        }

        // ── متن ──
        bool rtl = RightToLeft == RightToLeft.Yes;
        var font = on ? ModernTheme.GetFont(Font.Size, FontStyle.Bold) : Font;
        var textRect = new Rectangle(12, 0, Math.Max(0, dot.X - 10 - 12), Height);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        flags |= rtl ? (TextFormatFlags.Right | TextFormatFlags.RightToLeft) : TextFormatFlags.Left;
        Color tc = !enabled ? ModernTheme.TextDisabled : on ? ModernPaint.Darken(Accent, 60) : ForeColor;
        TextRenderer.DrawText(g, Text, font, textRect, tc, flags);

        // ── حلقه‌ی فوکوس کیبورد ──
        if (Focused && ShowFocusCues && enabled)
        {
            var fr = Rectangle.Inflate(rect, -3, -3);
            using var fp = ModernPaint.RoundedRect(fr, ModernTheme.CornerRadius - 2);
            using var pen = new Pen(Color.FromArgb(90, Accent), 1.5f);
            g.DrawPath(pen, fp);
        }
    }
}
