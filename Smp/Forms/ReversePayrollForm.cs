using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using SMP.Services;

namespace SMP.Forms
{
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
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(15) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var pnlSettings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 3, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(10) };
            for (int i = 0; i < 6; i++)
            {
                if (i % 2 == 0) pnlSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
                else pnlSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            }

            pnlSettings.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            pnlSettings.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            pnlSettings.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            var pc = new PersianCalendar();

            cmbYear = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            for (int i = 1400; i <= 1410; i++) cmbYear.Items.Add(i);
            cmbYear.SelectedItem = pc.GetYear(DateTime.Now);

            cmbMonth = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            for (int i = 1; i <= 12; i++) cmbMonth.Items.Add(i);
            cmbMonth.SelectedItem = pc.GetMonth(DateTime.Now);

            cmbMaritalStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            cmbMaritalStatus.Items.AddRange(new object[] { "مجرد", "متاهل" });
            cmbMaritalStatus.SelectedIndex = 0;

            numChildren = new NumericUpDown { Minimum = 0, Maximum = 10, Value = 0, Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Center };
            numYears = new NumericUpDown { Minimum = 0, Maximum = 30, Value = 0, Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Center };

            chkHasInsurance = new CheckBox { Text = "بیمه تامین اجتماعی", Checked = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            txtEmployeeName = new TextBox { Text = "کارمند نمونه", Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Center };
            txtPersonnelCode = new TextBox { Text = "10001", Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Center };

            UIHelper.AddField(pnlSettings, "سال مالی:", cmbYear, 0, 0, 1);
            UIHelper.AddField(pnlSettings, "ماه:", cmbMonth, 0, 2, 3);
            UIHelper.AddField(pnlSettings, "وضعیت تاهل:", cmbMaritalStatus, 0, 4, 5);

            UIHelper.AddField(pnlSettings, "تعداد فرزند:", numChildren, 1, 0, 1);
            UIHelper.AddField(pnlSettings, "سابقه (سال):", numYears, 1, 2, 3);

            var pnlIns = new Panel { Dock = DockStyle.Fill };
            chkHasInsurance.Anchor = AnchorStyles.Right;
            pnlIns.Controls.Add(chkHasInsurance);
            pnlSettings.Controls.Add(pnlIns, 4, 1);
            pnlSettings.SetColumnSpan(pnlIns, 2);

            UIHelper.AddField(pnlSettings, "نام و نام خانوادگی:", txtEmployeeName, 2, 0, 1);
            UIHelper.AddField(pnlSettings, "کد پرسنلی:", txtPersonnelCode, 2, 2, 3);

            mainLayout.Controls.Add(pnlSettings, 0, 0);

            var pnlVariables = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 1, BackColor = Color.FromArgb(245, 250, 255), Padding = new Padding(10) };
            for (int i = 0; i < 8; i++)
            {
                if (i % 2 == 0) pnlVariables.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
                else pnlVariables.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            }

            numOvertimeHours = new NumericUpDown { Minimum = 0, Maximum = 200, Value = 0, Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Center };
            numAbsenceHours = new NumericUpDown { Minimum = 0, Maximum = 200, Value = 0, Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Center };
            txtLoanDeduction = new TextBox { Text = "0", Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Center };
            SmartInputHelper.EnableSmartAmountTextBox(txtLoanDeduction);
            txtOtherDeductions = new TextBox { Text = "0", Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Center };
            SmartInputHelper.EnableSmartAmountTextBox(txtOtherDeductions);

            UIHelper.AddField(pnlVariables, "ساعت اضافه کار:", numOvertimeHours, 0, 0, 1);
            UIHelper.AddField(pnlVariables, "ساعت کسر کار:", numAbsenceHours, 0, 2, 3);
            UIHelper.AddField(pnlVariables, "کسر وام (ریال):", txtLoanDeduction, 0, 4, 5);
            UIHelper.AddField(pnlVariables, "سایر کسورات (ریال):", txtOtherDeductions, 0, 6, 7);

            mainLayout.Controls.Add(pnlVariables, 0, 1);

            var pnlInputs = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            pnlInputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            pnlInputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            var grpForward = new GroupBox { Text = "۱. محاسبه خالص از روی حقوق پایه", Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.SectionBlue, Padding = new Padding(10) };
            var pnlFwd = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            pnlFwd.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            pnlFwd.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            pnlFwd.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            pnlFwd.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            txtForwardBase = new TextBox { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(12f, FontStyle.Bold), TextAlign = HorizontalAlignment.Center };
            SmartInputHelper.EnableSmartAmountTextBox(txtForwardBase);

            btnCalcForward = UIHelper.CreateButton("محاسبه خالص", UIHelper.SectionBlue, 130, 40);
            btnCalcForward.Click += (s, e) => CalculateForward();

            btnPrintForward = UIHelper.CreateButton("🖨️ چاپ فیش حقوقی", UIHelper.ActionAmber, 130, 40);
            btnPrintForward.Enabled = false;
            btnPrintForward.Click += (s, e) => { if (_lastForwardResult != null) OpenPayslip(_lastForwardResult); };

            pnlFwd.Controls.Add(new Label { Text = "حقوق پایه (ریال):", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
            pnlFwd.Controls.Add(txtForwardBase, 1, 0);

            var pnlFwdBtns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            pnlFwdBtns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            pnlFwdBtns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            btnCalcForward.Dock = DockStyle.Fill;
            btnCalcForward.Margin = new Padding(5, 10, 5, 5);
            btnPrintForward.Dock = DockStyle.Fill;
            btnPrintForward.Margin = new Padding(5, 10, 5, 5);

            pnlFwdBtns.Controls.Add(btnCalcForward, 0, 0);
            pnlFwdBtns.Controls.Add(btnPrintForward, 1, 0);

            pnlFwd.Controls.Add(pnlFwdBtns, 0, 1);
            pnlFwd.SetColumnSpan(pnlFwdBtns, 2);
            grpForward.Controls.Add(pnlFwd);

            var grpReverse = new GroupBox { Text = "۲. محاسبه حقوق پایه از روی خالص", Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.ActionGreen, Padding = new Padding(10) };
            var pnlRev = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            pnlRev.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            pnlRev.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            pnlRev.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            pnlRev.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            txtReverseNet = new TextBox { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(12f, FontStyle.Bold), TextAlign = HorizontalAlignment.Center };
            SmartInputHelper.EnableSmartAmountTextBox(txtReverseNet);

            btnCalcReverse = UIHelper.CreateButton("محاسبه حقوق پایه", UIHelper.ActionGreen, 130, 40);
            btnCalcReverse.Click += (s, e) => CalculateReverse();

            btnPrintReverse = UIHelper.CreateButton("🖨️ چاپ فیش حقوقی", UIHelper.ActionAmber, 130, 40);
            btnPrintReverse.Enabled = false;
            btnPrintReverse.Click += (s, e) => { if (_lastReverseResult != null) OpenPayslip(_lastReverseResult); };

            pnlRev.Controls.Add(new Label { Text = "خالص مد نظر (ریال):", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
            pnlRev.Controls.Add(txtReverseNet, 1, 0);

            var pnlRevBtns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            pnlRevBtns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            pnlRevBtns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            btnCalcReverse.Dock = DockStyle.Fill;
            btnCalcReverse.Margin = new Padding(5, 10, 5, 5);
            btnPrintReverse.Dock = DockStyle.Fill;
            btnPrintReverse.Margin = new Padding(5, 10, 5, 5);

            pnlRevBtns.Controls.Add(btnCalcReverse, 0, 0);
            pnlRevBtns.Controls.Add(btnPrintReverse, 1, 0);

            pnlRev.Controls.Add(pnlRevBtns, 0, 1);
            pnlRev.SetColumnSpan(pnlRevBtns, 2);
            grpReverse.Controls.Add(pnlRev);

            pnlInputs.Controls.Add(grpForward, 0, 0);
            pnlInputs.Controls.Add(grpReverse, 1, 0);
            mainLayout.Controls.Add(pnlInputs, 0, 2);

            var pnlOutputs = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            pnlOutputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            pnlOutputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            dgvForward = Create4ColumnGrid();
            dgvReverse = Create4ColumnGrid();

            pnlOutputs.Controls.Add(dgvForward, 0, 0);
            pnlOutputs.Controls.Add(dgvReverse, 1, 0);
            mainLayout.Controls.Add(pnlOutputs, 0, 3);

            this.Controls.Add(mainLayout);
        }

        private DataGridView Create4ColumnGrid()
        {
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true,
                Margin = new Padding(5, 5, 5, 5)
            };
            dgv.ApplyModernStyle();

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

            decimal totalOtherDeds = r.OtherDeductions + r.LoanDeduction + r.SupplementaryInsuranceDeduction + r.CarInsuranceDeduction + r.AbsenceDeduction;
            dgv.Rows.Add("سایر کسورات", totalOtherDeds, "جمع کسورات", r.TotalDeductions);

            dgv.Rows.Add("مانده قابل پرداخت", r.NetPay, "خالص پرداختی", r.NetPay);

            Color headerColor = isReverse ? Color.FromArgb(240, 253, 244) : Color.FromArgb(240, 248, 255);
            Color totalColor = isReverse ? Color.FromArgb(220, 252, 231) : Color.FromArgb(219, 234, 254);

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

            dgv.Rows[totalSalaryRow].DefaultCellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold);
            for (int j = 0; j < 4; j++) dgv.Rows[totalSalaryRow].Cells[j].Style.BackColor = totalColor;

            dgv.Rows[grossRow].DefaultCellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold);
            dgv.Rows[grossRow].Cells["Item2"].Style.BackColor = totalColor;
            dgv.Rows[grossRow].Cells["Amount2"].Style.BackColor = totalColor;

            dgv.Rows[deductionRow].DefaultCellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold);
            dgv.Rows[deductionRow].Cells["Item2"].Style.BackColor = totalColor;
            dgv.Rows[deductionRow].Cells["Amount2"].Style.BackColor = totalColor;

            dgv.Rows[netRow].DefaultCellStyle.Font = UIHelper.PersianFont(11f, FontStyle.Bold);
            for (int j = 0; j < 4; j++) dgv.Rows[netRow].Cells[j].Style.BackColor = totalColor;

            Color netColor = isReverse ? UIHelper.ActionGreen : UIHelper.SectionBlue;
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
            if (cmbYear.SelectedItem == null) return;
            int year = (int)cmbYear.SelectedItem;

            _settings = await _settingsRepo.GetByYearAsync(year);
            _taxBrackets = _calculatorService.GetTaxBracketsSafe(year);

            if (_settings == null)
            {
                ModernMessageBox.ShowWarning($"تنظیمات حقوق برای سال {year} یافت نشد. لطفاً ابتدا از منوی تنظیمات، حقوق سال را ذخیره کنید.");
            }
        }

        private Employee CreateDummyEmployee(decimal baseSalary)
        {
            decimal yearsBonus = 0;
            if (_settings != null && numYears.Value > 0)
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
            if (_settings == null) { ModernMessageBox.ShowWarning("تنظیمات سال لود نشده است."); return; }

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
            if (_settings == null) { ModernMessageBox.ShowWarning("تنظیمات سال لود نشده است."); return; }

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

            if (finalResult == null) return;
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
}