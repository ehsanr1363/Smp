using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

/// <summary>
/// چاپ گروهی قرارداد پرسنل: نوع پرسنل (قراردادی / روزمزد / ساعتی) را انتخاب می‌کنید،
/// افراد دلخواه را تیک می‌زنید و همه‌ی قراردادها پشت‌سرهم (هر قرارداد ۲ صفحه) در یک پیش‌نمایش چاپ می‌شوند.
///
/// نسخه‌ی مدرن‌شده:
///  • ترتیب نوار فیلتر و دکمه‌ها درست راست‌به‌چپ شد (لیبل «نوع پرسنل» دیگر در انتهای چپ نیست)
///  • کارت‌های خلاصه (کل / انتخاب‌شده / صفحه / ناقص)
///  • جستجوی سریع (نام، کد، سمت، واحد) با Ctrl+F
///  • ستون «وضعیت» (معتبر / منقضی / بدون تاریخ قرارداد / غیرفعال)
///  • پیش‌فرض انتخاب فقط قراردادهای کامل؛ قراردادهای بدون تاریخ تیک نمی‌خورند و هنگام چاپ هشدار داده می‌شود
///  • کلیک روی هر ردیف = تیک زدن، کلیک روی هدر «چاپ» = انتخاب/لغو همه‌ی ردیف‌های نمایش‌داده‌شده
///  • مرتب‌سازی با کلیک روی هدر ستون‌ها، چک‌باکس «انتخاب همه» همیشه هماهنگ با وضعیت واقعی
///  • پنجره قابل تغییر اندازه، حالت خالی، ردیف‌های انتخاب‌نشده کم‌رنگ
/// </summary>
public class BatchContractPrintForm : Form
{
    private const string StatusValid = "معتبر";
    private const string StatusExpired = "منقضی";
    private const string StatusNoDates = "بدون تاریخ قرارداد";
    private const string StatusInactive = "غیرفعال";

    private readonly string _connectionString;
    private readonly string[] _typeItems = { "قراردادی / ماهانه", "روزمزد", "ساعتی", "همه" };

    private ComboBox cmbType = null!;
    private ModernTextBox txtSearch = null!;
    private CheckBox chkInactive = null!, chkSelectAll = null!;
    private Button btnPrint = null!;
    private DataGridView dgv = null!;
    private Label lblHint = null!, lblEmpty = null!;
    private ModernStatCard cardTotal = null!, cardSelected = null!, cardPages = null!, cardIssues = null!;
    private readonly System.Windows.Forms.Timer _searchTimer = new() { Interval = 250 };

    private List<ContractPrintRow> _all = new();                 // همه‌ی ردیف‌های بارگذاری‌شده (انتخاب‌ها اینجا نگه داشته می‌شود)
    private BindingList<ContractPrintRow> _view = new();         // ردیف‌های نمایش‌داده‌شده (بعد از جستجو و مرتب‌سازی)
    private readonly SemaphoreSlim _lock = new(1, 1);
    private int _loadSeq;
    private bool _suppressSelectAll;
    private string? _sortProp;
    private bool _sortAsc = true;

    public class ContractPrintRow
    {
        public bool Selected { get; set; }
        public Employee Emp { get; set; } = null!;

        public string PersonnelCode => StringHelper.ConvertToPersianDigits(Emp.PersonnelCode ?? "");
        public string FullName => $"{Emp.FirstName} {Emp.LastName}".Trim();
        public string PersonnelType => string.IsNullOrWhiteSpace(Emp.PersonnelType) ? "قراردادی / ماهانه" : Emp.PersonnelType!;
        public string Position => string.IsNullOrWhiteSpace(Emp.Position) ? "—" : Emp.Position!;
        public string DepartmentName => string.IsNullOrWhiteSpace(Emp.DepartmentName) ? "—" : Emp.DepartmentName!;

        public bool HasStart => Emp.ContractStartDate.HasValue && Emp.ContractStartDate.Value.Year > 1900;
        public bool HasEnd => Emp.ContractEndDate.HasValue && Emp.ContractEndDate.Value.Year > 1900;
        public bool HasDates => HasStart && HasEnd;

        public string ContractStart => HasStart
            ? StringHelper.ConvertToPersianDigits(DateHelper.ToPersianDate(Emp.ContractStartDate!.Value)) : "—";
        public string ContractEnd => HasEnd
            ? StringHelper.ConvertToPersianDigits(DateHelper.ToPersianDate(Emp.ContractEndDate!.Value)) : "—";

        public string Status
        {
            get
            {
                if (!Emp.IsActive) return StatusInactive;
                if (!HasDates) return StatusNoDates;
                if (Emp.ContractEndDate!.Value.Date < DateTime.Today) return StatusExpired;
                return StatusValid;
            }
        }
    }

    public BatchContractPrintForm(string connectionString, string initialType = "قراردادی / ماهانه")
    {
        _connectionString = connectionString;
        BuildUI();
        // پنجره قابل تغییر اندازه (قبلاً FixedDialog بود و لیست بلند را نمی‌شد بزرگ کرد)
        this.ApplyStandardHeader("چاپ گروهی قراردادهای پرسنلی", new Size(1180, 760), isDialog: false);
        MinimumSize = new Size(980, 600);
        ShowInTaskbar = false;

        _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); ApplyView(); };

        int idx = Array.IndexOf(_typeItems, initialType);
        cmbType.SelectedIndex = idx >= 0 ? idx : 0;   // انتخاب، خودش لیست را بارگذاری می‌کند
        ShortcutHelper.ApplyStandardShortcuts(this);
        FormClosed += (s, e) =>
        {
            _searchTimer.Stop();
            _searchTimer.Dispose();
            _lock.Dispose();
        };
    }

    // ───────────────────────── ساخت رابط ─────────────────────────
    private void BuildUI()
    {
        // ── کارت‌های خلاصه (اولین کارت در راست) ──
        var strip = ModernReport.CreateStatStrip(4, 88);
        cardTotal = strip.Add("کل پرسنل لیست", ModernTheme.Primary);
        cardSelected = strip.Add("انتخاب‌شده برای چاپ", ModernTheme.Success);
        cardPages = strip.Add("تعداد صفحه (هر قرارداد ۲ صفحه)", ModernTheme.Purple);
        cardIssues = strip.Add("انتخاب‌شده‌های ناقص", ModernTheme.Warning, "بدون تاریخ شروع/پایان");
        strip.Dock = DockStyle.Fill;

        // ── فیلترها ──
        cmbType = ModernUI.Combo();
        cmbType.Items.AddRange(_typeItems);
        cmbType.Width = 210;
        cmbType.Margin = new Padding(0, 4, 14, 0);
        cmbType.SelectedIndexChanged += async (s, e) => await LoadAsync();

        var lblType = ModernUI.Caption("نوع پرسنل", 10.5f, true);
        lblType.AutoSize = true;
        lblType.Margin = new Padding(6, 10, 6, 0);

        txtSearch = ModernUI.Input(ModernInputKind.Text, "جستجوی نام، کد پرسنلی، سمت یا واحد…   (Ctrl+F)");
        txtSearch.Width = 330;
        txtSearch.Height = ModernUI.InputHeight;
        txtSearch.Margin = new Padding(0, 4, 14, 0);
        txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };

        chkInactive = ModernUI.Check("نمایش غیرفعال‌ها");
        chkInactive.Width = 170;
        chkInactive.Margin = new Padding(0, 6, 8, 0);
        chkInactive.CheckedChanged += async (s, e) => await LoadAsync();

        chkSelectAll = ModernUI.Check("انتخاب / لغو همه");
        chkSelectAll.Width = 170;
        chkSelectAll.Margin = new Padding(0, 6, 0, 0);
        chkSelectAll.CheckedChanged += (s, e) =>
        {
            if (_suppressSelectAll) return;
            SetAllVisible(chkSelectAll.Checked);
        };

        var filterBar = ModernReport.CreateFilterBar();
        filterBar.Dock = DockStyle.Fill;
        filterBar.Padding = new Padding(0, 6, 0, 4);
        // در فرم RightToLeft اولین کنترلِ Flow در سمت چپ می‌نشیند؛ پس به ترتیب معکوس اضافه می‌کنیم
        // تا از راست: «نوع پرسنل» ← کمبو ← جستجو ← غیرفعال‌ها ← انتخاب همه
        filterBar.Controls.Add(chkSelectAll);
        filterBar.Controls.Add(chkInactive);
        filterBar.Controls.Add(txtSearch);
        filterBar.Controls.Add(cmbType);
        filterBar.Controls.Add(lblType);

        // ── جدول ──
        dgv = ModernGrid.Create();
        dgv.AllowUserToAddRows = false;
        dgv.AutoGenerateColumns = false;
        dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;   // قبلاً CellSelect بود و یک سلول صورتی گیج‌کننده می‌شد
        dgv.MultiSelect = false;
        dgv.ReadOnly = false;
        dgv.FillColumns();
        dgv.UseAiryLook(46);

        dgv.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", DataPropertyName = "Selected", HeaderText = "چاپ", FillWeight = 6, MinimumWidth = 56 });
        AddText("PersonnelCode", "کد پرسنلی", 9, true);
        AddText("FullName", "نام و نام خانوادگی", 18, false);
        AddText("PersonnelType", "نوع پرسنل", 11, true);
        AddText("Position", "سمت", 14, false);
        AddText("DepartmentName", "واحد سازمانی", 15, false);
        AddText("ContractStart", "شروع قرارداد", 10, true);
        AddText("ContractEnd", "پایان قرارداد", 10, true);
        AddText("Status", "وضعیت", 13, true);

        dgv.ColorizeText("Status", new Dictionary<string, Color>
        {
            [StatusValid] = ModernTheme.Success,
            [StatusExpired] = ModernTheme.Danger,
            [StatusNoDates] = ModernTheme.Warning,
            [StatusInactive] = ModernTheme.Neutral
        });

        // ثبت فوری تیک
        dgv.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (dgv.IsCurrentCellDirty && dgv.CurrentCell is DataGridViewCheckBoxCell)
                dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        dgv.CellValueChanged += (s, e) => { if (e.RowIndex >= 0) { dgv.InvalidateRow(e.RowIndex); UpdateCount(); } };

        // کلیک روی هر جای ردیف = تیک زدن/برداشتن
        dgv.CellClick += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgv.Columns[e.ColumnIndex].Name == "Selected") return;   // خود چک‌باکس کار خودش را می‌کند
            ToggleRow(e.RowIndex);
        };
        dgv.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Space && dgv.CurrentRow is not null &&
                !(dgv.CurrentCell is DataGridViewCheckBoxCell))
            {
                ToggleRow(dgv.CurrentRow.Index);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };

        // هدر «چاپ» = انتخاب/لغو همه؛ هدر بقیه‌ی ستون‌ها = مرتب‌سازی
        dgv.ColumnHeaderMouseClick += (s, e) =>
        {
            if (e.ColumnIndex < 0) return;
            var col = dgv.Columns[e.ColumnIndex];
            if (col.Name == "Selected")
            {
                SetAllVisible(!(_view.Count > 0 && _view.All(r => r.Selected)));
                return;
            }
            if (_sortProp == col.DataPropertyName) _sortAsc = !_sortAsc;
            else { _sortProp = col.DataPropertyName; _sortAsc = true; }
            ApplyView();
        };

        // ردیف‌های انتخاب‌نشده کم‌رنگ؛ تاریخ خالی با رنگ هشدار
        dgv.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _view.Count || e.CellStyle is null) return;
            var row = _view[e.RowIndex];
            string name = dgv.Columns[e.ColumnIndex].Name;
            if (name == "Selected") return;
            if (!row.Selected) e.CellStyle.ForeColor = ModernTheme.TextDisabled;
            if ((name == "ContractStart" && !row.HasStart) || (name == "ContractEnd" && !row.HasEnd))
                e.CellStyle.ForeColor = ModernTheme.Warning;
        };

        var gridHost = ModernReport.WrapGrid(dgv);

        lblEmpty = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(11.5f),
            ForeColor = ModernTheme.TextMuted,
            BackColor = ModernTheme.Surface,
            Text = "موردی برای نمایش وجود ندارد.",
            Visible = false
        };
        gridHost.Controls.Add(lblEmpty);
        lblEmpty.BringToFront();

        // ── نوار پایین ──
        btnPrint = ModernButton.Success("چاپ قراردادهای انتخاب‌شده", 270, 44);
        btnPrint.Enabled = false;
        btnPrint.Click += async (s, e) => await PrintAsync();

        var btnClose = ModernButton.Cancel("بستن", 110, 44);
        btnClose.Click += (s, e) => Close();

        lblHint = ModernUI.Caption("", 9.5f, false, ModernTheme.TextMuted);
        lblHint.AutoSize = true;
        lblHint.Margin = new Padding(14, 14, 0, 0);

        // اولین کنترل در چپ می‌نشیند: توضیح ← بستن ← چاپ (چاپ در راست)
        var bar = ModernForm.CreateButtonBar(lblHint, btnClose, btnPrint);
        bar.Dock = DockStyle.Fill;
        bar.Padding = new Padding(0, 10, 0, 10);

        // ── چیدمان ──
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16, 12, 16, 0),
            BackColor = ModernTheme.Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));
        root.Controls.Add(strip, 0, 0);
        root.Controls.Add(filterBar, 0, 1);
        root.Controls.Add(gridHost, 0, 2);
        root.Controls.Add(bar, 0, 3);
        Controls.Add(root);
    }

    private void AddText(string prop, string header, float weight, bool center)
    {
        var col = new DataGridViewTextBoxColumn
        {
            Name = prop,
            DataPropertyName = prop,
            HeaderText = header,
            ReadOnly = true,
            FillWeight = weight,
            SortMode = DataGridViewColumnSortMode.Programmatic
        };
        // همه‌ی ستون‌ها وسط‌چین (MiddleRight در فرم Mirror شده سمت چپ سلول می‌نشست و با هدر هم‌خوان نبود)
        col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgv.Columns.Add(col);
    }

    // ───────────────────────── بارگذاری و نمایش ─────────────────────────
    private async Task LoadAsync()
    {
        if (IsDisposed || cmbType.SelectedItem is null) return;
        int seq = ++_loadSeq;
        string type = cmbType.SelectedItem.ToString()!;
        lblHint.Text = "در حال بارگذاری لیست…";

        List<Employee> list;
        try
        {
            var repo = new EmployeeRepository(_connectionString);
            list = await repo.GetEmployeesWithDetailsAsync("", chkInactive.Checked);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "BatchContractPrintForm.Load");
            if (seq == _loadSeq && !IsDisposed) lblHint.Text = "";
            ModernMessageBox.ShowError("خطا در بارگذاری لیست پرسنل:\n" + ex.Message);
            return;
        }
        if (seq != _loadSeq || IsDisposed) return;

        // همان منطق فیلتر لیست پرسنل
        if (type == "قراردادی / ماهانه")
            list = list.Where(x => x.PersonnelType != "روزمزد" && x.PersonnelType != "ساعتی").ToList();
        else if (type != "همه")
            list = list.Where(x => x.PersonnelType == type).ToList();

        // فقط قراردادهای کامل و فعال به‌طور پیش‌فرض تیک می‌خورند
        _all = list.Select(e =>
        {
            var r = new ContractPrintRow { Emp = e };
            r.Selected = e.IsActive && r.HasDates;
            return r;
        }).ToList();

        ApplyView();
    }

    /// <summary>جستجو + مرتب‌سازی روی _all و نمایش در جدول (انتخاب‌ها حفظ می‌شود)</summary>
    private void ApplyView()
    {
        if (IsDisposed) return;

        IEnumerable<ContractPrintRow> src = _all;
        string q = Normalize(txtSearch?.Text);
        if (q.Length > 0)
            src = src.Where(r => Normalize(r.FullName).Contains(q)
                              || Normalize(r.Emp.PersonnelCode).Contains(q)
                              || Normalize(r.Position).Contains(q)
                              || Normalize(r.DepartmentName).Contains(q));

        if (_sortProp is not null)
        {
            string prop = _sortProp;
            src = _sortAsc ? src.OrderBy(r => SortKey(r, prop)) : src.OrderByDescending(r => SortKey(r, prop));
        }

        _view = new BindingList<ContractPrintRow>(src.ToList());
        dgv.DataSource = _view;

        foreach (DataGridViewColumn c in dgv.Columns)
            c.HeaderCell.SortGlyphDirection = SortOrder.None;
        if (_sortProp is not null && dgv.Columns.Contains(_sortProp))
            dgv.Columns[_sortProp].HeaderCell.SortGlyphDirection = _sortAsc ? SortOrder.Ascending : SortOrder.Descending;

        lblEmpty.Visible = _view.Count == 0;
        UpdateCount();
    }

    private static IComparable SortKey(ContractPrintRow r, string prop) => prop switch
    {
        "PersonnelCode" => (IComparable)Normalize(r.Emp.PersonnelCode).PadLeft(12, '0'),
        "ContractStart" => (IComparable)(r.Emp.ContractStartDate ?? DateTime.MinValue),
        "ContractEnd" => (IComparable)(r.Emp.ContractEndDate ?? DateTime.MinValue),
        _ => (IComparable)Normalize(typeof(ContractPrintRow).GetProperty(prop)?.GetValue(r)?.ToString())
    };

    /// <summary>یکسان‌سازی متن برای جستجو: ارقام فارسی/عربی، «ي/ك»، فاصله و نیم‌فاصله</summary>
    private static string Normalize(string? s) => PersianText.NormalizeForSearch(s);

    // ───────────────────────── انتخاب‌ها ─────────────────────────
    private void ToggleRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _view.Count) return;
        _view[rowIndex].Selected = !_view[rowIndex].Selected;
        dgv.InvalidateRow(rowIndex);
        UpdateCount();
    }

    /// <summary>انتخاب/لغو همه‌ی ردیف‌های نمایش‌داده‌شده (ردیف‌های پنهان‌شده با جستجو دست‌نخورده می‌مانند)</summary>
    private void SetAllVisible(bool value)
    {
        foreach (var r in _view) r.Selected = value;
        dgv.Refresh();
        UpdateCount();
    }

    private void UpdateCount()
    {
        if (IsDisposed) return;

        var selected = _all.Where(r => r.Selected).ToList();
        int sel = selected.Count;
        int incomplete = selected.Count(r => !r.HasDates);
        int expired = selected.Count(r => r.Status == StatusExpired);

        cardTotal.SetCount(_all.Count, "نفر");
        cardTotal.SubText = _view.Count != _all.Count ? $"نمایش {StringHelper.ConvertToPersianDigits(_view.Count.ToString())} مورد (فیلتر جستجو)" : "";
        cardSelected.SetCount(sel, "نفر");
        cardSelected.SubText = expired > 0 ? $"{StringHelper.ConvertToPersianDigits(expired.ToString())} نفر قرارداد منقضی دارند" : "";
        cardPages.SetCount(sel * 2, "صفحه");
        cardIssues.SetCount(incomplete, "نفر");
        cardIssues.Accent = incomplete > 0 ? ModernTheme.Warning : ModernTheme.Success;

        btnPrint.Enabled = sel > 0;
        btnPrint.Text = sel > 0
            ? $"چاپ {StringHelper.ConvertToPersianDigits(sel.ToString())} قرارداد"
            : "چاپ قراردادهای انتخاب‌شده";

        lblHint.Text = incomplete > 0
            ? "⚠ برخی از انتخاب‌ها تاریخ قرارداد ندارند."
            : "برای تیک زدن روی هر ردیف کلیک کنید.";

        // چک‌باکس «انتخاب همه» همیشه با وضعیت واقعی ردیف‌های نمایش‌داده‌شده هماهنگ است
        bool allVisibleSelected = _view.Count > 0 && _view.All(r => r.Selected);
        if (chkSelectAll.Checked != allVisibleSelected)
        {
            _suppressSelectAll = true;
            chkSelectAll.Checked = allVisibleSelected;
            _suppressSelectAll = false;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.F)) { txtSearch.Focus(); return true; }
        if (keyData == (Keys.Control | Keys.P) && btnPrint.Enabled) { _ = PrintAsync(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ───────────────────────── چاپ ─────────────────────────
    private async Task PrintAsync()
    {
        var selectedRows = _all.Where(r => r.Selected).ToList();
        var selected = selectedRows.Select(r => r.Emp).ToList();
        if (selected.Count == 0 || IsDisposed) return;

        int incomplete = selectedRows.Count(r => !r.HasDates);
        if (incomplete > 0 &&
            !ModernMessageBox.AskConfirm($"{StringHelper.ConvertToPersianDigits(incomplete.ToString())} نفر از انتخاب‌ها تاریخ شروع یا پایان قرارداد ندارند و قرارداد آن‌ها ناقص چاپ می‌شود. ادامه می‌دهید؟"))
            return;

        if (selected.Count > 150 &&
            !ModernMessageBox.AskConfirm($"{StringHelper.ConvertToPersianDigits(selected.Count.ToString())} قرارداد انتخاب شده است و آماده‌سازی چند دقیقه طول می‌کشد. ادامه می‌دهید؟"))
            return;
        if (!await _lock.WaitAsync(0)) return;

        try
        {
            string? htmlPath = null;
            var failed = new List<string>();

            bool ok = await AsyncHelper.RunWithLoading(this, async () =>
            {
                string dir = Path.Combine(Path.GetTempPath(), "SmpContracts");
                Directory.CreateDirectory(dir);
                string logoHtml = ContractHtmlBuilder.LoadLogoHtml(Path.Combine(dir, "logo.png"));
                string customText = await ContractHtmlBuilder.LoadCustomTextAsync();

                var sb = new StringBuilder();
                for (int i = 0; i < selected.Count; i++)
                {
                    try { sb.Append(await ContractHtmlBuilder.BuildPagesAsync(selected[i], logoHtml, customText, pageBreakBefore: i > 0)); }
                    catch (Exception ex)
                    {
                        LogHelper.LogException(ex, "BatchContractPrintForm.Build");
                        failed.Add($"{selected[i].FirstName} {selected[i].LastName}");
                    }
                }

                htmlPath = Path.Combine(dir, $"contracts_{DateTime.Now:yyyyMMdd_HHmmss}.html");
                File.WriteAllText(htmlPath, ContractHtmlBuilder.WrapDocument(sb.ToString()), new UTF8Encoding(true));
            }, "در حال آماده‌سازی قراردادها...");

            if (!ok || htmlPath is null || IsDisposed) return;

            if (failed.Count > 0)
                ModernMessageBox.ShowWarning("ساخت قرارداد این افراد با خطا مواجه شد و در چاپ نیامده است:\n" + string.Join("\n", failed.Take(15)));

            using var preview = new HtmlPreviewForm(htmlPath, $"پیش‌نمایش چاپ گروهی - {cmbType.SelectedItem}");
            preview.ShowDialog(this);
        }
        finally
        {
            try { if (!IsDisposed) _lock.Release(); }
            catch (ObjectDisposedException) { /* فرم هنگام چاپ بسته شده */ }
        }
    }

    /// <summary>پنجره‌ی پیش‌نمایش: فایل HTML را باز می‌کند و پنجره‌ی پیش‌نمایش چاپ را نشان می‌دهد</summary>
    private sealed class HtmlPreviewForm : Form
    {
        private readonly WebBrowser _browser;
        private bool _shown;

        public HtmlPreviewForm(string htmlPath, string title)
        {
            Text = title;
            WindowState = FormWindowState.Maximized;
            ShowInTaskbar = false;
            _browser = new WebBrowser
            {
                Dock = DockStyle.Fill,
                IsWebBrowserContextMenuEnabled = false,
                WebBrowserShortcutsEnabled = false,
                AllowWebBrowserDrop = false,
                ScriptErrorsSuppressed = true
            };
            Controls.Add(_browser);

            _browser.DocumentCompleted += (s, e) =>
            {
                if (_shown) return;
                _shown = true;
                BeginInvoke(new Action(() => ContractHtmlBuilder.ShowPrintPreview(_browser)));
            };
            Shown += (s, e) => _browser.Navigate(new Uri(htmlPath).AbsoluteUri);
        }
    }
}
