using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using Smp.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class SalarySettingsForm : Form
{
    private readonly string _connectionString;

    private ComboBox cmbYear = null!;
    private Button btnReload = null!, btnSave = null!;
    private ModernTabHost tabControl = null!;

    private TextBox txtMinimumWage = null!, txtHousing = null!, txtFood = null!, txtMarriage = null!, txtChildRate = null!;
    private TextBox txtInsuranceEmp = null!, txtInsuranceEmpr = null!;
    // 🔴 فیلد txtTaxExemption به طور کامل حذف شد
    private NumericUpDown numAbsenceMultiplier = null!;
    private TextBox txtNormalDayHours = null!, txtHolidayDayHours = null!, txtMonthlyWorkingHours = null!;

    private DataGridView dgvTaxBrackets = null!;
    private Button btnAddBracket = null!, btnDeleteBracket = null!;

    // ✅ تب حقوق روزمزدها: نرخ روزانه‌ی طبقه‌ها + ضریب اضافه‌کار (ساعت موظفی‌ها هم در همین تب هستند)
    private DataGridView dgvDailyRates = null!;
    private NumericUpDown numDailyOvertimeMultiplier = null!;
    private Button btnCopyPrevDailyRates = null!;
    private Panel _pageDaily = null!;

    private CheckBox chkTaxBase = null!, chkTaxYears = null!, chkTaxAttr = null!, chkTaxResp = null!;
    private CheckBox chkTaxMgmt = null!, chkTaxTech = null!, chkTaxHouse = null!, chkTaxGroc = null!;
    private CheckBox chkTaxChild = null!, chkTaxMarr = null!, chkTaxTrans = null!, chkTaxOther = null!;

    private CheckBox chkInsBase = null!, chkInsYears = null!, chkInsAttr = null!, chkInsResp = null!;
    private CheckBox chkInsMgmt = null!, chkInsTech = null!, chkInsHouse = null!, chkInsGroc = null!;
    private CheckBox chkInsChild = null!, chkInsMarr = null!, chkInsTrans = null!, chkInsOther = null!;

    private CheckBox chkOvtBase = null!, chkOvtYears = null!, chkOvtAttr = null!, chkOvtResp = null!;
    private CheckBox chkOvtMgmt = null!, chkOvtTech = null!, chkOvtHouse = null!, chkOvtGroc = null!;
    private CheckBox chkOvtChild = null!, chkOvtMarr = null!, chkOvtTrans = null!, chkOvtOther = null!;

    // ✅ مشمولین عیدی و سنوات (ترتیب = SalaryComponents.All)
    private CheckBox[] _eidiChecks = Array.Empty<CheckBox>();
    private CheckBox[] _sanavatChecks = Array.Empty<CheckBox>();

    // ✅ مشمولین ماموریت + درصد حق ماموریت
    private CheckBox[] _missionChecks = Array.Empty<CheckBox>();
    private TextBox txtMissionRate = null!;

    private bool _isInitializing = true;

    // ── پیگیری تغییرات ذخیره‌نشده ──
    private bool _dirty, _loading, _suppressYear;
    private int _loadedYear;
    private Label lblDirty = null!;

    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public SalarySettingsForm(string connectionString)
    {
        _connectionString = connectionString;

        BuildUI();
        this.ApplyStandardHeader("تنظیمات حقوق سالانه", new Size(1230, 800), isDialog: false);
        ShowInTaskbar = false;

        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
        LoadDefaults();

        Shown += async (s, e) =>
        {
            _isInitializing = false;
            HookDirty(this);
            await LoadAllAsync();
        };
    }

    // ───────────────────────── تغییرات ذخیره‌نشده ─────────────────────────
    private void HookDirty(Control c)
    {
        switch (c)
        {
            case DataGridView g:
                g.CellValueChanged += (s, e) => MarkDirty();
                g.RowsAdded += (s, e) => MarkDirty();
                g.RowsRemoved += (s, e) => MarkDirty();
                break;
            case CheckBox cb: cb.CheckedChanged += (s, e) => MarkDirty(); break;
            case NumericUpDown n: n.ValueChanged += (s, e) => MarkDirty(); break;
            case TextBox t: t.TextChanged += (s, e) => MarkDirty(); break;
        }
        foreach (Control ch in c.Controls) HookDirty(ch);
    }

    private void MarkDirty()
    {
        if (_isInitializing || _loading || _dirty) return;
        _dirty = true;
        UpdateDirtyUi();
    }

    private void ClearDirty()
    {
        _dirty = false;
        UpdateDirtyUi();
    }

    private void UpdateDirtyUi()
    {
        if (IsDisposed || lblDirty is null) return;
        lblDirty.Text = _dirty ? "● تغییرات ذخیره‌نشده دارید" : "";
    }

    private bool ConfirmDiscard()
        => !_dirty || ModernMessageBox.AskConfirm("تغییرات ذخیره‌نشده از بین می‌رود. ادامه می‌دهید؟");

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !ConfirmDiscard()) { e.Cancel = true; return; }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _loadLock.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "SalarySettingsForm"); }
        }
        base.Dispose(disposing);
    }

    private void LoadDefaults()
    {
        try
        {
            var pc = new PersianCalendar();
            int currentYear = pc.GetYear(DateTime.Now);
            if (cmbYear.Items.Contains(currentYear)) cmbYear.SelectedItem = currentYear;
            else if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;
        }
        catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "SalarySettingsForm"); }
    }

    private const int LblW = 240;   // عرض لیبل در کارت‌های این فرم (لیبل‌ها طولانی‌اند)

    private void BuildUI()
    {
        // ── نوار بالا: سال مالی ──
        cmbYear = ModernUI.Combo();
        for (int i = 1400; i <= 1420; i++) cmbYear.Items.Add(i);
        cmbYear.SelectedIndexChanged += async (s, e) =>
        {
            if (_isInitializing || _suppressYear) return;
            if (!ConfirmDiscard())
            {
                // انصراف: سال را به سال بارگذاری‌شده برمی‌گردانیم
                _suppressYear = true;
                if (_loadedYear > 0 && cmbYear.Items.Contains(_loadedYear)) cmbYear.SelectedItem = _loadedYear;
                _suppressYear = false;
                return;
            }
            await LoadAllAsync();
        };

        btnReload = ModernButton.Soft("بارگذاری مجدد", ModernTheme.Primary, 140, 38);
        btnReload.Click += async (s, e) => { if (ConfirmDiscard()) await LoadAllAsync(); };

        // فرم Mirror شده است: با LeftToRight اولین کنترل در راست‌ترین جا می‌نشیند
        // (قبلاً لیبل «سال مالی» در چپ‌ترین نقطه و دکمه در راست بود)
        var topBar = ModernReport.CreateFilterBar();
        topBar.Dock = DockStyle.Fill;
        topBar.FlowDirection = FlowDirection.LeftToRight;
        topBar.WrapContents = false;
        topBar.Padding = new Padding(16, 12, 16, 8);   // قبلاً ارتفاع ردیف (۵۲) از محتوای نوار کمتر بود و پایین کمبو زیر نوار تب‌ها بریده می‌شد
        ModernReport.AddFilter(topBar, "سال مالی", cmbYear, 130);
        btnReload.Margin = new Padding(8, 4, 4, 0);
        topBar.Controls.Add(btnReload);

        tabControl = new ModernTabHost { Dock = DockStyle.Fill };
        int rh = ModernUI.RowHeight;

        // ==========================================
        // تب ۱: تنظیمات عمومی
        // ==========================================
        txtMinimumWage = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtMinimumWage);
        txtHousing = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtHousing);
        txtFood = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtFood);
        txtMarriage = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtMarriage);
        txtChildRate = new TextBox(); SmartInputHelper.EnableSmartPercentageTextBox(txtChildRate);
        txtInsuranceEmp = new TextBox(); SmartInputHelper.EnableSmartPercentageTextBox(txtInsuranceEmp);
        txtInsuranceEmpr = new TextBox(); SmartInputHelper.EnableSmartPercentageTextBox(txtInsuranceEmpr);

        numAbsenceMultiplier = new NumericUpDown { Minimum = 1, Maximum = 10, DecimalPlaces = 1, Value = 1, TextAlign = HorizontalAlignment.Center };

        txtNormalDayHours = new TextBox();
        txtHolidayDayHours = new TextBox();
        txtMonthlyWorkingHours = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtMonthlyWorkingHours);

        var pageGeneral = tabControl.AddPage("تنظیمات عمومی");
        var generalPage = new ModernPage();
        var cardGeneral = new ModernCard("تنظیمات عمومی حقوق", ModernTheme.Primary, 2, rh, rh, rh, rh, rh);
        cardGeneral.Body.Controls.Add(ModernUI.Row("حداقل حقوق ماهانه (ریال)", txtMinimumWage, LblW), 0, 0);
        cardGeneral.Body.Controls.Add(ModernUI.Row("حق مسکن (ریال)", txtHousing, LblW), 1, 0);
        cardGeneral.Body.Controls.Add(ModernUI.Row("حق بن خواربار (ریال)", txtFood, LblW), 0, 1);
        cardGeneral.Body.Controls.Add(ModernUI.Row("حق تاهل (ریال)", txtMarriage, LblW), 1, 1);
        cardGeneral.Body.Controls.Add(ModernUI.Row("نرخ حق اولاد (%)", txtChildRate, LblW), 0, 2);
        cardGeneral.Body.Controls.Add(ModernUI.Row("بیمه سهم کارمند (%)", txtInsuranceEmp, LblW), 1, 2);
        cardGeneral.Body.Controls.Add(ModernUI.Row("بیمه سهم کارفرما (%)", txtInsuranceEmpr, LblW), 0, 3);
        cardGeneral.Body.Controls.Add(ModernUI.Row("ضریب کسر کار (مثلاً ۲ برای ۲ برابر)", numAbsenceMultiplier, LblW), 1, 3);
        var monthlyRow = ModernUI.Row("ساعت کارکرد ماهانه (مبنای اضافه‌کار)", txtMonthlyWorkingHours, LblW);
        cardGeneral.Body.Controls.Add(monthlyRow, 0, 4);
        cardGeneral.Body.SetColumnSpan(monthlyRow, 2);
        generalPage.AddCard(cardGeneral);
        pageGeneral.Controls.Add(generalPage);

        // ==========================================
        // تب ۲: حقوق روزمزدها
        // ==========================================
        _pageDaily = tabControl.AddPage("حقوق روزمزدها");
        numDailyOvertimeMultiplier = new NumericUpDown { Minimum = 1, Maximum = 3, DecimalPlaces = 2, Increment = 0.1m, Value = 1.4m, TextAlign = HorizontalAlignment.Center };

        var cardDailyTop = new ModernCard("پارامترهای روزمزد", ModernTheme.Teal, 2, rh, rh);
        cardDailyTop.Body.Controls.Add(ModernUI.Row("ساعت موظفی روز عادی", txtNormalDayHours, LblW), 0, 0);
        cardDailyTop.Body.Controls.Add(ModernUI.Row("ساعت موظفی روز تعطیل", txtHolidayDayHours, LblW), 1, 0);
        cardDailyTop.Body.Controls.Add(ModernUI.Row("ضریب اضافه‌کار روزمزد (مثلاً ۱٫۴)", numDailyOvertimeMultiplier, LblW), 0, 1);
        cardDailyTop.Dock = DockStyle.Fill;

        var btnAddDaily = ModernButton.Soft("افزودن طبقه", ModernTheme.Success, 140, 38);
        btnAddDaily.Click += (s, e) => { if (dgvDailyRates.Columns.Count > 0) dgvDailyRates.Rows.Add(); };
        var btnDeleteDaily = ModernButton.Soft("حذف طبقه", ModernTheme.Danger, 130, 38);
        btnDeleteDaily.Click += (s, e) => { if (dgvDailyRates.CurrentRow is not null && !dgvDailyRates.CurrentRow.IsNewRow) dgvDailyRates.Rows.Remove(dgvDailyRates.CurrentRow); };
        btnCopyPrevDailyRates = ModernButton.Soft("کپی نرخ‌ها از سال قبل", ModernTheme.Primary, 200, 38);
        btnCopyPrevDailyRates.Click += async (s, e) => await CopyDailyRatesFromPreviousYearAsync();
        var dailyBtns = ModernForm.CreateButtonBar(btnAddDaily, btnDeleteDaily, btnCopyPrevDailyRates);
        dailyBtns.Padding = new Padding(0, 4, 0, 4);

        dgvDailyRates = ModernGrid.Create();
        dgvDailyRates.AllowUserToAddRows = true;
        dgvDailyRates.AllowUserToDeleteRows = true;
        dgvDailyRates.EditMode = DataGridViewEditMode.EditOnEnter;
        dgvDailyRates.SelectionMode = DataGridViewSelectionMode.CellSelect;
        dgvDailyRates.FillColumns();
        dgvDailyRates.ReadOnly = false;
        dgvDailyRates.Columns.Add(new DataGridViewTextBoxColumn { Name = "DailyCategory", HeaderText = "طبقه‌ی کارگری", FillWeight = 50 });
        dgvDailyRates.Columns.Add(new DataGridViewTextBoxColumn { Name = "DailyAmount", HeaderText = "دستمزد روزانه (ریال)", FillWeight = 50 });
        SmartInputHelper.EnableSmartGridInput(dgvDailyRates, amountColumns: new[] { "DailyAmount" }, percentageColumns: Array.Empty<string>());

        var layoutDaily = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(20, 14, 20, 10), BackColor = ModernTheme.Background };
        layoutDaily.RowStyles.Add(new RowStyle(SizeType.Absolute, cardDailyTop.Height + 8));
        layoutDaily.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        layoutDaily.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layoutDaily.Controls.Add(cardDailyTop, 0, 0);
        layoutDaily.Controls.Add(dailyBtns, 0, 1);
        layoutDaily.Controls.Add(ModernReport.WrapGrid(dgvDailyRates), 0, 2);
        TidyEditableGrid(dgvDailyRates);
        dgvDailyRates.Columns["DailyCategory"].HeaderText = "طبقه‌ی کارگری";
        dgvDailyRates.Columns["DailyAmount"].HeaderText = "دستمزد روزانه (ریال)";
        _pageDaily.Controls.Add(layoutDaily);

        // ==========================================
        // تب ۳: پله‌های مالیات
        // ==========================================
        var pageBrackets = tabControl.AddPage("پله‌های مالیات");
        btnAddBracket = ModernButton.Soft("افزودن پله", ModernTheme.Success, 130, 38);
        btnAddBracket.Click += (s, e) => { if (dgvTaxBrackets.Columns.Count > 0) dgvTaxBrackets.Rows.Add(); };
        btnDeleteBracket = ModernButton.Soft("حذف پله", ModernTheme.Danger, 120, 38);
        btnDeleteBracket.Click += (s, e) => { if (dgvTaxBrackets.CurrentRow is not null && !dgvTaxBrackets.CurrentRow.IsNewRow) dgvTaxBrackets.Rows.Remove(dgvTaxBrackets.CurrentRow); };
        var bracketBtns = ModernForm.CreateButtonBar(btnAddBracket, btnDeleteBracket);
        bracketBtns.Padding = new Padding(0, 4, 0, 4);

        dgvTaxBrackets = ModernGrid.Create();
        dgvTaxBrackets.AllowUserToAddRows = true;
        dgvTaxBrackets.AllowUserToDeleteRows = true;
        dgvTaxBrackets.EditMode = DataGridViewEditMode.EditOnEnter;
        dgvTaxBrackets.SelectionMode = DataGridViewSelectionMode.CellSelect;
        dgvTaxBrackets.FillColumns();
        dgvTaxBrackets.ReadOnly = false;
        dgvTaxBrackets.Columns.Add(new DataGridViewTextBoxColumn { Name = "FromAmount", HeaderText = "از مبلغ (ریال)", FillWeight = 30 });
        dgvTaxBrackets.Columns.Add(new DataGridViewTextBoxColumn { Name = "ToAmount", HeaderText = "تا مبلغ (ریال) - خالی = بدون سقف", FillWeight = 35 });
        dgvTaxBrackets.Columns.Add(new DataGridViewTextBoxColumn { Name = "TaxRate", HeaderText = "نرخ (مثلاً 10 یا 0.10)", FillWeight = 35 });
        SmartInputHelper.EnableSmartGridInput(dgvTaxBrackets, amountColumns: new[] { "FromAmount", "ToAmount" }, percentageColumns: new[] { "TaxRate" });

        var layoutBrackets = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(20, 14, 20, 10), BackColor = ModernTheme.Background };
        layoutBrackets.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        layoutBrackets.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layoutBrackets.Controls.Add(bracketBtns, 0, 0);
        layoutBrackets.Controls.Add(ModernReport.WrapGrid(dgvTaxBrackets), 0, 1);
        TidyEditableGrid(dgvTaxBrackets);
        // WrapGrid هدرها را با ترجمه‌ی پیش‌فرض بازنویسی می‌کند؛ هدرهای دقیق را بعد از آن می‌گذاریم
        dgvTaxBrackets.Columns["FromAmount"].HeaderText = "از مبلغ (ریال)";
        dgvTaxBrackets.Columns["ToAmount"].HeaderText = "تا مبلغ (ریال) — خالی = بدون سقف";
        dgvTaxBrackets.Columns["TaxRate"].HeaderText = "نرخ مالیات (٪)";
        pageBrackets.Controls.Add(layoutBrackets);

        // ==========================================
        // تب ۴: مشمولین مالیات
        // ==========================================
        chkTaxBase = CreateCheckBox("حقوق پایه"); chkTaxYears = CreateCheckBox("پایه سنوات"); chkTaxAttr = CreateCheckBox("حق جذب");
        chkTaxResp = CreateCheckBox("حق مسئولیت"); chkTaxMgmt = CreateCheckBox("حق مدیریت"); chkTaxTech = CreateCheckBox("حق فنی");
        chkTaxHouse = CreateCheckBox("حق مسکن"); chkTaxGroc = CreateCheckBox("حق خواربار"); chkTaxChild = CreateCheckBox("حق اولاد");
        chkTaxMarr = CreateCheckBox("حق تاهل"); chkTaxTrans = CreateCheckBox("حق ایاب و ذهاب"); chkTaxOther = CreateCheckBox("سایر مزایا");
        AddCheckPage("مشمولین مالیات", "مواردی که در مبنای محاسبه‌ی مالیات لحاظ می‌شوند", ModernTheme.Danger,
            chkTaxBase, chkTaxYears, chkTaxAttr, chkTaxResp, chkTaxMgmt, chkTaxTech, chkTaxHouse, chkTaxGroc, chkTaxChild, chkTaxMarr, chkTaxTrans, chkTaxOther);

        // ==========================================
        // تب ۵: مشمولین بیمه
        // ==========================================
        chkInsBase = CreateCheckBox("حقوق پایه"); chkInsYears = CreateCheckBox("پایه سنوات"); chkInsAttr = CreateCheckBox("حق جذب");
        chkInsResp = CreateCheckBox("حق مسئولیت"); chkInsMgmt = CreateCheckBox("حق مدیریت"); chkInsTech = CreateCheckBox("حق فنی");
        chkInsHouse = CreateCheckBox("حق مسکن"); chkInsGroc = CreateCheckBox("حق خواربار"); chkInsChild = CreateCheckBox("حق اولاد");
        chkInsMarr = CreateCheckBox("حق تاهل"); chkInsTrans = CreateCheckBox("حق ایاب و ذهاب"); chkInsOther = CreateCheckBox("سایر مزایا");
        AddCheckPage("مشمولین بیمه", "مواردی که در مبنای محاسبه‌ی بیمه لحاظ می‌شوند", ModernTheme.Teal,
            chkInsBase, chkInsYears, chkInsAttr, chkInsResp, chkInsMgmt, chkInsTech, chkInsHouse, chkInsGroc, chkInsChild, chkInsMarr, chkInsTrans, chkInsOther);

        // ==========================================
        // تب ۶: مشمولین اضافه کاری
        // ==========================================
        chkOvtBase = CreateCheckBox("حقوق پایه"); chkOvtYears = CreateCheckBox("پایه سنوات"); chkOvtAttr = CreateCheckBox("حق جذب");
        chkOvtResp = CreateCheckBox("حق مسئولیت"); chkOvtMgmt = CreateCheckBox("حق مدیریت"); chkOvtTech = CreateCheckBox("حق فنی");
        chkOvtHouse = CreateCheckBox("حق مسکن"); chkOvtGroc = CreateCheckBox("حق خواربار"); chkOvtChild = CreateCheckBox("حق اولاد");
        chkOvtMarr = CreateCheckBox("حق تاهل"); chkOvtTrans = CreateCheckBox("حق ایاب و ذهاب"); chkOvtOther = CreateCheckBox("سایر مزایا");
        AddCheckPage("مشمولین اضافه کاری", "مواردی که در مبنای محاسبه‌ی اضافه‌کار لحاظ می‌شوند", ModernTheme.Warning,
            chkOvtBase, chkOvtYears, chkOvtAttr, chkOvtResp, chkOvtMgmt, chkOvtTech, chkOvtHouse, chkOvtGroc, chkOvtChild, chkOvtMarr, chkOvtTrans, chkOvtOther);

        // ==========================================
        // تب ۷: مشمولین عیدی و سنوات
        // ==========================================
        var pageEidi = tabControl.AddPage("مشمولین عیدی و سنوات");
        var eidiPage = new ModernPage();
        eidiPage.AddCard(BuildHint("هر مورد که تیک داشته باشد در مبنای محاسبه‌ی عیدی / سنوات لحاظ می‌شود (مجموع ماهانه‌ی کامل)."), 6);
        eidiPage.AddCard(BuildComponentGroup("مبنای محاسبه‌ی عیدی", ModernTheme.Success, out _eidiChecks));
        eidiPage.AddCard(BuildComponentGroup("مبنای محاسبه‌ی سنوات", ModernTheme.Purple, out _sanavatChecks));
        pageEidi.Controls.Add(eidiPage);

        // ==========================================
        // تب ۸: مشمولین ماموریت
        // ==========================================
        var pageMission = tabControl.AddPage("مشمولین ماموریت");
        var missionPage = new ModernPage();
        txtMissionRate = new TextBox { TextAlign = HorizontalAlignment.Center };
        missionPage.AddCard(BuildHint("مجموع اجزای تیک‌خورده‌ی حقوق ماهانه، مبنای حق ماموریت است.\nحق ماموریت هر روز = مبنا × درصد ماموریت؛ جمع = مبلغ هر روز × تعداد روزهای ماموریت."), 6);
        var cardRate = new ModernCard("درصد حق ماموریت", ModernTheme.Orange, 1, rh);
        cardRate.Body.Controls.Add(ModernUI.Row("درصد حق ماموریت (٪ از حقوق مشمول برای هر روز)", txtMissionRate, 330), 0, 0);
        missionPage.AddCard(cardRate);
        missionPage.AddCard(BuildComponentGroup("مبنای محاسبه‌ی حق ماموریت", ModernTheme.Orange, out _missionChecks));
        pageMission.Controls.Add(missionPage);

        // ── دکمه ذخیره ──
        btnSave = ModernButton.Success("ذخیره تنظیمات", 210, 44);
        btnSave.Click += BtnSave_Click;
        lblDirty = ModernUI.Caption("", 10f, true, ModernTheme.Warning);
        lblDirty.AutoSize = true;
        lblDirty.Margin = new Padding(14, 14, 0, 0);

        var bar = ModernForm.CreateButtonBar(btnSave, lblDirty);
        bar.FlowDirection = FlowDirection.LeftToRight;   // دکمه‌ی ذخیره در راست (شروع خواندن)، وضعیت کنارش

        // ── چیدمان صفحه ──
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = ModernTheme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        root.Controls.Add(topBar, 0, 0);
        root.Controls.Add(tabControl, 0, 1);
        root.Controls.Add(bar, 0, 2);
        this.Controls.Add(root);
    }

    /// <summary>
    /// جدول‌های قابل‌ویرایش: همه‌ی ستون‌ها و هدرها وسط‌چین. قبلاً FillColumns مقدارها را MiddleRight می‌کرد که در فرم Mirror شده
    /// سمت چپ سلول می‌نشست و زیر هدرِ وسط‌چین نبود (ستون‌ها نامنظم دیده می‌شدند).
    /// </summary>
    private static void TidyEditableGrid(DataGridView g)
    {
        g.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        foreach (DataGridViewColumn c in g.Columns)
        {
            c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            c.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            c.SortMode = DataGridViewColumnSortMode.NotSortable;
        }
    }

    /// <summary>صفحه‌ای با یک کارت ۳ ستونه از چک‌باکس‌ها (۱۲ مورد)</summary>
    private void AddCheckPage(string tabTitle, string cardTitle, Color accent, params CheckBox[] checks)
    {
        var page = tabControl.AddPage(tabTitle);
        var mp = new ModernPage();
        int rh = ModernUI.RowHeight;
        int rows = (checks.Length + 2) / 3;
        var card = new ModernCard(cardTitle, accent, 3, Enumerable.Repeat(rh, rows).ToArray());
        for (int i = 0; i < checks.Length; i++)
            card.Body.Controls.Add(ModernUI.CenterInCell(checks[i], 12), i % 3, i / 3);
        mp.AddCard(card);
        page.Controls.Add(mp);
    }

    private static Control BuildHint(string text)
    {
        var lbl = ModernUI.Caption(text, 10f, false, ModernTheme.TextMuted);
        lbl.AutoSize = false;
        lbl.Height = text.Contains('\n') ? 48 : 28;
        lbl.Padding = new Padding(0, 0, 6, 0);
        return lbl;
    }

    /// <summary>کارت با چک‌باکس برای همه‌ی اجزای حقوق (۳ ستون)</summary>
    private ModernCard BuildComponentGroup(string title, Color accent, out CheckBox[] checks)
    {
        int rh = ModernUI.RowHeight;
        int rows = (SalaryComponents.All.Length + 2) / 3;
        var card = new ModernCard(title, accent, 3, Enumerable.Repeat(rh, rows).ToArray());

        var list = new CheckBox[SalaryComponents.All.Length];
        for (int i = 0; i < list.Length; i++)
        {
            list[i] = CreateCheckBox(SalaryComponents.All[i].Title);
            card.Body.Controls.Add(ModernUI.CenterInCell(list[i], 12), i % 3, i / 3);
        }
        checks = list;
        return card;
    }

    private static void ApplyChecks(CheckBox[] boxes, string? csv)
    {
        var keys = SalaryComponents.Parse(csv);
        for (int i = 0; i < boxes.Length; i++)
            boxes[i].Checked = keys.Contains(SalaryComponents.All[i].Key);
    }

    private static string CollectChecks(CheckBox[] boxes)
    {
        var keys = new List<string>();
        for (int i = 0; i < boxes.Length; i++)
            if (boxes[i].Checked) keys.Add(SalaryComponents.All[i].Key);
        return SalaryComponents.Join(keys);
    }

    private CheckBox CreateCheckBox(string text)
    {
        return ModernUI.Check(text);
    }

    private static string FormatToHoursMinutes(decimal hours)
    {
        int h = (int)hours;
        int m = (int)Math.Round((hours - h) * 60);
        if (m == 60) { m = 0; h++; }
        return $"{h}:{m:00}";
    }

    private static decimal ParseHoursMinutes(string text)
    {
        return TimeHelper.ParseTimeString(text);
    }

    private async Task LoadAllAsync()
    {
        if (cmbYear.SelectedItem is null || IsDisposed) return;

        try
        {
            await _loadLock.WaitAsync();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            if (IsDisposed || cmbYear.SelectedItem is null) return;
            int year = Convert.ToInt32(cmbYear.SelectedItem);

            _loading = true;
            bool loaded = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new SalarySettingsRepository(_connectionString);
                var settings = await repo.GetByYearAsync(year);

                if (settings is not null)
                {
                    txtMinimumWage.Text = NumberHelper.FormatThousands(settings.MinimumWage, usePersianDigits: true);
                    txtHousing.Text = NumberHelper.FormatThousands(settings.HousingAllowance, usePersianDigits: true);
                    txtFood.Text = NumberHelper.FormatThousands(settings.FoodAllowance, usePersianDigits: true);
                    txtMarriage.Text = NumberHelper.FormatThousands(settings.MarriageAllowance, usePersianDigits: true);
                    txtChildRate.Text = StringHelper.ConvertToPersianDigits(ToDisplayPercent(settings.ChildAllowanceRate).ToString("0.##"));
                    txtInsuranceEmp.Text = StringHelper.ConvertToPersianDigits(ToDisplayPercent(settings.InsuranceEmployeeRate).ToString("0.##"));
                    txtInsuranceEmpr.Text = StringHelper.ConvertToPersianDigits(ToDisplayPercent(settings.InsuranceEmployerRate).ToString("0.##"));

                    // 🔴 خط مربوط به TaxExemptionLimit کاملاً حذف شده است

                    numAbsenceMultiplier.Value = settings.AbsenceMultiplier > 0 ? settings.AbsenceMultiplier : 1.0m;

                    txtNormalDayHours.Text = FormatToHoursMinutes(settings.NormalDayHours);
                    txtHolidayDayHours.Text = FormatToHoursMinutes(settings.HolidayDayHours);
                    txtMonthlyWorkingHours.Text = settings.MonthlyWorkingHours.ToString("0.##");

                    chkTaxBase.Checked = settings.TaxIncludeBaseSalary; chkTaxYears.Checked = settings.TaxIncludeYearsBonus; chkTaxAttr.Checked = settings.TaxIncludeAttraction;
                    chkTaxResp.Checked = settings.TaxIncludeResponsibility; chkTaxMgmt.Checked = settings.TaxIncludeManagement; chkTaxTech.Checked = settings.TaxIncludeTechnical;
                    chkTaxHouse.Checked = settings.TaxIncludeHousing; chkTaxGroc.Checked = settings.TaxIncludeGrocery; chkTaxChild.Checked = settings.TaxIncludeChild;
                    chkTaxMarr.Checked = settings.TaxIncludeMarriage; chkTaxTrans.Checked = settings.TaxIncludeTransportation; chkTaxOther.Checked = settings.TaxIncludeOtherAllowances;

                    chkInsBase.Checked = settings.InsuranceIncludeBaseSalary; chkInsYears.Checked = settings.InsuranceIncludeYearsBonus; chkInsAttr.Checked = settings.InsuranceIncludeAttraction;
                    chkInsResp.Checked = settings.InsuranceIncludeResponsibility; chkInsMgmt.Checked = settings.InsuranceIncludeManagement; chkInsTech.Checked = settings.InsuranceIncludeTechnical;
                    chkInsHouse.Checked = settings.InsuranceIncludeHousing; chkInsGroc.Checked = settings.InsuranceIncludeGrocery; chkInsChild.Checked = settings.InsuranceIncludeChild;
                    chkInsMarr.Checked = settings.InsuranceIncludeMarriage; chkInsTrans.Checked = settings.InsuranceIncludeTransportation; chkInsOther.Checked = settings.InsuranceIncludeOther;

                    chkOvtBase.Checked = settings.OvertimeIncludeBaseSalary; chkOvtYears.Checked = settings.OvertimeIncludeYearsBonus; chkOvtAttr.Checked = settings.OvertimeIncludeAttraction;
                    chkOvtResp.Checked = settings.OvertimeIncludeResponsibility; chkOvtMgmt.Checked = settings.OvertimeIncludeManagement; chkOvtTech.Checked = settings.OvertimeIncludeTechnical;
                    chkOvtHouse.Checked = settings.OvertimeIncludeHousing; chkOvtGroc.Checked = settings.OvertimeIncludeGrocery; chkOvtChild.Checked = settings.OvertimeIncludeChild;
                    chkOvtMarr.Checked = settings.OvertimeIncludeMarriage; chkOvtTrans.Checked = settings.OvertimeIncludeTransportation; chkOvtOther.Checked = settings.OvertimeIncludeOther;

                    ApplyChecks(_eidiChecks, settings.EidiIncludeComponents);
                    ApplyChecks(_sanavatChecks, settings.SanavatIncludeComponents);

                    ApplyChecks(_missionChecks, settings.MissionIncludeComponents);
                    txtMissionRate.Text = StringHelper.ConvertToPersianDigits(settings.MissionRate.ToString("0.##", CultureInfo.InvariantCulture));
                }
                else
                {
                    txtMinimumWage.Text = "0"; txtHousing.Text = "0"; txtFood.Text = "0"; txtMarriage.Text = "0";
                    txtChildRate.Text = "10"; txtInsuranceEmp.Text = "7"; txtInsuranceEmpr.Text = "23";
                    txtNormalDayHours.Text = "8:30"; txtHolidayDayHours.Text = "7:00"; txtMonthlyWorkingHours.Text = "220";

                    numAbsenceMultiplier.Value = 1;

                    chkTaxBase.Checked = true; chkTaxYears.Checked = true; chkTaxAttr.Checked = true; chkTaxResp.Checked = true; chkTaxMgmt.Checked = true; chkTaxTech.Checked = true;
                    chkTaxHouse.Checked = false; chkTaxGroc.Checked = false; chkTaxChild.Checked = false; chkTaxMarr.Checked = false; chkTaxTrans.Checked = true; chkTaxOther.Checked = true;

                    chkInsBase.Checked = true; chkInsYears.Checked = true; chkInsAttr.Checked = true; chkInsResp.Checked = true; chkInsMgmt.Checked = true; chkInsTech.Checked = true;
                    chkInsHouse.Checked = true; chkInsGroc.Checked = true; chkInsChild.Checked = true; chkInsMarr.Checked = false; chkInsTrans.Checked = false; chkInsOther.Checked = true;

                    chkOvtBase.Checked = true; chkOvtYears.Checked = true; chkOvtAttr.Checked = false; chkOvtResp.Checked = false; chkOvtMgmt.Checked = false; chkOvtTech.Checked = false;
                    chkOvtHouse.Checked = false; chkOvtGroc.Checked = false; chkOvtChild.Checked = false; chkOvtMarr.Checked = false; chkOvtTrans.Checked = false; chkOvtOther.Checked = false;

                    ApplyChecks(_eidiChecks, SalaryComponents.DefaultKeys);
                    ApplyChecks(_sanavatChecks, SalaryComponents.DefaultKeys);

                    ApplyChecks(_missionChecks, SalaryComponents.DefaultKeys);
                    txtMissionRate.Text = StringHelper.ConvertToPersianDigits("3");
                }

                dgvTaxBrackets.AllowUserToAddRows = false;
                dgvTaxBrackets.Rows.Clear();
                var brackets = await repo.GetBracketsAsync(year);
                foreach (var b in brackets)
                {
                    string toStr = b.ToAmount.HasValue ? NumberHelper.FormatThousands(b.ToAmount.Value, usePersianDigits: true) : "";
                    dgvTaxBrackets.Rows.Add(NumberHelper.FormatThousands(b.FromAmount, usePersianDigits: true), toStr, ToDisplayPercent(b.TaxRate).ToString("0.##", CultureInfo.InvariantCulture));
                }
                dgvTaxBrackets.AllowUserToAddRows = true;

                // ✅ نرخ روزانه‌ی طبقه‌ها و ضریب اضافه‌کار روزمزد
                var dailyRepo = new DailyWageRateRepository(_connectionString);
                var dailyRates = await Task.Run(() => dailyRepo.GetRates(year));
                var dailyMultiplier = await Task.Run(() => dailyRepo.GetOvertimeMultiplier(year));
                FillDailyRates(dailyRates);
                numDailyOvertimeMultiplier.Value = Math.Min(numDailyOvertimeMultiplier.Maximum,
                    Math.Max(numDailyOvertimeMultiplier.Minimum, dailyMultiplier ?? 1.4m));
            }, "در حال بارگذاری تنظیمات...");

            if (loaded) { _loadedYear = year; ClearDirty(); }
        }
        finally
        {
            _loading = false;
            try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "SalarySettingsForm"); }
        }
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        if (cmbYear.SelectedItem is null) return;
        int year = Convert.ToInt32(cmbYear.SelectedItem);

        var settings = new SalarySettings
        {
            Year = year,
            MinimumWage = NumberHelper.ParseFormatted(txtMinimumWage.Text),
            HousingAllowance = NumberHelper.ParseFormatted(txtHousing.Text),
            FoodAllowance = NumberHelper.ParseFormatted(txtFood.Text),
            MarriageAllowance = NumberHelper.ParseFormatted(txtMarriage.Text),
            ChildAllowanceRate = ParsePercent(txtChildRate.Text),
            InsuranceEmployeeRate = ParsePercent(txtInsuranceEmp.Text),
            InsuranceEmployerRate = ParsePercent(txtInsuranceEmpr.Text),

            // 🔴 خط مربوط به TaxExemptionLimit کاملاً حذف شده است

            AbsenceMultiplier = numAbsenceMultiplier.Value,

            NormalDayHours = ParseHoursMinutes(txtNormalDayHours.Text),
            HolidayDayHours = ParseHoursMinutes(txtHolidayDayHours.Text),
            MonthlyWorkingHours = NumberHelper.ParseFormatted(txtMonthlyWorkingHours.Text),

            TaxIncludeBaseSalary = chkTaxBase.Checked,
            TaxIncludeYearsBonus = chkTaxYears.Checked,
            TaxIncludeAttraction = chkTaxAttr.Checked,
            TaxIncludeResponsibility = chkTaxResp.Checked,
            TaxIncludeManagement = chkTaxMgmt.Checked,
            TaxIncludeTechnical = chkTaxTech.Checked,
            TaxIncludeHousing = chkTaxHouse.Checked,
            TaxIncludeGrocery = chkTaxGroc.Checked,
            TaxIncludeChild = chkTaxChild.Checked,
            TaxIncludeMarriage = chkTaxMarr.Checked,
            TaxIncludeTransportation = chkTaxTrans.Checked,
            TaxIncludeOtherAllowances = chkTaxOther.Checked,

            InsuranceIncludeBaseSalary = chkInsBase.Checked,
            InsuranceIncludeYearsBonus = chkInsYears.Checked,
            InsuranceIncludeAttraction = chkInsAttr.Checked,
            InsuranceIncludeResponsibility = chkInsResp.Checked,
            InsuranceIncludeManagement = chkInsMgmt.Checked,
            InsuranceIncludeTechnical = chkInsTech.Checked,
            InsuranceIncludeHousing = chkInsHouse.Checked,
            InsuranceIncludeGrocery = chkInsGroc.Checked,
            InsuranceIncludeChild = chkInsChild.Checked,
            InsuranceIncludeMarriage = chkInsMarr.Checked,
            InsuranceIncludeTransportation = chkInsTrans.Checked,
            InsuranceIncludeOther = chkInsOther.Checked,

            OvertimeIncludeBaseSalary = chkOvtBase.Checked,
            OvertimeIncludeYearsBonus = chkOvtYears.Checked,
            OvertimeIncludeAttraction = chkOvtAttr.Checked,
            OvertimeIncludeResponsibility = chkOvtResp.Checked,
            OvertimeIncludeManagement = chkOvtMgmt.Checked,
            OvertimeIncludeTechnical = chkOvtTech.Checked,
            OvertimeIncludeHousing = chkOvtHouse.Checked,
            OvertimeIncludeGrocery = chkOvtGroc.Checked,
            OvertimeIncludeChild = chkOvtChild.Checked,
            OvertimeIncludeMarriage = chkOvtMarr.Checked,
            OvertimeIncludeTransportation = chkOvtTrans.Checked,
            OvertimeIncludeOther = chkOvtOther.Checked,

            EidiIncludeComponents = CollectChecks(_eidiChecks),
            SanavatIncludeComponents = CollectChecks(_sanavatChecks),

            MissionIncludeComponents = CollectChecks(_missionChecks),
            MissionRate = ParsePlainPercent(txtMissionRate.Text)
        };

        if (settings.ChildAllowanceRate < 0 || settings.ChildAllowanceRate > 100 ||
            settings.InsuranceEmployeeRate < 0 || settings.InsuranceEmployeeRate > 100 ||
            settings.InsuranceEmployerRate < 0 || settings.InsuranceEmployerRate > 100)
        {
            tabControl.SelectedIndex = 0;
            ModernMessageBox.ShowWarning("درصدهای حق اولاد و بیمه باید بین ۰ تا ۱۰۰ باشند.");
            return;
        }

        if (settings.MissionRate < 0 || settings.MissionRate > 100)
        {
            ModernMessageBox.ShowWarning("درصد حق ماموریت باید بین ۰ تا ۱۰۰ باشد.");
            return;
        }

        if (settings.NormalDayHours <= 0 || settings.HolidayDayHours <= 0 || settings.MonthlyWorkingHours <= 0)
        {
            ModernMessageBox.ShowWarning("ساعت موظفی و ساعت کارکرد ماهانه باید بزرگتر از صفر باشند.");
            return;
        }

        var brackets = new List<TaxBracketDto>();
        foreach (DataGridViewRow row in dgvTaxBrackets.Rows)
        {
            if (row.IsNewRow) continue;
            string fromStr = row.Cells[0].Value?.ToString() ?? "";
            string toStr = row.Cells[1].Value?.ToString() ?? "";
            string rateStr = row.Cells[2].Value?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(fromStr) && string.IsNullOrWhiteSpace(toStr) && string.IsNullOrWhiteSpace(rateStr)) continue;   // ردیف کاملاً خالی
            if (string.IsNullOrWhiteSpace(fromStr) || string.IsNullOrWhiteSpace(rateStr))
            {
                // قبلاً چنین ردیفی بی‌صدا حذف می‌شد و پله‌ی مالیاتی ناقص ذخیره می‌شد
                tabControl.SelectedIndex = 2;
                ModernMessageBox.ShowWarning($"پله‌ی مالیاتی شماره‌ی {StringHelper.ConvertToPersianDigits((row.Index + 1).ToString())} ناقص است؛ «از مبلغ» و «نرخ» الزامی است.");
                return;
            }

            brackets.Add(new TaxBracketDto
            {
                FromAmount = NumberHelper.ParseFormatted(fromStr),
                ToAmount = string.IsNullOrWhiteSpace(toStr) ? (decimal?)null : NumberHelper.ParseFormatted(toStr),
                TaxRate = ToDecimalRate(ParsePercent(rateStr))
            });
        }

        brackets = brackets.OrderBy(b => b.FromAmount).ToList();
        string? bracketError = null;
        for (int i = 0; i < brackets.Count && bracketError is null; i++)
        {
            var b = brackets[i];
            if (b.TaxRate < 0 || b.TaxRate > 1m) bracketError = "نرخ مالیات هر پله باید بین ۰ تا ۱۰۰ درصد باشد.";
            else if (b.ToAmount.HasValue && b.ToAmount.Value <= b.FromAmount) bracketError = "در یکی از پله‌ها «تا مبلغ» از «از مبلغ» بزرگتر نیست.";
            else if (i < brackets.Count - 1 && !b.ToAmount.HasValue) bracketError = "فقط آخرین پله می‌تواند «بدون سقف» (تا مبلغ خالی) باشد.";
            else if (i < brackets.Count - 1 && brackets[i + 1].FromAmount < b.ToAmount!.Value) bracketError = "پله‌های مالیاتی با هم هم‌پوشانی دارند.";
        }
        if (bracketError is not null)
        {
            tabControl.SelectedIndex = 2;
            ModernMessageBox.ShowWarning(bracketError);
            return;
        }

        var dailyRates = new List<DailyWageRateItem>();
        var seenCategories = new HashSet<string>();
        foreach (DataGridViewRow row in dgvDailyRates.Rows)
        {
            if (row.IsNewRow) continue;
            string cat = row.Cells[0].Value?.ToString()?.Trim() ?? "";
            string amtStr = row.Cells[1].Value?.ToString() ?? "";
            if (cat.Length == 0 && string.IsNullOrWhiteSpace(amtStr)) continue;

            decimal amt = NumberHelper.ParseFormatted(amtStr);
            string key = StringHelper.NormalizePersianText(cat);
            if (cat.Length == 0 || amt <= 0 || !seenCategories.Add(key))
            {
                tabControl.SelectedIndex = 1;
                ModernMessageBox.ShowWarning("در جدول حقوق روزمزدها هر طبقه باید نام و دستمزد روزانه‌ی بیشتر از صفر داشته باشد و نام طبقه‌ها تکراری نباشد.");
                return;
            }
            dailyRates.Add(new DailyWageRateItem { Category = cat, Amount = amt });
        }
        decimal dailyMultiplier = numDailyOvertimeMultiplier.Value;

        if (!btnSave.Enabled) return;   // جلوگیری از کلیک/میانبر دوباره‌ی ذخیره
        btnSave.Enabled = false;
        bool success;
        try
        {
            success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new SalarySettingsRepository(_connectionString);
                await repo.SaveWithBracketsAsync(settings, brackets);

                var dailyRepo = new DailyWageRateRepository(_connectionString);
                await Task.Run(() => dailyRepo.SaveYear(year, dailyRates, dailyMultiplier));
            }, "در حال ذخیره تنظیمات...");
        }
        finally
        {
            if (!IsDisposed) btnSave.Enabled = true;
        }

        if (success && !IsDisposed)
        {
            ClearDirty();
            ToastHelper.Show("تنظیمات حقوق با موفقیت ذخیره شد.", isSuccess: true);
            await LoadAllAsync();
        }
    }

    private void FillDailyRates(List<DailyWageRateItem> rates)
    {
        dgvDailyRates.AllowUserToAddRows = false;
        dgvDailyRates.Rows.Clear();
        foreach (var r in rates)
            dgvDailyRates.Rows.Add(r.Category, NumberHelper.FormatThousands(r.Amount, usePersianDigits: true));
        dgvDailyRates.AllowUserToAddRows = true;
    }

    /// <summary>نرخ طبقه‌ها (و ضریب) سال قبل را داخل جدول می‌ریزد؛ تا «ذخیره» نزنید چیزی ثبت نمی‌شود.</summary>
    private async Task CopyDailyRatesFromPreviousYearAsync()
    {
        if (cmbYear.SelectedItem is null) return;
        int prevYear = Convert.ToInt32(cmbYear.SelectedItem) - 1;
        var dailyRepo = new DailyWageRateRepository(_connectionString);
        var rates = await Task.Run(() => dailyRepo.GetRates(prevYear));
        if (rates.Count == 0)
        {
            ModernMessageBox.ShowWarning($"برای سال {prevYear} نرخ روزمزدی ثبت نشده است.");
            return;
        }
        FillDailyRates(rates);
        var mult = await Task.Run(() => dailyRepo.GetOvertimeMultiplier(prevYear));
        if (mult.HasValue)
            numDailyOvertimeMultiplier.Value = Math.Min(numDailyOvertimeMultiplier.Maximum, Math.Max(numDailyOvertimeMultiplier.Minimum, mult.Value));
        ToastHelper.Show("نرخ‌های سال قبل وارد جدول شد؛ مبالغ را اصلاح و ذخیره کنید.", isSuccess: true);
    }

    private static decimal ParsePercent(string text)
    {
        string clean = StringHelper.ConvertToEnglishDigits(text ?? "").Replace("%", "").Replace("٫", ".").Replace(",", "").Replace("٬", "").Trim();
        if (!decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v)) return 0m;
        return v > 0 && v <= 1m ? v * 100m : v;
    }

    /// <summary>درصد ساده (۳ یعنی ۳٪، ۰٫۵ یعنی نیم درصد) بدون حدس «کسر/درصد» برای مقادیر کوچک.</summary>
    private static decimal ParsePlainPercent(string text)
    {
        string clean = StringHelper.ConvertToEnglishDigits(text ?? "").Replace("%", "").Replace("٫", ".").Replace("٬", "").Replace(",", "").Trim();
        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v) ? v : 0m;
    }

    private static decimal ToDisplayPercent(decimal rate) => rate > 0 && rate <= 1m ? rate * 100m : rate;
    private static decimal ToDecimalRate(decimal rate) => rate > 1m ? rate / 100m : rate;
}
