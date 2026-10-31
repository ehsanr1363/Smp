using PersonnelApp.Data;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using PersonnelApp.Services;
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonnelApp.Forms
{
    public class SettlementForm : Form
    {
        private readonly EmployeeRepository _empRepository;
        private readonly LeaveRepository _leaveRepository;
        private readonly LoanRepository _loanRepository;
        private readonly SettlementRepository _settlementRepository;
        private readonly SettlementCalculatorService _calculator;

        private ComboBox cmbEmployee = null!;
        private Button btnCalculate = null!, btnSave = null!;
        private TableLayoutPanel resultLayout = null!;
        private Label lblNet = null!;

        private SettlementRecord? _currentSettlement;

        public SettlementForm(string connectionString)
        {
            _empRepository = new EmployeeRepository(connectionString);
            _leaveRepository = new LeaveRepository(connectionString);
            _loanRepository = new LoanRepository(connectionString);
            _settlementRepository = new SettlementRepository(connectionString);
            _calculator = new SettlementCalculatorService();

            BuildUI();

            this.ApplyStandardHeader("تسویه حساب پرسنل", new Size(800, 600));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            Shown += async (s, e) => await LoadEmployeesSafeAsync();
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));

            var topPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            var selectLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            selectLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            selectLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80F));

            cmbEmployee = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbEmployee.DisplayMember = "FullName";
            cmbEmployee.ValueMember = "Id";

            UIHelper.AddField(selectLayout, "انتخاب کارمند:", cmbEmployee, 0, 0, 1);
            topPanel.Controls.Add(selectLayout);

            resultLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20, 10, 20, 10) };
            resultLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            resultLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));

            AddResultRow("سال‌های کارکرد:", "---", 0);
            AddResultRow("مبلغ سنوات:", "0", 1);
            AddResultRow("بازخرید مرخصی:", "0", 2);
            AddResultRow("عیدی/پاداش پایان سال:", "0", 3);
            AddResultRow("مانده وام (کسر می‌شود):", "0", 4);

            var netLabel = new Label { Text = "خالص تسویه حساب:", Font = UIHelper.PersianFont(14f, FontStyle.Bold), ForeColor = UIHelper.ActionGreen, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
            lblNet = new Label { Text = "0 ریال", Name = "lblNet", Font = UIHelper.PersianFont(16f, FontStyle.Bold), ForeColor = UIHelper.ActionGreen, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

            resultLayout.Controls.Add(netLabel, 0, 5);
            resultLayout.Controls.Add(lblNet, 1, 5);

            var bottomPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };

            btnCalculate = UIHelper.CreateButton("محاسبه تسویه", UIHelper.SectionBlue, 180, 45);
            btnCalculate.Dock = DockStyle.Right;
            btnCalculate.Click += async (s, e) => await CalculateSafeAsync();

            btnSave = UIHelper.CreateButton("ثبت نهایی و غیرفعال‌سازی", UIHelper.ActionGreen, 220, 45);
            btnSave.Dock = DockStyle.Right;
            btnSave.Enabled = false;
            btnSave.Click += async (s, e) => await SaveSettlementSafeAsync();

            bottomPanel.Controls.Add(btnCalculate);
            bottomPanel.Controls.Add(new Panel { Width = 10, Dock = DockStyle.Right });
            bottomPanel.Controls.Add(btnSave);

            mainLayout.Controls.Add(topPanel, 0, 0);
            mainLayout.Controls.Add(resultLayout, 0, 1);
            mainLayout.Controls.Add(bottomPanel, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private void AddResultRow(string title, string value, int row)
        {
            var lbl = new Label { Text = title, Font = UIHelper.PersianFont(11f), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
            var val = new Label { Text = value, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Name = $"lbl_{row}" };
            resultLayout.Controls.Add(lbl, 0, row);
            resultLayout.Controls.Add(val, 1, row);
        }

        private async Task LoadEmployeesSafeAsync()
        {
            await AsyncHelper.RunWithLoading(this, async () =>
            {
                var employees = await _empRepository.GetEmployeesWithDetailsAsync("", false);
                cmbEmployee.DataSource = employees;
            }, "در حال بارگذاری لیست پرسنل...");
        }

        private async Task CalculateSafeAsync()
        {
            if (cmbEmployee.SelectedValue == null) return;

            int empId = (int)cmbEmployee.SelectedValue;
            SettlementRecord? result = null;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                // 👈 اصلاح شد: فقط همان کارمند از دیتابیس خوانده می‌شود
                var emp = await Task.Run(() => _empRepository.GetById(empId));
                if (emp == null) throw new InvalidOperationException("اطلاعات کارمند یافت نشد.");

                int approvedLeaves = await _leaveRepository.GetTotalApprovedLeaveDaysAsync(empId);

                // 👈 اصلاح شد: فقط وام‌های همین کارمند خوانده می‌شود
                var loans = await _loanRepository.GetLoansByEmployeeIdAsync(empId);
                decimal remainingLoan = loans.Where(l => l.IsActive).Sum(l => l.RemainingAmount);

                result = _calculator.CalculateSettlement(emp, approvedLeaves, remainingLoan, DateTime.Now);
            }, "در حال محاسبه تسویه حساب...");

            if (!success || result == null) return;

            _currentSettlement = result;

            ((Label)resultLayout.Controls["lbl_0"]).Text = StringHelper.ConvertToPersianDigits(result.YearsOfWork.ToString()) + " سال";
            ((Label)resultLayout.Controls["lbl_1"]).Text = NumberHelper.FormatThousands(result.ServiceBonus, usePersianDigits: true) + " ریال";
            ((Label)resultLayout.Controls["lbl_2"]).Text = NumberHelper.FormatThousands(result.LeaveBonus, usePersianDigits: true) + " ریال";
            ((Label)resultLayout.Controls["lbl_3"]).Text = NumberHelper.FormatThousands(result.SeverancePay, usePersianDigits: true) + " ریال";
            ((Label)resultLayout.Controls["lbl_4"]).Text = NumberHelper.FormatThousands(result.RemainingLoan, usePersianDigits: true) + " ریال";

            lblNet.Text = NumberHelper.FormatThousands(result.NetSettlement, usePersianDigits: true) + " ریال";

            btnSave.Enabled = true;
        }

        private async Task SaveSettlementSafeAsync()
        {
            if (_currentSettlement == null) return;

            if (!ModernMessageBox.AskConfirm("آیا از ثبت تسویه حساب و غیرفعال کردن این کارمند مطمئن هستید؟", "ثبت تسویه"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _settlementRepository.SaveSettlementAsync(_currentSettlement);

                var emp = await Task.Run(() => _empRepository.GetById(_currentSettlement.EmployeeId));
                if (emp != null)
                {
                    emp.IsActive = false;
                    await _empRepository.UpdateAsync(emp);
                }
            }, "در حال ثبت تسویه حساب...");

            if (success)
            {
                ToastHelper.Show("تسویه حساب با موفقیت ثبت شد و کارمند غیرفعال گردید.", isSuccess: true);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }
    }
}