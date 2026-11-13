using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using SMP.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class PayrollProcessForm : Form
    {
        private readonly string _connectionString;
        private readonly PayrollCalculatorService _calculatorService;
        private readonly PayrollRepository _payrollRepository;
        private readonly LoanRepository _loanRepository;
        private readonly SupplementaryInsuranceRepository _suppInsuranceRepository;
        private readonly CarInsuranceRepository _carInsuranceRepository;
        private readonly BankAccountRepository _bankRepo;

        private ComboBox cmbYear = null!, cmbMonth = null!;
        private Button btnCalculateAll = null!, btnSave = null!;
        private TabControl tabControl = null!;

        private DataGridView dgvContractual = null!, dgvDaily = null!, dgvHourly = null!;
        private CheckBox chkAllContractual = null!, chkAllDaily = null!, chkAllHourly = null!;
        private Button btnRecalcContractual = null!, btnRecalcDaily = null!, btnRecalcHourly = null!;
        private Label lblSummaryContractual = null!, lblSummaryDaily = null!, lblSummaryHourly = null!;

        private DataTable? _dtContractual;
        private DataTable? _dtDaily;
        private DataTable? _dtHourly;

        private readonly Dictionary<string, PayrollResult> _resultsByCode = new(StringComparer.Ordinal);

        private List<Employee> _currentEmployees = new();
        private int _currentYear;
        private int _currentMonth;
        private bool _isLoadedFromDb = false;

        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly SemaphoreSlim _actionLock = new(1, 1);

        public PayrollProcessForm(string connectionString)
        {
            _connectionString = connectionString;
            _calculatorService = new PayrollCalculatorService();
            _payrollRepository = new PayrollRepository(connectionString);
            _loanRepository = new LoanRepository(connectionString);
            _suppInsuranceRepository = new SupplementaryInsuranceRepository(connectionString);
            _carInsuranceRepository = new CarInsuranceRepository(connectionString);
            _bankRepo = new BankAccountRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("پردازش حقوق ماهانه (تفکیک‌شده)", new Size(1600, 800));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
            LoadDefaults();

            cmbYear.SelectedIndexChanged += async (s, e) => await TryLoadExistingPayrollsAsync();
            cmbMonth.SelectedIndexChanged += async (s, e) => await TryLoadExistingPayrollsAsync();

            Shown += async (s, e) => await TryLoadExistingPayrollsAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _loadLock?.Dispose(); } catch { }
                try { _actionLock?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private void LoadDefaults()
        {
            var pc = new PersianCalendar();
            int currentYear = pc.GetYear(DateTime.Now);
            int currentMonth = pc.GetMonth(DateTime.Now);

            if (cmbYear.Items.Contains(currentYear)) cmbYear.SelectedItem = currentYear;
            else if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;

            if (cmbMonth.Items.Contains(currentMonth)) cmbMonth.SelectedItem = currentMonth;
            else if (cmbMonth.Items.Count > 0) cmbMonth.SelectedIndex = 0;
        }

        private void BuildUI()
        {
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };
            var topFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };

            cmbYear = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 8, 15, 5) };
            for (int i = 1400; i <= 1410; i++) cmbYear.Items.Add(i);

            cmbMonth = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 8, 15, 5) };
            for (int i = 1; i <= 12; i++) cmbMonth.Items.Add(i);

            btnCalculateAll = UIHelper.CreateButton("محاسبه همه پرسنل", UIHelper.ActionGreen, 200, 35);
            btnCalculateAll.Margin = new Padding(5, 3, 15, 5);
            btnCalculateAll.Click += BtnCalculateAll_Click;

            btnSave = UIHelper.CreateButton("💾 ذخیره همه (سه تب)", UIHelper.SectionBlue, 200, 35);
            btnSave.Margin = new Padding(5, 3, 5, 5);
            btnSave.Enabled = false;
            btnSave.Click += BtnSave_Click;

            var lblHint = new Label
            {
                Text = "💡 سوابق ذخیره‌شده‌ی هر ماه به‌طور خودکار بارگذاری می‌شود. برای محاسبه‌ی مجدد، تیک بزنید + «محاسبه مجدد».",
                AutoSize = true,
                Font = UIHelper.PersianFont(9f),
                ForeColor = UIHelper.WarmDanger,
                Margin = new Padding(20, 14, 5, 5)
            };

            topFlow.Controls.Add(new Label { Text = "سال:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(cmbYear);
            topFlow.Controls.Add(new Label { Text = "ماه:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(cmbMonth);
            topFlow.Controls.Add(btnCalculateAll);
            topFlow.Controls.Add(btnSave);
            topFlow.Controls.Add(lblHint);

            topPanel.Controls.Add(topFlow);

            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(200, 40)
            };
            ModernLayoutHelper.StyleModernTabControl(tabControl);

            var tab1 = CreatePayrollTab("قراردادی / ماهانه (پرسنل)");
            dgvContractual = tab1.grid;
            chkAllContractual = tab1.chkAll;
            btnRecalcContractual = tab1.btnRecalc;
            lblSummaryContractual = tab1.lblSummary;
            tabControl.TabPages.Add(tab1.page);
            WireTabEvents(dgvContractual, TabKey.Contractual);
            chkAllContractual.CheckedChanged += (s, e) => ToggleSelectAll(TabKey.Contractual);
            btnRecalcContractual.Click += async (s, e) => await RecalculateSelectedAsync(TabKey.Contractual);

            var tab2 = CreatePayrollTab("روزمزد (کارگران)");
            dgvDaily = tab2.grid;
            chkAllDaily = tab2.chkAll;
            btnRecalcDaily = tab2.btnRecalc;
            lblSummaryDaily = tab2.lblSummary;
            tabControl.TabPages.Add(tab2.page);
            WireTabEvents(dgvDaily, TabKey.Daily);
            chkAllDaily.CheckedChanged += (s, e) => ToggleSelectAll(TabKey.Daily);
            btnRecalcDaily.Click += async (s, e) => await RecalculateSelectedAsync(TabKey.Daily);

            var tab3 = CreatePayrollTab("ساعتی (کارگران)");
            dgvHourly = tab3.grid;
            chkAllHourly = tab3.chkAll;
            btnRecalcHourly = tab3.btnRecalc;
            lblSummaryHourly = tab3.lblSummary;
            tabControl.TabPages.Add(tab3.page);
            WireTabEvents(dgvHourly, TabKey.Hourly);
            chkAllHourly.CheckedChanged += (s, e) => ToggleSelectAll(TabKey.Hourly);
            btnRecalcHourly.Click += async (s, e) => await RecalculateSelectedAsync(TabKey.Hourly);

            this.Controls.Add(tabControl);
            this.Controls.Add(topPanel);
        }

        private enum TabKey { Contractual, Daily, Hourly }

        private (TabPage page, DataGridView grid, CheckBox chkAll, Button btnRecalc, Label lblSummary) CreatePayrollTab(string title)
        {
            var page = new TabPage(title) { BackColor = UIHelper.WarmBackground, Padding = new Padding(5) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(5) };

            var lblSummary = new Label
            {
                AutoSize = true,
                Font = UIHelper.PersianFont(10.5f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText,
                Margin = new Padding(5, 12, 20, 5),
                Text = "—"
            };

            var btnRecalc = UIHelper.CreateButton("🔄 محاسبه مجدد انتخاب‌شده‌ها", UIHelper.SectionPurple, 260, 32);
            btnRecalc.Enabled = false;
            btnRecalc.Margin = new Padding(5, 4, 15, 3);

            var chkAll = new CheckBox
            {
                Text = "انتخاب همه",
                AutoSize = true,
                Font = UIHelper.PersianFont(10.5f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText,
                Enabled = false,
                Margin = new Padding(5, 12, 5, 5)
            };

            toolbar.Controls.Add(lblSummary);
            toolbar.Controls.Add(btnRecalc);
            toolbar.Controls.Add(chkAll);

            var grid = new DataGridView { Dock = DockStyle.Fill, EditMode = DataGridViewEditMode.EditOnEnter };
            grid.ApplyModernStyle();
            grid.ReadOnly = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;

            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(grid, 0, 1);
            page.Controls.Add(layout);

            return (page, grid, chkAll, btnRecalc, lblSummary);
        }

        private void WireTabEvents(DataGridView dgv, TabKey key)
        {
            dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (e.Value is decimal dec)
                {
                    e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
            };

            dgv.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgv.IsCurrentCellDirty && dgv.CurrentCell is DataGridViewCheckBoxCell)
                    dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            dgv.CellEndEdit += (s, e) => HandleOtherDeductionsEdit(dgv, key, e);

            dgv.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (dgv.Rows[e.RowIndex].DataBoundItem is not DataRowView drv) return;
                string code = drv["PersonnelCode"]?.ToString() ?? "";
                if (_resultsByCode.TryGetValue(code, out var res))
                {
                    using var form = new PayslipForm(res);
                    form.ShowDialog(this);
                }
            };
        }

        private async Task TryLoadExistingPayrollsAsync()
        {
            if (cmbYear.SelectedItem == null || cmbMonth.SelectedItem == null || IsDisposed) return;

            if (!_actionLock.Wait(0)) return;

            bool loadLockAcquired = false;
            try
            {
                try
                {
                    loadLockAcquired = await _loadLock.WaitAsync(0);
                    if (!loadLockAcquired) return;
                }
                catch (ObjectDisposedException) { return; }

                int year = Convert.ToInt32(cmbYear.SelectedItem);
                int month = Convert.ToInt32(cmbMonth.SelectedItem);

                _dtContractual = null;
                _dtDaily = null;
                _dtHourly = null;
                _resultsByCode.Clear();
                _isLoadedFromDb = false;

                dgvContractual.DataSource = null;
                dgvDaily.DataSource = null;
                dgvHourly.DataSource = null;

                btnSave.Enabled = false;
                btnRecalcContractual.Enabled = false;
                btnRecalcDaily.Enabled = false;
                btnRecalcHourly.Enabled = false;
                chkAllContractual.Enabled = false;
                chkAllDaily.Enabled = false;
                chkAllHourly.Enabled = false;
                chkAllContractual.Checked = false;
                chkAllDaily.Checked = false;
                chkAllHourly.Checked = false;

                List<Employee>? employeesList = null;
                Dictionary<int, PayrollResult>? computedResults = null;
                bool hasSaved = false;

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    hasSaved = await _payrollRepository.HasPayrollsForMonthAsync(year, month);
                    if (!hasSaved) return;

                    DataTable? savedDt = await _payrollRepository.GetSavedPayrollsAsync(year, month);
                    if (savedDt == null || savedDt.Rows.Count == 0)
                    {
                        hasSaved = false;
                        return;
                    }

                    var empRepo = new EmployeeRepository(_connectionString);
                    employeesList = await empRepo.GetEmployeesWithDetailsAsync("", false);
                    var employeesById = employeesList.ToDictionary(e => e.Id);

                    var settingsRepo = new SalarySettingsRepository(_connectionString);
                    var settings = await settingsRepo.GetByYearAsync(year);

                    var attRepo = new AttendanceRepository(_connectionString);
                    var attendances = await attRepo.GetAttendanceForMonthAsync(year, month);

                    var activeLoans = await _loanRepository.GetActiveLoansAsync();
                    var activeSuppIns = await _suppInsuranceRepository.GetActiveInsurancesAsync();
                    var activeCarIns = await _carInsuranceRepository.GetActiveCarInsurancesAsync();

                    var taxBrackets = _calculatorService.GetTaxBracketsSafe(year);

                    var loansByEmployee = activeLoans.GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
                    var suppInsByEmployee = activeSuppIns.GroupBy(i => i.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
                    var carInsByEmployee = activeCarIns.GroupBy(i => i.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());

                    computedResults = await Task.Run(() =>
                    {
                        var dict = new Dictionary<int, PayrollResult>();

                        foreach (DataRow row in savedDt.Rows)
                        {
                            int empId = Convert.ToInt32(row["EmployeeId"]);
                            if (!employeesById.TryGetValue(empId, out var emp)) continue;

                            decimal dbBaseSalary = row["BaseSalary"] == DBNull.Value ? 0m : Convert.ToDecimal(row["BaseSalary"]);
                            decimal dbOvertimePay = row.Table.Columns.Contains("OvertimePay") && row["OvertimePay"] != DBNull.Value ? Convert.ToDecimal(row["OvertimePay"]) : 0m;
                            decimal dbTotalEarnings = row["TotalEarnings"] == DBNull.Value ? 0m : Convert.ToDecimal(row["TotalEarnings"]);
                            decimal dbInsuranceEmp = row["InsuranceEmployee"] == DBNull.Value ? 0m : Convert.ToDecimal(row["InsuranceEmployee"]);
                            decimal dbInsuranceEmpr = row["InsuranceEmployer"] == DBNull.Value ? 0m : Convert.ToDecimal(row["InsuranceEmployer"]);
                            decimal dbIncomeTax = row["IncomeTax"] == DBNull.Value ? 0m : Convert.ToDecimal(row["IncomeTax"]);
                            decimal dbLoanDed = row["LoanDeduction"] == DBNull.Value ? 0m : Convert.ToDecimal(row["LoanDeduction"]);
                            decimal dbSuppInsDed = row["SupplementaryInsuranceDeduction"] == DBNull.Value ? 0m : Convert.ToDecimal(row["SupplementaryInsuranceDeduction"]);
                            decimal dbCarInsDed = row["CarInsuranceDeduction"] == DBNull.Value ? 0m : Convert.ToDecimal(row["CarInsuranceDeduction"]);
                            decimal dbOtherDed = row["OtherDeductions"] == DBNull.Value ? 0m : Convert.ToDecimal(row["OtherDeductions"]);
                            decimal dbAbsenceDed = row["AbsenceDeduction"] == DBNull.Value ? 0m : Convert.ToDecimal(row["AbsenceDeduction"]);
                            decimal dbTotalDed = row["TotalDeductions"] == DBNull.Value ? 0m : Convert.ToDecimal(row["TotalDeductions"]);
                            decimal dbNetPay = row["NetPay"] == DBNull.Value ? 0m : Convert.ToDecimal(row["NetPay"]);

                            PayrollResult res;

                            if (settings != null)
                            {
                                var att = attendances.Find(a => a.EmployeeId == emp.Id) ?? new Attendance
                                {
                                    EmployeeId = emp.Id,
                                    Year = year,
                                    Month = month,
                                    WorkingDays = 30,
                                    OvertimeHours = 0,
                                    AbsenceDays = 0
                                };

                                decimal loanInstallment = 0;
                                if (loansByEmployee.TryGetValue(emp.Id, out var empLoans) && empLoans.Count > 0)
                                    loanInstallment = Math.Min(empLoans.Sum(l => l.MonthlyInstallment), empLoans.Sum(l => l.RemainingAmount));

                                decimal suppInsInstallment = 0;
                                if (suppInsByEmployee.TryGetValue(emp.Id, out var empIns) && empIns.Count > 0)
                                    suppInsInstallment = Math.Min(empIns.Sum(i => i.MonthlyInstallment), empIns.Sum(i => i.RemainingAmount));

                                decimal carInsInstallment = 0;
                                if (carInsByEmployee.TryGetValue(emp.Id, out var empCarIns) && empCarIns.Count > 0)
                                    carInsInstallment = Math.Min(empCarIns.Sum(i => i.MonthlyInstallment), empCarIns.Sum(i => i.RemainingAmount));

                                res = _calculatorService.CalculatePayroll(
                                    emp, att, settings, loanInstallment, suppInsInstallment, carInsInstallment, dbOtherDed, taxBrackets);
                            }
                            else
                            {
                                res = new PayrollResult
                                {
                                    EmployeeId = emp.Id,
                                    EmployeeName = emp.FullName,
                                    PersonnelCode = emp.PersonnelCode ?? "",
                                    Year = year,
                                    Month = month,
                                    PersonnelType = emp.PersonnelType ?? "ماهانه",
                                    DailyWage = emp.DailyWage,
                                    HourlyWage = emp.HourlyWage
                                };
                            }

                            res.BaseSalary = dbBaseSalary;
                            res.OvertimePay = dbOvertimePay;
                            res.TotalEarnings = dbTotalEarnings;
                            res.InsuranceEmployee = dbInsuranceEmp;
                            res.InsuranceEmployer = dbInsuranceEmpr;
                            res.IncomeTax = dbIncomeTax;
                            res.LoanDeduction = dbLoanDed;
                            res.SupplementaryInsuranceDeduction = dbSuppInsDed;
                            res.CarInsuranceDeduction = dbCarInsDed;
                            res.OtherDeductions = dbOtherDed;
                            res.AbsenceDeduction = dbAbsenceDed;
                            res.TotalDeductions = dbTotalDed;
                            res.NetPay = dbNetPay;

                            dict[empId] = res;
                        }

                        return dict;
                    });
                }, "در حال بارگذاری سوابق محاسبه‌شده...");

                if (!success) return;

                _currentYear = year;
                _currentMonth = month;

                if (employeesList != null) _currentEmployees = employeesList;

                if (!hasSaved || computedResults == null || computedResults.Count == 0)
                {
                    RefreshAllSummaries();
                    return;
                }

                _dtContractual = CreatePayrollDataTable();
                _dtDaily = CreatePayrollDataTable();
                _dtHourly = CreatePayrollDataTable();

                foreach (var res in computedResults.Values)
                {
                    _resultsByCode[res.PersonnelCode ?? ""] = res;
                    var group = GetTypeGroup(res.PersonnelType);
                    var target = group switch
                    {
                        TabKey.Daily => _dtDaily,
                        TabKey.Hourly => _dtHourly,
                        _ => _dtContractual
                    };
                    target.Rows.Add(BuildRowValues(res));
                }

                dgvContractual.DataSource = null;
                dgvContractual.DataSource = _dtContractual;
                ConfigurePayrollGrid(dgvContractual, TabKey.Contractual);

                dgvDaily.DataSource = null;
                dgvDaily.DataSource = _dtDaily;
                ConfigurePayrollGrid(dgvDaily, TabKey.Daily);

                dgvHourly.DataSource = null;
                dgvHourly.DataSource = _dtHourly;
                ConfigurePayrollGrid(dgvHourly, TabKey.Hourly);

                btnSave.Enabled = true;
                btnRecalcContractual.Enabled = _dtContractual.Rows.Count > 0;
                btnRecalcDaily.Enabled = _dtDaily.Rows.Count > 0;
                btnRecalcHourly.Enabled = _dtHourly.Rows.Count > 0;
                chkAllContractual.Enabled = _dtContractual.Rows.Count > 0;
                chkAllDaily.Enabled = _dtDaily.Rows.Count > 0;
                chkAllHourly.Enabled = _dtHourly.Rows.Count > 0;

                _isLoadedFromDb = true;
                RefreshAllSummaries();

                ToastHelper.Show(
                    $"سوابق قبلی بارگذاری شد. پرسنل: {_dtContractual.Rows.Count} | روزمزد: {_dtDaily.Rows.Count} | ساعتی: {_dtHourly.Rows.Count}",
                    isSuccess: true);
            }
            finally
            {
                if (loadLockAcquired) { try { _loadLock.Release(); } catch { } }
                try { _actionLock.Release(); } catch { }
            }
        }

        private async void BtnCalculateAll_Click(object? sender, EventArgs e)
        {
            if (cmbYear.SelectedItem == null || cmbMonth.SelectedItem == null)
            {
                ModernMessageBox.ShowWarning("لطفاً سال و ماه را انتخاب کنید.");
                return;
            }

            if (!await _actionLock.WaitAsync(0))
            {
                ModernMessageBox.ShowWarning("یک عملیات دیگر در حال اجراست. لطفاً صبر کنید.");
                return;
            }

            try
            {
                int year = Convert.ToInt32(cmbYear.SelectedItem);
                int month = Convert.ToInt32(cmbMonth.SelectedItem);

                var (checkSuccess, hasSaved) = await AsyncHelper.RunWithLoading(this, async () =>
                    await _payrollRepository.HasPayrollsForMonthAsync(year, month),
                    "در حال بررسی سوابق...");

                if (!checkSuccess) return;

                if (hasSaved)
                {
                    var confirm = ModernMessageBox.AskConfirm(
                        "⚠️ برای این ماه سوابق ذخیره‌شده وجود دارد.\n\n" +
                        "«محاسبه همه» مقادیر ذخیره‌شده را بازنویسی می‌کند.\n" +
                        "اگر فقط می‌خواهید بعضی افراد را اصلاح کنید، ابتدا آن‌ها را تیک بزنید و «محاسبه مجدد» را بزنید.\n\n" +
                        "آیا می‌خواهید همه را از نو محاسبه کنید؟",
                        "تأیید محاسبه‌ی مجدد همه");
                    if (!confirm) return;
                }

                btnSave.Enabled = false;
                btnRecalcContractual.Enabled = false;
                btnRecalcDaily.Enabled = false;
                btnRecalcHourly.Enabled = false;
                chkAllContractual.Enabled = false;
                chkAllDaily.Enabled = false;
                chkAllHourly.Enabled = false;

                List<PayrollResult>? allResults = null;
                List<Employee>? employeesList = null;

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var settingsRepo = new SalarySettingsRepository(_connectionString);
                    var settings = await settingsRepo.GetByYearAsync(year);

                    if (settings == null)
                        throw new InvalidOperationException($"تنظیمات حقوق برای سال {year} یافت نشد. ابتدا از منوی تنظیمات، مبالغ سال را ذخیره کنید.");
                    if (settings.MinimumWage <= 0)
                        throw new InvalidOperationException("حداقل حقوق در تنظیمات صفر است. ابتدا تنظیمات سال را کامل کنید.");

                    var empRepo = new EmployeeRepository(_connectionString);
                    employeesList = await empRepo.GetEmployeesWithDetailsAsync("", false);

                    var attRepo = new AttendanceRepository(_connectionString);
                    var attendances = await attRepo.GetAttendanceForMonthAsync(year, month);

                    var activeLoans = await _loanRepository.GetActiveLoansAsync();
                    var activeSuppIns = await _suppInsuranceRepository.GetActiveInsurancesAsync();
                    var activeCarIns = await _carInsuranceRepository.GetActiveCarInsurancesAsync();

                    var taxBrackets = _calculatorService.GetTaxBracketsSafe(year);

                    var loansByEmployee = activeLoans.GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
                    var suppInsByEmployee = activeSuppIns.GroupBy(i => i.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
                    var carInsByEmployee = activeCarIns.GroupBy(i => i.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());

                    allResults = await Task.Run(() =>
                    {
                        var results = new List<PayrollResult>();
                        foreach (var emp in employeesList)
                        {
                            var att = attendances.Find(a => a.EmployeeId == emp.Id) ?? new Attendance
                            {
                                EmployeeId = emp.Id,
                                Year = year,
                                Month = month,
                                WorkingDays = 30,
                                OvertimeHours = 0,
                                AbsenceDays = 0
                            };

                            decimal loanInstallment = 0;
                            if (loansByEmployee.TryGetValue(emp.Id, out var empLoans) && empLoans.Count > 0)
                                loanInstallment = Math.Min(empLoans.Sum(l => l.MonthlyInstallment), empLoans.Sum(l => l.RemainingAmount));

                            decimal suppInsInstallment = 0;
                            if (suppInsByEmployee.TryGetValue(emp.Id, out var empIns) && empIns.Count > 0)
                                suppInsInstallment = Math.Min(empIns.Sum(i => i.MonthlyInstallment), empIns.Sum(i => i.RemainingAmount));

                            decimal carInsInstallment = 0;
                            if (carInsByEmployee.TryGetValue(emp.Id, out var empCarIns) && empCarIns.Count > 0)
                                carInsInstallment = Math.Min(empCarIns.Sum(i => i.MonthlyInstallment), empCarIns.Sum(i => i.RemainingAmount));

                            var result = _calculatorService.CalculatePayroll(
                                emp, att, settings, loanInstallment, suppInsInstallment, carInsInstallment, 0, taxBrackets);

                            results.Add(result);
                        }
                        return results;
                    });
                }, "در حال محاسبه حقوق پرسنل...");

                if (!success || allResults == null || employeesList == null) return;

                _currentEmployees = employeesList;
                _currentYear = year;
                _currentMonth = month;
                _resultsByCode.Clear();
                _isLoadedFromDb = false;

                _dtContractual = CreatePayrollDataTable();
                _dtDaily = CreatePayrollDataTable();
                _dtHourly = CreatePayrollDataTable();

                foreach (var res in allResults)
                {
                    _resultsByCode[res.PersonnelCode ?? ""] = res;
                    var group = GetTypeGroup(res.PersonnelType);
                    var target = group switch
                    {
                        TabKey.Daily => _dtDaily,
                        TabKey.Hourly => _dtHourly,
                        _ => _dtContractual
                    };
                    target.Rows.Add(BuildRowValues(res));
                }

                dgvContractual.DataSource = null;
                dgvContractual.DataSource = _dtContractual;
                ConfigurePayrollGrid(dgvContractual, TabKey.Contractual);

                dgvDaily.DataSource = null;
                dgvDaily.DataSource = _dtDaily;
                ConfigurePayrollGrid(dgvDaily, TabKey.Daily);

                dgvHourly.DataSource = null;
                dgvHourly.DataSource = _dtHourly;
                ConfigurePayrollGrid(dgvHourly, TabKey.Hourly);

                btnSave.Enabled = true;
                btnRecalcContractual.Enabled = _dtContractual.Rows.Count > 0;
                btnRecalcDaily.Enabled = _dtDaily.Rows.Count > 0;
                btnRecalcHourly.Enabled = _dtHourly.Rows.Count > 0;
                chkAllContractual.Enabled = _dtContractual.Rows.Count > 0;
                chkAllDaily.Enabled = _dtDaily.Rows.Count > 0;
                chkAllHourly.Enabled = _dtHourly.Rows.Count > 0;
                chkAllContractual.Checked = false;
                chkAllDaily.Checked = false;
                chkAllHourly.Checked = false;

                RefreshAllSummaries();

                ToastHelper.Show(
                    $"محاسبات انجام شد. پرسنل: {_dtContractual.Rows.Count} | روزمزد: {_dtDaily.Rows.Count} | ساعتی: {_dtHourly.Rows.Count}",
                    isSuccess: true);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "PayrollProcessForm.BtnCalculateAll_Click");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در محاسبه:\n" + ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }

        private async Task RecalculateSelectedAsync(TabKey key)
        {
            var dt = GetDt(key);
            if (dt == null || dt.Rows.Count == 0)
            {
                ModernMessageBox.ShowWarning("ابتدا «محاسبه همه» یا بارگذاری ماه را انجام دهید.");
                return;
            }

            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                var dgv = GetDgv(key);
                dgv.EndEdit();

                var selectedCodes = new HashSet<string>(StringComparer.Ordinal);
                var otherDeductionsByCode = new Dictionary<string, decimal>(StringComparer.Ordinal);

                foreach (DataRow row in dt.Rows)
                {
                    if (row["Selected"] is not bool b || !b) continue;
                    string code = row["PersonnelCode"]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(code)) continue;

                    selectedCodes.Add(code);
                    otherDeductionsByCode[code] = row["OtherDeductions"] != DBNull.Value
                        ? Convert.ToDecimal(row["OtherDeductions"]) : 0m;
                }

                if (selectedCodes.Count == 0)
                {
                    ModernMessageBox.ShowWarning("لطفاً حداقل یک کارمند را انتخاب کنید.");
                    return;
                }

                int year = _currentYear;
                int month = _currentMonth;

                List<(string Code, PayrollResult Result)>? recalcResults = null;

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var settingsRepo = new SalarySettingsRepository(_connectionString);
                    var settings = await settingsRepo.GetByYearAsync(year);
                    if (settings == null)
                        throw new InvalidOperationException($"تنظیمات حقوق سال {year} یافت نشد.");

                    var empRepo = new EmployeeRepository(_connectionString);
                    var employeesList = await empRepo.GetEmployeesWithDetailsAsync("", false);

                    var attRepo = new AttendanceRepository(_connectionString);
                    var attendances = await attRepo.GetAttendanceForMonthAsync(year, month);

                    var activeLoans = await _loanRepository.GetActiveLoansAsync();
                    var activeSuppIns = await _suppInsuranceRepository.GetActiveInsurancesAsync();
                    var activeCarIns = await _carInsuranceRepository.GetActiveCarInsurancesAsync();

                    var taxBrackets = _calculatorService.GetTaxBracketsSafe(year);

                    var loansByEmployee = activeLoans.GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
                    var suppInsByEmployee = activeSuppIns.GroupBy(i => i.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
                    var carInsByEmployee = activeCarIns.GroupBy(i => i.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());

                    var employeesByCode = employeesList
                        .Where(emp => selectedCodes.Contains(emp.PersonnelCode ?? ""))
                        .ToDictionary(emp => emp.PersonnelCode ?? "", emp => emp, StringComparer.Ordinal);

                    recalcResults = await Task.Run(() =>
                    {
                        var list = new List<(string, PayrollResult)>();

                        foreach (var code in selectedCodes)
                        {
                            if (!employeesByCode.TryGetValue(code, out var emp)) continue;

                            var att = attendances.Find(a => a.EmployeeId == emp.Id) ?? new Attendance
                            {
                                EmployeeId = emp.Id,
                                Year = year,
                                Month = month,
                                WorkingDays = 30,
                                OvertimeHours = 0,
                                AbsenceDays = 0
                            };

                            decimal loanInstallment = 0;
                            if (loansByEmployee.TryGetValue(emp.Id, out var empLoans) && empLoans.Count > 0)
                                loanInstallment = Math.Min(empLoans.Sum(l => l.MonthlyInstallment), empLoans.Sum(l => l.RemainingAmount));

                            decimal suppInsInstallment = 0;
                            if (suppInsByEmployee.TryGetValue(emp.Id, out var empIns) && empIns.Count > 0)
                                suppInsInstallment = Math.Min(empIns.Sum(i => i.MonthlyInstallment), empIns.Sum(i => i.RemainingAmount));

                            decimal carInsInstallment = 0;
                            if (carInsByEmployee.TryGetValue(emp.Id, out var empCarIns) && empCarIns.Count > 0)
                                carInsInstallment = Math.Min(empCarIns.Sum(i => i.MonthlyInstallment), empCarIns.Sum(i => i.RemainingAmount));

                            otherDeductionsByCode.TryGetValue(code, out decimal preservedOtherDed);

                            var result = _calculatorService.CalculatePayroll(
                                emp, att, settings, loanInstallment, suppInsInstallment, carInsInstallment, preservedOtherDed, taxBrackets);

                            list.Add((code, result));
                        }
                        return list;
                    });

                    _currentEmployees = employeesList;
                }, $"در حال محاسبه مجدد {selectedCodes.Count} نفر...");

                if (!success || recalcResults == null) return;

                var recalcDict = recalcResults.ToDictionary(x => x.Code, x => x.Result, StringComparer.Ordinal);
                foreach (DataRow row in dt.Rows)
                {
                    string code = row["PersonnelCode"]?.ToString() ?? "";
                    if (!recalcDict.TryGetValue(code, out var newResult)) continue;

                    ApplyResultToRow(row, newResult);
                    _resultsByCode[code] = newResult;
                }

                dgv.Refresh();
                btnSave.Enabled = true;
                RefreshSummary(key);

                ToastHelper.Show($"{recalcResults.Count} نفر با موفقیت محاسبه مجدد شدند.", isSuccess: true);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "PayrollProcessForm.RecalculateSelectedAsync");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در محاسبه مجدد:\n" + ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (!await _actionLock.WaitAsync(0)) return;

            try
            {
                if (cmbYear.SelectedItem == null || cmbMonth.SelectedItem == null)
                {
                    ModernMessageBox.ShowWarning("سال و ماه را انتخاب کنید.");
                    return;
                }

                int selectedYear = Convert.ToInt32(cmbYear.SelectedItem);
                int selectedMonth = Convert.ToInt32(cmbMonth.SelectedItem);

                if (selectedYear != _currentYear || selectedMonth != _currentMonth)
                {
                    ModernMessageBox.ShowWarning(
                        "⚠️ ماه/سال انتخاب‌شده با داده‌های نمایش‌داده‌شده تطابق ندارد.\n\n" +
                        $"سال/ماه انتخاب‌شده: {selectedYear}/{selectedMonth}\n" +
                        $"سال/ماه داده‌های موجود: {_currentYear}/{_currentMonth}\n\n" +
                        "لطفاً پس از تغییر ماه/سال، صبر کنید تا داده‌ها بارگذاری شوند، یا دوباره «محاسبه همه» را بزنید.");
                    return;
                }

                var allRows = new List<DataRow>();
                if (_dtContractual != null) allRows.AddRange(_dtContractual.Rows.Cast<DataRow>());
                if (_dtDaily != null) allRows.AddRange(_dtDaily.Rows.Cast<DataRow>());
                if (_dtHourly != null) allRows.AddRange(_dtHourly.Rows.Cast<DataRow>());

                if (allRows.Count == 0)
                {
                    ModernMessageBox.ShowWarning("ابتدا «محاسبه همه» را بزنید یا ماه دارای سابقه را بارگذاری کنید.");
                    return;
                }

                dgvContractual.EndEdit();
                dgvDaily.EndEdit();
                dgvHourly.EndEdit();

                int year = _currentYear;
                int month = _currentMonth;

                var (checkSuccess, alreadySaved) = await AsyncHelper.RunWithLoading(this, async () =>
                    await _payrollRepository.HasPayrollsForMonthAsync(year, month),
                    "در حال بررسی سوابق...");

                if (!checkSuccess) return;

                if (alreadySaved)
                {
                    var result = ModernMessageBox.AskConfirm(
                        "⚠️ فیش‌های حقوقی این ماه قبلاً ذخیره شده‌اند!\n\n" +
                        "با تایید شما:\n" +
                        "  • اقساط کسر شده در فیش‌های قبلی به حساب بدهی کارمندان برگردانده می‌شود\n" +
                        "  • فیش‌ها با مقادیر جدید بازنویسی می‌شوند\n" +
                        "  • اقساط جدید (مطابق محاسبه فعلی) کسر می‌گردد\n\n" +
                        "آیا ادامه می‌دهید؟",
                        "هشدار ذخیره مجدد");
                    if (!result) return;
                }

                decimal totalStaffNetPay = _dtContractual != null
                    ? _dtContractual.AsEnumerable().Sum(r => r["NetPay"] == DBNull.Value ? 0m : Convert.ToDecimal(r["NetPay"]))
                    : 0m;
                decimal totalWorkerNetPay = (_dtDaily != null
                    ? _dtDaily.AsEnumerable().Sum(r => r["NetPay"] == DBNull.Value ? 0m : Convert.ToDecimal(r["NetPay"]))
                    : 0m) +
                    (_dtHourly != null
                    ? _dtHourly.AsEnumerable().Sum(r => r["NetPay"] == DBNull.Value ? 0m : Convert.ToDecimal(r["NetPay"]))
                    : 0m);

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var records = new List<PayrollRecord>();
                    foreach (var row in allRows)
                    {
                        records.Add(new PayrollRecord
                        {
                            EmployeeId = Convert.ToInt32(row["EmployeeId"]),
                            Year = year,
                            Month = month,
                            BaseSalary = GetDecimal(row, "BaseSalary"),
                            // 👈 مبلغ اضافه کار به دیتابیس فرستاده می‌شود
                            OvertimePay = GetDecimal(row, "OvertimePay"),
                            TotalEarnings = GetDecimal(row, "TotalEarnings"),
                            TotalDeductions = GetDecimal(row, "TotalDeductions"),
                            NetPay = GetDecimal(row, "NetPay"),
                            InsuranceEmployee = GetDecimal(row, "InsuranceEmployee"),
                            InsuranceEmployer = GetDecimal(row, "InsuranceEmployer"),
                            IncomeTax = GetDecimal(row, "IncomeTax"),
                            LoanDeduction = GetDecimal(row, "LoanDeduction"),
                            SupplementaryInsuranceDeduction = GetDecimal(row, "SupplementaryInsuranceDeduction"),
                            CarInsuranceDeduction = GetDecimal(row, "CarInsuranceDeduction"),
                            OtherDeductions = GetDecimal(row, "OtherDeductions"),
                            AbsenceDeduction = GetDecimal(row, "AbsenceDeduction")
                        });
                    }

                    var loanDeductions = records.Where(r => r.LoanDeduction > 0).GroupBy(r => r.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(x => x.LoanDeduction));
                    var suppInsDeductions = records.Where(r => r.SupplementaryInsuranceDeduction > 0).GroupBy(r => r.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(x => x.SupplementaryInsuranceDeduction));
                    var carInsDeductions = records.Where(r => r.CarInsuranceDeduction > 0).GroupBy(r => r.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(x => x.CarInsuranceDeduction));

                    if (alreadySaved)
                    {
                        await _payrollRepository.ReSavePayrollsForMonthWithInstallmentRestoreAsync(
                            year, month, records,
                            loanDeductions, suppInsDeductions, carInsDeductions);
                    }
                    else
                    {
                        await _payrollRepository.SavePayrollsAndDeductInstallmentsAsync(
                            records, loanDeductions, suppInsDeductions, carInsDeductions);
                    }
                }, alreadySaved
                    ? "در حال بازنویسی فیش‌ها و بازمحاسبه اقساط..."
                    : "در حال ذخیره فیش‌های حقوقی...");

                if (success)
                {
                    string msg = alreadySaved
                        ? "فیش‌ها بازنویسی و اقساط به‌درستی بازمحاسبه شدند."
                        : "تمام فیش‌های حقوقی ذخیره شدند و اقساط کسر گردید.";
                    ToastHelper.Show(msg, isSuccess: true);
                    btnSave.Enabled = false;
                    _isLoadedFromDb = true;

                    if (!alreadySaved)
                    {
                        await AskForPayrollTransactionAsync(totalStaffNetPay, totalWorkerNetPay);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "PayrollProcessForm.BtnSave_Click");
                if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره:\n" + ex.Message);
            }
            finally
            {
                try { _actionLock.Release(); } catch { }
            }
        }

        private async Task AskForPayrollTransactionAsync(decimal staffNetPay, decimal workerNetPay)
        {
            if (staffNetPay <= 0 && workerNetPay <= 0) return;

            var accounts = await _bankRepo.GetActiveAsync();
            if (accounts == null || accounts.Count == 0)
            {
                ModernMessageBox.ShowWarning("هیچ حساب بانکی فعالی برای ثبت هزینه حقوق یافت نشد.");
                return;
            }

            using var form = new Form();
            form.Text = "ثبت خودکار هزینه حقوق و دستمزد در نقدینگی";
            form.Size = new Size(600, 380);
            form.StartPosition = FormStartPosition.CenterParent;
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.MaximizeBox = false;
            form.MinimizeBox = false;
            form.RightToLeft = RightToLeft.Yes;
            form.RightToLeftLayout = true;
            form.Font = UIHelper.PersianFont(11f);
            form.BackColor = Color.White;

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(15) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var lblMessage = new Label
            {
                Text = "فیش‌های حقوقی با موفقیت ذخیره شدند.\nآیا مایلید هزینه‌های زیر به صورت خودکار از حساب‌های بانکی کسر شوند؟",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText
            };

            var pnlStaff = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            pnlStaff.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            pnlStaff.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            pnlStaff.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

            var pnlStaffHeader = new Panel { Dock = DockStyle.Fill };
            pnlStaffHeader.Controls.Add(new Label { Text = "👨‍💼 حقوق پرسنل (ماهانه)", Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.SectionBlue, Dock = DockStyle.Top, Height = 25 });
            pnlStaffHeader.Controls.Add(new Label { Text = $"{NumberHelper.FormatThousands(staffNetPay, usePersianDigits: true)} ریال", Font = UIHelper.PersianFont(12f, FontStyle.Bold), Dock = DockStyle.Bottom, Height = 25 });

            var cmbStaffAcc = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "DisplayTitle", ValueMember = "Id", DataSource = new List<CompanyBankAccount>(accounts), Margin = new Padding(5, 25, 5, 25) };
            var chkStaff = new CheckBox { Text = "ثبت شود", AutoSize = true, Checked = staffNetPay > 0, Enabled = staffNetPay > 0, Dock = DockStyle.Fill, Margin = new Padding(10, 25, 5, 0) };

            pnlStaff.Controls.Add(pnlStaffHeader, 0, 0);
            pnlStaff.Controls.Add(cmbStaffAcc, 1, 0);
            pnlStaff.Controls.Add(chkStaff, 2, 0);

            var pnlWorker = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            pnlWorker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            pnlWorker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            pnlWorker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

            var pnlWorkerHeader = new Panel { Dock = DockStyle.Fill };
            pnlWorkerHeader.Controls.Add(new Label { Text = "👷 دستمزد کارگران (روزمزد/ساعتی)", Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.SectionOrange, Dock = DockStyle.Top, Height = 25 });
            pnlWorkerHeader.Controls.Add(new Label { Text = $"{NumberHelper.FormatThousands(workerNetPay, usePersianDigits: true)} ریال", Font = UIHelper.PersianFont(12f, FontStyle.Bold), Dock = DockStyle.Bottom, Height = 25 });

            var cmbWorkerAcc = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "DisplayTitle", ValueMember = "Id", DataSource = new List<CompanyBankAccount>(accounts), Margin = new Padding(5, 25, 5, 25) };
            var chkWorker = new CheckBox { Text = "ثبت شود", AutoSize = true, Checked = workerNetPay > 0, Enabled = workerNetPay > 0, Dock = DockStyle.Fill, Margin = new Padding(10, 25, 5, 0) };

            pnlWorker.Controls.Add(pnlWorkerHeader, 0, 0);
            pnlWorker.Controls.Add(cmbWorkerAcc, 1, 0);
            pnlWorker.Controls.Add(chkWorker, 2, 0);

            var btnConfirm = UIHelper.CreateButton("✔ تایید و کسر از حساب‌ها", UIHelper.ActionGreen, 220, 45);
            btnConfirm.Anchor = AnchorStyles.None;

            mainLayout.Controls.Add(lblMessage, 0, 0);
            mainLayout.Controls.Add(pnlStaff, 0, 1);
            mainLayout.Controls.Add(pnlWorker, 0, 2);
            mainLayout.Controls.Add(btnConfirm, 0, 3);

            form.Controls.Add(mainLayout);

            bool processStaff = false, processWorker = false;
            CompanyBankAccount? staffAcc = null, workerAcc = null;

            btnConfirm.Click += (s, e) =>
            {
                if (chkStaff.Checked && cmbStaffAcc.SelectedItem is CompanyBankAccount sAcc)
                {
                    processStaff = true;
                    staffAcc = sAcc;
                }
                if (chkWorker.Checked && cmbWorkerAcc.SelectedItem is CompanyBankAccount wAcc)
                {
                    processWorker = true;
                    workerAcc = wAcc;
                }
                form.DialogResult = DialogResult.OK;
                form.Close();
            };

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var transRepo = new CashTransactionRepository(_connectionString);

                    if (processStaff && staffAcc != null)
                    {
                        await transRepo.AddPayrollTransactionAsync(staffAcc.Id, staffAcc.Title, staffNetPay, _currentYear, _currentMonth, $"پرداخت حقوق پرسنل (ماهانه) - {_currentMonth}/{_currentYear}");
                    }

                    if (processWorker && workerAcc != null)
                    {
                        await transRepo.AddPayrollTransactionAsync(workerAcc.Id, workerAcc.Title, workerNetPay, _currentYear, _currentMonth, $"پرداخت دستمزد کارگران (روزمزد/ساعتی) - {_currentMonth}/{_currentYear}");
                    }
                }, "در حال ثبت هزینه‌ها در نقدینگی...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("هزینه‌های حقوق و دستمزد با موفقیت از حساب‌ها کسر شدند.", isSuccess: true);
                }
            }
        }

        private void HandleOtherDeductionsEdit(DataGridView dgv, TabKey key, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgv.Columns[e.ColumnIndex].Name != "OtherDeductions") return;

            string valStr = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "0";
            decimal newDeduction = NumberHelper.ParseFormatted(valStr);

            if (newDeduction < 0)
            {
                ModernMessageBox.ShowWarning("مبلغ سایر کسورات نمی‌تواند منفی باشد.");
                newDeduction = 0;
            }

            if (dgv.Rows[e.RowIndex].DataBoundItem is not DataRowView drv) return;
            string code = drv["PersonnelCode"]?.ToString() ?? "";
            if (!_resultsByCode.TryGetValue(code, out var res)) return;

            decimal oldDeduction = res.OtherDeductions;
            decimal diff = newDeduction - oldDeduction;

            res.OtherDeductions = newDeduction;
            res.TotalDeductions += diff;
            res.NetPay -= diff;
            if (res.NetPay < 0) res.NetPay = 0;

            drv["OtherDeductions"] = newDeduction;
            drv["TotalDeductions"] = res.TotalDeductions;
            drv["NetPay"] = res.NetPay;

            dgv.InvalidateRow(e.RowIndex);
            RefreshSummary(key);
        }

        private void ToggleSelectAll(TabKey key)
        {
            var dt = GetDt(key);
            var dgv = GetDgv(key);
            var chk = GetChkAll(key);
            if (dt == null || dgv == null || chk == null) return;

            foreach (DataRow row in dt.Rows) row["Selected"] = chk.Checked;
            dgv.Refresh();
            RefreshSummary(key);
        }

        private void RefreshAllSummaries()
        {
            RefreshSummary(TabKey.Contractual);
            RefreshSummary(TabKey.Daily);
            RefreshSummary(TabKey.Hourly);
        }

        private void RefreshSummary(TabKey key)
        {
            var dt = GetDt(key);
            var lbl = GetLblSummary(key);
            if (lbl == null) return;

            if (dt == null || dt.Rows.Count == 0)
            {
                lbl.Text = "— موردی یافت نشد —";
                return;
            }

            int total = dt.Rows.Count;
            int selected = dt.Rows.Cast<DataRow>().Count(r => r["Selected"] is bool b && b);
            decimal totalNet = dt.Rows.Cast<DataRow>()
                .Sum(r => r["NetPay"] == DBNull.Value ? 0m : Convert.ToDecimal(r["NetPay"]));

            string prefix = _isLoadedFromDb ? "📂 (از دیتابیس) " : "";

            lbl.Text = $"{prefix}تعداد: {NumberHelper.FormatThousands(total, usePersianDigits: true)}  |  " +
                       $"انتخاب‌شده: {NumberHelper.FormatThousands(selected, usePersianDigits: true)}  |  " +
                       $"جمع خالص: {NumberHelper.FormatThousands(totalNet, usePersianDigits: true)} ریال";
        }

        private static TabKey GetTypeGroup(string? personnelType)
        {
            string t = StringHelper.NormalizePersianText(personnelType ?? "ماهانه").Replace(" ", "");
            if (t == "روزمزد") return TabKey.Daily;
            if (t == "ساعتی") return TabKey.Hourly;
            return TabKey.Contractual;
        }

        private DataTable? GetDt(TabKey key) => key switch
        {
            TabKey.Daily => _dtDaily,
            TabKey.Hourly => _dtHourly,
            _ => _dtContractual
        };

        private DataGridView GetDgv(TabKey key) => key switch
        {
            TabKey.Daily => dgvDaily,
            TabKey.Hourly => dgvHourly,
            _ => dgvContractual
        };

        private CheckBox GetChkAll(TabKey key) => key switch
        {
            TabKey.Daily => chkAllDaily,
            TabKey.Hourly => chkAllHourly,
            _ => chkAllContractual
        };

        private Label GetLblSummary(TabKey key) => key switch
        {
            TabKey.Daily => lblSummaryDaily,
            TabKey.Hourly => lblSummaryHourly,
            _ => lblSummaryContractual
        };

        private static decimal GetDecimal(DataRow row, string colName)
            => row.Table.Columns.Contains(colName) && row[colName] != DBNull.Value ? Convert.ToDecimal(row[colName]) : 0m;

        private static DataTable CreatePayrollDataTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("EmployeeId", typeof(int));
            dt.Columns.Add("Selected", typeof(bool));
            dt.Columns.Add("EmployeeName", typeof(string));
            dt.Columns.Add("PersonnelCode", typeof(string));
            dt.Columns.Add("PersonnelType", typeof(string));

            dt.Columns.Add("BaseSalary", typeof(decimal));
            dt.Columns.Add("YearsBonus", typeof(decimal));
            dt.Columns.Add("HousingAllowance", typeof(decimal));
            dt.Columns.Add("GroceryAllowance", typeof(decimal));
            dt.Columns.Add("ChildAllowance", typeof(decimal));
            dt.Columns.Add("AttractionAllowance", typeof(decimal));
            dt.Columns.Add("ResponsibilityAllowance", typeof(decimal));
            dt.Columns.Add("ManagementAllowance", typeof(decimal));
            dt.Columns.Add("TechnicalAllowance", typeof(decimal));
            dt.Columns.Add("MarriageAllowance", typeof(decimal));
            dt.Columns.Add("TransportationAllowance", typeof(decimal));
            dt.Columns.Add("OtherAllowances", typeof(decimal));

            dt.Columns.Add("OvertimePay", typeof(decimal));
            dt.Columns.Add("TotalEarnings", typeof(decimal));

            dt.Columns.Add("AbsenceDeduction", typeof(decimal));

            dt.Columns.Add("InsuranceEmployee", typeof(decimal));
            dt.Columns.Add("InsuranceEmployer", typeof(decimal));
            dt.Columns.Add("IncomeTax", typeof(decimal));
            dt.Columns.Add("LoanDeduction", typeof(decimal));
            dt.Columns.Add("SupplementaryInsuranceDeduction", typeof(decimal));
            dt.Columns.Add("CarInsuranceDeduction", typeof(decimal));
            dt.Columns.Add("OtherDeductions", typeof(decimal));
            dt.Columns.Add("TotalDeductions", typeof(decimal));
            dt.Columns.Add("NetPay", typeof(decimal));

            return dt;
        }

        private static object[] BuildRowValues(PayrollResult res)
        {
            return new object[]
            {
                res.EmployeeId, false, res.EmployeeName, res.PersonnelCode, res.PersonnelType,
                res.BaseSalary, res.YearsBonus, res.HousingAllowance, res.GroceryAllowance,
                res.ChildAllowance, res.AttractionAllowance, res.ResponsibilityAllowance,
                res.ManagementAllowance, res.TechnicalAllowance, res.MarriageAllowance,
                res.TransportationAllowance, res.OtherAllowances, res.OvertimePay, res.TotalEarnings,
                res.AbsenceDeduction,
                res.InsuranceEmployee, res.InsuranceEmployer, res.IncomeTax, res.LoanDeduction,
                res.SupplementaryInsuranceDeduction, res.CarInsuranceDeduction, res.OtherDeductions,
                res.TotalDeductions, res.NetPay
            };
        }

        private static void ApplyResultToRow(DataRow row, PayrollResult res)
        {
            row["BaseSalary"] = res.BaseSalary;
            row["YearsBonus"] = res.YearsBonus;
            row["HousingAllowance"] = res.HousingAllowance;
            row["GroceryAllowance"] = res.GroceryAllowance;
            row["ChildAllowance"] = res.ChildAllowance;
            row["AttractionAllowance"] = res.AttractionAllowance;
            row["ResponsibilityAllowance"] = res.ResponsibilityAllowance;
            row["ManagementAllowance"] = res.ManagementAllowance;
            row["TechnicalAllowance"] = res.TechnicalAllowance;
            row["MarriageAllowance"] = res.MarriageAllowance;
            row["TransportationAllowance"] = res.TransportationAllowance;
            row["OtherAllowances"] = res.OtherAllowances;
            row["OvertimePay"] = res.OvertimePay;
            row["TotalEarnings"] = res.TotalEarnings;

            row["AbsenceDeduction"] = res.AbsenceDeduction;

            row["InsuranceEmployee"] = res.InsuranceEmployee;
            row["InsuranceEmployer"] = res.InsuranceEmployer;
            row["IncomeTax"] = res.IncomeTax;
            row["LoanDeduction"] = res.LoanDeduction;
            row["SupplementaryInsuranceDeduction"] = res.SupplementaryInsuranceDeduction;
            row["CarInsuranceDeduction"] = res.CarInsuranceDeduction;
            row["OtherDeductions"] = res.OtherDeductions;
            row["TotalDeductions"] = res.TotalDeductions;
            row["NetPay"] = res.NetPay;
        }

        private static void ConfigurePayrollGrid(DataGridView dgv, TabKey key)
        {
            if (dgv.Columns["EmployeeId"] != null) dgv.Columns["EmployeeId"].Visible = false;

            if (key == TabKey.Daily || key == TabKey.Hourly)
            {
                string[] alwaysZeroColumns =
                {
                    "YearsBonus", "HousingAllowance", "GroceryAllowance", "ChildAllowance",
                    "AttractionAllowance", "ResponsibilityAllowance", "ManagementAllowance",
                    "TechnicalAllowance", "MarriageAllowance", "TransportationAllowance",
                    "OtherAllowances"
                };
                foreach (var col in alwaysZeroColumns)
                    if (dgv.Columns[col] != null) dgv.Columns[col].Visible = false;
            }

            if (key == TabKey.Hourly && dgv.Columns["OvertimePay"] != null)
                dgv.Columns["OvertimePay"].Visible = false;

            if (dgv.Columns["Selected"] != null)
            {
                dgv.Columns["Selected"].HeaderText = "انتخاب";
                dgv.Columns["Selected"].ReadOnly = false;
                dgv.Columns["Selected"].DisplayIndex = 0;
                dgv.Columns["Selected"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                dgv.Columns["Selected"].Width = 60;
            }

            void H(string col, string header, bool readOnly = true)
            {
                if (dgv.Columns[col] == null) return;
                dgv.Columns[col].HeaderText = header;
                dgv.Columns[col].ReadOnly = readOnly;
            }

            H("EmployeeName", "نام و نام خانوادگی");
            H("PersonnelCode", "کد پرسنلی");
            H("PersonnelType", "نوع پرسنلی");
            H("BaseSalary", "حقوق پایه");
            H("YearsBonus", "پایه سنوات");
            H("HousingAllowance", "حق مسکن");
            H("GroceryAllowance", "حق خواربار");
            H("ChildAllowance", "حق اولاد");
            H("AttractionAllowance", "حق جذب");
            H("ResponsibilityAllowance", "حق مسئولیت");
            H("ManagementAllowance", "حق مدیریت");
            H("TechnicalAllowance", "حق فنی");
            H("MarriageAllowance", "حق تاهل");
            H("TransportationAllowance", "حق ایاب و ذهاب");
            H("OtherAllowances", "سایر مزایا");
            H("OvertimePay", "اضافه‌کاری");
            H("TotalEarnings", "جمع مزایا");

            H("AbsenceDeduction", "کسر کار");

            H("InsuranceEmployee", "بیمه سهم کارمند");

            if (dgv.Columns["InsuranceEmployer"] != null)
                dgv.Columns["InsuranceEmployer"].Visible = false;

            H("IncomeTax", "مالیات");
            H("LoanDeduction", "کسر وام");
            H("SupplementaryInsuranceDeduction", "بیمه تکمیلی");
            H("CarInsuranceDeduction", "بیمه خودرو");
            H("OtherDeductions", "سایر کسورات (قابل ویرایش)", readOnly: false);
            H("TotalDeductions", "جمع کسورات");
            H("NetPay", "خالص پرداختی");

            if (dgv.Columns["OtherDeductions"] != null)
                dgv.Columns["OtherDeductions"].DefaultCellStyle.BackColor = UIHelper.WarmWarningBackground;
        }
    }
}