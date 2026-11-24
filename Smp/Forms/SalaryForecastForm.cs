using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using Smp.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class SalaryForecastForm : Form
{
    private readonly EmployeeRepository _empRepo;
    private readonly SalarySettingsRepository _settingsRepo;
    private readonly LoanRepository _loanRepo;
    private readonly SupplementaryInsuranceRepository _suppInsRepo;
    private readonly CarInsuranceRepository _carInsRepo;
    private readonly PayrollCalculatorService _calculator;

    private ComboBox cmbYear = null!, cmbMonth = null!;
    private Button btnCalculate = null!;
    private DataGridView dgvForecast = null!;

    private ModernStatCard lblTotalGross = null!, lblTotalInsurance = null!, lblTotalTax = null!, lblTotalNet = null!;

    public SalaryForecastForm(string connectionString)
    {
        _empRepo = new EmployeeRepository(connectionString);
        _settingsRepo = new SalarySettingsRepository(connectionString);
        _loanRepo = new LoanRepository(connectionString);
        _suppInsRepo = new SupplementaryInsuranceRepository(connectionString);
        _carInsRepo = new CarInsuranceRepository(connectionString);
        _calculator = new PayrollCalculatorService();

        BuildUI();
        this.ApplyStandardHeader("پیش‌بینی هزینه حقوق (Forecast)", new Size(1150, 750));
        ShowInTaskbar = false;

        ShortcutHelper.ApplyStandardShortcuts(this);
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

        cmbYear = new ModernComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Font = ModernTheme.GetFont(11f), Margin = new Padding(5, 8, 15, 5) };
        ComboBoxHelper.FillYears(cmbYear);

        cmbMonth = new ModernComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Font = ModernTheme.GetFont(11f), Margin = new Padding(5, 8, 15, 5) };
        ComboBoxHelper.FillMonths(cmbMonth);

        btnCalculate = ModernButton.Create("📊 محاسبه پیش‌بینی", ModernTheme.Purple, 160, 35);
        btnCalculate.Margin = new Padding(5, 3, 15, 5);
        btnCalculate.Click += async (s, e) => await CalculateSafeAsync();

        var lblHint = new Label
        {
            Text = "💡 پیش‌بینی بر اساس کارکرد استاندارد (۳۰ روز) + وام + بیمه تکمیلی + بیمه خودرو",
            AutoSize = true,
            ForeColor = ModernTheme.TextMuted,
            Font = ModernTheme.GetFont(9f),
            Margin = new Padding(5, 13, 5, 5)
        };

        topFlow.Controls.Add(new Label { Text = "سال:", AutoSize = true, Font = ModernTheme.GetFont(11f), Margin = new Padding(5, 13, 5, 5) });
        topFlow.Controls.Add(cmbYear);
        topFlow.Controls.Add(new Label { Text = "ماه:", AutoSize = true, Font = ModernTheme.GetFont(11f), Margin = new Padding(5, 13, 5, 5) });
        topFlow.Controls.Add(cmbMonth);
        topFlow.Controls.Add(btnCalculate);
        topFlow.Controls.Add(lblHint);

        topPanel.Controls.Add(topFlow);

        var summaryPanel = ModernReport.CreateStatStrip(4, 92);
        lblTotalGross = summaryPanel.Add("جمع مزایا (برآوردی)", ModernTheme.Primary);
        lblTotalInsurance = summaryPanel.Add("جمع بیمه", ModernTheme.Orange);
        lblTotalTax = summaryPanel.Add("جمع مالیات", ModernTheme.Danger);
        lblTotalNet = summaryPanel.Add("خالص پرداختی", ModernTheme.Success);

        dgvForecast = new DataGridView { Dock = DockStyle.Fill };
        dgvForecast.ApplyModernStyle();

        dgvForecast.EnableThousandsFormatting(includeIntegers: true);

        this.Controls.Add(dgvForecast);
        this.Controls.Add(summaryPanel);
        this.Controls.Add(topPanel);
    }


    private async Task CalculateSafeAsync()
    {
        if (cmbYear.SelectedItem is null || cmbMonth.SelectedItem is null)
        {
            ModernMessageBox.ShowWarning("لطفاً سال و ماه را انتخاب کنید.");
            return;
        }

        int year = Convert.ToInt32(cmbYear.SelectedItem);
        int month = Convert.ToInt32(cmbMonth.SelectedItem);

        List<PayrollResult>? results = null;
        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            var settings = await _settingsRepo.GetByYearAsync(year);
            if (settings is null)
                throw new InvalidOperationException($"تنظیمات حقوق برای سال {year} یافت نشد.");

            var employees = await _empRepo.GetEmployeesWithDetailsAsync("", false);
            var activeLoans = await _loanRepo.GetActiveLoansAsync();
            var activeSuppIns = await _suppInsRepo.GetActiveInsurancesAsync();
            var activeCarIns = await _carInsRepo.GetActiveCarInsurancesAsync();

            var loansByEmployee = activeLoans.Where(l => !l.IsAdvance).GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
            var advByEmployee = activeLoans.Where(l => l.IsAdvance && l.IsDueInPeriod(year, month)).GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
            var suppInsByEmployee = activeSuppIns.GroupBy(i => i.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
            var carInsByEmployee = activeCarIns.GroupBy(i => i.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());

            var taxBrackets = _calculator.GetTaxBracketsSafe(year);

            results = await Task.Run(() =>
            {
                var list = new List<PayrollResult>();
                foreach (var emp in employees)
                {
                    var att = new Attendance
                    {
                        EmployeeId = emp.Id,
                        Year = year,
                        Month = month,
                        WorkingDays = 30,
                        HoursWorked = 220,
                        OvertimeHours = 0,
                        AbsenceDays = 0
                    };

                    decimal loanInst = 0;
                    if (loansByEmployee.TryGetValue(emp.Id, out var empLoans) && empLoans.Count > 0)
                    {
                        loanInst = Math.Min(empLoans.Sum(l => l.MonthlyInstallment), empLoans.Sum(l => l.RemainingAmount));
                    }

                    decimal advInst = 0;
                    if (advByEmployee.TryGetValue(emp.Id, out var empAdv) && empAdv.Count > 0)
                    {
                        advInst = Math.Min(empAdv.Sum(l => l.MonthlyInstallment), empAdv.Sum(l => l.RemainingAmount));
                    }

                    decimal suppInsInst = 0;
                    if (suppInsByEmployee.TryGetValue(emp.Id, out var empIns) && empIns.Count > 0)
                    {
                        suppInsInst = Math.Min(empIns.Sum(i => i.MonthlyInstallment), empIns.Sum(i => i.RemainingAmount));
                    }

                    decimal carInsInst = 0;
                    if (carInsByEmployee.TryGetValue(emp.Id, out var empCarIns) && empCarIns.Count > 0)
                    {
                        carInsInst = Math.Min(empCarIns.Sum(i => i.MonthlyInstallment), empCarIns.Sum(i => i.RemainingAmount));
                    }

                    // فراخوانی متد با ۸ پارامتر
                    var result = _calculator.CalculatePayroll(
                        emp, att, settings, loanInst, suppInsInst, carInsInst, 0, taxBrackets, advInst);
                    list.Add(result);
                }
                return list;
            });
        }, "در حال محاسبه پیش‌بینی حقوق...");

        if (!success || results is null) return;

        dgvForecast.DataSource = null;
        dgvForecast.DataSource = results;

        if (dgvForecast.Columns.Contains("EmployeeId")) dgvForecast.Columns["EmployeeId"].Visible = false;
        if (dgvForecast.Columns.Contains("Year")) dgvForecast.Columns["Year"].Visible = false;
        if (dgvForecast.Columns.Contains("Month")) dgvForecast.Columns["Month"].Visible = false;

        decimal totalGross = results.Sum(r => r.TotalEarnings);
        decimal totalIns = results.Sum(r => r.InsuranceEmployee);
        decimal totalTax = results.Sum(r => r.IncomeTax);
        decimal totalNet = results.Sum(r => r.NetPay);

        lblTotalGross.Text = NumberHelper.FormatThousands(totalGross, usePersianDigits: true);
        lblTotalInsurance.Text = NumberHelper.FormatThousands(totalIns, usePersianDigits: true);
        lblTotalTax.Text = NumberHelper.FormatThousands(totalTax, usePersianDigits: true);
        lblTotalNet.Text = NumberHelper.FormatThousands(totalNet, usePersianDigits: true);

        ToastHelper.Show(
            $"پیش‌بینی برای {TranslationHelper.GetMonthName(month)} {StringHelper.ConvertToPersianDigits(year.ToString())} انجام شد.",
            isSuccess: true);
    }
}
