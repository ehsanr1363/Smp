using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Smp.Helpers;

// ═══════════════════════════════════════════════════════════════════════
//  چک‌باکس و رادیو‌باتن مدرن
//  موقعیت‌ها روی صفحه (چپ ← راست):
//     LabelOnRight = true  (پیش‌فرض):   [ ☐ ] لیبل      لیبل در لبه‌ی راست، کادر سمت چپِ لیبل
//     LabelOnRight = false (کلاسیک):    لیبل [ ☐ ]      کادر در لبه‌ی راست، لیبل سمت چپِ کادر
//  خوشه‌ی (کادر + لیبل) همیشه به لبه‌ی راست کنترل می‌چسبد.
// ═══════════════════════════════════════════════════════════════════════
internal static class ModernToggle
{
    public const int BoxSize = 20;
    public const int Gap = 8;

    public static Size Measure(string? text, Font font, bool rtl)
    {
        int tw = 0, th = 0;
        if (!string.IsNullOrEmpty(text))
        {
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            if (rtl) flags |= TextFormatFlags.RightToLeft;
            var ts = TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), flags);
            tw = ts.Width; th = ts.Height;
        }
        int w = BoxSize + (tw > 0 ? Gap + tw : 0) + 6;
        return new Size(w, Math.Max(30, th + 8));
    }

    public static void Paint(Graphics g, Control c, string? text, bool isRadio, bool isChecked,
                             bool hover, bool focusCue, Color accent, bool labelOnRight)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(ModernPaint.BehindColor(c));

        bool rtl = c.RightToLeft == RightToLeft.Yes;
        bool enabled = c.Enabled;
        string caption = text ?? string.Empty;
        bool hasText = caption.Length > 0;

        int gap = hasText ? Gap : 0;
        int maxTextW = Math.Max(0, c.Width - 4 - BoxSize - gap);
        int tw = 0;
        if (hasText)
        {
            var mflags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            if (rtl) mflags |= TextFormatFlags.RightToLeft;
            tw = Math.Min(maxTextW, TextRenderer.MeasureText(caption, c.Font, new Size(int.MaxValue, int.MaxValue), mflags).Width);
        }

        int boxY = (c.Height - BoxSize) / 2;
        Rectangle box, textRect;
        if (labelOnRight)
        {
            // [کادر][فاصله][لیبل]  ← چسبیده به لبه‌ی راست
            textRect = new Rectangle(c.Width - 2 - tw, 0, tw, c.Height);
            box = new Rectangle(textRect.X - gap - BoxSize, boxY, BoxSize, BoxSize);
        }
        else
        {
            // [لیبل][فاصله][کادر]  ← کادر در لبه‌ی راست
            box = new Rectangle(c.Width - 2 - BoxSize, boxY, BoxSize, BoxSize);
            textRect = new Rectangle(box.X - gap - tw, 0, tw, c.Height);
        }
        if (box.X < 0) box.X = 0;

        // ── کادر ──
        Color offFill = enabled ? Color.White : Color.FromArgb(244, 242, 239);
        Color idleBorder = !enabled ? Color.FromArgb(226, 232, 240)
                         : (hover || focusCue) ? accent : Color.FromArgb(180, 190, 203);

        using (var path = isRadio ? EllipsePath(box) : ModernPaint.RoundedRect(box, 6))
        {
            if (isChecked)
            {
                Color fill = enabled ? accent : Color.FromArgb(190, 198, 208);
                using var br = new SolidBrush(fill);
                g.FillPath(br, path);
            }
            else
            {
                using var br = new SolidBrush(offFill);
                g.FillPath(br, path);
                using var pen = new Pen(idleBorder, 1.6f);
                g.DrawPath(pen, path);
            }
        }

        if (isChecked)
        {
            if (isRadio)
            {
                var dot = new Rectangle(box.X + 6, box.Y + 6, BoxSize - 12, BoxSize - 12);
                using var br = new SolidBrush(Color.White);
                g.FillEllipse(br, dot);
            }
            else
            {
                using var pen = new Pen(Color.White, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawLines(pen, new[]
                {
                    new PointF(box.X + 5f, box.Y + 10.5f),
                    new PointF(box.X + 8.6f, box.Y + 14f),
                    new PointF(box.X + 15f, box.Y + 6.5f)
                });
            }
        }

        if (focusCue && enabled)
        {
            using var fp = isRadio ? EllipsePath(Rectangle.Inflate(box, 3, 3)) : ModernPaint.RoundedRect(Rectangle.Inflate(box, 3, 3), 8);
            using var pen = new Pen(Color.FromArgb(90, accent), 1.5f);
            g.DrawPath(pen, fp);
        }

        // ── متن ──
        if (hasText && textRect.Width > 0)
        {
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
            flags |= rtl ? (TextFormatFlags.Right | TextFormatFlags.RightToLeft) : TextFormatFlags.Left;
            TextRenderer.DrawText(g, caption, c.Font, textRect, enabled ? c.ForeColor : ModernTheme.TextDisabled, flags);
        }
    }

    private static GraphicsPath EllipsePath(Rectangle r)
    {
        var p = new GraphicsPath();
        p.AddEllipse(r);
        return p;
    }
}

public class ModernCheckBox : CheckBox
{
    private bool _hover;
    private bool _labelOnRight = true;

    [DefaultValue(typeof(Color), "59, 130, 246")]
    public Color Accent { get; set; } = ModernTheme.Primary;

    /// <summary>true (پیش‌فرض): لیبل سمت راست کادر.  false: کادر در لبه‌ی راست و لیبل سمت چپ آن</summary>
    [DefaultValue(true)]
    public bool LabelOnRight { get => _labelOnRight; set { _labelOnRight = value; Invalidate(); } }

    public ModernCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Font = ModernTheme.GetFont(11f);
        ForeColor = ModernTheme.Text;
        Height = 30;
    }

    public override Size GetPreferredSize(Size proposedSize)
        => ModernToggle.Measure(Text, Font, RightToLeft == RightToLeft.Yes);

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
        => ModernToggle.Paint(e.Graphics, this, Text, false, Checked, _hover, Focused && ShowFocusCues, Accent, _labelOnRight);
}

public class ModernRadioButton : RadioButton
{
    private bool _hover;
    private bool _labelOnRight = true;

    [DefaultValue(typeof(Color), "59, 130, 246")]
    public Color Accent { get; set; } = ModernTheme.Primary;

    [DefaultValue(true)]
    public bool LabelOnRight { get => _labelOnRight; set { _labelOnRight = value; Invalidate(); } }

    public ModernRadioButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Font = ModernTheme.GetFont(11f);
        ForeColor = ModernTheme.Text;
        Height = 30;
    }

    public override Size GetPreferredSize(Size proposedSize)
        => ModernToggle.Measure(Text, Font, RightToLeft == RightToLeft.Yes);

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
        => ModernToggle.Paint(e.Graphics, this, Text, true, Checked, _hover, Focused && ShowFocusCues, Accent, _labelOnRight);
}
