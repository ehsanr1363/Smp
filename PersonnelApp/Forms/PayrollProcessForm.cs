using PersonnelApp.Data;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using PersonnelApp.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonnelApp.Forms
{
    public class PayrollProcessForm : Form
    {
        private readonly string _connectionString;
        private readonly PayrollCalculatorService _calculatorService;
        private readonly PayrollRepository _payrollRepository;
        private readonly LoanRepository _loanRepository;
        private readonly SupplementaryInsuranceRepository _suppInsuranceRepository;
        private readonly CarInsuranceRepository _carInsuranceRepository;

        private ComboBox cmbYear = null!, cmbMonth = null!;
        private Button btnCalculate = null!, btnSave = null!;
        private DataGridView dgvResults = null!;
        private List<PayrollResult> _currentResults = null!;
        private List<Employee> _currentEmployees = new();

        public PayrollProcessForm(string connectionString)
        {
            _connectionString = connectionString;
            _calculatorService = new PayrollCalculatorService();
            _payrollRepository = new PayrollRepository(connectionString);
            _loanRepository = new LoanRepository(connectionString);
            _suppInsuranceRepository = new SupplementaryInsuranceRepository(connectionString);
            _carInsuranceRepository = new CarInsuranceRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("پردازش و محاسبه حقوق ماهانه", new Size(1600, 700)); // عرض فرم بسیار زیاد شد تا همه ستون‌ها جا شوند
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
            LoadDefaults();
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

            btnCalculate = UIHelper.CreateButton("محاسبه حقوق همه پرسنل", UIHelper.ActionGreen, 200, 35);
            btnCalculate.Margin = new Padding(5, 3, 15, 5);
            btnCalculate.Click += BtnCalculate_Click;

            btnSave = UIHelper.CreateButton("💾 ذخیره و کسر اقساط", UIHelper.SectionBlue, 160, 35);
            btnSave.Margin = new Padding(5, 3, 5, 5);
            btnSave.Enabled = false;
            btnSave.Click += BtnSave_Click;

            topFlow.Controls.Add(new Label { Text = "سال:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(cmbYear);
            topFlow.Controls.Add(new Label { Text = "ماه:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(cmbMonth);
            topFlow.Controls.Add(btnCalculate);
            topFlow.Controls.Add(btnSave);

            var lblHint = new Label
            {
                Text = "💡 برای وارد کردن «سایر کسورات»، روی ستون آن کلیک کنید.",
                AutoSize = true,
                Font = UIHelper.PersianFont(10f, FontStyle.Bold),
                ForeColor = UIHelper.WarmDanger,
                Margin = new Padding(20, 14, 5, 5)
            };
            topFlow.Controls.Add(lblHint);

            topPanel.Controls.Add(topFlow);

            dgvResults = new DataGridView { Dock = DockStyle.Fill, EditMode = DataGridViewEditMode.EditOnEnter };
            dgvResults.ApplyModernStyle();
            dgvResults.ReadOnly = false;
            dgvResults.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells; // فشرده شدن ستون‌ها برای جا شدن در صفحه

            dgvResults.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (e.Value is decimal dec)
                {
                    e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
            };

            dgvResults.CellEndEdit += DgvResults_CellEndEdit;

            dgvResults.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgvResults.Rows[e.RowIndex].DataBoundItem is System.Data.DataRowView row && _currentResults != null)
                {
                    string personnelCode = row["PersonnelCode"]?.ToString() ?? "";
                    var res = _currentResults.FirstOrDefault(r => r.PersonnelCode == personnelCode);
                    if (res != null)
                    {
                        using var form = new PayslipForm(res);
                        form.ShowDialog(this);
                    }
                }
            };

            this.Controls.Add(dgvResults);
            this.Controls.Add(topPanel);
        }

        private void DgvResults_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgvResults.Columns[e.ColumnIndex].Name != "OtherDeductions") return;

            string valStr = dgvResults.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "0";
            decimal newDeduction = NumberHelper.ParseFormatted(valStr);

            if (newDeduction < 0)
            {
                ModernMessageBox.ShowWarning("مبلغ سایر کسورات نمی‌تواند منفی باشد.");
                newDeduction = 0;
            }

            string personnelCode = dgvResults.Rows[e.RowIndex].Cells["PersonnelCode"].Value?.ToString() ?? "";
            var res = _currentResults.FirstOrDefault(r => r.PersonnelCode == personnelCode);

            if (res != null)
            {
                decimal oldDeduction = res.OtherDeductions;
                decimal diff = newDeduction - oldDeduction;

                res.OtherDeductions = newDeduction;
                res.TotalDeductions += diff;
                res.NetPay -= diff;
                if (res.NetPay < 0) res.NetPay = 0;

                var row = (DataRowView)dgvResults.Rows[e.RowIndex].DataBoundItem;
                row["OtherDeductions"] = newDeduction;
                row["TotalDeductions"] = res.TotalDeductions;
                row["NetPay"] = res.NetPay;

                dgvResults.InvalidateRow(e.RowIndex);
            }
        }

        private async void BtnCalculate_Click(object? sender, EventArgs e)
        {
            if (cmbYear.SelectedItem == null || cmbMonth.SelectedItem == null)
            {
                ModernMessageBox.ShowWarning("لطفاً سال و ماه را انتخاب کنید.");
                return;
            }

            int year = Convert.ToInt32(cmbYear.SelectedItem);
            int month = Convert.ToInt32(cmbMonth.SelectedItem);
            btnSave.Enabled = false;

            List<PayrollResult>? calculatedResults = null;
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

                calculatedResults = await Task.Run(() =>
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
                        {
                            loanInstallment = Math.Min(empLoans.Sum(l => l.MonthlyInstallment), empLoans.Sum(l => l.RemainingAmount));
                        }

                        decimal suppInsInstallment = 0;
                        if (suppInsByEmployee.TryGetValue(emp.Id, out var empIns) && empIns.Count > 0)
                        {
                            suppInsInstallment = Math.Min(empIns.Sum(i => i.MonthlyInstallment), empIns.Sum(i => i.RemainingAmount));
                        }

                        decimal carInsInstallment = 0;
                        if (carInsByEmployee.TryGetValue(emp.Id, out var empCarIns) && empCarIns.Count > 0)
                        {
                            carInsInstallment = Math.Min(empCarIns.Sum(i => i.MonthlyInstallment), empCarIns.Sum(i => i.RemainingAmount));
                        }

                        var result = _calculatorService.CalculatePayroll(
                            emp, att, settings, loanInstallment, suppInsInstallment, carInsInstallment, 0, taxBrackets);

                        results.Add(result);
                    }
                    return results;
                });
            }, "در حال محاسبه حقوق پرسنل...");

            if (!success || calculatedResults == null || employeesList == null) return;

            _currentResults = calculatedResults;
            _currentEmployees = employeesList;

            // 👇👇👇 ایجاد جدول با تمام ستون‌های تفکیکی 👇👇👇
            var displayTable = new System.Data.DataTable();
            displayTable.Columns.Add("EmployeeName", typeof(string));
            displayTable.Columns.Add("PersonnelCode", typeof(string));
            displayTable.Columns.Add("PersonnelType", typeof(string));

            displayTable.Columns.Add("BaseSalary", typeof(decimal));
            displayTable.Columns.Add("YearsBonus", typeof(decimal));
            displayTable.Columns.Add("HousingAllowance", typeof(decimal));
            displayTable.Columns.Add("GroceryAllowance", typeof(decimal));
            displayTable.Columns.Add("ChildAllowance", typeof(decimal));
            displayTable.Columns.Add("AttractionAllowance", typeof(decimal));
            displayTable.Columns.Add("ResponsibilityAllowance", typeof(decimal));
            displayTable.Columns.Add("ManagementAllowance", typeof(decimal));
            displayTable.Columns.Add("TechnicalAllowance", typeof(decimal));
            displayTable.Columns.Add("MarriageAllowance", typeof(decimal));
            displayTable.Columns.Add("TransportationAllowance", typeof(decimal));
            displayTable.Columns.Add("OtherAllowances", typeof(decimal));

            displayTable.Columns.Add("OvertimePay", typeof(decimal));
            displayTable.Columns.Add("TotalEarnings", typeof(decimal));
            displayTable.Columns.Add("InsuranceEmployee", typeof(decimal));
            displayTable.Columns.Add("IncomeTax", typeof(decimal));
            displayTable.Columns.Add("LoanDeduction", typeof(decimal));
            displayTable.Columns.Add("SupplementaryInsuranceDeduction", typeof(decimal));
            displayTable.Columns.Add("CarInsuranceDeduction", typeof(decimal));
            displayTable.Columns.Add("OtherDeductions", typeof(decimal));
            displayTable.Columns.Add("TotalDeductions", typeof(decimal));
            displayTable.Columns.Add("NetPay", typeof(decimal));

            foreach (var res in _currentResults)
            {
                displayTable.Rows.Add(
                    res.EmployeeName,
                    res.PersonnelCode,
                    res.PersonnelType,
                    res.BaseSalary,
                    res.YearsBonus,
                    res.HousingAllowance,
                    res.GroceryAllowance,
                    res.ChildAllowance,
                    res.AttractionAllowance,
                    res.ResponsibilityAllowance,
                    res.ManagementAllowance,
                    res.TechnicalAllowance,
                    res.MarriageAllowance,
                    res.TransportationAllowance,
                    res.OtherAllowances,
                    res.OvertimePay,
                    res.TotalEarnings,
                    res.InsuranceEmployee,
                    res.IncomeTax,
                    res.LoanDeduction,
                    res.SupplementaryInsuranceDeduction,
                    res.CarInsuranceDeduction,
                    res.OtherDeductions,
                    res.TotalDeductions,
                    res.NetPay);
            }

            dgvResults.DataSource = null;
            dgvResults.DataSource = displayTable;

            // تنظیم هدرها
            dgvResults.Columns["EmployeeName"].HeaderText = "نام و نام خانوادگی"; dgvResults.Columns["EmployeeName"].ReadOnly = true;
            dgvResults.Columns["PersonnelCode"].HeaderText = "کد پرسنلی"; dgvResults.Columns["PersonnelCode"].ReadOnly = true;
            dgvResults.Columns["PersonnelType"].HeaderText = "نوع پرسنلی"; dgvResults.Columns["PersonnelType"].ReadOnly = true;

            dgvResults.Columns["BaseSalary"].HeaderText = "حقوق پایه"; dgvResults.Columns["BaseSalary"].ReadOnly = true;
            dgvResults.Columns["YearsBonus"].HeaderText = "پایه سنوات"; dgvResults.Columns["YearsBonus"].ReadOnly = true;
            dgvResults.Columns["HousingAllowance"].HeaderText = "حق مسکن"; dgvResults.Columns["HousingAllowance"].ReadOnly = true;
            dgvResults.Columns["GroceryAllowance"].HeaderText = "حق خواربار"; dgvResults.Columns["GroceryAllowance"].ReadOnly = true;
            dgvResults.Columns["ChildAllowance"].HeaderText = "حق اولاد"; dgvResults.Columns["ChildAllowance"].ReadOnly = true;
            dgvResults.Columns["AttractionAllowance"].HeaderText = "حق جذب"; dgvResults.Columns["AttractionAllowance"].ReadOnly = true;
            dgvResults.Columns["ResponsibilityAllowance"].HeaderText = "حق مسئولیت"; dgvResults.Columns["ResponsibilityAllowance"].ReadOnly = true;
            dgvResults.Columns["ManagementAllowance"].HeaderText = "حق مدیریت"; dgvResults.Columns["ManagementAllowance"].ReadOnly = true;
            dgvResults.Columns["TechnicalAllowance"].HeaderText = "حق فنی"; dgvResults.Columns["TechnicalAllowance"].ReadOnly = true;
            dgvResults.Columns["MarriageAllowance"].HeaderText = "حق تاهل"; dgvResults.Columns["MarriageAllowance"].ReadOnly = true;
            dgvResults.Columns["TransportationAllowance"].HeaderText = "حق ایاب و ذهاب"; dgvResults.Columns["TransportationAllowance"].ReadOnly = true;
            dgvResults.Columns["OtherAllowances"].HeaderText = "سایر مزایا"; dgvResults.Columns["OtherAllowances"].ReadOnly = true;

            dgvResults.Columns["OvertimePay"].HeaderText = "اضافه‌کاری"; dgvResults.Columns["OvertimePay"].ReadOnly = true;
            dgvResults.Columns["TotalEarnings"].HeaderText = "جمع مزایا"; dgvResults.Columns["TotalEarnings"].ReadOnly = true;
            dgvResults.Columns["InsuranceEmployee"].HeaderText = "بیمه سهم کارمند"; dgvResults.Columns["InsuranceEmployee"].ReadOnly = true;
            dgvResults.Columns["IncomeTax"].HeaderText = "مالیات"; dgvResults.Columns["IncomeTax"].ReadOnly = true;
            dgvResults.Columns["LoanDeduction"].HeaderText = "کسر وام"; dgvResults.Columns["LoanDeduction"].ReadOnly = true;
            dgvResults.Columns["SupplementaryInsuranceDeduction"].HeaderText = "بیمه تکمیلی"; dgvResults.Columns["SupplementaryInsuranceDeduction"].ReadOnly = true;
            dgvResults.Columns["CarInsuranceDeduction"].HeaderText = "بیمه خودرو"; dgvResults.Columns["CarInsuranceDeduction"].ReadOnly = true;

            dgvResults.Columns["OtherDeductions"].HeaderText = "سایر کسورات (قابل ویرایش)";
            dgvResults.Columns["OtherDeductions"].ReadOnly = false;
            dgvResults.Columns["OtherDeductions"].DefaultCellStyle.BackColor = UIHelper.WarmWarningBackground;

            dgvResults.Columns["TotalDeductions"].HeaderText = "جمع کسورات"; dgvResults.Columns["TotalDeductions"].ReadOnly = true;
            dgvResults.Columns["NetPay"].HeaderText = "خالص پرداختی"; dgvResults.Columns["NetPay"].ReadOnly = true;

            btnSave.Enabled = true;
            ToastHelper.Show("محاسبات انجام شد. در صورت نیاز، سایر کسورات را در جدول ویرایش کنید.", isSuccess: true);
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (_currentResults == null || _currentResults.Count == 0) return;

            int year = _currentResults[0].Year;
            int month = _currentResults[0].Month;

            var (checkSuccess, alreadySaved) = await AsyncHelper.RunWithLoading(this, async () =>
            {
                return await _payrollRepository.HasPayrollsForMonthAsync(year, month);
            }, "در حال بررسی سوابق...");

            if (!checkSuccess) return;

            bool deductInstallments = true;

            if (alreadySaved)
            {
                var result = ModernMessageBox.AskConfirm(
                    "⚠️ فیش‌های حقوقی این ماه قبلاً ذخیره شده‌اند!\n\n" +
                    "آیا مایلید مقادیر جدید را جایگزین کنید؟\n\n" +
                    "توجه: اگر تایید کنید، فیش‌ها آپدیت می‌شوند و اقساط مجدداً کسر نخواهند شد.",
                    "هشدار ذخیره مجدد");

                if (!result) return;
                deductInstallments = false;
            }

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var recordsToSave = _currentResults.Select(res => new PayrollRecord
                {
                    EmployeeId = res.EmployeeId,
                    Year = res.Year,
                    Month = res.Month,
                    BaseSalary = res.BaseSalary,
                    TotalEarnings = res.TotalEarnings,
                    TotalDeductions = res.TotalDeductions,
                    NetPay = res.NetPay,
                    InsuranceEmployee = res.InsuranceEmployee,
                    InsuranceEmployer = res.InsuranceEmployer,
                    IncomeTax = res.IncomeTax,
                    LoanDeduction = res.LoanDeduction,
                    SupplementaryInsuranceDeduction = res.SupplementaryInsuranceDeduction,
                    CarInsuranceDeduction = res.CarInsuranceDeduction,
                    OtherDeductions = res.OtherDeductions
                }).ToList();

                var loanDeductions = deductInstallments
                    ? _currentResults.Where(r => r.LoanDeduction > 0).GroupBy(r => r.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(x => x.LoanDeduction))
                    : new Dictionary<int, decimal>();

                var suppInsDeductions = deductInstallments
                    ? _currentResults.Where(r => r.SupplementaryInsuranceDeduction > 0).GroupBy(r => r.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(x => x.SupplementaryInsuranceDeduction))
                    : new Dictionary<int, decimal>();

                var carInsDeductions = deductInstallments
                    ? _currentResults.Where(r => r.CarInsuranceDeduction > 0).GroupBy(r => r.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(x => x.CarInsuranceDeduction))
                    : new Dictionary<int, decimal>();

                await _payrollRepository.SavePayrollsAndDeductInstallmentsAsync(recordsToSave, loanDeductions, suppInsDeductions, carInsDeductions);
            }, "در حال ذخیره فیش‌های حقوقی...");

            if (success)
            {
                string msg = deductInstallments
                    ? "تمام فیش‌های حقوقی ذخیره شدند و اقساط کسر گردید."
                    : "فیش‌های حقوقی با موفقیت به‌روزرسانی شدند (اقساط مجدداً کسر نشد).";
                ToastHelper.Show(msg, isSuccess: true);
                btnSave.Enabled = false;
            }
        }
    }
}