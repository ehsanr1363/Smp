using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Smp.Data;
using Smp.Helpers;
using Smp.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class PayrollReportsForm : Form
{
    private readonly PayrollRepository _repository;
    private ComboBox cmbYear = null!, cmbMonth = null!;
    private ModernTabHost tabMain = null!;

    private PillSwitcher tabListTypes = null!;
    private DataGridView dgvContractual = null!, dgvDaily = null!, dgvHourly = null!;

    private PillSwitcher tabPayslipTypes = null!;
    private DataGridView dgvPayslipContractual = null!, dgvPayslipDaily = null!, dgvPayslipHourly = null!;

    private Button btnLoad = null!, btnPrintList = null!, btnExcel = null!, btnPrintPayslip = null!;
    private Label lblHint = null!;
    private ComboBox cmbPrinter = null!;

    private CheckBox chkShowEarnings = null!;
    private CheckBox chkShowDeductions = null!;
    private CheckBox chkHideEmptyColumns = null!;

    // فیلتر محل کار (برای چاپ/اکسل بر اساس محل کار)
    private ComboBox cmbWorkLocation = null!;
    private bool _fillingLocations;
    private const string AllLocations = "همه محل‌های کار";
    private const string NoLocation = "— بدون محل کار —";

    private DataTable? _currentData;

    private readonly string[] _earningKeys = {
        "WorkingDays", "OvertimeHours", "AbsenceHours",
        "BaseSalary", "OvertimePay",
        "YearsBonus", "AttractionAllowance", "ResponsibilityAllowance",
        "ManagementAllowance", "TechnicalAllowance", "HousingAllowance", "GroceryAllowance",
        "ChildAllowance", "MarriageAllowance", "TransportationAllowance", "OtherAllowances"
    };

    private readonly string[] _deductionKeys = {
        "AbsenceDeduction", "InsuranceEmployee", "IncomeTax",
        "LoanDeduction", "AdvanceDeduction", "SupplementaryInsuranceDeduction", "CarInsuranceDeduction", "OtherDeductions"
    };

    private readonly Dictionary<string, string> _columnNames = new Dictionary<string, string>
    {
        { "PersonnelCode", "کد پرسنلی" },
        { "EmployeeName", "نام و نام خانوادگی" },
        { "WorkingDays", "روزهای کارکرد" },
        { "OvertimeHours", "ساعت اضافه کار" },
        { "AbsenceHours", "ساعت کسر کار" },
        { "BaseSalary", "حقوق پایه" },
        { "OvertimePay", "مبلغ اضافه کار" },
        { "YearsBonus", "پایه سنوات" },
        { "AttractionAllowance", "حق جذب" },
        { "ResponsibilityAllowance", "حق مسئولیت" },
        { "ManagementAllowance", "حق مدیریت" },
        { "TechnicalAllowance", "حق فنی" },
        { "HousingAllowance", "مسکن" },
        { "GroceryAllowance", "مزایای رفاهی" },
        { "ChildAllowance", "حق اولاد" },
        { "MarriageAllowance", "حق تاهل" },
        { "TransportationAllowance", "حق ایاب و ذهاب" },
        { "OtherAllowances", "سایر مزایا" },
        { "TotalEarnings", "جمع حقوق و مزایا" },
        { "AbsenceDeduction", "کسر غیبت" },
        { "InsuranceEmployee", "بیمه سهم کارمند" },
        { "IncomeTax", "مالیات" },
        { "LoanDeduction", "کسر وام" },
        { "AdvanceDeduction", "کسر مساعده" },
        { "SupplementaryInsuranceDeduction", "بیمه تکمیلی" },
        { "CarInsuranceDeduction", "بیمه خودرو" },
        { "OtherDeductions", "سایر کسورات" },
        { "TotalDeductions", "جمع کسورات" },
        { "InsuranceEmployer", "بیمه سهم کارفرما" },
        { "NetPay", "مانده قابل پرداخت" }
    };

    private readonly HashSet<string> _numericColumns = new HashSet<string>
    {
        "WorkingDays", "OvertimeHours", "AbsenceHours", "BaseSalary", "OvertimePay",
        "YearsBonus", "AttractionAllowance", "ResponsibilityAllowance", "ManagementAllowance", "TechnicalAllowance",
        "HousingAllowance", "GroceryAllowance", "ChildAllowance", "MarriageAllowance",
        "TransportationAllowance", "OtherAllowances", "TotalEarnings", "AbsenceDeduction",
        "InsuranceEmployee", "IncomeTax", "LoanDeduction", "AdvanceDeduction",
        "SupplementaryInsuranceDeduction", "CarInsuranceDeduction", "OtherDeductions", "TotalDeductions", "InsuranceEmployer", "NetPay"
    };

    public PayrollReportsForm(string connectionString)
    {
        _repository = new PayrollRepository(connectionString);
        BuildUI();
        this.ApplyStandardHeader("گزارش لیست حقوق و فیش حقوقی", new Size(1300, 800), isDialog: false);
        this.MinimumSize = new Size(1000, 620);
        LoadDefaults();
        Shown += async (s, e) => await LoadDataAsync();
    }

    private void LoadDefaults()
    {
        var pc = new PersianCalendar();
        int currentYear = pc.GetYear(DateTime.Now);
        int currentMonth = pc.GetMonth(DateTime.Now);

        for (int i = 1400; i <= 1410; i++) cmbYear.Items.Add(i);
        for (int i = 1; i <= 12; i++) cmbMonth.Items.Add(i);

        cmbYear.SelectedItem = currentYear;
        cmbMonth.SelectedItem = currentMonth;
    }

    private void BuildUI()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16, 14, 16, 10),
            BackColor = ModernTheme.Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));    // انتخاب سال و ماه
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));    // فیلترها + نتایج
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));    // دکمه‌های عملیات

        // ═══════ کارت انتخاب دوره ═══════
        var topCard = new ModernCard("", ModernTheme.Primary,
            new[]
            {
                new ColumnStyle(SizeType.Absolute, 54F),    // «سال»
                new ColumnStyle(SizeType.Absolute, 120F),   // کمبو سال
                new ColumnStyle(SizeType.Absolute, 54F),    // «ماه»
                new ColumnStyle(SizeType.Absolute, 120F),   // کمبو ماه
                new ColumnStyle(SizeType.Absolute, 150F),   // دکمه
                new ColumnStyle(SizeType.Percent, 100F)     // فضای خالی
            }, 44)
        { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };

        Label PeriodLabel(string t) => new Label
        {
            Text = t,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(10.5f, FontStyle.Bold),
            ForeColor = ModernTheme.TextMuted,
            BackColor = Color.Transparent
        };

        cmbYear = new ModernComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = ModernTheme.GetFont(11f), Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(4, 0, 4, 0) };
        cmbMonth = new ModernComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = ModernTheme.GetFont(11f), Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(4, 0, 4, 0) };

        btnLoad = ModernButton.Primary("📋  نمایش لیست", 140, 36);
        btnLoad.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        btnLoad.Margin = new Padding(12, 0, 4, 0);
        btnLoad.Click += async (s, e) => await LoadDataAsync();

        topCard.Body.Controls.Add(PeriodLabel("سال:"), 0, 0);
        topCard.Body.Controls.Add(cmbYear, 1, 0);
        topCard.Body.Controls.Add(PeriodLabel("ماه:"), 2, 0);
        topCard.Body.Controls.Add(cmbMonth, 3, 0);
        topCard.Body.Controls.Add(btnLoad, 4, 0);

        // ═══════ بدنه: فیلترها (راست) + نتایج ═══════
        var splitLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        splitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250F));
        splitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var filtersCard = new ModernCard("نمایش اطلاعات", ModernTheme.Teal, 1, 46, 46, 46, 52) { Dock = DockStyle.Top };

        ModernCheckBox MakeCheck(string text, Color fore) => new ModernCheckBox
        {
            Text = text,
            Checked = true,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = ModernTheme.GetFont(10.5f, FontStyle.Bold),
            ForeColor = fore,
            Margin = new Padding(2, 4, 2, 4)
        };
        chkShowEarnings = MakeCheck("نمایش ستون‌های مزایا", ModernTheme.Text);
        chkShowDeductions = MakeCheck("نمایش ستون‌های کسورات", ModernTheme.Text);
        chkHideEmptyColumns = MakeCheck("مخفی‌کردن ستون‌های خالی", ModernTheme.Brand);
        chkShowEarnings.CheckedChanged += (s, e) => ApplyGridFilter();
        chkShowDeductions.CheckedChanged += (s, e) => ApplyGridFilter();
        chkHideEmptyColumns.CheckedChanged += (s, e) => ApplyGridFilter();

        filtersCard.Body.Controls.Add(chkShowEarnings, 0, 0);
        filtersCard.Body.Controls.Add(chkShowDeductions, 0, 1);
        filtersCard.Body.Controls.Add(chkHideEmptyColumns, 0, 2);

        cmbWorkLocation = new ModernComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = ModernTheme.GetFont(10.5f), Dock = DockStyle.Fill, Margin = new Padding(2, 6, 2, 6) };
        cmbWorkLocation.Items.Add(AllLocations);
        cmbWorkLocation.SelectedIndex = 0;
        cmbWorkLocation.SelectedIndexChanged += (s, e) => { if (!_fillingLocations) ApplyGridFilter(); };
        filtersCard.Body.Controls.Add(cmbWorkLocation, 0, 3);

        var pnlFilters = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 12, 0), BackColor = Color.Transparent };
        pnlFilters.Controls.Add(filtersCard);
        splitLayout.Controls.Add(pnlFilters, 0, 0);

        // تب‌های اصلی
        tabMain = new ModernTabHost { Dock = DockStyle.Fill, AccentColor = ModernTheme.Primary };

        // — لیست حقوق —
        var pageList = tabMain.AddPage("📊 لیست حقوق ماهانه");
        pageList.Padding = new Padding(0, 12, 0, 0);
        tabListTypes = new PillSwitcher(new[] { "قراردادی / ماهانه", "روزمزد", "ساعتی" }, ModernTheme.Primary) { Dock = DockStyle.Fill };
        dgvContractual = CreateListGrid(); tabListTypes.AddToView(0, dgvContractual);
        dgvDaily = CreateListGrid(); tabListTypes.AddToView(1, dgvDaily);
        dgvHourly = CreateListGrid(); tabListTypes.AddToView(2, dgvHourly);
        pageList.Controls.Add(tabListTypes);

        // — فیش حقوقی —
        var pagePayslip = tabMain.AddPage("🧾 چاپ فیش حقوقی");
        pagePayslip.Padding = new Padding(0, 12, 0, 0);
        tabPayslipTypes = new PillSwitcher(new[] { "فیش قراردادی / ماهانه", "فیش روزمزد", "فیش ساعتی" }, ModernTheme.Purple) { Dock = DockStyle.Fill };
        dgvPayslipContractual = CreatePayslipGrid(); tabPayslipTypes.AddToView(0, dgvPayslipContractual);
        dgvPayslipDaily = CreatePayslipGrid(); tabPayslipTypes.AddToView(1, dgvPayslipDaily);
        dgvPayslipHourly = CreatePayslipGrid(); tabPayslipTypes.AddToView(2, dgvPayslipHourly);
        pagePayslip.Controls.Add(tabPayslipTypes);

        splitLayout.Controls.Add(tabMain, 1, 0);

        // ═══════ نوار دکمه‌ها ═══════
        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 10, 0, 0), Margin = Padding.Empty };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 720F));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var pnlButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = Padding.Empty };
        btnPrintList = ModernButton.Primary("🖨️  چاپ لیست حقوق", 160, 40);
        btnPrintList.Click += (s, e) => PrintList();
        btnExcel = ModernButton.Success("📥  خروجی اکسل", 140, 40);
        btnExcel.Click += (s, e) => ExportExcel();
        btnPrintPayslip = ModernButton.Create("🖨️  چاپ فیش (۲ نفر در A4)", ModernTheme.Purple, 230, 40);
        btnPrintPayslip.Click += (s, e) => PrintPayslip();
        cmbPrinter = new ModernComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = ModernTheme.GetFont(10f), Width = 250, Margin = new Padding(6, 6, 6, 0), RightToLeft = RightToLeft.No };
        FillPrinters();
        pnlButtons.Controls.Add(btnPrintList);
        pnlButtons.Controls.Add(cmbPrinter);
        pnlButtons.Controls.Add(btnExcel);
        pnlButtons.Controls.Add(btnPrintPayslip);

        lblHint = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = ModernTheme.GetFont(9.5f),
            ForeColor = ModernTheme.TextMuted,
            BackColor = Color.Transparent
        };

        bottom.Controls.Add(pnlButtons, 0, 0);
        bottom.Controls.Add(lblHint, 1, 0);

        tabMain.SelectedIndexChanged += (s, e) => UpdateActions();
        UpdateActions();

        root.Controls.Add(topCard, 0, 0);
        root.Controls.Add(splitLayout, 0, 1);
        root.Controls.Add(bottom, 0, 2);
        this.Controls.Add(root);
    }

    /// <summary>لیست پرینترهای نصب‌شده؛ پرینتر پیش‌فرض ویندوز به‌صورت خودکار انتخاب می‌شود</summary>
    private void FillPrinters()
    {
        cmbPrinter.Items.Clear();
        string defaultName = new System.Drawing.Printing.PrinterSettings().PrinterName;
        foreach (string name in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
            cmbPrinter.Items.Add(name);
        if (cmbPrinter.Items.Count == 0) { cmbPrinter.Items.Add("(پرینتری نصب نشده)"); cmbPrinter.SelectedIndex = 0; return; }
        int idx = cmbPrinter.Items.IndexOf(defaultName);
        cmbPrinter.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private string? SelectedPrinterName()
    {
        if (cmbPrinter.SelectedItem is not string name) return null;
        return System.Drawing.Printing.PrinterSettings.InstalledPrinters.Cast<string>().Contains(name) ? name : null;
    }

    /// <summary>فقط دکمه‌های مربوط به تب فعال نمایش داده می‌شوند</summary>
    private void UpdateActions()
    {
        bool listTab = tabMain.SelectedIndex <= 0;
        btnPrintList.Visible = listTab;
        cmbPrinter.Visible = listTab;
        btnExcel.Visible = listTab;
        btnPrintPayslip.Visible = !listTab;
        lblHint.Text = listTab
            ? "ستون‌های مزایا، کسورات و ستون‌های خالی را از پنل کناری تنظیم کنید"
            : "بدون انتخاب ردیف: همه چاپ می‌شوند  ·  برای چند نفر از Ctrl یا Shift استفاده کنید  ·  دوبار کلیک: چاپ";
    }

    /// <summary>ساعت اعشاری → ساعت:دقیقه (مثلاً ۷٫۵ → 07:30؛ صفر → 00:00)</summary>
    private static string ToHhMm(decimal hours)
    {
        int totalMinutes = (int)Math.Round(Math.Abs(hours) * 60m, MidpointRounding.AwayFromZero);
        return $"{totalMinutes / 60:00}:{totalMinutes % 60:00}";
    }

    private bool IsColumnEmpty(DataTable dt, string colName)
    {
        if (colName == "EmployeeName") return false;
        if (!dt.Columns.Contains(colName)) return true;
        if (colName == "WorkingDays" || colName == "OvertimeHours" || colName == "AbsenceHours" || colName == "OvertimePay") return false;

        foreach (DataRow row in dt.Rows)
        {
            if (row[colName] != DBNull.Value && row[colName] is not null)
            {
                if (decimal.TryParse(row[colName].ToString(), out decimal decVal))
                {
                    if (decVal != 0) return false;
                }
            }
        }
        return true;
    }

    private List<KeyValuePair<string, string>> GetActiveColumns(DataTable dt, bool includeEmployerInsurance = false)
    {
        var active = new List<KeyValuePair<string, string>>();
        active.Add(new KeyValuePair<string, string>("EmployeeName", _columnNames["EmployeeName"]));

        if (chkShowEarnings.Checked)
        {
            foreach (var key in _earningKeys)
            {
                if (!dt.Columns.Contains(key)) continue;
                if (!chkHideEmptyColumns.Checked || !IsColumnEmpty(dt, key))
                    active.Add(new KeyValuePair<string, string>(key, _columnNames[key]));
            }
            active.Add(new KeyValuePair<string, string>("TotalEarnings", _columnNames["TotalEarnings"]));
        }

        if (chkShowDeductions.Checked)
        {
            foreach (var key in _deductionKeys)
            {
                if (!dt.Columns.Contains(key)) continue;
                if (!chkHideEmptyColumns.Checked || !IsColumnEmpty(dt, key))
                    active.Add(new KeyValuePair<string, string>(key, _columnNames[key]));
            }
            active.Add(new KeyValuePair<string, string>("TotalDeductions", _columnNames["TotalDeductions"]));
        }

        // ✅ فقط برای گزارش روزمزدها: بیمه سهم کارفرما (قبل از مانده قابل پرداخت)
        if (includeEmployerInsurance && dt.Columns.Contains("InsuranceEmployer")
            && (!chkHideEmptyColumns.Checked || !IsColumnEmpty(dt, "InsuranceEmployer")))
            active.Add(new KeyValuePair<string, string>("InsuranceEmployer", _columnNames["InsuranceEmployer"]));

        active.Add(new KeyValuePair<string, string>("NetPay", _columnNames["NetPay"]));
        return active;
    }

    private DataGridView CreateListGrid()
    {
        var dgv = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, ReadOnly = true, AutoGenerateColumns = false, BorderStyle = BorderStyle.None, BackgroundColor = Color.White };
        dgv.ApplyModernStyle();
        // ✅ FIX: هندلر فقط یک‌بار ثبت می‌شود (قبلاً با هر بار نمایش یکی اضافه می‌شد)
        dgv.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgv.Columns[e.ColumnIndex].DataPropertyName;
            if (colName == "OvertimeHours" && (e.Value is decimal || e.Value == DBNull.Value))
            {
                decimal ot = e.Value is decimal d0 ? d0 : 0m;
                e.Value = StringHelper.ConvertToPersianDigits(ToHhMm(ot));
                e.FormattingApplied = true;
            }
            else if (colName != "EmployeeName" && e.Value is decimal dec)
            {
                e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                e.FormattingApplied = true;
            }
        };
        return dgv;
    }

    // ═══════════════════════════════════════════════════════════════
    // ✅ MultiSelect = true اضافه شد تا کاربر بتونه چند ردیف انتخاب کنه
    // ═══════════════════════════════════════════════════════════════
    private DataGridView CreatePayslipGrid()
    {
        var dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            ReadOnly = true,
            AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true,
            BorderStyle = BorderStyle.None,
            BackgroundColor = Color.White
        };
        dgv.ApplyModernStyle();
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EmployeeName", HeaderText = "نام کارمند", FillWeight = 50 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PersonnelCode", HeaderText = "کد پرسنلی", FillWeight = 20 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "NetPay", HeaderText = "خالص پرداختی", FillWeight = 30 });

        dgv.CellFormatting += (s, e) =>
        {
            if (e.Value is decimal dec && dgv.Columns[e.ColumnIndex].DataPropertyName == "NetPay")
            {
                e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                e.FormattingApplied = true;
            }
        };
        dgv.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) PrintPayslip(); };
        return dgv;
    }

    private async Task LoadDataAsync()
    {
        if (cmbYear.SelectedItem is null || cmbMonth.SelectedItem is null) return;
        int year = Convert.ToInt32(cmbYear.SelectedItem);
        int month = Convert.ToInt32(cmbMonth.SelectedItem);

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            _currentData = await _repository.GetSavedPayrollsAsync(year, month);
        }, "در حال بارگذاری لیست حقوق...");

        if (!success || _currentData is null) return;

        if (_currentData.Rows.Count == 0)
        {
            ModernMessageBox.ShowWarning("برای این ماه هیچ فیش حقوقی ذخیره نشده است.");
            dgvContractual.DataSource = null; dgvDaily.DataSource = null; dgvHourly.DataSource = null;
            dgvPayslipContractual.DataSource = null; dgvPayslipDaily.DataSource = null; dgvPayslipHourly.DataSource = null;
            for (int i = 0; i < 3; i++) { tabListTypes.SetCount(i, 0); tabPayslipTypes.SetCount(i, 0); }
            return;
        }

        FillWorkLocations();
        ApplyGridFilter();
    }

    private static string LocationOf(DataRow r)
        => r.Table.Columns.Contains("WorkLocation") ? (r["WorkLocation"]?.ToString() ?? "").Trim() : "";

    // لیست محل‌های کار را از داده‌ی همین ماه می‌سازد و انتخاب قبلی را حفظ می‌کند
    private void FillWorkLocations()
    {
        if (_currentData is null) return;
        string previous = cmbWorkLocation.SelectedItem?.ToString() ?? AllLocations;

        var locs = _currentData.AsEnumerable().Select(LocationOf).Where(x => x.Length > 0)
            .Distinct().OrderBy(x => x, StringComparer.CurrentCulture).ToList();
        bool hasEmpty = _currentData.AsEnumerable().Any(r => LocationOf(r).Length == 0);

        _fillingLocations = true;
        try
        {
            cmbWorkLocation.Items.Clear();
            cmbWorkLocation.Items.Add(AllLocations);
            foreach (var l in locs) cmbWorkLocation.Items.Add(l);
            if (hasEmpty && locs.Count > 0) cmbWorkLocation.Items.Add(NoLocation);

            int idx = cmbWorkLocation.Items.IndexOf(previous);
            cmbWorkLocation.SelectedIndex = idx >= 0 ? idx : 0;
        }
        finally { _fillingLocations = false; }
    }

    // نام محل کار انتخاب‌شده برای عنوان چاپ/اکسل؛ اگر «همه» باشد خالی است
    private string SelectedLocationTitle()
    {
        string sel = cmbWorkLocation.SelectedItem?.ToString() ?? AllLocations;
        return sel == AllLocations || sel == NoLocation ? "" : sel;
    }

    // ═══════════════════════════════════════════════════════════════
    // ✅ بعد از بارگذاری، انتخاب خودکار ردیف اول پاک میشه
    //    تا «چاپ همه» به‌طور پیش‌فرض همه رو چاپ کنه، نه یک نفر رو
    // ═══════════════════════════════════════════════════════════════
    private void ApplyGridFilter()
    {
        if (_currentData is null) return;

        PayrollKind KindOf(DataRow r) => PayrollCalculatorService.ResolveKind(r.Field<string>("PersonnelType"));

        // فیلتر محل کار
        string selLoc = cmbWorkLocation.SelectedItem?.ToString() ?? AllLocations;
        bool LocOk(DataRow r) => selLoc == AllLocations
            || (selLoc == NoLocation ? LocationOf(r).Length == 0 : string.Equals(LocationOf(r), selLoc, StringComparison.OrdinalIgnoreCase));
        var source = _currentData.AsEnumerable().Where(LocOk).ToList();

        var contractual = source.Where(r => KindOf(r) == PayrollKind.Monthly);
        var daily = source.Where(r => KindOf(r) == PayrollKind.Daily);
        var hourly = source.Where(r => KindOf(r) == PayrollKind.Hourly);

        var dtContractual = contractual.Any() ? contractual.CopyToDataTable() : _currentData.Clone();
        var dtDaily = daily.Any() ? daily.CopyToDataTable() : _currentData.Clone();
        var dtHourly = hourly.Any() ? hourly.CopyToDataTable() : _currentData.Clone();

        UpdateGridColumns(dgvContractual, dtContractual);
        UpdateGridColumns(dgvDaily, dtDaily);
        UpdateGridColumns(dgvHourly, dtHourly);

        dgvPayslipContractual.DataSource = dtContractual;
        dgvPayslipDaily.DataSource = dtDaily;
        dgvPayslipHourly.DataSource = dtHourly;

        dgvPayslipContractual.ClearSelection();
        dgvPayslipDaily.ClearSelection();
        dgvPayslipHourly.ClearSelection();

        var counts = new[] { dtContractual.Rows.Count, dtDaily.Rows.Count, dtHourly.Rows.Count };
        for (int i = 0; i < counts.Length; i++)
        {
            tabListTypes.SetCount(i, counts[i]);
            tabPayslipTypes.SetCount(i, counts[i]);
        }
    }

    private void UpdateGridColumns(DataGridView dgv, DataTable dt)
    {
        dgv.DataSource = null;
        dgv.Columns.Clear();

        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "RowNum", HeaderText = "ردیف", FillWeight = 5, ReadOnly = true });

        var activeCols = GetActiveColumns(dt, ReferenceEquals(dgv, dgvDaily));
        foreach (var col in activeCols)
        {
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = col.Key,
                HeaderText = col.Value,
                FillWeight = 15
            });
        }


        dgv.DataSource = dt;

        for (int i = 0; i < dgv.Rows.Count; i++)
        {
            dgv.Rows[i].Cells["RowNum"].Value = i + 1;
        }
    }

    private void PrintList()
    {
        if (tabMain.SelectedIndex != 0) { ModernMessageBox.ShowWarning("لطفاً به تب «لیست حقوق ماهانه» بروید."); return; }

        DataGridView activeGrid = tabListTypes.SelectedIndex switch
        {
            0 => dgvContractual,
            1 => dgvDaily,
            2 => dgvHourly,
            _ => dgvContractual
        };

        if (activeGrid.DataSource is not DataTable dt || dt.Rows.Count == 0) { ModernMessageBox.ShowWarning("داده‌ای برای چاپ وجود ندارد."); return; }

        string yearMonth = $"{TranslationHelper.GetMonthName(Convert.ToInt32(cmbMonth.SelectedItem))} {cmbYear.SelectedItem}";
        string locTitle = SelectedLocationTitle();
        if (locTitle.Length > 0) yearMonth += $" - محل کار: {locTitle}";
        string listTitle = "محاسبه حقوق و مزایا و فوق العاده های";

        var selectedCols = GetActiveColumns(dt, ReferenceEquals(activeGrid, dgvDaily));

        var companyRepo = new CompanyRepository(AppConfig.ConnectionString);
        var company = companyRepo.GetAll().FirstOrDefault();
        string companyName = company?.Name ?? "شرکت";
        Image? logo = ImageHelper.ByteArrayToImage(company?.Logo);

        List<string>? headerLines = null;
        string[]? signatures = null;
        if (ReferenceEquals(activeGrid, dgvDaily))
        {
            int py = Convert.ToInt32(cmbYear.SelectedItem), pm = Convert.ToInt32(cmbMonth.SelectedItem);
            var pcal = new PersianCalendar();
            int lastDay = pcal.GetDaysInMonth(py, pm);
            string fromDate = StringHelper.ConvertToPersianDigits($"{py}/{pm:00}/01");
            string toDate = StringHelper.ConvertToPersianDigits($"{py}/{pm:00}/{lastDay:00}");
            string project = locTitle.Length > 0 ? locTitle : "......................";
            headerLines = new List<string>
            {
                $"پروژه: {project}",
                $"دستمزد (حقوق و مزایا)، عیدی و سنوات روزانه به میزان کارکرد - ماه {TranslationHelper.GetMonthName(pm)} {StringHelper.ConvertToPersianDigits(py.ToString())} کارگران روزمزد",
                $"از تاریخ {fromDate} تا تاریخ {toDate}"
            };
            signatures = new[] { "امور مالی پروژه", "سرپرست کارگاه", "مدیر مالی", "مدیر عامل" };
        }

        var printer = new PayrollListPrinter(dt, selectedCols, yearMonth, listTitle, companyName, logo, _numericColumns, headerLines, signatures);
        printer.ShowPreview(SelectedPrinterName());
    }

    // ═══════════════════════════════════════════════════════════════
    // ✅ منطق هوشمند چاپ فیش:
    //    - هیچ ردیفی انتخاب نشده → همه چاپ می‌شن (۲ نفر در A4)
    //    - یک یا چند ردیف انتخاب شده → فقط همون‌ها چاپ می‌شن
    // ═══════════════════════════════════════════════════════════════
    private void PrintPayslip()
    {
        if (tabMain.SelectedIndex != 1)
        {
            ModernMessageBox.ShowWarning("لطفاً به تب «چاپ فیش حقوقی» بروید.");
            return;
        }

        DataGridView activeGrid = tabPayslipTypes.SelectedIndex switch
        {
            0 => dgvPayslipContractual,
            1 => dgvPayslipDaily,
            2 => dgvPayslipHourly,
            _ => dgvPayslipContractual
        };

        if (activeGrid.DataSource is not DataTable dt || dt.Rows.Count == 0)
        {
            ModernMessageBox.ShowWarning("داده‌ای برای چاپ وجود ندارد.");
            return;
        }

        DataTable printData = dt;
        int totalRowsInGrid = dt.Rows.Count;

        if (activeGrid.SelectedRows.Count > 0 && activeGrid.SelectedRows.Count < totalRowsInGrid)
        {
            var selectedEmployeeIds = new HashSet<int>();
            foreach (DataGridViewRow gRow in activeGrid.SelectedRows)
            {
                if (gRow.DataBoundItem is DataRowView drv && drv.Row.Table.Columns.Contains("EmployeeId"))
                {
                    selectedEmployeeIds.Add(Convert.ToInt32(drv.Row["EmployeeId"]));
                }
            }

            if (selectedEmployeeIds.Count > 0)
            {
                var filtered = dt.Clone();
                foreach (DataRow row in dt.Rows)
                {
                    if (selectedEmployeeIds.Contains(Convert.ToInt32(row["EmployeeId"])))
                        filtered.ImportRow(row);
                }
                if (filtered.Rows.Count > 0)
                    printData = filtered;
            }
        }

        string yearMonth = $"{TranslationHelper.GetMonthName(Convert.ToInt32(cmbMonth.SelectedItem))} {cmbYear.SelectedItem}";
        var companyRepo = new CompanyRepository(AppConfig.ConnectionString);
        var company = companyRepo.GetAll().FirstOrDefault();
        string companyName = company?.Name ?? "شرکت";
        Image? logo = ImageHelper.ByteArrayToImage(company?.Logo);

        var printer = new PayslipPrinter(printData, yearMonth, companyName, logo);
        printer.ShowPreview();
    }

    private void ExportExcel()
    {
        if (tabMain.SelectedIndex != 0) { ModernMessageBox.ShowWarning("لطفاً به تب «لیست حقوق» بروید."); return; }

        DataGridView activeGrid = tabListTypes.SelectedIndex switch
        {
            0 => dgvContractual,
            1 => dgvDaily,
            2 => dgvHourly,
            _ => dgvContractual
        };

        if (activeGrid.DataSource is not DataTable dt || dt.Rows.Count == 0) { ModernMessageBox.ShowWarning("داده‌ای برای خروجی وجود ندارد."); return; }

        string listTitle = (string.IsNullOrEmpty(tabListTypes.SelectedTitle) ? "لیست_حقوق" : tabListTypes.SelectedTitle);
        using var sfd = new SaveFileDialog { Filter = "Excel Files|*.xlsx", FileName = $"{listTitle.Replace(" ", "_")}{(SelectedLocationTitle().Length > 0 ? "_" + SelectedLocationTitle().Replace(" ", "_") : "")}_{cmbYear.SelectedItem}_{cmbMonth.SelectedItem}.xlsx" };
        if (sfd.ShowDialog() != DialogResult.OK) return;

        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(listTitle);
            ws.RightToLeft = true;

            var selectedCols = GetActiveColumns(dt, ReferenceEquals(activeGrid, dgvDaily));

            ws.Cell(1, 1).Value = "ردیف";
            for (int i = 0; i < selectedCols.Count; i++)
            {
                ws.Cell(1, i + 2).Value = selectedCols[i].Value;
            }

            for (int c = 1; c <= selectedCols.Count + 1; c++)
            {
                var cell = ws.Cell(1, c);
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            for (int r = 0; r < dt.Rows.Count; r++)
            {
                var row = dt.Rows[r];
                ws.Cell(r + 2, 1).Value = r + 1;

                for (int i = 0; i < selectedCols.Count; i++)
                {
                    var val = row[selectedCols[i].Key];
                    var cell = ws.Cell(r + 2, i + 2);

                    if (selectedCols[i].Key == "OvertimeHours")
                    {
                        decimal.TryParse(val?.ToString(), out decimal otVal);
                        cell.Value = ToHhMm(otVal);
                        cell.Style.NumberFormat.Format = "@";
                    }
                    else if (selectedCols[i].Key != "EmployeeName" && decimal.TryParse(val?.ToString(), out decimal decVal))
                    {
                        cell.Value = decVal;
                        cell.Style.NumberFormat.Format = "#,##0";
                    }
                    else
                    {
                        cell.Value = val?.ToString() ?? "";
                    }

                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(sfd.FileName);
            ToastHelper.Show("فایل اکسل ذخیره شد.", true);
        }
        catch (Exception ex) { ModernMessageBox.ShowError("خطا:\n" + ex.Message); }
    }
}
