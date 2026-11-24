using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Smp.Models;

namespace Smp.Helpers;

// ═══════════════════════════════════════════════════════════════════════
//  ModernEmployeePicker — جایگزین ComboBox انتخاب پرسنل
//
//     var pick = new EmployeePickerField();
//     pick.SetEmployees(listOfEmployees);                 // بعد از لود شدن لیست
//     pick.SelectedChanged += (s, e) => { var id = pick.SelectedId; ... };
//     card.Body.Controls.Add(ModernUI.Row("کارمند", pick), 0, 0);
//
//  کلیک روی فیلد (یا ذره‌بین، یا Enter/F4) پنجره‌ی جستجوی مدرن را باز می‌کند.
// ═══════════════════════════════════════════════════════════════════════
public class EmployeePickerField : Control
{
    private List<Employee> _employees = new();
    private bool _hover, _hoverButton, _hoverClear;
    private const int ButtonW = 44;

    public event EventHandler? SelectedChanged;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Employee? Selected { get; private set; }

    [Browsable(false)]
    public int? SelectedId => Selected?.Id;

    public string Placeholder { get; set; } = "برای انتخاب کارمند کلیک کنید...";
    public string DialogTitle { get; set; } = "انتخاب کارمند";
    /// <summary>اگر true باشد دکمه‌ی × برای خالی‌کردن انتخاب نمایش داده می‌شود</summary>
    public bool AllowClear { get; set; }
    public bool IncludeInactive { get; set; } = true;
    public Color AccentColor { get; set; } = ModernTheme.Primary;

    public EmployeePickerField()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick | ControlStyles.SupportsTransparentBackColor, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        Height = ModernUI.InputHeight;
        MinimumSize = new Size(120, ModernUI.InputHeight);
        Font = ModernTheme.GetFont(11f);
        BackColor = Color.Transparent;
        RightToLeft = RightToLeft.Yes;
    }

    // ───────────── داده ─────────────
    public void SetEmployees(IEnumerable<Employee> employees)
    {
        _employees = (employees ?? Enumerable.Empty<Employee>()).ToList();
        // اگر کارمند انتخاب‌شده هنوز در لیست جدید هست، همان را نگه دار
        if (Selected is not null)
        {
            var again = _employees.FirstOrDefault(e => e.Id == Selected.Id);
            if (again is null) SetSelected(null);
            else Selected = again;
        }
        Invalidate();
    }

    public void SelectById(int? id)
    {
        SetSelected(id is null ? null : _employees.FirstOrDefault(e => e.Id == id.Value));
    }

    public void SetSelectedEmployee(Employee? emp) => SetSelected(emp);
    public void Clear() => SetSelected(null);

    private void SetSelected(Employee? emp)
    {
        if (ReferenceEquals(Selected, emp)) { Invalidate(); return; }
        Selected = emp;
        Invalidate();
        SelectedChanged?.Invoke(this, EventArgs.Empty);
    }

    // ───────────── باز کردن پنجره‌ی جستجو ─────────────
    public void OpenPicker()
    {
        if (!Enabled) return;
        using var dlg = new EmployeePickerDialog(_employees, Selected?.Id, DialogTitle, IncludeInactive);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK && dlg.Selected is not null)
            SetSelected(dlg.Selected);
        Focus();
    }

    // ───────────── تعامل ─────────────
    private Rectangle ButtonRect => new Rectangle(4, 4, ButtonW, Height - 8);                 // سمت چپ (RTL: انتهای کادر)
    private Rectangle ClearRect => new Rectangle(ButtonRect.Right + 4, (Height - 22) / 2, 22, 22);
    private bool CanClear => AllowClear && Selected is not null && Enabled;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool b = ButtonRect.Contains(e.Location);
        bool c = CanClear && ClearRect.Contains(e.Location);
        if (b != _hoverButton || c != _hoverClear) { _hoverButton = b; _hoverClear = c; Invalidate(); }
    }
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = _hoverButton = _hoverClear = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        if (CanClear && ClearRect.Contains(e.Location)) { Clear(); return; }
        OpenPicker();
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData == Keys.Enter || keyData == Keys.Space || keyData == Keys.F4 || keyData == Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space || e.KeyCode == Keys.F4 || e.KeyCode == Keys.Down)
        {
            e.Handled = e.SuppressKeyPress = true;
            OpenPicker();
        }
        else if ((e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back) && AllowClear)
        {
            e.Handled = e.SuppressKeyPress = true;
            Clear();
        }
    }

    // ───────────── رسم ─────────────
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        ModernPaint.EnableSmooth(g);
        g.Clear(ModernPaint.BehindColor(this));

        var r = new Rectangle(0, (Height - ModernUI.InputHeight) / 2, Width - 1, ModernUI.InputHeight - 1);
        if (r.Height < 10) r = new Rectangle(0, 0, Width - 1, Height - 1);
        int rad = ModernTheme.CornerRadius;

        bool focused = Focused;
        Color fill = Enabled ? ModernTheme.InputFill : ModernTheme.InputDisabled;
        Color border = !Enabled ? ModernTheme.InputBorderOff
                     : focused ? AccentColor
                     : _hover ? ModernTheme.InputBorderHover
                     : ModernTheme.InputBorder;

        using (var path = ModernPaint.RoundedRect(r, rad))
        {
            using (var br = new SolidBrush(fill)) g.FillPath(br, path);
            using (var pen = new Pen(border, focused ? 1.6f : 1f)) g.DrawPath(pen, path);
        }

        // ─── دکمه‌ی ذره‌بین (سمت چپ) ───
        var bRect = new Rectangle(r.Left + 4, r.Top + 4, ButtonW, r.Height - 8);
        Color bc = !Enabled ? ModernTheme.Neutral : (_hoverButton ? ModernPaint.Darken(AccentColor, 15) : AccentColor);
        using (var bp = ModernPaint.RoundedRect(bRect, Math.Max(4, rad - 2)))
        using (var bb = new SolidBrush(bc))
            g.FillPath(bb, bp);
        DrawMagnifier(g, bRect, Color.White);

        // ─── دکمه‌ی × ───
        int leftLimit = bRect.Right + 6;
        if (CanClear)
        {
            var cr = new Rectangle(bRect.Right + 4, r.Top + (r.Height - 22) / 2, 22, 22);
            using var pen = new Pen(_hoverClear ? ModernTheme.Danger : ModernTheme.TextMuted, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, cr.Left + 6, cr.Top + 6, cr.Right - 6, cr.Bottom - 6);
            g.DrawLine(pen, cr.Right - 6, cr.Top + 6, cr.Left + 6, cr.Bottom - 6);
            leftLimit = cr.Right + 4;
        }

        // ─── آواتار + متن (سمت راست) ───
        int d = r.Height - 12;
        var av = new Rectangle(r.Right - d - 8, r.Top + 6, d, d);
        var flags = TextFormatFlags.Right | TextFormatFlags.RightToLeft | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;

        if (Selected is null)
        {
            using (var br = new SolidBrush(ModernTheme.SurfaceMuted)) g.FillEllipse(br, av);
            DrawPersonGlyph(g, av, ModernTheme.TextMuted);
            var tr = new Rectangle(leftLimit, r.Top, av.Left - 8 - leftLimit, r.Height);
            TextRenderer.DrawText(g, Placeholder, Font, tr, ModernTheme.Placeholder, flags);
        }
        else
        {
            string name = Selected.FullName ?? string.Empty;
            string code = string.IsNullOrWhiteSpace(Selected.PersonnelCode) ? "" : "کد " + StringHelper.ConvertToPersianDigits(Selected.PersonnelCode!);

            Color c = AvatarColor(name);
            using (var br = new SolidBrush(ModernPaint.Tint(c, 18))) g.FillEllipse(br, av);
            TextRenderer.DrawText(g, Initials(name), ModernTheme.GetFont(9.5f, FontStyle.Bold), av, ModernPaint.Darken(c, 40),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            var nameFont = ModernTheme.GetFont(11f, FontStyle.Bold);
            int nameRight = av.Left - 8;
            var nameSz = TextRenderer.MeasureText(g, name, nameFont, new Size(int.MaxValue, 0),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.RightToLeft);
            int maxName = Math.Max(40, nameRight - leftLimit - (code.Length > 0 ? 70 : 0));
            int nameW = Math.Min(nameSz.Width + 2, maxName);
            var nameRect = new Rectangle(nameRight - nameW, r.Top, nameW, r.Height);
            TextRenderer.DrawText(g, name, nameFont, nameRect, Enabled ? ModernTheme.TextStrong : ModernTheme.TextDisabled, flags);

            if (code.Length > 0)
            {
                var codeRect = new Rectangle(leftLimit, r.Top, Math.Max(0, nameRect.Left - 10 - leftLimit), r.Height);
                if (codeRect.Width > 30)
                    TextRenderer.DrawText(g, code, ModernTheme.GetFont(9f), codeRect, ModernTheme.TextMuted, flags);
            }
        }
    }

    private static void DrawMagnifier(Graphics g, Rectangle b, Color color)
    {
        int s = 16;
        int x = b.Left + (b.Width - s) / 2, y = b.Top + (b.Height - s) / 2;
        using var pen = new Pen(color, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawEllipse(pen, x, y, 10, 10);
        g.DrawLine(pen, x + 9, y + 9, x + 15, y + 15);
    }

    private static void DrawPersonGlyph(Graphics g, Rectangle a, Color color)
    {
        using var br = new SolidBrush(color);
        int cx = a.Left + a.Width / 2;
        g.FillEllipse(br, cx - 4, a.Top + a.Height / 2 - 9, 8, 8);
        var body = new Rectangle(cx - 8, a.Top + a.Height / 2 + 1, 16, 12);
        using var path = new GraphicsPath();
        path.AddArc(body.Left, body.Top, body.Width, body.Height * 2, 180, 180);
        path.CloseFigure();
        var old = g.Clip;
        g.SetClip(a);
        g.FillPath(br, path);
        g.Clip = old;
    }

    // ───────────── کمکی‌های مشترک آواتار ─────────────
    private static readonly Color[] Palette =
    {
        ModernTheme.Primary, ModernTheme.Teal, ModernTheme.Purple, ModernTheme.Orange,
        ModernTheme.Brand, ModernTheme.Secondary, ModernTheme.Success
    };
    internal static Color AvatarColor(string name) => Palette[Math.Abs((name ?? "").GetHashCode()) % Palette.Length];
    internal static string Initials(string fullName)
    {
        var parts = (fullName ?? "").Split(new[] { ' ', '\u200c' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "؟";
        if (parts.Length == 1) return parts[0].Substring(0, 1);
        return parts[0].Substring(0, 1) + "\u200c" + parts[parts.Length - 1].Substring(0, 1);
    }
}

// ═══════════════════════════════════════════════════════════════════════
//  پنجره‌ی جستجوی کارمند: جستجوی زنده روی نام / کد پرسنلی / کد ملی / سمت / واحد
//  Enter = انتخاب اولین نتیجه ، ↓ = رفتن به لیست ، دابل‌کلیک = انتخاب ، Esc = بستن
// ═══════════════════════════════════════════════════════════════════════
public class EmployeePickerDialog : Form
{
    private readonly List<Employee> _all;
    private readonly ModernTextBox _txtSearch;
    private readonly DataGridView _grid;
    private readonly Label _lblCount;
    private readonly ModernButton _btnOk;

    public Employee? Selected { get; private set; }

    public EmployeePickerDialog(IEnumerable<Employee> employees, int? selectedId = null,
                                string title = "انتخاب کارمند", bool includeInactive = true)
    {
        _all = (employees ?? Enumerable.Empty<Employee>())
               .Where(e => includeInactive || e.IsActive)
               .OrderBy(e => e.PersonnelCode).ThenBy(e => e.FullName).ToList();

        // تنظیمات پایه (بدون EnterNavigationHelper تا Enter برای انتخاب آزاد بماند)
        Text = title;
        Size = new Size(720, 660);
        MinimumSize = new Size(620, 520);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        Font = ModernTheme.GetFont(11f);
        BackColor = ModernTheme.Background;
        KeyPreview = true;

        // ─── جستجو ───
        _txtSearch = ModernUI.Input(placeholder: "جستجوی نام، کد پرسنلی، کد ملی، سمت یا واحد...");
        _txtSearch.Height = ModernUI.InputHeight;
        _txtSearch.SelectAllOnFocus = false;

        _lblCount = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = ModernTheme.TextMuted,
            Font = ModernTheme.GetFont(9.5f),
            BackColor = Color.Transparent
        };

        // ─── جدول ───
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false
        };
        _grid.ApplyModernStyle();
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "FullName", DataPropertyName = "FullName", HeaderText = "پرسنل", FillWeight = 46, MinimumWidth = 200 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PersonnelCode", DataPropertyName = "PersonnelCode", HeaderText = "کد پرسنلی", Visible = false });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Position", DataPropertyName = "Position", HeaderText = "سمت", FillWeight = 24 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DepartmentName", DataPropertyName = "DepartmentName", HeaderText = "واحد", FillWeight = 30 });
        _grid.UseAiryLook(54);
        _grid.UseSelectionAccent(ModernTheme.Primary);
        _grid.UsePersonColumn("FullName", "PersonnelCode");
        _grid.UseTextAlignment("Position", "DepartmentName");
        // عنوان ستون‌ها وسط‌چین است؛ داده‌ی راست‌چین زیر عنوان نمی‌افتاد (در جدول RTL حتی به لبه‌ی چپ می‌رفت).
        // این هندلر بعد از UseTextAlignment ثبت می‌شود، پس آخرین کلمه با ماست.
        _grid.DataBindingComplete += (s, e) =>
        {
            foreach (var n in new[] { "Position", "DepartmentName" })
                if (_grid.Columns.Contains(n))
                    _grid.Columns[n].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        };
        _grid.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.CellStyle is null) return;
            if (_grid.Rows[e.RowIndex].DataBoundItem is Employee emp && !emp.IsActive)
                e.CellStyle.ForeColor = ModernTheme.TextDisabled;
        };
        _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Accept(); };
        _grid.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; e.Handled = true; Accept(); }
            else if (e.KeyCode == Keys.Up && _grid.CurrentCell?.RowIndex == 0) { e.Handled = true; _txtSearch.Focus(); }
        };

        // ─── دکمه‌ها ───
        _btnOk = ModernButton.Create("✔  انتخاب", ModernTheme.Success, 140, 42);
        _btnOk.Click += (s, e) => Accept();
        var btnCancel = ModernButton.Cancel();
        btnCancel.Height = 42;
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        var bar = ModernForm.CreateButtonBar(_btnOk, btnCancel);

        // ─── چیدمان ───
        var searchRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = Color.Transparent };
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        _txtSearch.Dock = DockStyle.None;
        _txtSearch.Anchor = AnchorStyles.Left | AnchorStyles.Right;   // ارتفاع ثابت و وسط‌چین عمودی در ردیف
        _txtSearch.Margin = new Padding(0, 5, 10, 5);
        searchRow.Controls.Add(_txtSearch, 0, 0);
        searchRow.Controls.Add(_lblCount, 1, 0);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Padding = new Padding(16, 14, 16, 4), BackColor = ModernTheme.Background
        };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, ModernUI.InputHeight + 10));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        content.Controls.Add(searchRow, 0, 0);
        content.Controls.Add(ModernReport.WrapGrid(_grid), 0, 1);
        bar.Dock = DockStyle.Fill;
        content.Controls.Add(bar, 0, 2);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));   // عنوان + زیرعنوان + لوگو
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.Controls.Add(ModernForm.CreateHeader(title, "با تایپ نام یا کد پرسنلی نتایج فیلتر می‌شوند"), 0, 0);
        root.Controls.Add(content, 0, 1);
        Controls.Add(root);

        // ─── رویدادها ───
        _txtSearch.TextChanged += (s, e) => ApplyFilter();
        _txtSearch.TextBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; e.Handled = true; Accept(); }
            else if (e.KeyCode == Keys.Down && _grid.Rows.Count > 0)
            {
                e.SuppressKeyPress = true; e.Handled = true;
                _grid.Focus();
                if (_grid.CurrentRow is null) _grid.CurrentCell = _grid.Rows[0].Cells["FullName"];
            }
        };
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };

        ApplyFilter();
        Shown += (s, e) =>
        {
            if (selectedId is not null)
            {
                foreach (DataGridViewRow row in _grid.Rows)
                    if (row.DataBoundItem is Employee emp && emp.Id == selectedId.Value)
                    {
                        _grid.CurrentCell = row.Cells["FullName"];
                        break;
                    }
            }
            _txtSearch.Focus();
        };
    }

    // ───────────── فیلتر ─────────────
    private static string Norm(string? s)
        => StringHelper.ConvertToEnglishDigits(StringHelper.NormalizePersianText(s ?? string.Empty)).ToLowerInvariant();

    private void ApplyFilter()
    {
        var tokens = Norm(_txtSearch.Text).Split(new[] { ' ', '\u200c' }, StringSplitOptions.RemoveEmptyEntries);
        IEnumerable<Employee> q = _all;
        if (tokens.Length > 0)
        {
            q = _all.Where(emp =>
            {
                string hay = Norm(string.Join(" ", emp.FullName, emp.PersonnelCode, emp.NationalCode, emp.Position, emp.DepartmentName));
                return tokens.All(t => hay.Contains(t));
            });
        }

        var list = q.ToList();
        _grid.DataSource = new BindingList<Employee>(list);
        _lblCount.Text = StringHelper.ConvertToPersianDigits(list.Count.ToString()) + " نفر";
        if (_grid.Rows.Count > 0) _grid.CurrentCell = _grid.Rows[0].Cells["FullName"];
        _btnOk.Enabled = list.Count > 0;
    }

    private void Accept()
    {
        if (_grid.CurrentRow?.DataBoundItem is Employee emp)
        {
            Selected = emp;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
