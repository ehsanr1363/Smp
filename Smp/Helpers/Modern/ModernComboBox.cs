using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Smp.Helpers;

/// <summary>
/// کمبوباکس مدرن (گوشه گرد، فلش نرم، حاشیه رنگی هنگام فوکوس/هاور، لیست باز سفارشی).
/// فقط حالت DropDownList را پشتیبانی می‌کند. جایگزین مستقیم ComboBox معمولی است.
/// </summary>
public class ModernComboBox : ComboBox
{
    private bool _hover;

    [DefaultValue(typeof(Color), "59, 130, 246")]
    public Color AccentColor { get; set; } = Color.FromArgb(59, 130, 246);

    [DefaultValue(typeof(Color), "255, 254, 252")]
    public Color FieldColor { get; set; } = Color.FromArgb(255, 254, 252);

    [DefaultValue(typeof(Color), "222, 212, 202")]
    public Color BorderColorNormal { get; set; } = Color.FromArgb(222, 212, 202);

    [DefaultValue(10)]
    public int CornerRadius { get; set; } = 10;

    public ModernComboBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        ItemHeight = 24;
        DropDownHeight = 280;
    }

    // ───────────── رویدادهایی که ظاهر را به‌روز می‌کنند ─────────────
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnDropDown(EventArgs e) { Invalidate(); base.OnDropDown(e); }
    protected override void OnDropDownClosed(EventArgs e) { Invalidate(); base.OnDropDownClosed(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnSelectedIndexChanged(EventArgs e) { Invalidate(); base.OnSelectedIndexChanged(e); }
    protected override void OnDataSourceChanged(EventArgs e) { Invalidate(); base.OnDataSourceChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void WndProc(ref Message m)
    {
        const int WM_ERASEBKGND = 0x0014;
        if (m.Msg == WM_ERASEBKGND) { m.Result = (IntPtr)1; return; }
        base.WndProc(ref m);
    }

    // ───────────── رسم بدنه کمبو ─────────────
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // رنگ پشت گوشه‌های گرد = رنگ اولین والد غیرشفاف
        Color behind = SystemColors.Control;
        for (Control? p = Parent; p is not null; p = p.Parent)
            if (p.BackColor.A == 255) { behind = p.BackColor; break; }
        g.Clear(behind);

        bool focused = Focused || DroppedDown;
        Color border = !Enabled ? Color.FromArgb(230, 226, 221)
                     : focused ? AccentColor
                     : _hover ? Color.FromArgb(190, 200, 215)
                     : BorderColorNormal;
        Color fill = Enabled ? FieldColor : Color.FromArgb(244, 242, 239);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = RoundedRect(rect, CornerRadius))
        {
            using (var br = new SolidBrush(fill)) g.FillPath(br, path);
            using (var pen = new Pen(border, focused ? 2f : 1f)) g.DrawPath(pen, path);
        }

        // دکمه فلش (سمت چپ، مناسب چیدمان راست‌به‌چپ)
        int chipW = 26, chipH = Math.Max(14, Height - 10);
        var chip = new Rectangle(5, (Height - chipH) / 2, chipW, chipH);
        Color chipBack = !Enabled ? Color.FromArgb(236, 233, 229)
                       : focused ? Color.FromArgb(219, 234, 254)
                       : _hover ? Color.FromArgb(232, 240, 253)
                       : Color.FromArgb(240, 245, 252);
        using (var cp = RoundedRect(chip, Math.Max(4, CornerRadius - 3)))
        using (var cb = new SolidBrush(chipBack)) g.FillPath(cb, cp);

        Color arrow = !Enabled ? Color.FromArgb(185, 180, 175) : focused ? AccentColor : Color.FromArgb(100, 116, 139);
        float cx = chip.X + chip.Width / 2f, cy = chip.Y + chip.Height / 2f;
        float d = DroppedDown ? -1f : 1f;
        using (var pen = new Pen(arrow, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            g.DrawLines(pen, new[] { new PointF(cx - 4f, cy - 2f * d), new PointF(cx, cy + 2f * d), new PointF(cx + 4f, cy - 2f * d) });

        // متن انتخاب‌شده
        string text = SelectedIndex >= 0 ? GetItemText(SelectedItem) : string.Empty;
        var textRect = new Rectangle(chip.Right + 6, 2, Width - chip.Right - 6 - 12, Height - 4);
        Color textColor = !Enabled ? Color.FromArgb(150, 145, 140)
                        : IsPlaceholder(text) ? Color.FromArgb(150, 150, 155)
                        : Color.FromArgb(50, 45, 42);
        TextRenderer.DrawText(g, text, Font, textRect, textColor,
            TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }

    // ───────────── رسم آیتم‌های لیست باز ─────────────
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        var g = e.Graphics;
        string text = GetItemText(Items[e.Index]);
        bool hot = (e.State & DrawItemState.Selected) != 0;

        using (var bg = new SolidBrush(FieldColor)) g.FillRectangle(bg, e.Bounds);

        if (hot)
        {
            var r = Rectangle.Inflate(e.Bounds, -4, -1);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRect(r, 6))
            using (var br = new SolidBrush(Color.FromArgb(225, 238, 254))) g.FillPath(br, path);
            using (var bar = new SolidBrush(AccentColor))
                g.FillRectangle(bar, r.Right - 3, r.Y + 4, 3, r.Height - 8);   // نوار رنگی سمت راست
        }

        Color tc = hot ? Color.FromArgb(30, 64, 175) : IsPlaceholder(text) ? Color.FromArgb(150, 150, 155) : Color.FromArgb(50, 45, 42);
        var tr = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 24, e.Bounds.Height);
        TextRenderer.DrawText(g, text, Font, tr, tc,
            TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }

    private static bool IsPlaceholder(string t) => t.TrimStart().StartsWith("---");

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = Math.Max(1, radius * 2);
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
