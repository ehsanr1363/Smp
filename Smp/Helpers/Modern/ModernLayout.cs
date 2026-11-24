using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Smp.Data;

namespace Smp.Helpers;

// ═══════════════════════════════════════════════════════════════════════
//  ModernUI — ساخت فیلدها و ردیف‌های فرم
//
//  چیدمان فارسی:  [ ورودی ........ ][ لیبل ]   ← لیبل همیشه سمت راست
//  چک‌باکس و رادیو هم همین‌طور: لیبل سمت راست، کادر کنار آن.
//
//  توجه: جدول‌هایی که RightToLeft = Yes دارند ستون ۰ را در سمت راست می‌گذارند.
//  بنابراین labelCol = 0 (راست) و controlCol = 1 (چپ) درست است.
// ═══════════════════════════════════════════════════════════════════════
public static class ModernUI
{
    public const int LabelHeight = 22;
    public const int InputHeight = ModernTheme.InputHeight;
    public const int FieldHeight = LabelHeight + InputHeight + 2;   // ارتفاع فیلد با لیبل بالا
    public const int LabelWidth = ModernTheme.LabelWidth;
    public const int RowHeight = ModernTheme.RowHeight;             // ارتفاع پیشنهادی ردیف افقی

    // ───────────── ساخت کنترل‌های آماده ─────────────

    /// <summary>کمبوباکس مدرن هم‌قد با تکست‌باکس‌ها</summary>
    public static ModernComboBox Combo()
        => new ModernComboBox { DropDownStyle = ComboBoxStyle.DropDownList, ItemHeight = InputHeight - 6 };

    /// <summary>تکست‌باکس مدرن آماده؛ kind برای مبلغ/درصد/رقم و ...</summary>
    public static ModernTextBox Input(ModernInputKind kind = ModernInputKind.Text, string? placeholder = null)
    {
        var t = new ModernTextBox { Kind = kind };
        if (!string.IsNullOrEmpty(placeholder)) t.PlaceholderText = placeholder;
        return t;
    }

    public static ModernTextBox Money(string? suffix = "ریال")
    {
        var t = new ModernTextBox { Kind = ModernInputKind.Money };
        if (!string.IsNullOrEmpty(suffix)) t.Suffix = suffix;
        return t;
    }

    public static ModernTextBox Percent() => new ModernTextBox { Kind = ModernInputKind.Percent };

    /// <summary>تکست‌باکس چندخطی</summary>
    public static ModernTextBox Memo(int height = 90)
    {
        var t = new ModernTextBox { Multiline = true };
        t.Height = height;
        return t;
    }

    /// <summary>چک‌باکس مدرن (لیبل سمت راست کادر)</summary>
    public static ModernCheckBox Check(string text, Color? accent = null)
        => new ModernCheckBox { Text = text, Accent = accent ?? ModernTheme.Primary, AutoSize = false, Width = 260, Height = 32 };

    public static ModernRadioButton Radio(string text, Color? accent = null)
        => new ModernRadioButton { Text = text, Accent = accent ?? ModernTheme.Primary, AutoSize = false, Width = 200, Height = 32 };

    public static ModernListBox List(bool checkBoxes = false, int height = 160)
        => new ModernListBox { ShowCheckBoxes = checkBoxes, Height = height };

    /// <summary>پنل زمینه‌ی گرم با حاشیه‌ی نازک (جایگزین UIHelper.CreateWarmPanel)</summary>
    public static Panel SurfacePanel()
    {
        var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15), BackColor = ModernTheme.Background };
        pnl.Paint += (s, e) =>
        {
            var r = pnl.ClientRectangle;
            r.Width -= 1; r.Height -= 1;
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawRectangle(pen, r);
        };
        return pnl;
    }

    /// <summary>لیبل استاندارد فرم (راست‌چین)</summary>
    public static Label Caption(string text, float size = 11f, bool bold = false, Color? color = null)
        => new Label
        {
            Text = text,
            AutoSize = false,
            // فرم‌های Mirror شده (RightToLeftLayout) جهت تراز را وارونه می‌کنند؛ MiddleLeft یعنی لیبل در سمت راست
            TextAlign = ContentAlignment.MiddleLeft,
            Font = ModernTheme.GetFont(size, bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = color ?? ModernTheme.Text,
            BackColor = Color.Transparent
        };

    // ───────────── ردیف افقی: لیبل راست، ورودی چپ ─────────────

    /// <summary>
    /// یک ردیف کامل: لیبل در راست، ورودی در چپ. TextBox و NumericUpDown به‌طور خودکار قاب مدرن می‌گیرند.
    /// خروجی را مستقیم در ModernCard.Body یا هر TableLayoutPanel دیگر بگذارید.
    /// </summary>
    public static TableLayoutPanel Row(string label, Control input, int labelWidth = LabelWidth, Color? labelColor = null)
    {
        Control c = Wrap(input);

        var t = new BufferedTable
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            RightToLeft = RightToLeft.Yes,
            Margin = new Padding(6, 2, 6, 2),
            BackColor = Color.Transparent
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var lbl = Caption(label, 10.5f, true, labelColor ?? Color.FromArgb(90, 80, 75));
        lbl.Dock = DockStyle.Fill;
        lbl.Margin = new Padding(2, 0, 4, 0);

        Place(c);
        t.Controls.Add(lbl, 0, 0);
        t.Controls.Add(c, 1, 0);
        return t;
    }

    /// <summary>ردیف چک‌باکس: لیبل در ستون راست، کادر چک در ستون ورودی و چسبیده به لیبل</summary>
    public static TableLayoutPanel CheckRow(string label, CheckBox check, int labelWidth = LabelWidth)
    {
        if (check is ModernCheckBox mc) mc.Text = string.Empty; else check.Text = string.Empty;
        check.AutoSize = false;
        check.Width = 40;
        var row = Row(label, check, labelWidth);
        // در فرم Mirror شده Anchor.Left یعنی سمت راست سلول؛ کادر چک بلافاصله کنار لیبل می‌نشیند (نه ته سلول)
        check.Anchor = AnchorStyles.Left;
        return row;
    }

    // ───────────── فیلد عمودی (لیبل بالا) — سازگار با کدهای قبلی ─────────────
    public static TableLayoutPanel Field(string label, Control input, Color? labelColor = null)
    {
        Control c = Wrap(input);

        var t = new BufferedTable
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(6, 0, 6, 0),
            BackColor = Color.Transparent
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, LabelHeight));
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, InputHeight));

        var lbl = Caption(label, 10f, true, labelColor ?? Color.FromArgb(90, 80, 75));
        lbl.Dock = DockStyle.Fill;
        lbl.Margin = new Padding(2, 0, 2, 0);

        c.Margin = Padding.Empty;
        if (c is ComboBox) c.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        else c.Dock = DockStyle.Fill;

        t.Controls.Add(lbl, 0, 0);
        t.Controls.Add(c, 0, 1);
        return t;
    }

    // ───────────── افزودن به جدول موجود (جایگزین مستقیم UIHelper.AddField) ─────────────

    /// <summary>
    /// لیبل را در labelCol (پیش‌فرض ۰ = راست) و کنترل را در controlCol (پیش‌فرض ۱ = چپ) می‌گذارد.
    /// TextBox/NumericUpDown داخل قاب مدرن می‌روند؛ متغیر قبلی (مثلاً txtName) همچنان همان TextBox است.
    /// </summary>
    public static void AddField(TableLayoutPanel tlp, string labelText, Control control, int row,
                                int labelCol = 0, int controlCol = 1, int controlSpan = 1)
    {
        Control c = Wrap(control);

        var lbl = Caption(labelText, 11f);
        lbl.Dock = DockStyle.Fill;
        lbl.Margin = new Padding(2, 0, 4, 0);

        Place(c);
        tlp.Controls.Add(lbl, labelCol, row);
        tlp.Controls.Add(c, controlCol, row);
        if (controlSpan > 1) tlp.SetColumnSpan(c, controlSpan);
    }

    /// <summary>
    /// چک‌باکس/رادیو در جدول: لیبل در labelCol، کادر در controlCol و چسبیده به لیبل (سمت راست سلول).
    /// اگر labelText خالی باشد، چک‌باکس با متن خودش در سلول کنترل قرار می‌گیرد.
    /// </summary>
    public static void AddCheck(TableLayoutPanel tlp, string labelText, CheckBox check, int row,
                                int labelCol = 0, int controlCol = 1, int controlSpan = 1)
    {
        if (!string.IsNullOrEmpty(labelText))
        {
            var lbl = Caption(labelText, 11f);
            lbl.Dock = DockStyle.Fill;
            lbl.Margin = new Padding(2, 0, 4, 0);
            tlp.Controls.Add(lbl, labelCol, row);
            check.Text = string.Empty;
            check.Width = 40;
        }
        check.AutoSize = false;
        check.Anchor = AnchorStyles.Right;
        check.Margin = new Padding(5, 4, 5, 4);
        tlp.Controls.Add(check, controlCol, row);
        if (controlSpan > 1) tlp.SetColumnSpan(check, controlSpan);
    }

    /// <summary>جایگذاری کنترل در سلول جدول با وسط‌چین عمودی و چسبیدن به راست</summary>
    public static Control CenterInCell(Control c, int rightPad = 8)
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = Padding.Empty };
        c.Anchor = AnchorStyles.Right;
        host.Controls.Add(c);
        host.Resize += (s, e) =>
        {
            c.Left = Math.Max(0, host.Width - c.Width - rightPad);
            c.Top = Math.Max(0, (host.Height - c.Height) / 2);
        };
        return host;
    }

    // ───────────── کمکی داخلی ─────────────

    /// <summary>TextBox / NumericUpDown ساده را در قاب مدرن می‌گذارد؛ بقیه را همان‌طور برمی‌گرداند</summary>
    private static Control Wrap(Control input)
    {
        if (input is TextBox || input is NumericUpDown) return new ModernInputHost(input);
        return input;
    }

    /// <summary>قاعده‌ی جایگذاری در سلول: ورودی‌های تک‌خطی وسط‌چین عمودی با ارتفاع ثابت، بقیه Fill</summary>
    private static void Place(Control c)
    {
        c.Margin = new Padding(5, 4, 5, 4);
        if (c is ModernInputHost host)
        {
            bool multi = host.Inner is TextBox tb && tb.Multiline;
            if (multi) { c.Dock = DockStyle.Fill; return; }
            c.Height = InputHeight;
            c.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        }
        else if (c is ComboBox || c is DateTimePicker)
        {
            c.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        }
        else if (c is ModernCheckBox || c is ModernRadioButton)
        {
            c.Anchor = AnchorStyles.Right;
        }
        else
        {
            c.Dock = DockStyle.Fill;
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
//  ModernForm — راه‌اندازی فرم، هدر، چیدمان‌های استاندارد
//  جایگزین: FormHelper.ApplyStandardHeader ، StandardFormHelper.* ،
//           UIHelper.SetupForm ، ModernLayoutHelper.StyleModernTabControl
// ═══════════════════════════════════════════════════════════════════════
public static class ModernForm
{
    // لوگوی شرکت یک بار از دیتابیس خوانده و کش می‌شود (قبلاً با هر بار باز شدن فرم خوانده می‌شد)
    private static Image? _logo;
    private static bool _logoLoaded;
    private static readonly object _logoLock = new object();

    /// <summary>بعد از تغییر لوگوی شرکت صدا بزنید تا فرم‌های بعدی لوگوی جدید را بخوانند</summary>
    public static void ResetLogoCache()
    {
        lock (_logoLock) { _logo = null; _logoLoaded = false; }
    }

    public static Image? GetCompanyLogo()
    {
        lock (_logoLock)
        {
            if (_logoLoaded) return _logo;
            _logoLoaded = true;
            try
            {
                var repo = new CompanyRepository(AppConfig.ConnectionString);
                var company = repo.GetAll().FirstOrDefault();
                if (company is not null && company.Logo is not null && company.Logo.Length > 0)
                {
                    using var ms = new MemoryStream(company.Logo);
                    using var src = Image.FromStream(ms);
                    _logo = new Bitmap(src);   // کپی مستقل از Stream
                }
            }
            catch (Exception __ex) { /* اگر لوگو لود نشد، برنامه متوقف نشود */ Smp.Helpers.LogHelper.LogException(__ex, "ModernLayout"); }
            return _logo;
        }
    }

    /// <summary>تنظیمات پایه‌ی هر فرم: راست‌به‌چپ، فونت، رنگ زمینه، Enter = فیلد بعدی</summary>
    public static void Setup(Form form, string title, Size size, bool isDialog = true)
    {
        form.Text = title;
        form.Size = size;
        form.StartPosition = FormStartPosition.CenterParent;
        form.RightToLeft = RightToLeft.Yes;
        form.RightToLeftLayout = true;
        form.Font = ModernTheme.GetFont(11f);
        form.BackColor = ModernTheme.Background;
        form.Padding = new Padding(0);
        form.FormBorderStyle = isDialog ? FormBorderStyle.FixedDialog : FormBorderStyle.Sizable;
        form.MaximizeBox = !isDialog;
        EnterNavigationHelper.EnableForForm(form);
        ModernSkin.Attach(form);   // کنترل‌های خام باقی‌مانده هنگام Load مدرن می‌شوند
    }

    /// <summary>
    /// هدر مدرن (عنوان + لوگوی شرکت در راست) روی فرم می‌گذارد و کنترل‌های موجود فرم را زیر آن می‌برد.
    /// جایگزین مستقیم form.ApplyStandardHeader(...) قبلی.
    /// </summary>
    public static void ApplyStandardHeader(this Form form, string title, Size size, bool isDialog = true)
    {
        form.SuspendLayout();
        Setup(form, title, size, isDialog);
        form.StartPosition = FormStartPosition.CenterScreen;

        var existing = form.Controls.Cast<Control>().ToList();
        form.Controls.Clear();

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        root.Controls.Add(CreateHeader(title), 0, 0);

        var content = new Panel { Dock = DockStyle.Fill, Padding = Padding.Empty };
        foreach (var c in existing) content.Controls.Add(c);
        root.Controls.Add(content, 0, 1);

        form.Controls.Add(root);
        form.ResumeLayout();
    }

    /// <summary>هدر مدرن: زمینه‌ی سفید، خط نازک پایین، عنوان و لوگو در سمت راست</summary>
    public static Panel CreateHeader(string title, string? subtitle = null)
    {
        var header = new Panel { Dock = DockStyle.Fill, BackColor = ModernTheme.Surface };
        header.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(12, 3, 12, 3),
            BackColor = Color.Transparent
        };

        var pic = new PictureBox
        {
            Size = new Size(40, 40),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Image = GetCompanyLogo()
        };

        var texts = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(5, string.IsNullOrEmpty(subtitle) ? 7 : 2, 8, 0),
            BackColor = Color.Transparent
        };
        texts.Controls.Add(new Label
        {
            Text = title,
            Font = ModernTheme.GetFont(13f, FontStyle.Bold),
            ForeColor = ModernTheme.Text,
            AutoSize = true,
            Margin = Padding.Empty
        });
        if (!string.IsNullOrEmpty(subtitle))
            texts.Controls.Add(new Label
            {
                Text = subtitle,
                Font = ModernTheme.GetFont(9.5f),
                ForeColor = ModernTheme.TextMuted,
                AutoSize = true,
                Margin = Padding.Empty
            });

        // چون فرم RightToLeft است، اولین کنترلِ این Flow در سمت چپ قرار می‌گیرد؛
        // پس ابتدا عنوان و بعد لوگو اضافه می‌شود تا لوگو در راست‌ترین جا و عنوان کنار آن باشد.
        flow.Controls.Add(texts);
        flow.Controls.Add(pic);
        header.Controls.Add(flow);
        return header;
    }

    /// <summary>نوار دکمه‌های پایین فرم؛ اولین دکمه در راست</summary>
    public static FlowLayoutPanel CreateButtonBar(params Control[] buttons)
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(15, 10, 15, 10),
            BackColor = ModernTheme.Background
        };
        foreach (var b in buttons) bar.Controls.Add(b);
        return bar;
    }

    // ───────────── چیدمان‌های استاندارد (هم‌امضا با StandardFormHelper) ─────────────

    public static (TableLayoutPanel ContentLayout, FlowLayoutPanel ButtonPanel) SetupStandardLayout(
        Form form, string title, Size size)
    {
        var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 75F));

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 4,
            AutoSize = true,
            Padding = new Padding(20, 15, 20, 15),
            RightToLeft = RightToLeft.Yes
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        scroll.Controls.Add(content);

        var buttons = CreateButtonBar();

        main.Controls.Add(scroll, 0, 0);
        main.Controls.Add(buttons, 0, 1);

        form.Controls.Add(main);
        form.ApplyStandardHeader(title, size);
        form.ShowInTaskbar = false;
        return (content, buttons);
    }

    public static (FlowLayoutPanel TopRightPanel, FlowLayoutPanel TopLeftPanel, DataGridView Grid, FlowLayoutPanel BottomPanel)
        SetupStandardListLayout(Form form, string title, Size size)
    {
        var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 55F));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 75F));

        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = ModernTheme.Background };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var topRight = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, AutoSize = true, Padding = new Padding(15, 5, 15, 5) };
        var topLeft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Padding = new Padding(15, 5, 15, 5) };
        top.Controls.Add(topRight, 0, 0);
        top.Controls.Add(topLeft, 2, 0);

        var grid = new DataGridView { Dock = DockStyle.Fill, Margin = new Padding(10, 0, 10, 0) };
        grid.ApplyModernStyle();
        grid.FillColumns();

        var bottom = CreateButtonBar();

        main.Controls.Add(top, 0, 0);
        main.Controls.Add(grid, 0, 1);
        main.Controls.Add(bottom, 0, 2);

        form.Controls.Add(main);
        form.ApplyStandardHeader(title, size);
        form.ShowInTaskbar = false;
        return (topRight, topLeft, grid, bottom);
    }

    public static (TableLayoutPanel FormLayout, DataGridView Grid, FlowLayoutPanel ActionPanel)
        SetupMasterDetailLayout(Form form, string title, Size size)
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = Padding.Empty };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 75F));

        var inputPanel = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = ModernTheme.Background,
            Padding = new Padding(10, 10, 10, 0)
        };

        var formLayout = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, AutoSize = true, RightToLeft = RightToLeft.Yes };
        formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        inputPanel.Controls.Add(formLayout);
        root.Controls.Add(inputPanel, 0, 0);

        var grid = new DataGridView { Dock = DockStyle.Fill, Margin = new Padding(10, 5, 10, 5) };
        grid.ApplyModernStyle();
        grid.FillColumns();
        root.Controls.Add(grid, 0, 1);

        var actions = CreateButtonBar();
        root.Controls.Add(actions, 0, 2);

        form.Controls.Add(root);
        form.ApplyStandardHeader(title, size);
        form.ShowInTaskbar = false;
        return (formLayout, grid, actions);
    }

    public static void AddRow(TableLayoutPanel layout, float height = 50F)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        layout.RowCount = layout.RowStyles.Count;
    }

    // ───────────── TabControl مدرن (جایگزین ModernLayoutHelper.StyleModernTabControl) ─────────────
    public static void StyleTabControl(TabControl tabs)
    {
        if (tabs is null) return;
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabs.SizeMode = TabSizeMode.Fixed;
        tabs.ItemSize = new Size(160, 40);
        tabs.Font = ModernTheme.GetFont(11f, FontStyle.Bold);
        tabs.Padding = new Point(15, 5);

        tabs.DrawItem += (sender, e) =>
        {
            var tc = (TabControl)sender!;
            if (e.Index < 0 || e.Index >= tc.TabPages.Count) return;
            var page = tc.TabPages[e.Index];
            Rectangle rect = tc.GetTabRect(e.Index);
            bool sel = e.Index == tc.SelectedIndex;

            using (var br = new SolidBrush(sel ? Color.White : Color.FromArgb(244, 241, 237)))
                e.Graphics.FillRectangle(br, rect);

            if (sel)
            {
                using var pen = new Pen(ModernTheme.Primary, 3);
                e.Graphics.DrawLine(pen, rect.Left, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
            }
            else
            {
                using var pen = new Pen(ModernTheme.Border, 1);
                e.Graphics.DrawLine(pen, rect.Right - 1, rect.Top + 10, rect.Right - 1, rect.Bottom - 10);
            }

            TextRenderer.DrawText(e.Graphics, page.Text, tc.Font, rect,
                sel ? Color.FromArgb(30, 41, 59) : ModernTheme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.RightToLeft);
        };
    }
}
