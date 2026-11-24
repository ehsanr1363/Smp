using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using Smp.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

/// <summary>
/// ثبت و محاسبه‌ی حق ماموریت داخل کشور.
/// مبنای حقوق = مجموع اجزای تیک‌خورده در «تنظیمات حقوق سالانه ← مشمولین ماموریت»
/// حق ماموریت هر روز = مبنا × درصد ماموریت ؛ جمع = مبلغ هر روز × تعداد روز (+ شب/اقامت/ایاب‌وذهاب/نهار).
/// </summary>
public class MissionForm : Form
{
    private static readonly string[] Provinces =
    {
        "آذربایجان شرقی", "آذربایجان غربی", "اردبیل", "اصفهان", "البرز", "ایلام", "بوشهر", "تهران",
        "چهارمحال و بختیاری", "خراسان جنوبی", "خراسان رضوی", "خراسان شمالی", "خوزستان", "زنجان", "سمنان",
        "سیستان و بلوچستان", "فارس", "قزوین", "قم", "کردستان", "کرمان", "کرمانشاه", "کهگیلویه و بویراحمد",
        "گلستان", "گیلان", "لرستان", "مازندران", "مرکزی", "هرمزگان", "همدان", "یزد"
    };

    private static readonly Regex TimeRegex = new Regex(@"^(\d{1,2}):(\d{1,2})$", RegexOptions.Compiled);

    private readonly string _connectionString;
    private readonly MissionRepository _repo;
    private readonly EmployeeRepository _empRepo;
    private readonly SalarySettingsRepository _settingsRepo;

    private EmployeePickerField employeePicker = null!;
    private ComboBox cmbProvince = null!;
    private TextBox txtLocation = null!, txtSubject = null!, txtNotes = null!;
    private TextBox txtDepartDate = null!, txtDepartTime = null!, txtReturnDate = null!, txtReturnTime = null!;
    private TextBox txtSalaryBase = null!, txtRate = null!, txtDailyAmount = null!, txtDaysTotal = null!;
    private TextBox txtNightUnit = null!, txtStayUnit = null!, txtLunchUnit = null!, txtTravel = null!, txtGrand = null!;
    private NumericUpDown numDays = null!, numNights = null!, numStay = null!, numLunch = null!;
    private Label lblInfo = null!;
    private DataGridView dgv = null!;
    private Button btnSave = null!, btnNew = null!, btnDelete = null!, btnPrint = null!, btnPrintSelected = null!, btnExcel = null!;

    private List<Employee> _employees = new();
    private List<Company> _companies = new();
    private readonly Dictionary<int, Image?> _logos = new();
    private readonly Dictionary<int, SalarySettings?> _settingsCache = new();

    private int _editingId;
    private decimal _defaultRate = 3m;
    private bool _suppress;                  // جلوگیری از محاسبه/رفرش در زمان پر کردن خودکار فرم
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public MissionForm(string connectionString)
    {
        _connectionString = connectionString;
        _repo = new MissionRepository(connectionString);
        _empRepo = new EmployeeRepository(connectionString);
        _settingsRepo = new SalarySettingsRepository(connectionString);

        BuildUI();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

        Shown += async (s, e) => await LoadDataSafeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _loadLock.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "MissionForm"); }
            try { _actionLock.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "MissionForm"); }
            foreach (var img in _logos.Values) { try { img?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "MissionForm"); } }
        }
        base.Dispose(disposing);
    }

    // ═══════════════════════════ ساخت رابط کاربری ═══════════════════════════
    private void BuildUI()
    {
        // ─── جدول فهرست ماموریت‌ها ───
        dgv = ModernGrid.Create("Id");
        dgv.AutoGenerateColumns = false;
        dgv.FillColumns();
        dgv.MultiSelect = true;
        dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgv.ReadOnly = true;
        dgv.AllowUserToAddRows = false;

        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "نام کارمند", FillWeight = 20 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Province", DataPropertyName = "Province", HeaderText = "استان", FillWeight = 11 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Location", DataPropertyName = "Location", HeaderText = "محل ماموریت", FillWeight = 12 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Subject", DataPropertyName = "Subject", HeaderText = "موضوع", FillWeight = 22 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "DepartDate", DataPropertyName = "DepartDate", HeaderText = "تاریخ رفت", FillWeight = 10 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ReturnDate", DataPropertyName = "ReturnDate", HeaderText = "تاریخ برگشت", FillWeight = 10 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "DaysCount", DataPropertyName = "DaysCount", HeaderText = "روز", FillWeight = 5 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "GrandTotal", DataPropertyName = "GrandTotal", HeaderText = "جمع کل (ریال)", FillWeight = 14 });
        foreach (var c in new[] { "DepartDate", "ReturnDate", "DaysCount", "GrandTotal" })
            dgv.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        dgv.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string col = dgv.Columns[e.ColumnIndex].Name;
            if ((col == "DepartDate" || col == "ReturnDate") && e.Value is DateTime dt)
            {
                e.Value = StringHelper.ConvertToPersianDigits(DateHelper.ToPersianDate(dt));
                e.FormattingApplied = true;
            }
            else if (col == "GrandTotal" && e.Value is decimal d)
            {
                e.Value = NumberHelper.FormatThousands(d, usePersianDigits: true);
                e.CellStyle.ForeColor = ModernTheme.Success;
                e.CellStyle.Font = ModernTheme.GetFont(11f, FontStyle.Bold);
                e.FormattingApplied = true;
            }
        };
        dgv.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            if (dgv.Rows[e.RowIndex].DataBoundItem is MissionRecord rec) LoadRecordIntoForm(rec);
        };

        // ─── کنترل‌ها ───
        employeePicker = new EmployeePickerField();
        employeePicker.SelectedChanged += async (s, e) => { if (!_suppress) await RefreshSalaryBaseAsync(); };

        cmbProvince = ModernUI.Combo();
        // ModernComboBox فقط حالت DropDownList را درست رسم می‌کند (DropDown باعث بهم‌ریختگی و متن ناپدید می‌شد)
        cmbProvince.Items.AddRange(Provinces);

        txtLocation = new TextBox();
        txtSubject = new TextBox();
        txtNotes = new TextBox();

        txtDepartDate = MakeDateBox();
        txtReturnDate = MakeDateBox();
        txtDepartTime = MakeTimeBox("08:00");
        txtReturnTime = MakeTimeBox("17:00");

        txtSalaryBase = new TextBox();
        txtRate = new TextBox();
        txtDailyAmount = new TextBox();
        txtDaysTotal = new TextBox();
        txtNightUnit = MakeAmountBox();
        txtStayUnit = MakeAmountBox();
        txtLunchUnit = MakeAmountBox();
        txtTravel = MakeAmountBox();
        txtGrand = new TextBox();

        numDays = MakeNum(1); numNights = MakeNum(0); numStay = MakeNum(0); numLunch = MakeNum(0);

        // رویدادهای محاسبه
        txtDepartDate.TextChanged += async (s, e) => await OnDatesChangedAsync();
        txtReturnDate.TextChanged += async (s, e) => await OnDatesChangedAsync();
        txtRate.TextChanged += (s, e) => Recalc();
        foreach (var t in new[] { txtNightUnit, txtStayUnit, txtLunchUnit, txtTravel }) t.TextChanged += (s, e) => Recalc();
        foreach (var n in new[] { numDays, numNights, numStay, numLunch }) n.ValueChanged += (s, e) => Recalc();

        // فقط‌خواندنی‌ها
        foreach (var t in new[] { txtSalaryBase, txtDailyAmount, txtDaysTotal, txtGrand })
        {
            t.ReadOnly = true; t.TabStop = false; t.BackColor = ModernTheme.InputDisabled;
            t.TextAlign = HorizontalAlignment.Center;
        }
        txtGrand.Font = ModernTheme.GetFont(13f, FontStyle.Bold);
        txtGrand.ForeColor = ModernTheme.Success;

        lblInfo = ModernUI.Caption("", 9.5f, false, ModernTheme.TextMuted);
        lblInfo.AutoSize = false;

        // ─── سه کارت کنار هم (هر کدام ۸ ردیف هم‌قد) ───
        // ارتفاع ردیف باید حداقل InputHeight(36) + حاشیه‌های ردیف و ورودی (۴ + ۸) باشد؛
        // با ۴۲ قبلی، ورودی‌ها بریده می‌شدند و کمبو/تاریخ/عددی هم‌قد و هم‌تراز نبودند.
        const int rh = ModernTheme.InputHeight + 12;
        const int lw = 165;
        ModernCard NewCard(string title, Color accent) => new ModernCard(title, accent, 1, rh, rh, rh, rh, rh, rh, rh, rh) { Dock = DockStyle.Fill, Margin = new Padding(5, 0, 5, 0) };

        var cardInfo = NewCard("مشخصات ماموریت", ModernTheme.Primary);
        cardInfo.Body.Controls.Add(ModernUI.Row("کارمند", employeePicker, lw), 0, 0);
        cardInfo.Body.Controls.Add(ModernUI.Row("مبنای حقوق مشمول (ریال)", txtSalaryBase, lw), 0, 1);
        cardInfo.Body.Controls.Add(ModernUI.Row("استان ماموریت", cmbProvince, lw), 0, 2);
        cardInfo.Body.Controls.Add(ModernUI.Row("محل ماموریت", txtLocation, lw), 0, 3);
        cardInfo.Body.Controls.Add(ModernUI.Row("موضوع ماموریت", txtSubject, lw), 0, 4);
        cardInfo.Body.Controls.Add(ModernUI.Row("توضیحات", txtNotes, lw), 0, 5);
        cardInfo.Body.Controls.Add(ModernUI.Row("جمع کل (ریال)", txtGrand, lw), 0, 6);
        lblInfo.Dock = DockStyle.Fill;
        cardInfo.Body.Controls.Add(lblInfo, 0, 7);

        var cardTime = NewCard("زمان و حق ماموریت روزانه", ModernTheme.Teal);
        cardTime.Body.Controls.Add(ModernUI.Row("تاریخ رفت", txtDepartDate, lw), 0, 0);
        cardTime.Body.Controls.Add(ModernUI.Row("ساعت رفت (مثلاً 06:00)", txtDepartTime, lw), 0, 1);
        cardTime.Body.Controls.Add(ModernUI.Row("تاریخ برگشت", txtReturnDate, lw), 0, 2);
        cardTime.Body.Controls.Add(ModernUI.Row("ساعت برگشت (مثلاً 16:00)", txtReturnTime, lw), 0, 3);
        cardTime.Body.Controls.Add(ModernUI.Row("تعداد روز ماموریت", numDays, lw), 0, 4);
        cardTime.Body.Controls.Add(ModernUI.Row("درصد حق ماموریت (٪)", txtRate, lw), 0, 5);
        cardTime.Body.Controls.Add(ModernUI.Row("مبلغ هر روز (ریال)", txtDailyAmount, lw), 0, 6);
        cardTime.Body.Controls.Add(ModernUI.Row("جمع حق روزها (ریال)", txtDaysTotal, lw), 0, 7);

        var cardCost = NewCard("شب، اقامت، نهار و ایاب‌وذهاب", ModernTheme.Orange);
        cardCost.Body.Controls.Add(ModernUI.Row("تعداد شب", numNights, lw), 0, 0);
        cardCost.Body.Controls.Add(ModernUI.Row("مبلغ هر شب (ریال)", txtNightUnit, lw), 0, 1);
        cardCost.Body.Controls.Add(ModernUI.Row("تعداد اقامت", numStay, lw), 0, 2);
        cardCost.Body.Controls.Add(ModernUI.Row("مبلغ هر اقامت (ریال)", txtStayUnit, lw), 0, 3);
        cardCost.Body.Controls.Add(ModernUI.Row("تعداد نهار", numLunch, lw), 0, 4);
        cardCost.Body.Controls.Add(ModernUI.Row("مبلغ هر نهار (ریال)", txtLunchUnit, lw), 0, 5);
        cardCost.Body.Controls.Add(ModernUI.Row("ایاب و ذهاب (ریال)", txtTravel, lw), 0, 6);

        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = ModernTheme.Background };
        for (int i = 0; i < 3; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        cards.Controls.Add(cardInfo, 0, 0);
        cards.Controls.Add(cardTime, 1, 0);
        cards.Controls.Add(cardCost, 2, 0);

        // ─── دکمه‌ها ───
        btnSave = ModernButton.Save("ذخیره", 130, 42);
        btnSave.Click += async (s, e) => await SaveSafeAsync();
        btnNew = ModernButton.Outline("جدید", ModernTheme.Neutral, 110, 42);
        btnNew.Click += async (s, e) => await ClearFormAsync();
        btnPrint = ModernButton.Primary("چاپ فرم", 130, 42);
        btnPrint.Click += (s, e) => PrintCurrent();
        btnPrintSelected = ModernButton.Soft("چاپ لیست انتخابی", ModernTheme.Primary, 170, 42);
        btnPrintSelected.Click += (s, e) => PrintSelected();
        btnExcel = ModernButton.Soft("خروجی اکسل", ModernTheme.Teal, 140, 42);
        btnExcel.Click += (s, e) => ExportExcel();
        btnDelete = ModernButton.Danger("حذف", 100, 42);
        btnDelete.Click += async (s, e) => await DeleteSafeAsync();

        var hint = ModernUI.Caption("برای ویرایش، روی ردیف لیست دوبار کلیک کنید. (انتخاب چند ردیف با Ctrl/Shift برای چاپ یا اکسل)", 9.5f, false, ModernTheme.TextMuted);
        hint.AutoSize = true;
        hint.Margin = new Padding(15, 14, 0, 0);
        var bar = ModernForm.CreateButtonBar(btnSave, btnNew, btnPrint, btnPrintSelected, btnExcel, btnDelete, hint);

        // ─── چیدمان صفحه ───
        var gridCard = new ModernCard("فهرست ماموریت‌های ثبت‌شده", ModernTheme.Success, new[] { new ColumnStyle(SizeType.Percent, 100F) });
        dgv.Dock = DockStyle.Fill;
        dgv.Margin = Padding.Empty;
        gridCard.Body.Controls.Add(dgv, 0, 0);
        gridCard.Dock = DockStyle.Fill;
        gridCard.Margin = new Padding(5, 0, 5, 0);

        int cardsHeight = ModernCard.TitleHeight + rh * 8 + 14 + 14;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8, 10, 8, 0), BackColor = ModernTheme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, cardsHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        root.Controls.Add(cards, 0, 0);
        root.Controls.Add(gridCard, 0, 1);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);

        this.ApplyStandardHeader("ماموریت و حق ماموریت", new Size(1400, 900), isDialog: false);
        MinimumSize = new Size(1150, 800);

        ResetFormFields();
    }

    private static TextBox MakeDateBox()
    {
        var t = new TextBox();
        DateHelper.ApplyDateMask(t);
        t.Text = DateHelper.ToPersianDate(DateTime.Today);
        PersianDatePicker.AttachTo(t);
        return t;
    }

    private TextBox MakeTimeBox(string def)
    {
        var t = new TextBox { MaxLength = 5, Text = def };
        t.Leave += (s, e) => t.Text = NormalizeTime(t.Text, keepInvalid: true);
        return t;
    }

    private static TextBox MakeAmountBox()
    {
        var t = new TextBox { Text = "0" };
        SmartInputHelper.EnableSmartAmountTextBox(t);
        return t;
    }

    private static NumericUpDown MakeNum(int min) => new NumericUpDown
    {
        Minimum = min,
        Maximum = 365,
        Value = min,
        TextAlign = HorizontalAlignment.Center
    };

    // ═══════════════════════════ بارگذاری ═══════════════════════════
    private async Task LoadDataSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;
        try
        {
            await AsyncHelper.RunWithLoading(this, async () =>
            {
                var empTask = Task.Run(() => _empRepo.GetEmployeesWithDetailsAsync("", false));
                var missionTask = Task.Run(() => _repo.GetAllAsync());
                var companyTask = Task.Run(() => new CompanyRepository(_connectionString).GetAll());
                await Task.WhenAll(empTask, missionTask, companyTask);
                if (IsDisposed) return;

                _employees = empTask.Result;
                _companies = companyTask.Result;
                foreach (var c in _companies)
                    if (!_logos.ContainsKey(c.Id)) _logos[c.Id] = ImageHelper.ByteArrayToImage(c.Logo);

                _suppress = true;
                try
                {
                    employeePicker.SetEmployees(_employees);   // انتخاب فعلی (اگر در فهرست باشد) حفظ می‌شود
                }
                finally { _suppress = false; }

                dgv.DataSource = missionTask.Result;
            }, "در حال بارگذاری اطلاعات...");

            if (_editingId == 0) await RefreshSalaryBaseAsync();
        }
        catch (Exception ex) { LogHelper.LogException(ex, "MissionForm.LoadDataSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError(ex.Message); }
        finally { try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "MissionForm"); } }
    }

    private async Task ReloadListAsync()
    {
        var list = await _repo.GetAllAsync();
        if (!IsDisposed) dgv.DataSource = list;
    }

    // ═══════════════════════════ مبنای حقوق ═══════════════════════════
    private Employee? SelectedEmployee => employeePicker.Selected;

    private static int PersianYearOf(DateTime d) => new PersianCalendar().GetYear(d);

    private async Task<SalarySettings?> GetSettingsAsync(int year)
    {
        if (_settingsCache.TryGetValue(year, out var cached)) return cached;
        var st = await _settingsRepo.GetByYearAsync(year);
        _settingsCache[year] = st;
        return st;
    }

    /// <summary>مبنا و درصد را از تنظیمات سال تاریخ رفت و اجزای تیک‌خوردهٔ «مشمولین ماموریت» می‌خواند.</summary>
    private async Task RefreshSalaryBaseAsync()
    {
        var emp = SelectedEmployee;
        if (emp is null)
        {
            txtSalaryBase.Text = NumberHelper.FormatThousands(0, usePersianDigits: true);
            lblInfo.Text = "";
            Recalc();
            return;
        }

        DateTime depart = DateHelper.ParsePersianDate(txtDepartDate.Text) ?? DateTime.Today;
        int year = PersianYearOf(depart);

        SalarySettings? st;
        try { st = await GetSettingsAsync(year); }
        catch (Exception ex) { LogHelper.LogException(ex, "MissionForm.RefreshSalaryBaseAsync"); st = null; }
        if (IsDisposed) return;

        decimal baseAmount, newDefault;
        if (st is null)
        {
            baseAmount = emp.BaseSalary;
            newDefault = 3m;
            lblInfo.ForeColor = ModernTheme.DangerText;
            lblInfo.Text = $"⚠️ تنظیمات حقوق سال {StringHelper.ConvertToPersianDigits(year.ToString())} ثبت نشده؛ فقط «حقوق پایه» و نرخ ۳٪ لحاظ شد.";
        }
        else
        {
            var keys = SalaryComponents.Parse(st.MissionIncludeComponents);
            baseAmount = SalaryComponents.ComputeBase(emp, st, keys);
            newDefault = st.MissionRate;
            var titles = SalaryComponents.All.Where(c => keys.Contains(c.Key)).Select(c => c.Title).ToList();
            lblInfo.ForeColor = ModernTheme.TextMuted;
            lblInfo.Text = "مبنا شامل: " + (titles.Count == 0 ? "— (هیچ مورد تیک نخورده؛ تنظیمات حقوق ← مشمولین ماموریت)" : string.Join("، ", titles));
        }

        // اگر کاربر درصد را دستی تغییر نداده بود، با پیش‌فرض جدید جایگزین شود
        bool rateWasDefault = string.IsNullOrWhiteSpace(txtRate.Text) || ParseRate(txtRate.Text) == _defaultRate;
        _defaultRate = newDefault;

        _suppress = true;
        try
        {
            txtSalaryBase.Text = NumberHelper.FormatThousands(baseAmount, usePersianDigits: true);
            if (rateWasDefault) txtRate.Text = StringHelper.ConvertToPersianDigits(newDefault.ToString("0.##", CultureInfo.InvariantCulture));
        }
        finally { _suppress = false; }

        Recalc();
    }

    // ═══════════════════════════ تاریخ‌ها و محاسبه ═══════════════════════════
    private DateTime _lastDepartYearCheck = DateTime.MinValue;

    private async Task OnDatesChangedAsync()
    {
        if (_suppress) return;

        var dep = DateHelper.ParsePersianDate(txtDepartDate.Text);
        var ret = DateHelper.ParsePersianDate(txtReturnDate.Text);
        if (dep is null || ret is null) return;

        // تعداد روز/شب به‌صورت پیش‌فرض از روی تاریخ‌ها (قابل ویرایش دستی)
        int days = Math.Max(1, (ret.Value.Date - dep.Value.Date).Days + 1);
        _suppress = true;
        try
        {
            numDays.Value = Math.Min(days, (int)numDays.Maximum);
            numNights.Value = Math.Min(Math.Max(0, days - 1), (int)numNights.Maximum);
        }
        finally { _suppress = false; }

        // اگر سال شمسی تاریخ رفت عوض شد، مبنا/درصد از تنظیمات سال جدید خوانده شود
        if (PersianYearOf(dep.Value) != (_lastDepartYearCheck == DateTime.MinValue ? -1 : PersianYearOf(_lastDepartYearCheck)))
        {
            _lastDepartYearCheck = dep.Value;
            await RefreshSalaryBaseAsync();
        }
        else Recalc();
    }

    private static decimal ParseRate(string text)
    {
        string clean = StringHelper.ConvertToEnglishDigits(text ?? "").Replace("%", "").Replace("٪", "").Replace("٫", ".").Replace(",", "").Replace("٬", "").Trim();
        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v) ? v : 0m;
    }

    private static string NormalizeTime(string? text, bool keepInvalid = false)
    {
        string raw = StringHelper.ConvertToEnglishDigits((text ?? "").Trim()).Replace("٫", ":").Replace(".", ":").Replace("/", ":");
        if (raw.Length == 0) return "";
        if (Regex.IsMatch(raw, @"^\d{1,2}$")) raw += ":00";
        var m = TimeRegex.Match(raw);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int h) && int.TryParse(m.Groups[2].Value, out int mi) && h <= 23 && mi <= 59)
            return $"{h:00}:{mi:00}";
        return keepInvalid ? (text ?? "").Trim() : "";
    }

    private static bool IsValidTimeOrEmpty(string? text) =>
        string.IsNullOrWhiteSpace(text) || NormalizeTime(text).Length > 0;

    /// <summary>استان را انتخاب می‌کند؛ اگر مقدار ذخیره‌شده در فهرست نبود (رکورد قدیمی) به فهرست اضافه می‌شود تا از دست نرود</summary>
    private void SetProvince(string? province)
    {
        province = (province ?? "").Trim();
        if (province.Length == 0) { cmbProvince.SelectedIndex = -1; return; }

        int idx = cmbProvince.FindStringExact(province);
        if (idx < 0) idx = cmbProvince.Items.Add(province);
        cmbProvince.SelectedIndex = idx;
    }

    private MissionRecord ReadInputs()
    {
        var emp = SelectedEmployee;
        return new MissionRecord
        {
            Id = _editingId,
            EmployeeId = emp?.Id ?? 0,
            EmployeeName = emp?.FullName ?? "",
            Province = (cmbProvince.SelectedItem?.ToString() ?? "").Trim(),
            Location = txtLocation.Text.Trim(),
            Subject = txtSubject.Text.Trim(),
            DepartDate = (DateHelper.ParsePersianDate(txtDepartDate.Text) ?? DateTime.Today).Date,
            DepartTime = NormalizeTime(txtDepartTime.Text),
            ReturnDate = (DateHelper.ParsePersianDate(txtReturnDate.Text) ?? DateTime.Today).Date,
            ReturnTime = NormalizeTime(txtReturnTime.Text),
            DaysCount = (int)numDays.Value,
            SalaryBase = NumberHelper.ParseFormatted(txtSalaryBase.Text),
            MissionRate = ParseRate(txtRate.Text),
            NightsCount = (int)numNights.Value,
            NightUnit = NumberHelper.ParseFormatted(txtNightUnit.Text),
            StayCount = (int)numStay.Value,
            StayUnit = NumberHelper.ParseFormatted(txtStayUnit.Text),
            LunchCount = (int)numLunch.Value,
            LunchUnit = NumberHelper.ParseFormatted(txtLunchUnit.Text),
            TravelCost = NumberHelper.ParseFormatted(txtTravel.Text),
            Notes = string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim()
        };
    }

    private void Recalc()
    {
        if (_suppress) return;
        var m = ReadInputs();
        m.Recalculate();
        txtDailyAmount.Text = NumberHelper.FormatThousands(m.DailyAmount, usePersianDigits: true);
        txtDaysTotal.Text = NumberHelper.FormatThousands(m.DaysTotal, usePersianDigits: true);
        txtGrand.Text = NumberHelper.FormatThousands(m.GrandTotal, usePersianDigits: true);
    }

    /// <summary>رکورد را از فرم می‌خواند و اعتبارسنجی می‌کند؛ در صورت خطا پیام می‌دهد و null برمی‌گرداند.</summary>
    private MissionRecord? BuildValidatedRecord()
    {
        var emp = SelectedEmployee;
        if (emp is null && _editingId == 0) { ModernMessageBox.ShowWarning("لطفاً یک کارمند را انتخاب کنید."); return null; }

        var dep = DateHelper.ParsePersianDate(txtDepartDate.Text);
        var ret = DateHelper.ParsePersianDate(txtReturnDate.Text);
        if (dep is null || ret is null) { ModernMessageBox.ShowWarning("تاریخ رفت یا برگشت معتبر نیست."); return null; }
        if (ret.Value.Date < dep.Value.Date) { ModernMessageBox.ShowWarning("تاریخ برگشت نمی‌تواند قبل از تاریخ رفت باشد."); return null; }

        if (!IsValidTimeOrEmpty(txtDepartTime.Text) || !IsValidTimeOrEmpty(txtReturnTime.Text))
        { ModernMessageBox.ShowWarning("ساعت باید به شکل ساعت:دقیقه باشد؛ مثلاً 06:00 یا 16:30."); return null; }

        string dt = NormalizeTime(txtDepartTime.Text), rt = NormalizeTime(txtReturnTime.Text);
        if (ret.Value.Date == dep.Value.Date && dt.Length > 0 && rt.Length > 0 && string.CompareOrdinal(rt, dt) <= 0)
        { ModernMessageBox.ShowWarning("در ماموریت یک‌روزه، ساعت برگشت باید بعد از ساعت رفت باشد."); return null; }

        var m = ReadInputs();
        if (m.DaysCount < 1) { ModernMessageBox.ShowWarning("تعداد روز ماموریت باید حداقل ۱ باشد."); return null; }
        if (string.IsNullOrWhiteSpace(m.Subject)) { ModernMessageBox.ShowWarning("موضوع ماموریت را وارد کنید."); return null; }
        if (string.IsNullOrWhiteSpace(m.Province) && string.IsNullOrWhiteSpace(m.Location)) { ModernMessageBox.ShowWarning("استان یا محل ماموریت را وارد کنید."); return null; }
        if (m.MissionRate < 0 || m.MissionRate > 100) { ModernMessageBox.ShowWarning("درصد حق ماموریت باید بین ۰ تا ۱۰۰ باشد."); return null; }

        if (m.SalaryBase <= 0 &&
            !ModernMessageBox.AskConfirm("مبنای حقوق صفر است (اجزای مشمول ماموریت در تنظیمات حقوق تیک نخورده یا حقوق کارمند ثبت نشده).\nبا این حال ادامه می‌دهید؟", "تایید"))
            return null;

        m.Recalculate();
        return m;
    }

    // ═══════════════════════════ ذخیره / جدید / حذف ═══════════════════════════
    private async Task SaveSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;
        try
        {
            var rec = BuildValidatedRecord();
            if (rec is null) return;
            if (rec.Id == 0) DateStampHelper.StampCreated(rec);

            bool ok = await AsyncHelper.RunWithLoading(this, async () => { await _repo.SaveAsync(rec); }, "در حال ذخیره...");
            if (ok && !IsDisposed)
            {
                ToastHelper.Show("ماموریت ذخیره شد.", isSuccess: true);
                await ReloadListAsync();
                await ClearFormAsync();
            }
        }
        catch (Exception ex) { LogHelper.LogException(ex, "MissionForm.SaveSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError(ex.Message); }
        finally { try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "MissionForm"); } }
    }

    private async Task DeleteSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;
        try
        {
            if (dgv.CurrentRow?.DataBoundItem is not MissionRecord rec) { ModernMessageBox.ShowWarning("لطفاً یک ماموریت را از لیست انتخاب کنید."); return; }
            if (!ModernMessageBox.AskConfirm($"ماموریت «{rec.EmployeeName}» به «{rec.Location}» حذف شود؟", "تایید حذف")) return;

            bool ok = await AsyncHelper.RunWithLoading(this, async () => { await _repo.DeleteAsync(rec.Id); }, "در حال حذف...");
            if (ok && !IsDisposed)
            {
                ToastHelper.Show("ماموریت حذف شد.", isSuccess: true);
                if (_editingId == rec.Id) await ClearFormAsync();
                await ReloadListAsync();
            }
        }
        catch (Exception ex) { LogHelper.LogException(ex, "MissionForm.DeleteSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError(ex.Message); }
        finally { try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "MissionForm"); } }
    }

    private void ResetFormFields()
    {
        _suppress = true;
        try
        {
            _editingId = 0;
            btnSave.Text = "ذخیره";
            cmbProvince.SelectedIndex = -1;
            txtLocation.Clear(); txtSubject.Clear(); txtNotes.Clear();
            txtDepartDate.Text = DateHelper.ToPersianDate(DateTime.Today);
            txtReturnDate.Text = DateHelper.ToPersianDate(DateTime.Today);
            txtDepartTime.Text = "08:00"; txtReturnTime.Text = "17:00";
            numDays.Value = 1; numNights.Value = 0; numStay.Value = 0; numLunch.Value = 0;
            txtNightUnit.Text = "0"; txtStayUnit.Text = "0"; txtLunchUnit.Text = "0"; txtTravel.Text = "0";
            _lastDepartYearCheck = DateTime.Today;
        }
        finally { _suppress = false; }
    }

    private async Task ClearFormAsync()
    {
        ResetFormFields();
        await RefreshSalaryBaseAsync();
    }

    private void LoadRecordIntoForm(MissionRecord rec)
    {
        _suppress = true;
        try
        {
            _editingId = rec.Id;
            btnSave.Text = "ذخیره تغییرات";

            // ✅ اصلاح خطای CS0023
            employeePicker.SelectById(rec.EmployeeId);
            if (employeePicker.SelectedId != rec.EmployeeId)
            {
                ModernMessageBox.ShowWarning("این کارمند در فهرست کارمندان فعال نیست؛ کارمند فعلی فرم تغییر نکرد.");
            }

            SetProvince(rec.Province);
            txtLocation.Text = rec.Location;
            txtSubject.Text = rec.Subject;
            txtNotes.Text = rec.Notes ?? "";

            txtDepartDate.Text = DateHelper.ToPersianDate(rec.DepartDate);
            txtReturnDate.Text = DateHelper.ToPersianDate(rec.ReturnDate);
            txtDepartTime.Text = rec.DepartTime;
            txtReturnTime.Text = rec.ReturnTime;
            _lastDepartYearCheck = rec.DepartDate;

            numDays.Value = Math.Min(Math.Max(rec.DaysCount, (int)numDays.Minimum), (int)numDays.Maximum);
            numNights.Value = Math.Min(rec.NightsCount, (int)numNights.Maximum);
            numStay.Value = Math.Min(rec.StayCount, (int)numStay.Maximum);
            numLunch.Value = Math.Min(rec.LunchCount, (int)numLunch.Maximum);

            // مبنا و درصد همان مقداری که موقع ثبت ذخیره شده (اسنپ‌شات) نمایش داده می‌شود
            txtSalaryBase.Text = NumberHelper.FormatThousands(rec.SalaryBase, usePersianDigits: true);
            txtRate.Text = StringHelper.ConvertToPersianDigits(rec.MissionRate.ToString("0.##", CultureInfo.InvariantCulture));
            _defaultRate = rec.MissionRate;

            txtNightUnit.Text = NumberHelper.FormatThousands(rec.NightUnit);
            txtStayUnit.Text = NumberHelper.FormatThousands(rec.StayUnit);
            txtLunchUnit.Text = NumberHelper.FormatThousands(rec.LunchUnit);
            txtTravel.Text = NumberHelper.FormatThousands(rec.TravelCost);

            lblInfo.ForeColor = ModernTheme.TextMuted;
            lblInfo.Text = "در حال ویرایش ماموریت ثبت‌شده (مبنا و درصد همان زمان ثبت). با تغییر کارمند، مبنا دوباره محاسبه می‌شود.";
        }
        finally { _suppress = false; }
        Recalc();
    }

    // ═══════════════════════════ چاپ و اکسل ═══════════════════════════
    private MissionPrintItem ToPrintItem(MissionRecord rec)
    {
        var emp = _employees.FirstOrDefault(e => e.Id == rec.EmployeeId);
        Company? company = null;
        if (emp?.CompanyId is not null) company = _companies.FirstOrDefault(c => c.Id == emp.CompanyId.Value);
        company ??= _companies.FirstOrDefault(c => c.Logo is not null && c.Logo.Length > 0) ?? _companies.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(rec.EmployeeName) && emp is not null) rec.EmployeeName = emp.FullName;

        return new MissionPrintItem
        {
            Record = rec,
            CompanyName = company?.Name ?? "شرکت",
            Logo = company is not null && _logos.TryGetValue(company.Id, out var img) ? img : null
        };
    }

    private void PrintCurrent()
    {
        var rec = BuildValidatedRecord();
        if (rec is null) return;
        new MissionPrinter(new List<MissionPrintItem> { ToPrintItem(rec) }).ShowPreview();
    }

    private List<MissionRecord> GetSelectedRecords() =>
        dgv.SelectedRows.Cast<DataGridViewRow>()
           .OrderBy(r => r.Index)
           .Select(r => r.DataBoundItem as MissionRecord)
           .Where(r => r is not null).Select(r => r!).ToList();

    private void PrintSelected()
    {
        var recs = GetSelectedRecords();
        if (recs.Count == 0) { ModernMessageBox.ShowWarning("ابتدا یک یا چند ماموریت را از لیست انتخاب کنید."); return; }
        new MissionPrinter(recs.Select(ToPrintItem).ToList()).ShowPreview();
    }

    private void ExportExcel()
    {
        List<MissionPrintItem> items;
        var recs = GetSelectedRecords();
        if (recs.Count > 0) items = recs.Select(ToPrintItem).ToList();
        else
        {
            var rec = BuildValidatedRecord();
            if (rec is null) return;
            items = new List<MissionPrintItem> { ToPrintItem(rec) };
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = $"ماموریت_{StringHelper.ConvertToEnglishDigits(DateHelper.ToPersianDate(DateTime.Today)).Replace("/", "-")}.xlsx"
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            MissionPrinter.ExportExcel(items, sfd.FileName);
            ToastHelper.Show("فایل اکسل ساخته شد.", isSuccess: true);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "MissionForm.ExportExcel");
            ModernMessageBox.ShowError("خطا در ساخت فایل اکسل:\n" + ex.Message);
        }
    }
}
