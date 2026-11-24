using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Smp.Helpers;

// ═══════════════════════════════════════════════════════════════════════
//  ModernListBox — لیست‌باکس مدرن (قاب گرد، آیتم‌های گرد، هاور، حالت چک‌دار)
//  ShowCheckBoxes = true  ⇒ جایگزین CheckedListBox
// ═══════════════════════════════════════════════════════════════════════
public class ModernListBox : Control
{
    private readonly ListBox _lb;
    private readonly HashSet<int> _checked = new HashSet<int>();
    private bool _hover, _focus, _showChecks;
    private int _hot = -1;

    public Color AccentColor { get; set; } = ModernTheme.Primary;
    public int CornerRadius { get; set; } = ModernTheme.CornerRadius;

    /// <summary>ListBox داخلی (برای دسترسی به امکانات پیشرفته)</summary>
    public ListBox Inner => _lb;

    public event EventHandler? SelectedIndexChanged;
    public event EventHandler? CheckedChanged;

    public ModernListBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = false;
        Size = new Size(240, 160);

        _lb = new ListBox
        {
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 32,
            IntegralHeight = false,
            RightToLeft = RightToLeft.Yes,
            Font = ModernTheme.GetFont(11f),
            BackColor = ModernTheme.InputFill,
            ForeColor = ModernTheme.TextStrong
        };
        Controls.Add(_lb);

        _lb.DrawItem += OnDrawItem;
        _lb.SelectedIndexChanged += (s, e) => { Invalidate(true); SelectedIndexChanged?.Invoke(this, EventArgs.Empty); };
        _lb.DataSourceChanged += (s, e) => _checked.Clear();
        _lb.Enter += (s, e) => { _focus = true; Invalidate(); };
        _lb.Leave += (s, e) => { _focus = false; Invalidate(); };
        _lb.MouseEnter += (s, e) => { _hover = true; Invalidate(); };
        _lb.MouseLeave += (s, e) => { _hover = false; SetHot(-1); Invalidate(); };
        _lb.MouseMove += (s, e) => SetHot(_lb.IndexFromPoint(e.Location));
        _lb.MouseDown += OnInnerMouseDown;
        _lb.KeyDown += OnInnerKeyDown;
        _lb.EnabledChanged += (s, e) => Invalidate();

        LayoutInner();
    }

    // ───────────── API شبیه ListBox ─────────────
    public ListBox.ObjectCollection Items => _lb.Items;

    [Browsable(false)]
    public object? DataSource
    {
        get => _lb.DataSource;
        set => _lb.DataSource = value;
    }

    [DefaultValue("")]
    public string DisplayMember { get => _lb.DisplayMember; set => _lb.DisplayMember = value ?? string.Empty; }

    [DefaultValue("")]
    public string ValueMember { get => _lb.ValueMember; set => _lb.ValueMember = value ?? string.Empty; }

    [Browsable(false)]
    public object? SelectedItem { get => _lb.SelectedItem; set => _lb.SelectedItem = value; }

    [Browsable(false)]
    public object? SelectedValue { get => _lb.SelectedValue; set => _lb.SelectedValue = value; }

    [Browsable(false)]
    public int SelectedIndex { get => _lb.SelectedIndex; set => _lb.SelectedIndex = value; }

    [DefaultValue(SelectionMode.One)]
    public SelectionMode SelectionMode { get => _lb.SelectionMode; set => _lb.SelectionMode = value; }

    [DefaultValue(32)]
    public int ItemHeight { get => _lb.ItemHeight; set => _lb.ItemHeight = Math.Max(20, value); }

    public string GetItemText(object item) => _lb.GetItemText(item);

    // ───────────── حالت چک‌دار ─────────────
    [DefaultValue(false)]
    public bool ShowCheckBoxes { get => _showChecks; set { _showChecks = value; _lb.Invalidate(); } }

    public bool GetItemChecked(int index) => _checked.Contains(index);

    public void SetItemChecked(int index, bool value)
    {
        if (index < 0 || index >= _lb.Items.Count) return;
        bool changed = value ? _checked.Add(index) : _checked.Remove(index);
        if (!changed) return;
        _lb.Invalidate();
        CheckedChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CheckAll(bool value)
    {
        _checked.Clear();
        if (value) for (int i = 0; i < _lb.Items.Count; i++) _checked.Add(i);
        _lb.Invalidate();
        CheckedChanged?.Invoke(this, EventArgs.Empty);
    }

    [Browsable(false)]
    public IReadOnlyList<int> CheckedIndices
        => _checked.Where(i => i >= 0 && i < _lb.Items.Count).OrderBy(i => i).ToList();

    [Browsable(false)]
    public IReadOnlyList<object> CheckedItems
        => CheckedIndices.Select(i => _lb.Items[i]).ToList();

    // ───────────── چیدمان و رویدادها ─────────────
    private void LayoutInner()
    {
        if (_lb is null) return;
        _lb.SetBounds(6, 6, Math.Max(10, Width - 12), Math.Max(10, Height - 12));
    }

    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); LayoutInner(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); _lb.Enabled = Enabled; Invalidate(); }
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (_lb is not null && _lb.Font != Font) _lb.Font = Font;
    }

    private void SetHot(int index)
    {
        if (index == _hot) return;
        _hot = index;
        _lb.Invalidate();
    }

    private void OnInnerMouseDown(object? sender, MouseEventArgs e)
    {
        if (!_showChecks || e.Button != MouseButtons.Left) return;
        int idx = _lb.IndexFromPoint(e.Location);
        if (idx >= 0) SetItemChecked(idx, !_checked.Contains(idx));
    }

    private void OnInnerKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_showChecks || e.KeyCode != Keys.Space) return;
        int idx = _lb.SelectedIndex;
        if (idx >= 0) { SetItemChecked(idx, !_checked.Contains(idx)); e.Handled = true; }
    }

    // ───────────── رسم آیتم‌ها ─────────────
    private void OnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _lb.Items.Count) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var bg = new SolidBrush(_lb.BackColor)) g.FillRectangle(bg, e.Bounds);

        bool selected = (e.State & DrawItemState.Selected) != 0;
        bool hot = e.Index == _hot;
        var r = Rectangle.Inflate(e.Bounds, -3, -1);

        if (selected || hot)
        {
            using var path = ModernPaint.RoundedRect(r, 6);
            using var br = new SolidBrush(selected ? ModernTheme.PrimarySoft : ModernTheme.GridHover);
            g.FillPath(br, path);
            if (selected)
            {
                using var bar = new SolidBrush(AccentColor);
                g.FillRectangle(bar, r.Right - 3, r.Y + 5, 3, Math.Max(2, r.Height - 10));   // نوار سمت راست
            }
        }

        int textRight = e.Bounds.Right - 12;
        if (_showChecks)
        {
            const int box = 18;
            var cb = new Rectangle(e.Bounds.Right - 12 - box, e.Bounds.Y + (e.Bounds.Height - box) / 2, box, box);
            bool on = _checked.Contains(e.Index);
            using (var path = ModernPaint.RoundedRect(cb, 5))
            {
                using var fill = new SolidBrush(on ? AccentColor : Color.White);
                g.FillPath(fill, path);
                if (!on)
                {
                    using var pen = new Pen(Color.FromArgb(180, 190, 203), 1.5f);
                    g.DrawPath(pen, path);
                }
            }
            if (on)
            {
                using var pen = new Pen(Color.White, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawLines(pen, new[]
                {
                    new PointF(cb.X + 4.5f, cb.Y + 9.5f),
                    new PointF(cb.X + 7.5f, cb.Y + 12.5f),
                    new PointF(cb.X + 13.5f, cb.Y + 5.5f)
                });
            }
            textRight = cb.X - 8;
        }

        string text = _lb.GetItemText(_lb.Items[e.Index]);
        Color tc = !Enabled ? ModernTheme.TextDisabled
                 : selected ? Color.FromArgb(30, 64, 175)
                 : ModernTheme.TextStrong;
        var tr = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, Math.Max(10, textRight - e.Bounds.X - 8), e.Bounds.Height);
        TextRenderer.DrawText(g, text, _lb.Font, tr, tc,
            TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }

    // ───────────── رسم قاب ─────────────
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(ModernPaint.BehindColor(this));

        Color border = !Enabled ? ModernTheme.InputBorderOff
                     : _focus ? AccentColor
                     : _hover ? ModernTheme.InputBorderHover
                     : ModernTheme.InputBorder;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = ModernPaint.RoundedRect(rect, CornerRadius);
        using (var br = new SolidBrush(Enabled ? _lb.BackColor : ModernTheme.InputDisabled)) g.FillPath(br, path);
        using (var pen = new Pen(border, _focus && Enabled ? 2f : 1f)) g.DrawPath(pen, path);
    }
}
