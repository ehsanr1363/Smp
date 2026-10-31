using PersonnelApp.Data;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonnelApp.Forms
{
    public class SupplementaryInsuranceManagementForm : Form
    {
        private readonly SupplementaryInsuranceRepository _repository;
        private readonly EmployeeRepository _empRepository;

        private ComboBox cmbEmployee = null!, cmbStartMonth = null!;
        private NumericUpDown numStartYear = null!, numInstallments = null!;
        private TextBox txtTotalAmount = null!, txtDescription = null!;
        private Label lblMonthlyInstallment = null!;
        private Button btnSave = null!, btnDelete = null!, btnNew = null!;
        private DataGridView dgvInsurances = null!;

        private EmployeeSupplementaryInsurance? _editingInsurance = null;
        private bool _isInitializing = true;

        public SupplementaryInsuranceManagementForm(string connectionString)
        {
            _repository = new SupplementaryInsuranceRepository(connectionString);
            _empRepository = new EmployeeRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("مدیریت بیمه تکمیلی پرسنل", new Size(1050, 750));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            Shown += async (s, e) => await LoadDataSafeAsync();
        }

        private void BuildUI()
        {
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 260, Padding = new Padding(10) };

            var formLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 4 };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));

            for (int i = 0; i < 4; i++) formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));

            cmbEmployee = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "FullName", ValueMember = "Id" };
            UIHelper.AddField(formLayout, "کارمند:", cmbEmployee, 0, 0, 1, 3);

            txtTotalAmount = new TextBox();
            SmartInputHelper.EnableSmartAmountTextBox(txtTotalAmount);
            txtTotalAmount.TextChanged += (s, e) => CalculateInstallment();
            UIHelper.AddField(formLayout, "مبلغ کل بیمه:", txtTotalAmount, 1, 0, 1);

            numInstallments = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 120,
                Value = 12,
                TextAlign = HorizontalAlignment.Center,
                Font = UIHelper.PersianFont(11f),
                BackColor = UIHelper.WarmInputBackground
            };
            numInstallments.ValueChanged += (s, e) => CalculateInstallment();
            UIHelper.AddField(formLayout, "تعداد اقساط:", numInstallments, 1, 2, 3);

            lblMonthlyInstallment = new Label
            {
                Text = "مبلغ هر قسط: 0 ریال",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                ForeColor = UIHelper.ActionGreen
            };
            formLayout.Controls.Add(lblMonthlyInstallment, 2, 0);
            formLayout.SetColumnSpan(lblMonthlyInstallment, 2);

            var pc = new PersianCalendar();
            int currentYear = pc.GetYear(DateTime.Now);
            int currentMonth = pc.GetMonth(DateTime.Now);

            numStartYear = new NumericUpDown
            {
                Minimum = 1400,
                Maximum = 1450,
                Value = currentYear,
                TextAlign = HorizontalAlignment.Center,
                Font = UIHelper.PersianFont(11f),
                BackColor = UIHelper.WarmInputBackground
            };
            UIHelper.AddField(formLayout, "سال شروع:", numStartYear, 2, 0, 1);

            cmbStartMonth = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), BackColor = UIHelper.WarmInputBackground };
            for (int i = 1; i <= 12; i++) cmbStartMonth.Items.Add(i);
            cmbStartMonth.SelectedItem = currentMonth;
            UIHelper.AddField(formLayout, "ماه شروع:", cmbStartMonth, 2, 2, 3);

            txtDescription = new TextBox();
            UIHelper.AddField(formLayout, "توضیحات:", txtDescription, 3, 0, 1);
            formLayout.SetColumnSpan(txtDescription, 3);

            var btnPanelTop = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft };

            btnSave = UIHelper.CreateButton("➕ ثبت بیمه جدید", UIHelper.ActionGreen, 200, 40);
            btnSave.Click += BtnSave_Click;

            btnNew = UIHelper.CreateButton("🔄 جدید / پاک‌سازی", UIHelper.SectionGray, 180, 40);
            btnNew.Click += (s, e) => ResetForm();

            btnPanelTop.Controls.Add(btnSave);
            btnPanelTop.Controls.Add(btnNew);

            topPanel.Controls.Add(btnPanelTop);
            topPanel.Controls.Add(formLayout);

            dgvInsurances = new DataGridView { Dock = DockStyle.Fill };
            dgvInsurances.ApplyModernStyle("EmployeeId", "IsActive", "Id");
            dgvInsurances.SelectionChanged += DgvInsurances_SelectionChanged;

            dgvInsurances.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                string colName = dgvInsurances.Columns[e.ColumnIndex].Name;

                if ((colName == "TotalAmount" || colName == "MonthlyInstallment" || colName == "RemainingAmount") && e.Value is decimal dec)
                {
                    e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
            };

            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                Padding = new Padding(10),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            btnDelete = UIHelper.CreateButton("🗑️ حذف بیمه انتخاب‌شده", UIHelper.ActionRed, 220, 40);
            btnDelete.Enabled = false;
            btnDelete.Click += BtnDelete_Click;
            bottomPanel.Controls.Add(btnDelete);

            this.Controls.Add(dgvInsurances);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(topPanel);
        }

        private void CalculateInstallment()
        {
            decimal total = NumberHelper.ParseFormatted(txtTotalAmount.Text);
            int installments = (int)numInstallments.Value;

            if (total > 0 && installments > 0)
            {
                decimal monthly = NumberHelper.Round(total / installments, 0);
                lblMonthlyInstallment.Text = $"مبلغ هر قسط: {NumberHelper.FormatThousands(monthly, usePersianDigits: true)} ریال";
            }
            else
            {
                lblMonthlyInstallment.Text = "مبلغ هر قسط: 0 ریال";
            }
        }

        private async Task LoadDataSafeAsync()
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var employees = await _empRepository.GetEmployeesWithDetailsAsync("", false);
                cmbEmployee.DataSource = employees;

                var insurances = await _repository.GetActiveInsurancesAsync();
                dgvInsurances.DataSource = insurances;

                if (dgvInsurances.Columns.Contains("TotalAmount")) dgvInsurances.Columns["TotalAmount"].HeaderText = "مبلغ کل";
                if (dgvInsurances.Columns.Contains("MonthlyInstallment")) dgvInsurances.Columns["MonthlyInstallment"].HeaderText = "مبلغ قسط";
                if (dgvInsurances.Columns.Contains("RemainingAmount")) dgvInsurances.Columns["RemainingAmount"].HeaderText = "مانده";
                if (dgvInsurances.Columns.Contains("StartPeriod")) dgvInsurances.Columns["StartPeriod"].HeaderText = "شروع کسر";
                if (dgvInsurances.Columns.Contains("EmployeeName")) dgvInsurances.Columns["EmployeeName"].HeaderText = "نام کارمند";
                if (dgvInsurances.Columns.Contains("Description")) dgvInsurances.Columns["Description"].HeaderText = "توضیحات";
            }, "در حال بارگذاری اطلاعات...");

            _isInitializing = false;
            btnDelete.Enabled = dgvInsurances.CurrentRow != null;
        }

        private void DgvInsurances_SelectionChanged(object? sender, EventArgs e)
        {
            if (_isInitializing) return;
            if (dgvInsurances.CurrentRow?.DataBoundItem is not EmployeeSupplementaryInsurance selected) return;

            _editingInsurance = selected;

            foreach (Employee emp in cmbEmployee.Items)
            {
                if (emp.Id == selected.EmployeeId)
                {
                    cmbEmployee.SelectedItem = emp;
                    break;
                }
            }

            txtTotalAmount.Text = NumberHelper.FormatThousands(selected.TotalAmount);

            // ✅ FIX: محدودسازی تعداد اقساط
            decimal calcInst = selected.MonthlyInstallment > 0
                ? (int)Math.Round(selected.TotalAmount / selected.MonthlyInstallment)
                : 1;
            calcInst = Math.Max(numInstallments.Minimum, Math.Min(numInstallments.Maximum, calcInst));
            numInstallments.Value = calcInst;

            // ✅ FIX: محدودسازی سال شروع
            decimal startYear = selected.StartYear > 0 ? selected.StartYear : 1403;
            startYear = Math.Max(numStartYear.Minimum, Math.Min(numStartYear.Maximum, startYear));
            numStartYear.Value = startYear;
            // ------------------------------------

            if (selected.StartMonth >= 1 && selected.StartMonth <= 12)
                cmbStartMonth.SelectedItem = selected.StartMonth;

            txtDescription.Text = selected.Description ?? "";

            btnSave.Text = "💾 ذخیره تغییرات";
            btnSave.BackColor = UIHelper.SectionBlue;
            btnDelete.Enabled = true;
        }

        private void ResetForm()
        {
            _editingInsurance = null;
            txtTotalAmount.Clear();
            numInstallments.Value = 12;
            txtDescription.Clear();
            cmbEmployee.SelectedIndex = -1;

            var pc = new PersianCalendar();
            numStartYear.Value = pc.GetYear(DateTime.Now);
            cmbStartMonth.SelectedItem = pc.GetMonth(DateTime.Now);

            btnSave.Text = "➕ ثبت بیمه جدید";
            btnSave.BackColor = UIHelper.ActionGreen;
            btnDelete.Enabled = false;

            if (dgvInsurances.Rows.Count > 0) { dgvInsurances.ClearSelection(); dgvInsurances.CurrentCell = null; }
            CalculateInstallment();
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (cmbEmployee.SelectedValue == null)
            {
                ModernMessageBox.ShowWarning("لطفاً یک کارمند را انتخاب کنید.");
                return;
            }

            decimal totalAmount = NumberHelper.ParseFormatted(txtTotalAmount.Text);
            if (totalAmount <= 0)
            {
                ModernMessageBox.ShowWarning("مبلغ بیمه باید بزرگتر از صفر باشد.");
                return;
            }

            if (numStartYear.Value <= 0) { ModernMessageBox.ShowWarning("سال شروع را انتخاب کنید."); return; }
            if (cmbStartMonth.SelectedItem == null) { ModernMessageBox.ShowWarning("ماه شروع را انتخاب کنید."); return; }

            int empId = (int)cmbEmployee.SelectedValue;
            int startYear = (int)numStartYear.Value;
            int startMonth = (int)cmbStartMonth.SelectedItem;
            decimal monthlyInstallment = NumberHelper.Round(totalAmount / (int)numInstallments.Value, 0);

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                if (_editingInsurance == null)
                {
                    var insurance = new EmployeeSupplementaryInsurance
                    {
                        EmployeeId = empId,
                        TotalAmount = totalAmount,
                        MonthlyInstallment = monthlyInstallment,
                        RemainingAmount = totalAmount,
                        StartYear = startYear,
                        StartMonth = startMonth,
                        StartDate = DateTime.Now,
                        Description = txtDescription.Text.Trim()
                    };
                    await _repository.AddAsync(insurance);
                }
                else
                {
                    _editingInsurance.TotalAmount = totalAmount;
                    _editingInsurance.MonthlyInstallment = monthlyInstallment;
                    _editingInsurance.StartYear = startYear;
                    _editingInsurance.StartMonth = startMonth;
                    _editingInsurance.Description = txtDescription.Text.Trim();

                    if (_editingInsurance.RemainingAmount > totalAmount)
                        _editingInsurance.RemainingAmount = totalAmount;

                    await _repository.UpdateAsync(_editingInsurance);
                }
            }, "در حال ذخیره بیمه تکمیلی...");

            if (success)
            {
                ToastHelper.Show("بیمه تکمیلی با موفقیت ذخیره شد.", isSuccess: true);
                ResetForm();
                _isInitializing = true;
                await LoadDataSafeAsync();
            }
        }

        private async void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (dgvInsurances.CurrentRow?.DataBoundItem is not EmployeeSupplementaryInsurance selected) return;

            if (!ModernMessageBox.AskConfirm(
                $"آیا از حذف بیمه تکمیلی «{selected.EmployeeName}» مطمئن هستید؟\n\n" +
                $"مبلغ: {NumberHelper.FormatThousands(selected.TotalAmount, usePersianDigits: true)} ریال\n" +
                $"مانده: {NumberHelper.FormatThousands(selected.RemainingAmount, usePersianDigits: true)} ریال",
                "حذف بیمه تکمیلی"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _repository.DeleteAsync(selected.Id);
            }, "در حال حذف بیمه تکمیلی...");

            if (success)
            {
                ToastHelper.Show("بیمه تکمیلی با موفقیت حذف شد.", isSuccess: true);
                ResetForm();
                _isInitializing = true;
                await LoadDataSafeAsync();
            }
        }
    }
}