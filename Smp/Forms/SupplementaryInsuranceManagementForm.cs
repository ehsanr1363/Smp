using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class SupplementaryInsuranceManagementForm : Form
    {
        private readonly SupplementaryInsuranceRepository _repository;
        private readonly EmployeeRepository _empRepository;

        private ComboBox cmbEmployee = null!, cmbStartMonth = null!;
        private NumericUpDown numStartYear = null!, numInstallments = null!;
        private TextBox txtTotalAmount = null!, txtDescription = null!, txtSearch = null!;
        private Label lblMonthlyInstallment = null!;
        private Button btnSave = null!, btnDelete = null!, btnNew = null!;
        private DataGridView dgvInsurances = null!;

        private EmployeeSupplementaryInsurance? _editingInsurance = null;
        private bool _isInitializing = true;

        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly SemaphoreSlim _actionLock = new(1, 1);

        public SupplementaryInsuranceManagementForm(string connectionString)
        {
            _repository = new SupplementaryInsuranceRepository(connectionString);
            _empRepository = new EmployeeRepository(connectionString);

            BuildUI();
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
            Shown += async (s, e) => await LoadDataSafeAsync();
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

        private void BuildUI()
        {
            var (formLayout, grid, bottomPanel) = StandardFormHelper.SetupMasterDetailLayout(
                this, "مدیریت بیمه تکمیلی پرسنل", new Size(1100, 740));

            dgvInsurances = grid;
            dgvInsurances.AutoGenerateColumns = false;

            dgvInsurances.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgvInsurances.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "نام کارمند", FillWeight = 25 });
            dgvInsurances.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalAmount", DataPropertyName = "TotalAmount", HeaderText = "مبلغ کل", FillWeight = 15 });
            dgvInsurances.Columns.Add(new DataGridViewTextBoxColumn { Name = "MonthlyInstallment", DataPropertyName = "MonthlyInstallment", HeaderText = "مبلغ قسط", FillWeight = 15 });
            dgvInsurances.Columns.Add(new DataGridViewTextBoxColumn { Name = "RemainingAmount", DataPropertyName = "RemainingAmount", HeaderText = "مانده", FillWeight = 15 });
            dgvInsurances.Columns.Add(new DataGridViewTextBoxColumn { Name = "StartPeriod", DataPropertyName = "StartPeriod", HeaderText = "شروع کسر", FillWeight = 10 });
            dgvInsurances.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "توضیحات", FillWeight = 20 });

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
            dgvInsurances.SelectionChanged += (s, e) => { try { DgvInsurances_SelectionChanged(s, e); } catch (Exception ex) { LogHelper.LogException(ex, "SupplementaryInsuranceManagementForm.SelectionChanged"); } };

            cmbEmployee = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "FullName", ValueMember = "Id" };
            txtTotalAmount = new TextBox();
            SmartInputHelper.EnableSmartAmountTextBox(txtTotalAmount);
            txtTotalAmount.TextChanged += (s, e) => CalculateInstallment();
            numInstallments = new NumericUpDown { Minimum = 1, Maximum = 120, Value = 12, TextAlign = HorizontalAlignment.Center, Font = UIHelper.PersianFont(11f), BackColor = UIHelper.WarmInputBackground };
            numInstallments.ValueChanged += (s, e) => CalculateInstallment();

            var pc = new PersianCalendar();
            numStartYear = new NumericUpDown { Minimum = 1400, Maximum = 1450, Value = pc.GetYear(DateTime.Now), TextAlign = HorizontalAlignment.Center, Font = UIHelper.PersianFont(11f), BackColor = UIHelper.WarmInputBackground };
            cmbStartMonth = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), BackColor = UIHelper.WarmInputBackground };
            for (int i = 1; i <= 12; i++) cmbStartMonth.Items.Add(i);
            cmbStartMonth.SelectedItem = pc.GetMonth(DateTime.Now);

            lblMonthlyInstallment = new Label { Text = "مبلغ هر قسط: 0 ریال", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.ActionGreen, BackColor = UIHelper.WarmInputBackground, BorderStyle = BorderStyle.FixedSingle };
            txtDescription = new TextBox();

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "کارمند:", cmbEmployee, 0, 0, 1);
            formLayout.SetColumnSpan(cmbEmployee, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "مبلغ کل بیمه (ریال):", txtTotalAmount, 1, 0, 1);
            UIHelper.AddField(formLayout, "تعداد اقساط:", numInstallments, 1, 2, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "سال شروع کسر:", numStartYear, 2, 0, 1);
            UIHelper.AddField(formLayout, "ماه شروع کسر:", cmbStartMonth, 2, 2, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            formLayout.Controls.Add(new Label { Text = "مبلغ محاسبه‌شده:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText }, 0, 3);
            formLayout.Controls.Add(lblMonthlyInstallment, 1, 3);
            formLayout.SetColumnSpan(lblMonthlyInstallment, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "توضیحات:", txtDescription, 4, 0, 1);
            formLayout.SetColumnSpan(txtDescription, 3);

            btnSave = UIHelper.CreateButton("➕ ثبت بیمه جدید", UIHelper.ActionGreen, 160, 42);
            btnNew = UIHelper.CreateButton("🔄 جدید / پاک‌سازی", UIHelper.SectionGray, 160, 42);
            btnDelete = UIHelper.CreateButton("🗑️ حذف انتخاب‌شده", UIHelper.ActionRed, 180, 42);
            btnDelete.Enabled = false;

            btnSave.Click += async (s, e) => await SaveSafeAsync();
            btnNew.Click += (s, e) => ResetForm();
            btnDelete.Click += async (s, e) => await DeleteSafeAsync();

            bottomPanel.Controls.Add(btnSave);
            bottomPanel.Controls.Add(btnNew);
            bottomPanel.Controls.Add(btnDelete);

            var searchPanel = new TableLayoutPanel { Width = 300, Height = 42, ColumnCount = 2, Margin = new Padding(20, 0, 0, 0) };
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 35F));
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            var lblSearch = new Label { Text = "🔍", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(12f, FontStyle.Bold) };
            txtSearch = new TextBox { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f), PlaceholderText = "جستجو نام کارمند...", Margin = new Padding(0, 6, 0, 6) };
            searchPanel.Controls.Add(lblSearch, 0, 0);
            searchPanel.Controls.Add(txtSearch, 1, 0);
            bottomPanel.Controls.Add(searchPanel);
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
            else lblMonthlyInstallment.Text = "مبلغ هر قسط: 0 ریال";
        }

        private async Task LoadDataSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _loadLock.WaitAsync(0)) return;
            try
            {
                _isInitializing = true;
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var employees = await _empRepository.GetEmployeesWithDetailsAsync("", false);
                    cmbEmployee.DataSource = employees;
                    var insurances = await _repository.GetActiveInsurancesAsync();
                    dgvInsurances.DataSource = insurances;
                }, "در حال بارگذاری اطلاعات...");
                if (!success || IsDisposed) return;
                btnDelete.Enabled = dgvInsurances.CurrentRow != null;
            }
            finally { _isInitializing = false; try { _loadLock.Release(); } catch { } }
        }

        private void DgvInsurances_SelectionChanged(object? sender, EventArgs e)
        {
            if (_isInitializing || IsDisposed) return;
            if (dgvInsurances.CurrentRow?.DataBoundItem is not EmployeeSupplementaryInsurance selected) return;
            _editingInsurance = selected;

            foreach (Employee emp in cmbEmployee.Items)
                if (emp.Id == selected.EmployeeId) { cmbEmployee.SelectedItem = emp; break; }

            txtTotalAmount.Text = NumberHelper.FormatThousands(selected.TotalAmount);

            decimal calcInst = selected.MonthlyInstallment > 0 ? (int)Math.Round(selected.TotalAmount / selected.MonthlyInstallment) : 1;
            calcInst = Math.Max(numInstallments.Minimum, Math.Min(numInstallments.Maximum, calcInst));
            numInstallments.Value = calcInst;

            decimal startYear = selected.StartYear > 0 ? selected.StartYear : 1403;
            startYear = Math.Max(numStartYear.Minimum, Math.Min(numStartYear.Maximum, startYear));
            numStartYear.Value = startYear;

            if (selected.StartMonth >= 1 && selected.StartMonth <= 12) cmbStartMonth.SelectedItem = selected.StartMonth;
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

        private async Task SaveSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;
            try
            {
                if (cmbEmployee.SelectedValue == null) { ModernMessageBox.ShowWarning("لطفاً یک کارمند را انتخاب کنید."); return; }
                decimal totalAmount = NumberHelper.ParseFormatted(txtTotalAmount.Text);
                if (totalAmount <= 0) { ModernMessageBox.ShowWarning("مبلغ بیمه باید بزرگتر از صفر باشد."); return; }
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
                        decimal paidSoFar = _editingInsurance.TotalAmount - _editingInsurance.RemainingAmount;
                        if (paidSoFar < 0) paidSoFar = 0;
                        _editingInsurance.TotalAmount = totalAmount;
                        _editingInsurance.MonthlyInstallment = monthlyInstallment;
                        _editingInsurance.StartYear = startYear;
                        _editingInsurance.StartMonth = startMonth;
                        _editingInsurance.Description = txtDescription.Text.Trim();
                        _editingInsurance.RemainingAmount = Math.Max(0, totalAmount - paidSoFar);
                        await _repository.UpdateAsync(_editingInsurance);
                    }
                }, "در حال ذخیره بیمه تکمیلی...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("بیمه تکمیلی با موفقیت ذخیره شد.", isSuccess: true);
                    ResetForm();
                    _isInitializing = true;
                    await LoadDataSafeAsync();
                }
            }
            catch (Exception ex) { LogHelper.LogException(ex, "SupplementaryInsuranceManagementForm.SaveSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره:\n" + ex.Message); }
            finally { try { _actionLock.Release(); } catch { } }
        }

        private async Task DeleteSafeAsync()
        {
            if (IsDisposed) return;
            if (dgvInsurances.CurrentRow?.DataBoundItem is not EmployeeSupplementaryInsurance selected) return;
            if (!ModernMessageBox.AskConfirm($"آیا از حذف بیمه تکمیلی «{selected.EmployeeName}» مطمئن هستید؟\n\nمبلغ: {NumberHelper.FormatThousands(selected.TotalAmount, usePersianDigits: true)} ریال\nمانده: {NumberHelper.FormatThousands(selected.RemainingAmount, usePersianDigits: true)} ریال", "حذف بیمه تکمیلی")) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () => { await _repository.DeleteAsync(selected.Id); }, "در حال حذف...");
            if (success && !IsDisposed)
            {
                ToastHelper.Show("بیمه تکمیلی با موفقیت حذف شد.", isSuccess: true);
                ResetForm();
                _isInitializing = true;
                await LoadDataSafeAsync();
            }
        }
    }
}