using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Smp.Helpers;

// ═══════════════════════════════════════════════════════════════════════
//  کارت (سفید، گوشه گرد، نوار رنگی لبه‌ی راست، عنوان رنگی)
// ═══════════════════════════════════════════════════════════════════════
public class ModernCard : Panel
{
    public const int TitleHeight = 36;
    private const int BottomPad = 12;

    private readonly Color _accent;
    private readonly bool _hasTitle;

    public BufferedTable Body { get; }
    public Label TitleLabel { get; }
    public Color FillColor { get; set; } = Color.White;
    public Color BorderColor { get; set; } = ModernTheme.Border;

    /// <summary>کارت با ستون‌های هم‌عرض و ردیف‌های با ارتفاع ثابت (ارتفاع کارت خودکار محاسبه می‌شود)</summary>
    public ModernCard(string title, Color accent, int columns, params int[] rowHeights)
        : this(title, accent, Enumerable.Repeat(0, Math.Max(1, columns)).Select(_ => new ColumnStyle(SizeType.Percent, 100f / Math.Max(1, columns))).ToArray(), rowHeights)
    { }

    /// <summary>کارت با ستون‌های دلخواه. اگر rowHeights خالی باشد کارت Dock=Fill است و بدنه یک سلول کامل دارد.</summary>
    public ModernCard(string title, Color accent, ColumnStyle[] columns, params int[] rowHeights)
    {
        _accent = accent;
        _hasTitle = !string.IsNullOrEmpty(title);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;

        Body = new BufferedTable
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(14, _hasTitle ? 0 : 8, 20, BottomPad),
            BackColor = Color.Transparent,
            ColumnCount = columns.Length
        };
        foreach (var c in columns) Body.ColumnStyles.Add(new ColumnStyle(c.SizeType, c.Width));

        if (rowHeights is not null && rowHeights.Length > 0)
        {
            Body.RowCount = rowHeights.Length;
            foreach (int h in rowHeights) Body.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
            Height = (_hasTitle ? TitleHeight : 0) + rowHeights.Sum() + Body.Padding.Vertical + 2;
            Dock = DockStyle.Top;
        }
        else
        {
            Body.RowCount = 1;
            Body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Dock = DockStyle.Fill;
        }

        TitleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = _hasTitle ? TitleHeight : 0,
            Visible = _hasTitle,
            TextAlign = ContentAlignment.MiddleLeft,      // فرم Mirror شده: عنوان کنار نوار رنگی سمت راست
            Font = ModernTheme.GetFont(11.5f, FontStyle.Bold),
            ForeColor = accent,
            BackColor = Color.Transparent,
            Padding = new Padding(22, 4, 0, 0),
            Margin = Padding.Empty
        };

        Controls.Add(Body);        // Fill (اول)
        Controls.Add(TitleLabel);  // Top (بعد ⇒ بالاتر)
    }

    public string Title { get => TitleLabel.Text; set => TitleLabel.Text = value; }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(ModernPaint.BehindColor(this));

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        if (rect.Width < 8 || rect.Height < 8) return;
        using var path = ModernPaint.RoundedRect(rect, 12);
        using (var br = new SolidBrush(FillColor)) g.FillPath(br, path);

        // نوار رنگی لبه‌ی راست (بریده‌شده در گوشه‌های گرد)
        var state = g.Save();
        g.SetClip(path, CombineMode.Intersect);
        using (var sb = new SolidBrush(_accent)) g.FillRectangle(sb, Width - 5, 0, 5, Height);
        g.Restore(state);

        using var pen = new Pen(BorderColor, 1f);
        g.DrawPath(pen, path);
    }
}

// ═══════════════════════════════════════════════════════════════════════
//  صفحه‌ی اسکرول‌دار که کارت‌ها را زیر هم می‌چیند
// ═══════════════════════════════════════════════════════════════════════
public class ModernPage : Panel
{
    private readonly BufferedTable _stack;

    public ModernPage()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
        BackColor = ModernTheme.Background;
        Padding = new Padding(20, 14, 20, 8);

        _stack = new BufferedTable
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 0,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        _stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        Controls.Add(_stack);
    }

    public void AddCard(Control card, int bottomGap = 12)
    {
        int row = _stack.RowCount;
        _stack.RowCount = row + 1;
        _stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.Dock = DockStyle.Top;
        card.Margin = new Padding(0, 0, 0, bottomGap);
        _stack.Controls.Add(card, 0, row);
    }
}

// ═══════════════════════════════════════════════════════════════════════
//  تب‌های مدرن (قرص‌های گرد، شروع از راست)
//
//  ساختار: [ نوار تب‌ها (Dock=Top) ] + [ بدنه‌ی صفحه‌ها (Dock=Fill) ]
//  هر تب یک کنترل واقعی (ModernTabButton) داخل FlowLayoutPanel است، نه نقاشی دستی روی خود Panel؛
//  بنابراین چیدمان راست‌به‌چپ و فرم‌های Mirror شده (RightToLeftLayout) هیچ مشکلی ایجاد نمی‌کنند
//  و نمایش/پنهان شدن صفحه‌ها به Padding یا ترتیب Z وابسته نیست.
// ═══════════════════════════════════════════════════════════════════════
public class ModernTabButton : Control
{
    private bool _hot, _selected;
    private Color _accent = ModernTheme.Primary;

    public bool Selected
    {
        get => _selected;
        set { if (_selected == value) return; _selected = value; Invalidate(); }
    }

    public Color Accent
    {
        get => _accent;
        set { _accent = value; Invalidate(); }
    }

    public ModernTabButton(string title, int height = ModernTabHost.StripHeight - 8, int minWidth = 132)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = false;
        Cursor = Cursors.Hand;
        Text = title;
        Font = ModernTheme.GetFont(11.5f, FontStyle.Bold);
        Margin = new Padding(3, 0, 3, 0);
        int tw = TextRenderer.MeasureText(title, Font).Width;
        NaturalWidth = Math.Max(minWidth, tw + 44);
        CompactWidth = tw + 28;
        Size = new Size(NaturalWidth, height);
    }

    /// <summary>عرض دلخواه تب (وقتی جا کافی است)</summary>
    public int NaturalWidth { get; }

    /// <summary>کمترین عرضی که متن کامل در آن جا می‌شود (وقتی تب‌ها زیاد باشند)</summary>
    public int CompactWidth { get; }

    protected override void OnMouseEnter(EventArgs e) { _hot = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hot = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(ModernPaint.BehindColor(this));

        // قرص گرد (بالای دکمه)؛ زیر آن جای خط رنگی تب فعال است
        var pill = new Rectangle(0, 0, Width - 1, Height - 11);
        if (pill.Width < 8 || pill.Height < 8) return;

        if (_selected || _hot)
        {
            using var path = ModernPaint.RoundedRect(pill, 11);
            Color fill = _selected ? ModernPaint.Blend(Color.White, Accent, 0.13f) : Color.FromArgb(244, 242, 239);
            using var br = new SolidBrush(fill);
            g.FillPath(br, path);
        }

        if (_selected)
        {
            using var br = new SolidBrush(Accent);
            using var bar = ModernPaint.RoundedRect(new Rectangle(14, Height - 6, Width - 29, 3), 1);
            g.FillPath(br, bar);
        }

        Color tc = _selected ? ModernPaint.Darken(Accent, 45)
                 : _hot ? ModernTheme.Text
                 : Color.FromArgb(100, 116, 139);
        TextRenderer.DrawText(g, Text, Font, pill, tc,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
            TextFormatFlags.NoPrefix | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis);
    }
}

public class ModernTabHost : Panel
{
    public const int StripHeight = 58;

    private readonly List<(string Title, Panel Page, ModernTabButton Button)> _tabs = new();
    private readonly Panel _strip;
    private readonly FlowLayoutPanel _flow;
    private readonly Panel _body;
    private int _selected = -1;
    private Color _accent = ModernTheme.Primary;
    private readonly ToolTip _tip;

    public event EventHandler? SelectedIndexChanged;

    /// <summary>عرض تب‌ها را با عرض موجود هماهنگ می‌کند: طبیعی ← فشرده ← فشرده با ... در انتهای متن</summary>
    private void FitTabs()
    {
        if (_tabs.Count == 0 || _flow.ClientSize.Width <= 0) return;
        int avail = _flow.ClientSize.Width - _flow.Padding.Horizontal;
        int margins = _tabs.Sum(t => t.Button.Margin.Horizontal);
        int natural = _tabs.Sum(t => t.Button.NaturalWidth);
        int compact = _tabs.Sum(t => t.Button.CompactWidth);
        int room = avail - margins;

        _flow.SuspendLayout();
        foreach (var t in _tabs)
        {
            int w;
            if (room >= natural) w = t.Button.NaturalWidth;
            else if (room >= compact)
            {
                // فضای اضافه بین حالت فشرده و طبیعی متناسب پخش می‌شود
                double f = (double)(room - compact) / Math.Max(1, natural - compact);
                w = t.Button.CompactWidth + (int)((t.Button.NaturalWidth - t.Button.CompactWidth) * f);
            }
            else w = Math.Max(70, (int)(t.Button.CompactWidth * (double)room / compact));
            if (t.Button.Width != w) t.Button.Width = w;
        }
        _flow.ResumeLayout();
    }

    public Color AccentColor
    {
        get => _accent;
        set { _accent = value; foreach (var t in _tabs) t.Button.Accent = value; }
    }

    public ModernTabHost()
    {
        BackColor = ModernTheme.Background;
        TabStop = false;

        _body = new Panel { Dock = DockStyle.Fill, BackColor = ModernTheme.Background, Margin = Padding.Empty };

        _flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,   // در فرم Mirror شده: اولین تب در راست‌ترین جا
            WrapContents = false,
            AutoScroll = false,
            BackColor = Color.Transparent,
            Padding = new Padding(16, 8, 16, 0),
            Margin = Padding.Empty
        };

        _strip = new Panel { Dock = DockStyle.Top, Height = StripHeight, BackColor = Color.White };
        _strip.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, _strip.Height - 1, _strip.Width, _strip.Height - 1);
        };
        _strip.Controls.Add(_flow);

        // وقتی مجموع عرض تب‌ها از عرض پنجره بیشتر شود، تب‌ها جمع می‌شوند (قبلاً تب‌های آخر بریده می‌شدند)
        _strip.Resize += (s, e) => FitTabs();
        _tip = new ToolTip();

        // اول بدنه (Fill) و بعد نوار (Top) اضافه می‌شود تا نوار بالا و بدنه در بقیه‌ی فضا بنشیند
        Controls.Add(_body);
        Controls.Add(_strip);
    }

    public int Count => _tabs.Count;

    public IReadOnlyList<Panel> Pages => _tabs.Select(t => t.Page).ToList();

    public Panel AddPage(string title)
    {
        var page = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ModernTheme.Background,
            Padding = Padding.Empty,
            Visible = false
        };
        var button = new ModernTabButton(title) { Accent = _accent };
        int index = _tabs.Count;
        button.Click += (s, e) => SelectedIndex = index;

        _body.Controls.Add(page);
        _flow.Controls.Add(button);
        _tabs.Add((title, page, button));
        _tip.SetToolTip(button, title);   // اگر عنوان با ... کوتاه شد، با نگه‌داشتن ماوس کامل دیده می‌شود
        FitTabs();

        if (_selected < 0) SelectedIndex = 0;
        return page;
    }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (value < 0 || value >= _tabs.Count || value == _selected) return;
            _selected = value;
            for (int i = 0; i < _tabs.Count; i++)
            {
                bool on = i == value;
                _tabs[i].Button.Selected = on;
                if (on) { _tabs[i].Page.Visible = true; _tabs[i].Page.BringToFront(); }
            }
            for (int i = 0; i < _tabs.Count; i++)
                if (i != value) _tabs[i].Page.Visible = false;

            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SelectNext() { if (_tabs.Count > 0) SelectedIndex = (_selected + 1) % _tabs.Count; }
    public void SelectPrevious() { if (_tabs.Count > 0) SelectedIndex = (_selected - 1 + _tabs.Count) % _tabs.Count; }

    // Ctrl+Tab / Ctrl+Shift+Tab = تب بعدی / قبلی
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Tab)) { SelectNext(); return true; }
        if (keyData == (Keys.Control | Keys.Shift | Keys.Tab)) { SelectPrevious(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
