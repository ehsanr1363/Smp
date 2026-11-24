using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using Smp.Services;

namespace Smp.Forms;

public class ReversePayrollForm : Form
{
    private readonly PayrollCalculatorService _calculatorService;
    private readonly SalarySettingsRepository _settingsRepo;

    private ComboBox cmbYear = null!, cmbMonth = null!, cmbMaritalStatus = null!;
    private NumericUpDown numChildren = null!, numYears = null!;
    private CheckBox chkHasInsurance = null!;
    private TextBox txtEmployeeName = null!, txtPersonnelCode = null!;

    private NumericUpDown numOvertimeHours = null!, numAbsenceHours = null!;
    private TextBox txtLoanDeduction = null!, txtOtherDeductions = null!;

    private TextBox txtForwardBase = null!;
    private Button btnCalcForward = null!, btnPrintForward = null!;

    private TextBox txtReverseNet = null!;
    private Button btnCalcReverse = null!, btnPrintReverse = null!;

    private DataGridView dgvForward = null!, dgvReverse = null!;

    private SalarySettings? _settings;
    private System.Collections.Generic.List<TaxBracketDto> _taxBrackets = new();

    private PayrollResult? _lastForwardResult;
    private PayrollResult? _lastReverseResult;

    public ReversePayrollForm(string connectionString)
    {
        _calculatorService = new PayrollCalculatorService();
        _settingsRepo = new SalarySettingsRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader("ماشین حساب حقوق و مهندسی معکوس", new Size(1300, 950));

        Shown += async (s, e) => await LoadSettingsAsync();
        cmbYear.SelectedIndexChanged += async (s, e) => await LoadSettingsAsync();
    }

    private void BuildUI()
    {
        int FH = ModernUI.FieldHeight;
        var pc = new PersianCalendar();

        // ───────────── کارت ۱: مشخصات کارمند و شرایط ─────────────
        var cardInfo = new ModernCard("👤 مشخصات کارمند", ModernTheme.Primary, 4, FH, FH);

        cmbYear = ModernUI.Combo();
        for (int i = 1400; i <= 1410; i++) cmbYear.Items.Add(i);
        cmbYear.SelectedItem = pc.GetYear(DateTime.Now);

        cmbMonth = ModernUI.Combo();
        for (int i = 1; i <= 12; i++) cmbMonth.Items.Add(i);
        cmbMonth.SelectedItem = pc.GetMonth(DateTime.Now);

        cmbMaritalStatus = ModernUI.Combo();
        cmbMaritalStatus.Items.AddRange(new object[] { "مجرد", "متاهل" });
        cmbMaritalStatus.SelectedIndex = 0;

        numChildren = new NumericUpDown { Minimum = 0, Maximum = 10, Value = 0, TextAlign = HorizontalAlignment.Center };
        numYears = new NumericUpDown { Minimum = 0, Maximum = 30, Value = 0, TextAlign = HorizontalAlignment.Center };

        var nameBox = ModernUI.Input();
        nameBox.Text = "کارمند نمونه";
        nameBox.TextAlign = HorizontalAlignment.Center;
        txtEmployeeName = nameBox.TextBox;

        var codeBox = ModernUI.Input();
        codeBox.Text = "10001";
        codeBox.TextAlign = HorizontalAlignment.Center;
        txtPersonnelCode = codeBox.TextBox;

        var chkIns = ModernUI.Check("بیمه تامین اجتماعی");
        chkIns.Checked = true;
        chkIns.Width = 200;
        chkHasInsurance = chkIns;

        cardInfo.Body.Controls.Add(ModernUI.Field("سال مالی", cmbYear), 0, 0);
        cardInfo.Body.Controls.Add(ModernUI.Field("ماه", cmbMonth), 1, 0);
        cardInfo.Body.Controls.Add(ModernUI.Field("وضعیت تاهل", cmbMaritalStatus), 2, 0);
        cardInfo.Body.Controls.Add(ModernUI.Field("تعداد فرزند", numChildren), 3, 0);
        cardInfo.Body.Controls.Add(ModernUI.Field("سابقه (سال)", numYears), 0, 1);
        cardInfo.Body.Controls.Add(ModernUI.Field("نام و نام خانوادگی", nameBox), 1, 1);
        cardInfo.Body.Controls.Add(ModernUI.Field("کد پرسنلی", codeBox), 2, 1);
        cardInfo.Body.Controls.Add(ModernUI.CenterInCell(chkIns, 8), 3, 1);

        // ───────────── کارت ۲: متغیرهای ماه ─────────────
        var cardVars = new ModernCard("⏱️ اضافه‌کار و کسورات ماه", ModernTheme.Warning, 4, FH);

        numOvertimeHours = new NumericUpDown { Minimum = 0, Maximum = 200, Value = 0, TextAlign = HorizontalAlignment.Center };
        numAbsenceHours = new NumericUpDown { Minimum = 0, Maximum = 200, Value = 0, TextAlign = HorizontalAlignment.Center };

        var loanBox = ModernUI.Money();
        loanBox.Text = "0";
        txtLoanDeduction = loanBox.TextBox;

        var otherBox = ModernUI.Money();
        otherBox.Text = "0";
        txtOtherDeductions = otherBox.TextBox;

        cardVars.Body.Controls.Add(ModernUI.Field("ساعت اضافه کار", numOvertimeHours), 0, 0);
        cardVars.Body.Controls.Add(ModernUI.Field("ساعت کسر کار", numAbsenceHours), 1, 0);
        cardVars.Body.Controls.Add(ModernUI.Field("کسر وام", loanBox), 2, 0);
        cardVars.Body.Controls.Add(ModernUI.Field("سایر کسورات", otherBox), 3, 0);

        // ───────────── کارت ۳ و ۴: دو ماشین‌حساب ─────────────
        const int BtnRow = 52;

        ModernButton MakeBtn(ModernButton b)
        {
            b.Dock = DockStyle.Fill;
            b.Margin = new Padding(6, 6, 6, 4);
            return b;
        }

        // ۱) خالص از روی حقوق پایه
        var cardFwd = new ModernCard("۱. محاسبه خالص از روی حقوق پایه", ModernTheme.Primary, 2, FH, BtnRow);
        var fwdBox = ModernUI.Money();
        fwdBox.Font = ModernTheme.GetFont(13f, FontStyle.Bold);
        txtForwardBase = fwdBox.TextBox;

        var calcFwd = MakeBtn(ModernButton.Primary("محاسبه خالص", 130, 40));
        calcFwd.Click += (s, e) => CalculateForward();
        btnCalcForward = calcFwd;

        var printFwd = MakeBtn(ModernButton.Soft("🖨️ چاپ فیش حقوقی", ModernTheme.Warning, 130, 40));
        printFwd.Enabled = false;
        printFwd.Click += (s, e) => { if (_lastForwardResult is not null) OpenPayslip(_lastForwardResult); };
        btnPrintForward = printFwd;

        var fldFwd = ModernUI.Field("حقوق پایه", fwdBox);
        cardFwd.Body.Controls.Add(fldFwd, 0, 0);
        cardFwd.Body.SetColumnSpan(fldFwd, 2);
        cardFwd.Body.Controls.Add(calcFwd, 0, 1);
        cardFwd.Body.Controls.Add(printFwd, 1, 1);

        // ۲) حقوق پایه از روی خالص
        var cardRev = new ModernCard("۲. محاسبه حقوق پایه از روی خالص", ModernTheme.Success, 2, FH, BtnRow);
        var revBox = ModernUI.Money();
        revBox.Font = ModernTheme.GetFont(13f, FontStyle.Bold);
        txtReverseNet = revBox.TextBox;

        var calcRev = MakeBtn(ModernButton.Success("محاسبه حقوق پایه", 130, 40));
        calcRev.Click += (s, e) => CalculateReverse();
        btnCalcReverse = calcRev;

        var printRev = MakeBtn(ModernButton.Soft("🖨️ چاپ فیش حقوقی", ModernTheme.Warning, 130, 40));
        printRev.Enabled = false;
        printRev.Click += (s, e) => { if (_lastReverseResult is not null) OpenPayslip(_lastReverseResult); };
        btnPrintReverse = printRev;

        var fldRev = ModernUI.Field("خالص مد نظر", revBox);
        cardRev.Body.Controls.Add(fldRev, 0, 0);
        cardRev.Body.SetColumnSpan(fldRev, 2);
        cardRev.Body.Controls.Add(calcRev, 0, 1);
        cardRev.Body.Controls.Add(printRev, 1, 1);

        // ───────────── جدول‌های خروجی ─────────────
        dgvForward = Create4ColumnGrid();
        dgvReverse = Create4ColumnGrid();

        // ───────────── چیدمان صفحه ─────────────
        const int Gap = 12;
        int calcHeight = Math.Max(cardFwd.Height, cardRev.Height);

        var calcRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = Color.Transparent };
        calcRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        calcRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        calcRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        cardFwd.Dock = DockStyle.Fill; cardFwd.Margin = new Padding(0, 0, Gap / 2, 0);
        cardRev.Dock = DockStyle.Fill; cardRev.Margin = new Padding(Gap / 2, 0, 0, 0);
        calcRow.Controls.Add(cardFwd, 0, 0);
        calcRow.Controls.Add(cardRev, 1, 0);

        var gridRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = Color.Transparent };
        gridRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        gridRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        gridRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        var gFwd = ModernReport.WrapGrid(dgvForward); gFwd.Margin = new Padding(0, 0, Gap / 2, 0);
        var gRev = ModernReport.WrapGrid(dgvReverse); gRev.Margin = new Padding(Gap / 2, 0, 0, 0);
        gridRow.Controls.Add(gFwd, 0, 0);
        gridRow.Controls.Add(gRev, 1, 0);

        cardInfo.Dock = DockStyle.Fill; cardInfo.Margin = Padding.Empty;
        cardVars.Dock = DockStyle.Fill; cardVars.Margin = Padding.Empty;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(20, 14, 20, 12), BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, cardInfo.Height + Gap));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, cardVars.Height + Gap));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, calcHeight + Gap));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.Controls.Add(WithGap(cardInfo, Gap), 0, 0);
        root.Controls.Add(WithGap(cardVars, Gap), 0, 1);
        root.Controls.Add(WithGap(calcRow, Gap), 0, 2);
        root.Controls.Add(gridRow, 0, 3);

        Controls.Add(root);
    }

    /// <summary>فاصله‌ی زیر یک بخش (ردیف‌های ثابت جدول اصلی)</summary>
    private static Control WithGap(Control c, int gap)
    {
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, gap), BackColor = Color.Transparent, Margin = Padding.Empty };
        c.Dock = DockStyle.Fill;
        host.Controls.Add(c);
        return host;
    }

    private DataGridView Create4ColumnGrid()
    {
        var dgv = new DataGridView
        {
            AllowUserToAddRows = false,
            ReadOnly = true
        };
        dgv.ApplyModernStyle();
        dgv.FillColumns();

        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Item1", HeaderText = "شرح آیتم (حقوق)", FillWeight = 30 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount1", HeaderText = "مبلغ (ریال)", FillWeight = 20 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Item2", HeaderText = "شرح آیتم (مزایا/کسورات)", FillWeight = 30 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount2", HeaderText = "مبلغ (ریال)", FillWeight = 20 });

        dgv.Columns["Amount1"].DefaultCellStyle.Format = "N0";
        dgv.Columns["Amount1"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgv.Columns["Amount2"].DefaultCellStyle.Format = "N0";
        dgv.Columns["Amount2"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        return dgv;
    }

    private void PopulateGrid(DataGridView dgv, PayrollResult r, bool isReverse)
    {
        dgv.Rows.Clear();

        dgv.Rows.Add("حقوق پایه", r.BaseSalary, "حق مسکن", r.HousingAllowance);
        dgv.Rows.Add("پایه سنوات", r.YearsBonus, "حق خواربار", r.GroceryAllowance);
        dgv.Rows.Add("حق مدیریت", r.ManagementAllowance, "حق اولاد", r.ChildAllowance);
        dgv.Rows.Add("حق مسئولیت", r.ResponsibilityAllowance, "حق تاهل", r.MarriageAllowance);
        dgv.Rows.Add("حق فنی", r.TechnicalAllowance, "سایر مزایا", r.OtherAllowances);
        dgv.Rows.Add("حق جذب", r.AttractionAllowance, "حق ایاب و ذهاب", r.TransportationAllowance);

        if (r.OvertimePay > 0)
            dgv.Rows.Add("", 0, "اضافه کار", r.OvertimePay);

        decimal totalSalary = r.BaseSalary + r.YearsBonus + r.ManagementAllowance + r.ResponsibilityAllowance + r.TechnicalAllowance + r.AttractionAllowance;
        decimal totalBenefits = r.HousingAllowance + r.GroceryAllowance + r.ChildAllowance + r.MarriageAllowance + r.OtherAllowances + r.TransportationAllowance + r.OvertimePay;

        dgv.Rows.Add("جمع حقوق", totalSalary, "جمع مزایا", totalBenefits);
        dgv.Rows.Add("", 0, "جمع کل حقوق و مزایا (ناخالص)", r.TotalEarnings);

        dgv.Rows.Add("بیمه سهم کارمند", r.InsuranceEmployee, "مالیات بر درآمد", r.IncomeTax);

        decimal totalOtherDeds = r.OtherDeductions + r.LoanDeduction + r.AdvanceDeduction + r.SupplementaryInsuranceDeduction + r.CarInsuranceDeduction + r.AbsenceDeduction;
        dgv.Rows.Add("سایر کسورات", totalOtherDeds, "جمع کسورات", r.TotalDeductions);

        dgv.Rows.Add("مانده قابل پرداخت", r.NetPay, "خالص پرداختی", r.NetPay);

        Color headerColor = isReverse ? Color.FromArgb(240, 253, 244) : Color.FromArgb(240, 248, 255);
        Color totalColor = isReverse ? ModernTheme.SuccessSoft : Color.FromArgb(219, 234, 254);

        for (int i = 0; i < dgv.Rows.Count; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                dgv.Rows[i].Cells[j].Style.BackColor = headerColor;
            }
        }

        int totalSalaryRow = r.OvertimePay > 0 ? 7 : 6;
        int grossRow = totalSalaryRow + 1;
        int deductionRow = grossRow + 2;
        int netRow = deductionRow + 1;

        dgv.Rows[totalSalaryRow].DefaultCellStyle.Font = ModernTheme.GetFont(10f, FontStyle.Bold);
        for (int j = 0; j < 4; j++) dgv.Rows[totalSalaryRow].Cells[j].Style.BackColor = totalColor;

        dgv.Rows[grossRow].DefaultCellStyle.Font = ModernTheme.GetFont(10f, FontStyle.Bold);
        dgv.Rows[grossRow].Cells["Item2"].Style.BackColor = totalColor;
        dgv.Rows[grossRow].Cells["Amount2"].Style.BackColor = totalColor;

        dgv.Rows[deductionRow].DefaultCellStyle.Font = ModernTheme.GetFont(10f, FontStyle.Bold);
        dgv.Rows[deductionRow].Cells["Item2"].Style.BackColor = totalColor;
        dgv.Rows[deductionRow].Cells["Amount2"].Style.BackColor = totalColor;

        dgv.Rows[netRow].DefaultCellStyle.Font = ModernTheme.GetFont(11f, FontStyle.Bold);
        for (int j = 0; j < 4; j++) dgv.Rows[netRow].Cells[j].Style.BackColor = totalColor;

        Color netColor = isReverse ? ModernTheme.Success : ModernTheme.Primary;
        dgv.Rows[netRow].Cells["Item1"].Style.ForeColor = netColor;
        dgv.Rows[netRow].Cells["Amount1"].Style.ForeColor = netColor;
        dgv.Rows[netRow].Cells["Item2"].Style.ForeColor = netColor;
        dgv.Rows[netRow].Cells["Amount2"].Style.ForeColor = netColor;
    }

    private void OpenPayslip(PayrollResult r)
    {
        try
        {
            var form = new PayslipForm(r);
            form.ShowDialog(this);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "ReversePayrollForm.OpenPayslip");
            ModernMessageBox.ShowError("خطا در باز کردن فیش حقوقی:\n" + ex.Message);
        }
    }

    private async Task LoadSettingsAsync()
    {
        if (cmbYear.SelectedItem is null) return;
        int year = (int)cmbYear.SelectedItem;

        _settings = await _settingsRepo.GetByYearAsync(year);
        _taxBrackets = _calculatorService.GetTaxBracketsSafe(year);

        if (_settings is null)
        {
            ModernMessageBox.ShowWarning($"تنظیمات حقوق برای سال {year} یافت نشد. لطفاً ابتدا از منوی تنظیمات، حقوق سال را ذخیره کنید.");
        }
    }

    private Employee CreateDummyEmployee(decimal baseSalary)
    {
        decimal yearsBonus = 0;
        if (_settings is not null && numYears.Value > 0)
        {
            yearsBonus = Math.Round((_settings.MinimumWage * 0.05m) * numYears.Value, 0);
        }

        string fullName = string.IsNullOrWhiteSpace(txtEmployeeName.Text) ? "کارمند فرضی" : txtEmployeeName.Text.Trim();
        var nameParts = fullName.Split(' ');
        string firstName = nameParts[0];
        string lastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "";

        return new Employee
        {
            FirstName = firstName,
            LastName = lastName,
            PersonnelCode = string.IsNullOrWhiteSpace(txtPersonnelCode.Text) ? "---" : txtPersonnelCode.Text.Trim(),
            BaseSalary = baseSalary,
            YearsBonus = yearsBonus,
            MaritalStatus = cmbMaritalStatus.Text,
            ChildrenCount = (int)numChildren.Value,
            HasInsurance = chkHasInsurance.Checked,
            PersonnelType = "ماهانه",
            ManagementAllowance = 0,
            ResponsibilityAllowance = 0,
            TechnicalAllowance = 0,
            AttractionAllowance = 0,
            GroceryAllowance = 0,
            HousingAllowance = 0,
            ChildAllowance = 0,
            MarriageAllowance = 0,
            OtherAllowances = 0,
            DailyWage = 0,
            HourlyWage = 0
        };
    }

    private void CalculateForward()
    {
        if (_settings is null) { ModernMessageBox.ShowWarning("تنظیمات سال لود نشده است."); return; }

        decimal baseSalary = NumberHelper.ParseFormatted(txtForwardBase.Text);
        if (baseSalary <= 0) { ModernMessageBox.ShowWarning("لطفاً حقوق پایه را وارد کنید."); return; }

        var emp = CreateDummyEmployee(baseSalary);
        var att = new Attendance
        {
            Year = (int)cmbYear.SelectedItem,
            Month = (int)cmbMonth.SelectedItem,
            WorkingDays = 30,
            HoursWorked = _settings.MonthlyWorkingHours,
            GrossOvertime = numOvertimeHours.Value,
            AbsenceHours = numAbsenceHours.Value,
            NetOvertime = numOvertimeHours.Value - numAbsenceHours.Value
        };

        decimal loan = NumberHelper.ParseFormatted(txtLoanDeduction.Text);
        decimal otherDed = NumberHelper.ParseFormatted(txtOtherDeductions.Text);

        _lastForwardResult = _calculatorService.CalculatePayroll(emp, att, _settings, loan, 0, 0, otherDed, _taxBrackets);

        PopulateGrid(dgvForward, _lastForwardResult, isReverse: false);
        btnPrintForward.Enabled = true;
    }

    private void CalculateReverse()
    {
        if (_settings is null) { ModernMessageBox.ShowWarning("تنظیمات سال لود نشده است."); return; }

        decimal targetNet = NumberHelper.ParseFormatted(txtReverseNet.Text);
        if (targetNet <= 0) { ModernMessageBox.ShowWarning("لطفاً مبلغ خالص را وارد کنید."); return; }

        var att = new Attendance
        {
            Year = (int)cmbYear.SelectedItem,
            Month = (int)cmbMonth.SelectedItem,
            WorkingDays = 30,
            HoursWorked = _settings.MonthlyWorkingHours,
            GrossOvertime = numOvertimeHours.Value,
            AbsenceHours = numAbsenceHours.Value,
            NetOvertime = numOvertimeHours.Value - numAbsenceHours.Value
        };

        decimal loan = NumberHelper.ParseFormatted(txtLoanDeduction.Text);
        decimal otherDed = NumberHelper.ParseFormatted(txtOtherDeductions.Text);

        decimal low = 0;
        decimal high = targetNet * 2;
        decimal bestBase = 0;
        decimal minDiff = decimal.MaxValue;
        PayrollResult? finalResult = null;

        for (int i = 0; i < 100; i++)
        {
            decimal mid = (low + high) / 2;
            mid = Math.Round(mid / 10000) * 10000;

            var emp = CreateDummyEmployee(mid);
            var result = _calculatorService.CalculatePayroll(emp, att, _settings, loan, 0, 0, otherDed, _taxBrackets);

            decimal diff = Math.Abs(result.NetPay - targetNet);

            if (diff < minDiff)
            {
                minDiff = diff;
                bestBase = mid;
                finalResult = result;
            }

            if (result.NetPay == targetNet) break;

            if (result.NetPay < targetNet) low = mid;
            else high = mid;
        }

        if (finalResult is null) return;
        _lastReverseResult = finalResult;

        var displayResult = new PayrollResult
        {
            EmployeeId = finalResult.EmployeeId,
            EmployeeName = finalResult.EmployeeName,
            PersonnelCode = finalResult.PersonnelCode,
            Year = finalResult.Year,
            Month = finalResult.Month,
            PersonnelType = finalResult.PersonnelType,

            BaseSalary = bestBase,
            YearsBonus = finalResult.YearsBonus,
            HousingAllowance = finalResult.HousingAllowance,
            GroceryAllowance = finalResult.GroceryAllowance,
            ChildAllowance = finalResult.ChildAllowance,
            MarriageAllowance = finalResult.MarriageAllowance,
            OtherAllowances = finalResult.OtherAllowances,
            AttractionAllowance = finalResult.AttractionAllowance,
            ResponsibilityAllowance = finalResult.ResponsibilityAllowance,
            ManagementAllowance = finalResult.ManagementAllowance,
            TechnicalAllowance = finalResult.TechnicalAllowance,
            TransportationAllowance = finalResult.TransportationAllowance,
            OvertimePay = finalResult.OvertimePay,
            TotalEarnings = finalResult.TotalEarnings,
            InsuranceEmployee = finalResult.InsuranceEmployee,
            IncomeTax = finalResult.IncomeTax,
            OtherDeductions = finalResult.OtherDeductions,
            TotalDeductions = finalResult.TotalDeductions,
            NetPay = finalResult.NetPay,
            InsuranceEmployer = finalResult.InsuranceEmployer,
            AbsenceDeduction = finalResult.AbsenceDeduction,
            LoanDeduction = finalResult.LoanDeduction,
            AdvanceDeduction = finalResult.AdvanceDeduction,
            SupplementaryInsuranceDeduction = finalResult.SupplementaryInsuranceDeduction,
            CarInsuranceDeduction = finalResult.CarInsuranceDeduction
        };

        PopulateGrid(dgvReverse, displayResult, isReverse: true);
        btnPrintReverse.Enabled = true;

        if (minDiff > 1000)
        {
            ToastHelper.Show($"توجه: خالص نهایی {NumberHelper.FormatThousands(finalResult.NetPay, usePersianDigits: true)} شد که {NumberHelper.FormatThousands(minDiff, usePersianDigits: true)} ریال با مبلغ درخواستی اختلاف دارد.", false);
        }
    }
}
