using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Smp.Helpers;

// ═══════════════════════════════════════════════════════════════════════
//  ModernStatCard — کارت خلاصه‌ی گزارش (عنوان، مقدار بزرگ، توضیح کوچک)
//  جایگزین UIHelper.CreateSummaryCard. Text همان مقدار نمایشی است،
//  پس کدهایی مثل card.Text = "..." بدون تغییر کار می‌کنند.
// ═══════════════════════════════════════════════════════════════════════
public class ModernStatCard : Control
{
    private string _title = string.Empty;
    private string _value = "۰";
    private string _sub = string.Empty;
    private Color _accent = ModernTheme.Primary;
    private bool _negativeRed = true;
    private decimal? _amount;
    private float _valueFontSize = 11f;
    private float _titleFontSize = 8.5f;
    private bool _autoShrink = true;
    private bool _dimZero;
    private Color _fill = ModernTheme.Surface;
    private Color _border = ModernTheme.Border;

    public ModernStatCard()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(200, 56);
        Margin = new Padding(6);
        RightToLeft = RightToLeft.Yes;
    }

    public string Title { get => _title; set { _title = value ?? string.Empty; Invalidate(); } }
    /// <summary>اندازه‌ی فونت مقدار (پیش‌فرض ۱۱)</summary>
    public float ValueFontSize { get => _valueFontSize; set { _valueFontSize = value; Invalidate(); } }
    /// <summary>اندازه‌ی فونت عنوان (پیش‌فرض ۸٫۵)</summary>
    public float TitleFontSize { get => _titleFontSize; set { _titleFontSize = value; Invalidate(); } }

    /// <summary>اگر مقدار/عنوان در عرض کارت جا نشد، فونت کم‌کم کوچک شود (به‌جای بریده شدن با ...)</summary>
    public bool AutoShrink { get => _autoShrink; set { _autoShrink = value; Invalidate(); } }

    /// <summary>مبلغ صفر (SetAmount(0)) کم‌رنگ نمایش داده شود تا کارت‌های پُر دیده شوند</summary>
    public bool DimZero { get => _dimZero; set { _dimZero = value; Invalidate(); } }

    /// <summary>رنگ زمینه‌ی کارت (پیش‌فرض سفید)؛ برای کارت‌های وضعیت با زمینه‌ی ملایم</summary>
    public Color FillColor { get => _fill; set { _fill = value; Invalidate(); } }

    /// <summary>رنگ کادر کارت</summary>
    public Color BorderColor { get => _border; set { _border = value; Invalidate(); } }

    public string SubText { get => _sub; set { _sub = value ?? string.Empty; Invalidate(); } }
    public Color Accent { get => _accent; set { _accent = value; Invalidate(); } }

    /// <summary>اگر مقدار عددی (SetAmount) منفی بود، متن قرمز شود</summary>
    public bool NegativeRed { get => _negativeRed; set { _negativeRed = value; Invalidate(); } }

    /// <summary>مقدار نمایشی کارت</summary>
    public override string Text
    {
        get => _value;
        set { _value = value ?? string.Empty; _amount = null; Invalidate(); }
    }

    /// <summary>مبلغ را با هزارگان و ارقام فارسی نمایش می‌دهد (unit مثلاً «ریال»)</summary>
    public void SetAmount(decimal amount, string unit = "")
    {
        _amount = amount;
        _value = NumberHelper.FormatThousands(amount, usePersianDigits: true) + (string.IsNullOrEmpty(unit) ? "" : " " + unit);
        Invalidate();
    }

    public void SetCount(int count, string unit = "")
    {
        _amount = null;
        _value = StringHelper.ConvertToPersianDigits(count.ToString()) + (string.IsNullOrEmpty(unit) ? "" : " " + unit);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(ModernPaint.BehindColor(this));

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        if (rect.Width < 12 || rect.Height < 12) return;

        using var path = ModernPaint.RoundedRect(rect, 12);
        using (var br = new SolidBrush(_fill)) g.FillPath(br, path);

        // نوار رنگی لبه‌ی راست
        var state = g.Save();
        g.SetClip(path, CombineMode.Intersect);
        using (var sb = new SolidBrush(_accent)) g.FillRectangle(sb, Width - 5, 0, 5, Height);
        g.Restore(state);

        using (var pen = new Pen(_border, 1f)) g.DrawPath(pen, path);

        const int padR = 16, padL = 10;
        var flags = TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;

        int y = 4;
        if (_title.Length > 0)
        {
            int titleH = _titleFontSize > 9f ? 20 : 16;
            var tr = new Rectangle(padL, y, Width - padL - padR, titleH);
            float tfs = _titleFontSize;
            var tf = ModernTheme.GetFont(tfs, FontStyle.Bold);
            if (_autoShrink)
            {
                var tprobe = new Size(int.MaxValue, titleH);
                while (tfs > 7.5f && TextRenderer.MeasureText(g, _title, tf, tprobe, flags | TextFormatFlags.NoPadding).Width > tr.Width)
                {
                    tfs -= 0.5f;
                    tf = ModernTheme.GetFont(tfs, FontStyle.Bold);
                }
            }
            TextRenderer.DrawText(g, _title, tf, tr,
                ModernPaint.Darken(_accent, 25), flags | TextFormatFlags.VerticalCenter);
            y += titleH;
        }

        int subH = _sub.Length > 0 ? 18 : 0;
        Color vc = (_negativeRed && _amount.HasValue && _amount.Value < 0) ? ModernTheme.Danger : ModernTheme.TextStrong;
        if (_dimZero && _amount.HasValue && _amount.Value == 0) vc = ModernTheme.TextDisabled;
        var vr = new Rectangle(padL, y, Width - padL - padR, Math.Max(20, Height - y - subH - 5));

        float fs = _valueFontSize;
        var vf = ModernTheme.GetFont(fs, FontStyle.Bold);
        if (_autoShrink)
        {
            var probe = new Size(int.MaxValue, vr.Height);
            while (fs > 8f && TextRenderer.MeasureText(g, _value, vf, probe, flags | TextFormatFlags.NoPadding).Width > vr.Width)
            {
                fs -= 0.5f;
                vf = ModernTheme.GetFont(fs, FontStyle.Bold);
            }
        }
        TextRenderer.DrawText(g, _value, vf, vr, vc, flags | TextFormatFlags.VerticalCenter);

        if (_sub.Length > 0)
        {
            var sr = new Rectangle(padL, Height - subH - 6, Width - padL - padR, subH);
            TextRenderer.DrawText(g, _sub, ModernTheme.GetFont(8.5f), sr, ModernTheme.TextMuted, flags | TextFormatFlags.VerticalCenter);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
//  ModernReport — ساخت بخش‌های صفحه‌ی گزارش
// ═══════════════════════════════════════════════════════════════════════
public static class ModernReport
{
    /// <summary>
    /// کارت خلاصه را در ستون col از ردیف row جدول می‌افزاید و برمی‌گرداند
    /// (جایگزین UIHelper.CreateSummaryCard که Label برمی‌گرداند).
    /// </summary>
    public static ModernStatCard AddStatCard(TableLayoutPanel parent, string title, Color accent, int col, int row = 0)
    {
        var card = new ModernStatCard { Title = title, Accent = accent, Dock = DockStyle.Fill };
        parent.Controls.Add(card, col, row);
        return card;
    }

    /// <summary>
    /// ردیف کارت‌های خلاصه با ستون‌های هم‌عرض. اولین کارت در راست قرار می‌گیرد.
    /// نمونه:  var strip = ModernReport.StatStrip(4, 92);  var c1 = strip.Add("جمع قراردادها", ModernTheme.Primary); ...
    /// </summary>
    public static StatStrip CreateStatStrip(int columns, int height = 92)
        => new StatStrip(columns, height);

    /// <summary>
    /// نوار فیلتر: هر فیلتر = لیبل (راست) + ورودی (چپ‌ترش). اولین فیلتر در راست‌ترین جاست.
    /// </summary>
    public static FlowLayoutPanel CreateFilterBar()
        => new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            Padding = new Padding(12, 10, 12, 6),
            BackColor = ModernTheme.Background
        };

    public static void AddFilter(FlowLayoutPanel bar, string label, Control input, int width = 160)
    {
        var lbl = ModernUI.Caption(label, 10.5f, true);
        lbl.AutoSize = true;
        lbl.Margin = new Padding(8, 10, 4, 0);

        Control c = input;
        if (input is TextBox || input is NumericUpDown) c = new ModernInputHost(input);
        c.Width = width;
        c.Margin = new Padding(0, 4, 4, 0);
        if (c is ModernInputHost || c is ModernComboBox || c is ModernTextBox) c.Height = ModernUI.InputHeight;

        bar.Controls.Add(lbl);   // اول لیبل (راست)
        bar.Controls.Add(c);     // بعد ورودی (سمت چپ لیبل)
    }

    /// <summary>جدول گزارش + کادر: گرید را مدرن می‌کند و داخل یک پنل با حاشیه می‌گذارد</summary>
    public static Panel WrapGrid(DataGridView grid)
    {
        grid.ApplyModernStyle();
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = ModernTheme.Border };
        grid.Dock = DockStyle.Fill;
        panel.Controls.Add(grid);
        return panel;
    }
}

// ═══════════════════════════════════════════════════════════════════════
//  ModernProgressBar — نوار پیشرفت باریک؛ عنوان در راست و نوار از راست به چپ پر می‌شود
// ═══════════════════════════════════════════════════════════════════════
public class ModernProgressBar : Control
{
    private decimal _value;
    private Color _accent = ModernTheme.Success;
    private string _caption = string.Empty;

    public ModernProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(300, 22);
        RightToLeft = RightToLeft.Yes;
    }

    /// <summary>درصد پیشرفت (۰ تا ۱۰۰؛ بیشتر از ۱۰۰ پر نمایش داده می‌شود)</summary>
    public decimal Value { get => _value; set { _value = value; Invalidate(); } }
    public Color Accent { get => _accent; set { _accent = value; Invalidate(); } }
    public string Caption { get => _caption; set { _caption = value ?? string.Empty; Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(ModernPaint.BehindColor(this));

        var flags = TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.NoPrefix |
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter;
        var font = ModernTheme.GetFont(9.5f, FontStyle.Bold);

        int textW = 0;
        if (_caption.Length > 0)
        {
            textW = TextRenderer.MeasureText(g, _caption, font, new Size(int.MaxValue, Height), flags).Width + 14;
            TextRenderer.DrawText(g, _caption, font, new Rectangle(Width - textW, 0, textW - 14, Height),
                ModernPaint.Darken(_accent, 40), flags);
        }

        const int barH = 8;
        var track = new Rectangle(0, (Height - barH) / 2, Width - textW - 1, barH);
        if (track.Width < 12) return;

        using (var trackPath = ModernPaint.RoundedRect(track, 4))
        using (var trackBrush = new SolidBrush(ModernTheme.SurfaceMuted))
            g.FillPath(trackBrush, trackPath);

        double pct = (double)Math.Max(0m, Math.Min(100m, _value));
        int fillW = (int)Math.Round(track.Width * pct / 100.0);
        if (fillW >= 6)
        {
            var fill = new Rectangle(track.Right - fillW, track.Y, fillW, track.Height);
            using var fillPath = ModernPaint.RoundedRect(fill, 4);
            using var fillBrush = new SolidBrush(_accent);
            g.FillPath(fillBrush, fillPath);
        }
    }
}

/// <summary>ردیف کارت‌های خلاصه (ستون‌های هم‌عرض، راست‌به‌چپ)</summary>
public sealed class StatStrip : BufferedTable
{
    private int _next;

    public StatStrip(int columns, int height)
    {
        Dock = DockStyle.Top;
        Height = height;
        ColumnCount = Math.Max(1, columns);
        RowCount = 1;
        RightToLeft = RightToLeft.Yes;
        Padding = new Padding(6, 0, 6, 0);
        BackColor = Color.Transparent;
        for (int i = 0; i < ColumnCount; i++)
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / ColumnCount));
        RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
    }

    public ModernStatCard Add(string title, Color accent, string subText = "")
    {
        var card = new ModernStatCard { Title = title, Accent = accent, SubText = subText, Dock = DockStyle.Fill, Margin = new Padding(4, 0, 4, 0) };
        if (_next < ColumnCount) Controls.Add(card, _next++, 0);
        else Controls.Add(card);
        return card;
    }
}
