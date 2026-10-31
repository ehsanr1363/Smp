using PersonnelApp.Data;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonnelApp.Forms
{
    public class LoanManagementForm : Form
    {
        private readonly LoanRepository _repository;
        private readonly EmployeeRepository _empRepository;

        private ComboBox cmbEmployee = null!, cmbLoanType = null!, cmbStartMonth = null!;
        private NumericUpDown numStartYear = null!, numInstallments = null!;
        private TextBox txtAmount = null!, txtDescription = null!;
        private Label lblMonthlyInstallment = null!;
        private Button btnSave = null!, btnDelete = null!, btnNew = null!;
        private DataGridView dgvLoans = null!;

        private EmployeeLoan? _editingLoan = null;
        private bool _isInitializing = true;

        public LoanManagementForm(string connectionString)
        {
            _repository = new LoanRepository(connectionString);
            _empRepository = new EmployeeRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("مدیریت وام و مساعده پرسنل", new Size(1050, 750));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            Shown += async (s, e) => await LoadDataSafeAsync();
        }

        private void BuildUI()
        {
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 260, Padding = new Padding(10) };

            var formLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5 };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));

            for (int i = 0; i < 5; i++) formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));

            cmbEmployee = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "FullName", ValueMember = "Id" };
            UIHelper.AddField(formLayout, "کارمند:", cmbEmployee, 0, 0, 1, 3);

            cmbLoanType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbLoanType.Items.AddRange(new object[] { "وام", "مساعده" });
            cmbLoanType.SelectedIndex = 0;
            cmbLoanType.SelectedIndexChanged += (s, e) => OnLoanTypeChanged();
            UIHelper.AddField(formLayout, "نوع:", cmbLoanType, 1, 0, 1);

            txtAmount = new TextBox();
            SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
            txtAmount.TextChanged += (s, e) => CalculateInstallment();
            UIHelper.AddField(formLayout, "مبلغ کل (ریال):", txtAmount, 1, 2, 3);

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
            UIHelper.AddField(formLayout, "تعداد اقساط:", numInstallments, 2, 0, 1);

            lblMonthlyInstallment = new Label
            {
                Text = "مبلغ هر قسط: 0 ریال",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                ForeColor = UIHelper.ActionGreen
            };
            formLayout.Controls.Add(lblMonthlyInstallment, 2, 2);
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
            UIHelper.AddField(formLayout, "سال شروع:", numStartYear, 3, 0, 1);

            cmbStartMonth = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), BackColor = UIHelper.WarmInputBackground };
            for (int i = 1; i <= 12; i++) cmbStartMonth.Items.Add(i);
            cmbStartMonth.SelectedItem = currentMonth;
            UIHelper.AddField(formLayout, "ماه شروع:", cmbStartMonth, 3, 2, 3);

            txtDescription = new TextBox();
            UIHelper.AddField(formLayout, "توضیحات:", txtDescription, 4, 0, 1);
            formLayout.SetColumnSpan(txtDescription, 3);

            var btnPanelTop = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft };

            btnSave = UIHelper.CreateButton("➕ ثبت وام جدید", UIHelper.ActionGreen, 180, 40);
            btnSave.Click += BtnSave_Click;

            btnNew = UIHelper.CreateButton("🔄 جدید / پاک‌سازی", UIHelper.SectionGray, 180, 40);
            btnNew.Click += (s, e) => ResetForm();

            btnPanelTop.Controls.Add(btnSave);
            btnPanelTop.Controls.Add(btnNew);

            topPanel.Controls.Add(btnPanelTop);
            topPanel.Controls.Add(formLayout);

            dgvLoans = new DataGridView { Dock = DockStyle.Fill };
            dgvLoans.ApplyModernStyle("EmployeeId", "IsActive", "Id");
            dgvLoans.SelectionChanged += DgvLoans_SelectionChanged;

            dgvLoans.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                string colName = dgvLoans.Columns[e.ColumnIndex].Name;

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

            btnDelete = UIHelper.CreateButton("🗑️ حذف انتخاب‌شده", UIHelper.ActionRed, 200, 40);
            btnDelete.Enabled = false;
            btnDelete.Click += BtnDelete_Click;
            bottomPanel.Controls.Add(btnDelete);

            this.Controls.Add(dgvLoans);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(topPanel);
        }

        private void OnLoanTypeChanged()
        {
            bool isAdvance = cmbLoanType.SelectedItem?.ToString() == "مساعده";

            numInstallments.Enabled = !isAdvance;
            if (isAdvance)
            {
                numInstallments.Value = 1;
            }

            CalculateInstallment();
        }

        private void CalculateInstallment()
        {
            decimal total = NumberHelper.ParseFormatted(txtAmount.Text);
            bool isAdvance = cmbLoanType.SelectedItem?.ToString() == "مساعده";
            int installments = (int)numInstallments.Value;

            if (total > 0)
            {
                if (isAdvance)
                {
                    lblMonthlyInstallment.Text = $"کسر یک‌جا در ماه شروع: {NumberHelper.FormatThousands(total, usePersianDigits: true)} ریال";
                }
                else if (installments > 0)
                {
                    decimal monthly = NumberHelper.Round(total / installments, 0);
                    lblMonthlyInstallment.Text = $"مبلغ هر قسط: {NumberHelper.FormatThousands(monthly, usePersianDigits: true)} ریال";
                }
            }
            else
            {
                lblMonthlyInstallment.Text = isAdvance ? "کسر یک‌جا در ماه شروع: 0 ریال" : "مبلغ هر قسط: 0 ریال";
            }
        }

        private async Task LoadDataSafeAsync()
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var employees = await _empRepository.GetEmployeesWithDetailsAsync("", false);
                cmbEmployee.DataSource = employees;

                var loans = await _repository.GetActiveLoansAsync();
                dgvLoans.DataSource = loans;

                if (dgvLoans.Columns.Contains("LoanType")) dgvLoans.Columns["LoanType"].HeaderText = "نوع";
                if (dgvLoans.Columns.Contains("TotalAmount")) dgvLoans.Columns["TotalAmount"].HeaderText = "مبلغ کل";
                if (dgvLoans.Columns.Contains("MonthlyInstallment")) dgvLoans.Columns["MonthlyInstallment"].HeaderText = "مبلغ قسط";
                if (dgvLoans.Columns.Contains("RemainingAmount")) dgvLoans.Columns["RemainingAmount"].HeaderText = "مانده";
                if (dgvLoans.Columns.Contains("StartPeriod")) dgvLoans.Columns["StartPeriod"].HeaderText = "شروع کسر";
                if (dgvLoans.Columns.Contains("EmployeeName")) dgvLoans.Columns["EmployeeName"].HeaderText = "نام کارمند";
                if (dgvLoans.Columns.Contains("Description")) dgvLoans.Columns["Description"].HeaderText = "توضیحات";
            }, "در حال بارگذاری اطلاعات...");

            _isInitializing = false;
            btnDelete.Enabled = dgvLoans.CurrentRow != null;
        }

        private void DgvLoans_SelectionChanged(object? sender, EventArgs e)
        {
            if (_isInitializing) return;

            // اگر رکوردی انتخاب نشده باشد (مثلاً کلیک روی فضای خالی)، دکمه غیرفعال می‌شود
            if (dgvLoans.CurrentRow?.DataBoundItem is not EmployeeLoan selectedLoan)
            {
                _editingLoan = null;
                btnDelete.Enabled = false;
                return;
            }

            _editingLoan = selectedLoan;

            foreach (Employee emp in cmbEmployee.Items)
            {
                if (emp.Id == selectedLoan.EmployeeId)
                {
                    cmbEmployee.SelectedItem = emp;
                    break;
                }
            }

            cmbLoanType.SelectedItem = selectedLoan.LoanType;
            txtAmount.Text = NumberHelper.FormatThousands(selectedLoan.TotalAmount);

            decimal calcInst = selectedLoan.MonthlyInstallment > 0
                ? (int)Math.Round(selectedLoan.TotalAmount / selectedLoan.MonthlyInstallment)
                : 1;
            calcInst = Math.Max(numInstallments.Minimum, Math.Min(numInstallments.Maximum, calcInst));
            numInstallments.Value = calcInst;

            decimal startYear = selectedLoan.StartYear > 0 ? selectedLoan.StartYear : 1403;
            startYear = Math.Max(numStartYear.Minimum, Math.Min(numStartYear.Maximum, startYear));
            numStartYear.Value = startYear;

            if (selectedLoan.StartMonth >= 1 && selectedLoan.StartMonth <= 12)
                cmbStartMonth.SelectedItem = selectedLoan.StartMonth;

            txtDescription.Text = selectedLoan.Description;

            btnSave.Text = "💾 ذخیره تغییرات";
            btnSave.BackColor = UIHelper.SectionBlue;
            btnDelete.Enabled = true;
        }

        private void ResetForm()
        {
            _editingLoan = null;
            cmbLoanType.SelectedIndex = 0;
            txtAmount.Clear();
            numInstallments.Value = 12;
            numInstallments.Enabled = true;
            txtDescription.Clear();
            cmbEmployee.SelectedIndex = -1;

            var pc = new PersianCalendar();
            numStartYear.Value = pc.GetYear(DateTime.Now);
            cmbStartMonth.SelectedItem = pc.GetMonth(DateTime.Now);

            btnSave.Text = "➕ ثبت وام جدید";
            btnSave.BackColor = UIHelper.ActionGreen;
            btnDelete.Enabled = false;

            if (dgvLoans.Rows.Count > 0) { dgvLoans.ClearSelection(); dgvLoans.CurrentCell = null; }
            CalculateInstallment();
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (cmbEmployee.SelectedValue == null)
            {
                ModernMessageBox.ShowWarning("لطفاً یک کارمند را انتخاب کنید.");
                return;
            }

            decimal totalAmount = NumberHelper.ParseFormatted(txtAmount.Text);
            if (totalAmount <= 0)
            {
                ModernMessageBox.ShowWarning("مبلغ باید بزرگتر از صفر باشد.");
                return;
            }

            if (numStartYear.Value <= 0) { ModernMessageBox.ShowWarning("سال شروع را انتخاب کنید."); return; }
            if (cmbStartMonth.SelectedItem == null) { ModernMessageBox.ShowWarning("ماه شروع را انتخاب کنید."); return; }

            string loanType = cmbLoanType.SelectedItem?.ToString() ?? "وام";
            bool isAdvance = loanType == "مساعده";
            int empId = (int)cmbEmployee.SelectedValue;
            int startYear = (int)numStartYear.Value;
            int startMonth = (int)cmbStartMonth.SelectedItem;

            decimal monthlyInstallment = isAdvance
                ? totalAmount
                : NumberHelper.Round(totalAmount / (int)numInstallments.Value, 0);

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                if (_editingLoan == null)
                {
                    var loan = new EmployeeLoan
                    {
                        EmployeeId = empId,
                        LoanType = loanType,
                        TotalAmount = totalAmount,
                        MonthlyInstallment = monthlyInstallment,
                        RemainingAmount = totalAmount,
                        StartYear = startYear,
                        StartMonth = startMonth,
                        LoanDate = DateTime.Now,
                        Description = txtDescription.Text.Trim()
                    };
                    await _repository.AddLoanAsync(loan);
                }
                else
                {
                    _editingLoan.LoanType = loanType;
                    _editingLoan.TotalAmount = totalAmount;
                    _editingLoan.MonthlyInstallment = monthlyInstallment;
                    _editingLoan.StartYear = startYear;
                    _editingLoan.StartMonth = startMonth;
                    _editingLoan.Description = txtDescription.Text.Trim();

                    if (_editingLoan.RemainingAmount > totalAmount)
                        _editingLoan.RemainingAmount = totalAmount;

                    await _repository.UpdateLoanAsync(_editingLoan);
                }
            }, "در حال ذخیره...");

            if (success)
            {
                ToastHelper.Show($"{loanType} با موفقیت ذخیره شد.", isSuccess: true);
                ResetForm();
                _isInitializing = true;
                await LoadDataSafeAsync();
            }
        }

        private async void BtnDelete_Click(object? sender, EventArgs e)
        {
            // ✅ استفاده از _editingLoan برای جلوگیری از پاک شدن انتخاب هنگام کلیک روی دکمه
            if (_editingLoan == null)
            {
                ModernMessageBox.ShowWarning("لطفاً ابتدا یک رکورد را از جدول انتخاب کنید.");
                return;
            }

            if (!ModernMessageBox.AskConfirm(
                $"آیا از حذف {_editingLoan.LoanType} «{_editingLoan.EmployeeName}» مطمئن هستید؟\n\n" +
                $"مبلغ: {NumberHelper.FormatThousands(_editingLoan.TotalAmount, usePersianDigits: true)} ریال\n" +
                $"مانده: {NumberHelper.FormatThousands(_editingLoan.RemainingAmount, usePersianDigits: true)} ریال",
                "حذف"))
                return;

            bool success = false;
            string errorMessage = "";

            // ✅ استفاده از try-catch برای مدیریت خطای "استفاده در حقوق" بدون توقف در ویژوال استودیو
            try
            {
                success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    await _repository.DeleteLoanAsync(_editingLoan.Id);
                }, "در حال حذف...");
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }

            if (success)
            {
                ToastHelper.Show("با موفقیت حذف شد.", isSuccess: true);
                ResetForm();
                _isInitializing = true;
                await LoadDataSafeAsync();
            }
            else if (!string.IsNullOrEmpty(errorMessage))
            {
                // نمایش خطای مربوط به محاسبات حقوق به صورت اختصاصی
                ModernMessageBox.ShowError(errorMessage, "عدم امکان حذف");
            }
        }
    }
}