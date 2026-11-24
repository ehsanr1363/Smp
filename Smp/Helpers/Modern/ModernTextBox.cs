using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

namespace Smp.Helpers;

// ═══════════════════════════════════════════════════════════════════════
//  ModernInputHost — قاب گرد دور یک TextBox یا NumericUpDown موجود
//  (کنترل داخلی همان می‌ماند؛ Text و رویدادهای قبلی بدون تغییر کار می‌کنند)
// ═══════════════════════════════════════════════════════════════════════
public class ModernInputHost : Control
{
    private readonly Control _inner;
    private bool _hover, _focus, _inLayout;
    private string _suffix = string.Empty;
    private string? _error;
    private readonly ToolTip _tip = new ToolTip();
    private Label? _phLabel;
    private string _phText = string.Empty;

    public Color AccentColor { get; set; } = ModernTheme.Primary;
    public Color BorderColorNormal { get; set; } = ModernTheme.InputBorder;
    public int CornerRadius { get; set; } = ModernTheme.CornerRadius;
    public Control Inner => _inner;

    /// <summary>متن ثابت سمت چپ فیلد (مثلاً «ریال» یا «٪»)</summary>
    [DefaultValue("")]
    public string Suffix
    {
        get => _suffix;
        set { _suffix = value ?? string.Empty; LayoutInner(); Invalidate(); }
    }

    /// <summary>اگر پر شود، کادر قرمز می‌شود و متن به‌صورت تولتیپ نمایش داده می‌شود. null/خالی = بدون خطا</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? ErrorText
    {
        get => _error;
        set
        {
            _error = string.IsNullOrWhiteSpace(value) ? null : value;
            _tip.SetToolTip(this, _error ?? string.Empty);
            _tip.SetToolTip(_inner, _error ?? string.Empty);
            Invalidate();
        }
    }

    public ModernInputHost(Control inner)
    {
        _inner = inner;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = false;
        Height = ModernTheme.InputHeight;

        if (inner is TextBox tb)
        {
            tb.BorderStyle = BorderStyle.None;
        }
        else if (inner is NumericUpDown nud)
        {
            nud.BorderStyle = BorderStyle.None;
            nud.TextAlign = HorizontalAlignment.Center;
        }

        // رنگ پیش‌فرض سیستمی → رنگ گرم برنامه (رنگ‌های دستی بعدی دست‌نخورده می‌مانند)
        if (inner.BackColor == SystemColors.Window || inner.BackColor == SystemColors.Control)
            inner.BackColor = ModernTheme.InputFill;

        inner.Dock = DockStyle.None;
        inner.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        inner.Margin = Padding.Empty;
        Controls.Add(inner);

        inner.Enter += (s, e) => { _focus = true; Invalidate(); };
        inner.Leave += (s, e) => { _focus = false; Invalidate(); };
        inner.MouseEnter += (s, e) => UpdateHover();
        inner.MouseLeave += (s, e) => UpdateHover();
        inner.BackColorChanged += (s, e) => Invalidate();
        inner.EnabledChanged += (s, e) => Invalidate();
        inner.SizeChanged += (s, e) => LayoutInner();
        inner.FontChanged += (s, e) => LayoutInner();
        if (inner is TextBox t2)
        {
            t2.ReadOnlyChanged += (s, e) => Invalidate();
            t2.MultilineChanged += (s, e) => LayoutInner();
        }

        LayoutInner();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }

    private void UpdateHover()
    {
        bool h = IsHandleCreated && ClientRectangle.Contains(PointToClient(Cursor.Position));
        if (h != _hover) { _hover = h; Invalidate(); }
    }

    private int SuffixWidth()
        => _suffix.Length == 0 ? 0
         : TextRenderer.MeasureText(_suffix, ModernTheme.GetFont(9.5f), new Size(int.MaxValue, int.MaxValue),
               TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width + 12;

    /// <summary>
    /// متن کمرنگ (placeholder). کنترل بومی TextBox جهت آن را در فرم‌های Mirror/RTL درست نشان نمی‌دهد (چپ می‌افتد)،
    /// پس خودمان با یک Label هم‌اندازه‌ی کادر، راست‌چین نمایش می‌دهیم.
    /// </summary>
    protected string PlaceholderValue
    {
        get => _phText;
        set { _phText = value ?? string.Empty; if (_inner is TextBox t) t.PlaceholderText = string.Empty; SyncPlaceholder(); }
    }

    private void SyncPlaceholder()
    {
        if (_inner is not TextBox tb) return;

        // TextBox ساده‌ای که PlaceholderText بومی گرفته (مثلاً new TextBox { PlaceholderText = ... }) را تحویل می‌گیریم
        if (!string.IsNullOrEmpty(tb.PlaceholderText))
        {
            _phText = tb.PlaceholderText;
            tb.PlaceholderText = string.Empty;
        }

        if (string.IsNullOrEmpty(_phText))
        {
            if (_phLabel is not null) _phLabel.Visible = false;
            return;
        }

        if (_phLabel is null)
        {
            _phLabel = new Label
            {
                AutoSize = false,
                UseMnemonic = false,
                // مثل ModernUI.Caption: در فرم‌های RTL/Mirror، MiddleLeft یعنی متن در سمت راست
                TextAlign = ContentAlignment.MiddleLeft,
                RightToLeft = RightToLeft.Yes,
                Cursor = Cursors.IBeam,
                ForeColor = ModernTheme.Placeholder
            };
            _phLabel.MouseDown += (s, e) => { if (_inner.Enabled) _inner.Focus(); };
            tb.TextChanged += (s, e) => UpdatePlaceholderVisibility();
            tb.Enter += (s, e) => UpdatePlaceholderVisibility();   // با فوکوس، متن کمرنگ برای دیده‌شدن مکان‌نما پنهان می‌شود
            tb.Leave += (s, e) => UpdatePlaceholderVisibility();
            tb.BackColorChanged += (s, e) => { if (_phLabel is not null) _phLabel.BackColor = tb.BackColor; };
            tb.FontChanged += (s, e) => { if (_phLabel is not null) _phLabel.Font = tb.Font; };
            Controls.Add(_phLabel);
        }

        _phLabel.Text = _phText;
        _phLabel.Font = tb.Font;
        _phLabel.BackColor = tb.BackColor;
        _phLabel.SetBounds(_inner.Left, _inner.Top, _inner.Width, _inner.Height);
        _phLabel.BringToFront();
        UpdatePlaceholderVisibility();
    }

    private void UpdatePlaceholderVisibility()
    {
        if (_phLabel is not null && _inner is TextBox tb)
            _phLabel.Visible = tb.Text.Length == 0 && _phText.Length > 0 && !tb.Focused;
    }

    private void LayoutInner()
    {
        if (_inLayout || _inner is null) return;
        _inLayout = true;
        try
        {
            const int padX = 12;
            int suffixW = SuffixWidth();
            int x = padX + suffixW;
            int w = Math.Max(10, Width - 2 * padX - suffixW);
            bool multiline = _inner is TextBox tb && tb.Multiline;

            if (multiline)
            {
                _inner.SetBounds(x, 8, w, Math.Max(10, Height - 16));
            }
            else
            {
                int h = _inner.Height;
                _inner.SetBounds(x, Math.Max(0, (Height - h) / 2), w, h);
            }
            SyncPlaceholder();
        }
        finally { _inLayout = false; }
    }

    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); LayoutInner(); }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); UpdateHover(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); UpdateHover(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (_inner.Enabled) _inner.Focus(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(ModernPaint.BehindColor(this));

        bool enabled = Enabled && _inner.Enabled;
        bool readOnly = _inner is TextBox tb && tb.ReadOnly;
        bool hasError = _error is not null;
        Color fill = enabled ? (readOnly ? ModernPaint.Blend(_inner.BackColor, ModernTheme.InputDisabled, 0.5f) : _inner.BackColor)
                             : ModernTheme.InputDisabled;
        Color border = !enabled ? ModernTheme.InputBorderOff
                     : hasError ? ModernTheme.Danger
                     : (_focus && !readOnly) ? AccentColor
                     : _hover ? ModernTheme.InputBorderHover
                     : BorderColorNormal;
        float bw = ((_focus && !readOnly && enabled) || hasError) ? 2f : 1f;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = ModernPaint.RoundedRect(rect, CornerRadius);
        using (var br = new SolidBrush(fill)) g.FillPath(br, path);

        if ((_focus && !readOnly && enabled) || hasError)
        {
            using var glow = new Pen(Color.FromArgb(40, hasError ? ModernTheme.Danger : AccentColor), 4f);
            g.DrawPath(glow, path);
        }
        using (var pen = new Pen(border, bw)) g.DrawPath(pen, path);

        if (_suffix.Length > 0)
        {
            var r = new Rectangle(10, 0, SuffixWidth(), Height);
            TextRenderer.DrawText(g, _suffix, ModernTheme.GetFont(9.5f), r,
                enabled ? ModernTheme.TextMuted : ModernTheme.TextDisabled,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.RightToLeft);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
//  ModernTextBox — تکست‌باکس مدرن کامل (قاب + TextBox داخلی + حالت‌های ورودی)
// ═══════════════════════════════════════════════════════════════════════
public enum ModernInputKind
{
    /// <summary>متن معمولی</summary>
    Text,
    /// <summary>عدد صحیح با جداکننده‌ی هزارگان و ارقام فارسی</summary>
    Integer,
    /// <summary>مبلغ (مثل Integer؛ برای خوانایی کد)</summary>
    Money,
    /// <summary>عدد اعشاری با جداکننده‌ی هزارگان</summary>
    Decimal,
    /// <summary>درصد (عدد اعشاری بدون جداکننده؛ پسوند ٪)</summary>
    Percent,
    /// <summary>فقط رقم لاتین (کد ملی، شماره حساب، موبایل)</summary>
    Digits,
    /// <summary>رمز عبور</summary>
    Password
}

public class ModernTextBox : ModernInputHost
{
    private ModernInputKind _kind = ModernInputKind.Text;
    private bool _formatting;
    private bool _alignExplicit;
    private int _decimals = 2;

    public ModernTextBox() : this(NewInner()) { }

    private ModernTextBox(TextBox tb) : base(tb)
    {
        tb.KeyPress += OnInnerKeyPress;
        tb.TextChanged += OnInnerTextChanged;
        tb.Enter += (s, e) =>
        {
            if (SelectAllOnFocus && IsNumeric && tb.IsHandleCreated)
                tb.BeginInvoke(new Action(() => { if (!tb.IsDisposed) tb.SelectAll(); }));
        };
    }

    private static TextBox NewInner() => new TextBox
    {
        Font = ModernTheme.GetFont(11f),
        ForeColor = ModernTheme.TextStrong,
        RightToLeft = RightToLeft.Yes,
        // فرم‌ها Mirror هستند (RightToLeftLayout)؛ در حالت Mirror مقدار Left یعنی متن/placeholder در سمت راست دیده می‌شود
        TextAlign = HorizontalAlignment.Left
    };

    /// <summary>TextBox داخلی (برای کدهایی که هنوز TextBox می‌خواهند)</summary>
    public TextBox TextBox => (TextBox)Inner;

    // ───────────── ویژگی‌ها ─────────────
    [DefaultValue(ModernInputKind.Text)]
    public ModernInputKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            TextBox.UseSystemPasswordChar = value == ModernInputKind.Password;
            if (!_alignExplicit)
                TextBox.TextAlign = IsNumeric || value == ModernInputKind.Digits ? HorizontalAlignment.Center : HorizontalAlignment.Left;   // Left در فرم Mirror = راست
            if (value == ModernInputKind.Percent && Suffix.Length == 0) Suffix = "٪";
            if (IsNumeric || value == ModernInputKind.Digits) ReformatNow();
        }
    }

    /// <summary>تعداد رقم اعشار مجاز (برای Decimal و Percent)</summary>
    [DefaultValue(2)]
    public int Decimals { get => _decimals; set => _decimals = Math.Max(0, Math.Min(8, value)); }

    [DefaultValue(false)]
    public bool AllowNegative { get; set; }

    [DefaultValue(true)]
    public bool SelectAllOnFocus { get; set; } = true;

    [AllowNull]
    public override string Text
    {
        get => TextBox.Text;
        set => TextBox.Text = value ?? string.Empty;
    }

    [DefaultValue("")]
    public string PlaceholderText { get => PlaceholderValue; set => PlaceholderValue = value ?? string.Empty; }

    [DefaultValue(false)]
    public bool ReadOnly { get => TextBox.ReadOnly; set { TextBox.ReadOnly = value; Invalidate(); } }

    [DefaultValue(32767)]
    public int MaxLength { get => TextBox.MaxLength; set => TextBox.MaxLength = value; }

    [DefaultValue(false)]
    public bool Multiline
    {
        get => TextBox.Multiline;
        set { TextBox.Multiline = value; TextBox.AcceptsReturn = value; TextBox.ScrollBars = value ? ScrollBars.Vertical : ScrollBars.None; }
    }

    public HorizontalAlignment TextAlign
    {
        get => TextBox.TextAlign;
        set { _alignExplicit = true; TextBox.TextAlign = value; }
    }

    /// <summary>مقدار عددی فیلد (برای Integer / Money / Decimal / Percent). متن خالی یا نامعتبر = ۰</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public decimal Value
    {
        get => NumberHelper.TryParseFormatted(TextBox.Text, out decimal v) ? v : 0m;
        set => TextBox.Text = FormatValue(value);
    }

    /// <summary>مثل Value ولی برای متن خالی/نامعتبر null برمی‌گرداند</summary>
    [Browsable(false)]
    public decimal? ValueOrNull => NumberHelper.TryParseFormatted(TextBox.Text, out decimal v) ? v : (decimal?)null;

    public new bool Focus() => TextBox.Focus();

    public void SelectAll() => TextBox.SelectAll();

    // ───────────── منطق ورودی ─────────────
    private bool IsNumeric => _kind == ModernInputKind.Integer || _kind == ModernInputKind.Money
                           || _kind == ModernInputKind.Decimal || _kind == ModernInputKind.Percent;
    private bool AllowsDecimals => _kind == ModernInputKind.Decimal || _kind == ModernInputKind.Percent;
    private bool UsesGrouping => _kind == ModernInputKind.Integer || _kind == ModernInputKind.Money || _kind == ModernInputKind.Decimal;

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (Inner is TextBox tb && tb.Font != Font) tb.Font = Font;
    }

    private static bool IsDecimalSeparator(char c) => c == '.' || c == '\u066B' || c == '/';

    private void OnInnerKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (char.IsControl(e.KeyChar)) return;
        if (_kind == ModernInputKind.Digits)
        {
            if (!char.IsDigit(e.KeyChar)) e.Handled = true;
            return;
        }
        if (!IsNumeric) return;

        if (char.IsDigit(e.KeyChar)) return;
        if (e.KeyChar == '-' && AllowNegative) return;
        if (AllowsDecimals && IsDecimalSeparator(e.KeyChar)) return;
        e.Handled = true;
    }

    private void OnInnerTextChanged(object? sender, EventArgs e)
    {
        if (_formatting) return;
        if (IsNumeric) ReformatNumber();
        else if (_kind == ModernInputKind.Digits) ReformatDigits();
        OnTextChanged(EventArgs.Empty);
    }

    private void ReformatNow()
    {
        if (IsNumeric) ReformatNumber();
        else if (_kind == ModernInputKind.Digits) ReformatDigits();
    }

    private void ReformatDigits()
    {
        string text = TextBox.Text;
        string latin = StringHelper.ConvertToEnglishDigits(text);
        var sb = new StringBuilder(latin.Length);
        foreach (char c in latin) if (c >= '0' && c <= '9') sb.Append(c);
        string clean = sb.ToString();
        if (clean == text) return;

        int caret = TextBox.SelectionStart;
        _formatting = true;
        try
        {
            TextBox.Text = clean;
            TextBox.SelectionStart = Math.Min(caret, clean.Length);
        }
        finally { _formatting = false; }
    }

    private void ReformatNumber()
    {
        var tb = TextBox;
        string text = tb.Text;
        int caret = tb.SelectionStart;

        // تعداد نویسه‌های «معنادار» (رقم و ممیز) قبل از مکان‌نما
        int significant = 0;
        for (int i = 0; i < Math.Min(caret, text.Length); i++)
            if (char.IsDigit(text[i]) || IsDecimalSeparator(text[i])) significant++;

        string raw = Sanitize(NumberHelper.NormalizeNumberText(text));
        string formatted = FormatRaw(raw);
        if (formatted == text) return;

        _formatting = true;
        try
        {
            tb.Text = formatted;
            int pos = 0, seen = 0;
            while (pos < formatted.Length && seen < significant)
            {
                if (char.IsDigit(formatted[pos]) || IsDecimalSeparator(formatted[pos])) seen++;
                pos++;
            }
            tb.SelectionStart = pos;
            tb.SelectionLength = 0;
        }
        finally { _formatting = false; }
    }

    /// <summary>فقط رقم لاتین، یک ممیز (در صورت مجاز بودن) و منفی ابتدای متن را نگه می‌دارد</summary>
    private string Sanitize(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        bool dot = false;
        int fracDigits = 0;
        foreach (char ch in raw)
        {
            if (ch == '-' && sb.Length == 0 && AllowNegative)
            {
                sb.Append('-');
            }
            else if (ch >= '0' && ch <= '9')
            {
                if (dot)
                {
                    if (fracDigits >= _decimals) continue;
                    fracDigits++;
                }
                sb.Append(ch);
            }
            else if (ch == '.' && !dot && AllowsDecimals && _decimals > 0)
            {
                dot = true;
                if (sb.Length == 0 || (sb.Length == 1 && sb[0] == '-')) sb.Append('0');
                sb.Append('.');
            }
        }
        return sb.ToString();
    }

    /// <summary>رشته‌ی لاتین پاک‌شده → نمایش فارسی (با جداکننده‌ی هزارگان در صورت نیاز)</summary>
    private string FormatRaw(string raw)
    {
        if (raw.Length == 0) return string.Empty;

        bool neg = raw[0] == '-';
        string body = neg ? raw.Substring(1) : raw;
        string intPart = body, frac = string.Empty;
        bool hasDot = false;
        int d = body.IndexOf('.');
        if (d >= 0) { hasDot = true; intPart = body.Substring(0, d); frac = body.Substring(d + 1); }

        string trimmed = intPart.TrimStart('0');
        if (trimmed.Length == 0 && (intPart.Length > 0 || hasDot)) trimmed = "0";
        intPart = trimmed;

        if (intPart.Length == 0 && !hasDot) return neg ? "-" : string.Empty;

        string grouped = intPart;
        if (UsesGrouping && intPart.Length > 3)
        {
            var sb = new StringBuilder();
            int first = intPart.Length % 3;
            if (first > 0) sb.Append(intPart, 0, first);
            for (int i = first; i < intPart.Length; i += 3)
            {
                if (sb.Length > 0) sb.Append('\u066C');
                sb.Append(intPart, i, 3);
            }
            grouped = sb.ToString();
        }

        string result = (neg ? "-" : string.Empty) + grouped + (hasDot ? "\u066B" + frac : string.Empty);
        return StringHelper.ConvertToPersianDigits(result);
    }

    private string FormatValue(decimal value)
    {
        if (!IsNumeric)
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        string raw = value.ToString(AllowsDecimals ? "0.############################" : "0", System.Globalization.CultureInfo.InvariantCulture);
        if (!AllowNegative && raw.StartsWith("-")) raw = raw.Substring(1);
        return FormatRaw(Sanitize(raw));
    }
}
