using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class LoanManagementForm : Form
    {
        private readonly LoanRepository _repository;
        private readonly EmployeeRepository _empRepository;

        private TextBox txtEmployee = null!;
        private Button btnSelectEmployee = null!;
        private int? _selectedEmployeeId = null;
        private List<Employee> _allEmployees = new();

        private ComboBox cmbLoanType = null!, cmbStartMonth = null!;
        private NumericUpDown numStartYear = null!, numInstallments = null!;
        private TextBox txtAmount = null!, txtDescription = null!, txtSearch = null!;
        private Label lblMonthlyInstallment = null!;
        private Button btnSave = null!, btnDelete = null!, btnNew = null!;
        private DataGridView dgvLoans = null!;

        private EmployeeLoan? _editingLoan = null;
        private bool _isInitializing = true;

        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly SemaphoreSlim _actionLock = new(1, 1);

        public LoanManagementForm(string connectionString)
        {
            _repository = new LoanRepository(connectionString);
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
                this, "مدیریت وام و مساعده پرسنل", new Size(1100, 740));

            dgvLoans = grid;
            dgvLoans.AutoGenerateColumns = false;

            dgvLoans.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgvLoans.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "نام کارمند", FillWeight = 25 });
            dgvLoans.Columns.Add(new DataGridViewTextBoxColumn { Name = "LoanType", DataPropertyName = "LoanType", HeaderText = "نوع", FillWeight = 10 });
            dgvLoans.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalAmount", DataPropertyName = "TotalAmount", HeaderText = "مبلغ کل", FillWeight = 15 });
            dgvLoans.Columns.Add(new DataGridViewTextBoxColumn { Name = "MonthlyInstallment", DataPropertyName = "MonthlyInstallment", HeaderText = "مبلغ قسط", FillWeight = 15 });
            dgvLoans.Columns.Add(new DataGridViewTextBoxColumn { Name = "RemainingAmount", DataPropertyName = "RemainingAmount", HeaderText = "مانده", FillWeight = 15 });
            dgvLoans.Columns.Add(new DataGridViewTextBoxColumn { Name = "StartPeriod", DataPropertyName = "StartPeriod", HeaderText = "شروع کسر", FillWeight = 10 });
            dgvLoans.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "توضیحات", FillWeight = 10 });

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
            dgvLoans.SelectionChanged += (s, e) => { try { DgvLoans_SelectionChanged(s, e); } catch (Exception ex) { LogHelper.LogException(ex, "LoanManagementForm.SelectionChanged"); } };

            var pnlEmployee = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            pnlEmployee.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlEmployee.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45F));

            txtEmployee = new TextBox
            {
                ReadOnly = true,
                Dock = DockStyle.Fill,
                TextAlign = HorizontalAlignment.Center,
                BackColor = UIHelper.WarmInputBackground,
                Font = UIHelper.PersianFont(11f, FontStyle.Bold),
                Margin = new Padding(0, 0, 5, 0)
            };

            btnSelectEmployee = new Button
            {
                Text = "🔍",
                Dock = DockStyle.Fill,
                BackColor = UIHelper.SectionBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0)
            };
            btnSelectEmployee.FlatAppearance.BorderSize = 0;
            btnSelectEmployee.Click += BtnSelectEmployee_Click;

            pnlEmployee.Controls.Add(txtEmployee, 0, 0);
            pnlEmployee.Controls.Add(btnSelectEmployee, 1, 0);

            cmbLoanType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbLoanType.Items.AddRange(new object[] { "وام", "مساعده" });
            cmbLoanType.SelectedIndex = 0;
            cmbLoanType.SelectedIndexChanged += (s, e) => OnLoanTypeChanged();

            txtAmount = new TextBox();
            SmartInputHelper.EnableSmartAmountTextBox(txtAmount);
            txtAmount.TextChanged += (s, e) => CalculateInstallment();

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
            UIHelper.AddField(formLayout, "کارمند:", pnlEmployee, 0, 0, 1);
            formLayout.SetColumnSpan(pnlEmployee, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "نوع:", cmbLoanType, 1, 0, 1);
            UIHelper.AddField(formLayout, "مبلغ کل (ریال):", txtAmount, 1, 2, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "تعداد اقساط:", numInstallments, 2, 0, 1);
            formLayout.Controls.Add(new Label { Text = "مبلغ محاسبه‌شده:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText }, 2, 2);
            formLayout.Controls.Add(lblMonthlyInstallment, 3, 2);

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "سال شروع:", numStartYear, 3, 0, 1);
            UIHelper.AddField(formLayout, "ماه شروع:", cmbStartMonth, 3, 2, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "توضیحات:", txtDescription, 4, 0, 1);
            formLayout.SetColumnSpan(txtDescription, 3);

            btnSave = UIHelper.CreateButton("➕ ثبت وام جدید", UIHelper.ActionGreen, 160, 42);
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

        private void BtnSelectEmployee_Click(object? sender, EventArgs e)
        {
            using var selectForm = new Form();
            selectForm.Text = "انتخاب کارمند";
            selectForm.Size = new Size(600, 500);
            selectForm.StartPosition = FormStartPosition.CenterParent;
            selectForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            selectForm.MaximizeBox = false;
            selectForm.MinimizeBox = false;
            selectForm.RightToLeft = RightToLeft.Yes;
            selectForm.RightToLeftLayout = true;
            selectForm.Font = UIHelper.PersianFont(11f);

            var txtSearchEmp = new TextBox { Dock = DockStyle.Top, Margin = new Padding(10), Font = UIHelper.PersianFont(12f), PlaceholderText = "نام یا کد پرسنلی را جستجو کنید..." };
            var pnlSearch = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10) };
            pnlSearch.Controls.Add(txtSearchEmp);

            var dgvEmp = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoGenerateColumns = false, BackgroundColor = Color.White };
            dgvEmp.ApplyModernStyle();
            dgvEmp.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PersonnelCode", HeaderText = "کد پرسنلی", FillWeight = 30 });
            dgvEmp.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FullName", HeaderText = "نام و نام خانوادگی", FillWeight = 70 });

            var btnOk = UIHelper.CreateButton("✔ انتخاب", UIHelper.ActionGreen, 120, 40);
            btnOk.Dock = DockStyle.Bottom;
            btnOk.Height = 50;

            void FilterData()
            {
                string s = txtSearchEmp.Text.Trim();
                var filtered = _allEmployees.Where(x =>
                    (x.FullName ?? "").Contains(s) ||
                    (x.PersonnelCode ?? "").Contains(s)
                ).ToList();
                dgvEmp.DataSource = filtered;
            }

            txtSearchEmp.TextChanged += (s, e) => FilterData();
            FilterData();

            // 🔴 پشتیبانی از کلید اینتر در تکست‌باکس جستجو
            txtSearchEmp.KeyDown += (s, ev) =>
            {
                if (ev.KeyCode == Keys.Enter)
                {
                    ev.SuppressKeyPress = true;
                    if (dgvEmp.Rows.Count > 0)
                    {
                        dgvEmp.CurrentCell = dgvEmp.Rows[0].Cells[0]; // انتخاب اولین نتیجه
                        btnOk.PerformClick();
                    }
                }
            };

            dgvEmp.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0) { btnOk.PerformClick(); }
            };

            // 🔴 پشتیبانی از کلید اینتر در گرید
            dgvEmp.KeyDown += (s, ev) =>
            {
                if (ev.KeyCode == Keys.Enter)
                {
                    ev.SuppressKeyPress = true;
                    btnOk.PerformClick();
                }
            };

            btnOk.Click += (s, e) =>
            {
                if (dgvEmp.CurrentRow?.DataBoundItem is Employee selectedEmp)
                {
                    _selectedEmployeeId = selectedEmp.Id;
                    txtEmployee.Text = selectedEmp.FullName;
                    selectForm.DialogResult = DialogResult.OK;
                }
                else
                {
                    ModernMessageBox.ShowWarning("لطفاً یک کارمند را انتخاب کنید.");
                }
            };

            // 🔴 اختصاص دکمه اینتر به کل فرم (در صورت نیاز)
            selectForm.AcceptButton = btnOk;

            selectForm.Controls.Add(dgvEmp);
            selectForm.Controls.Add(btnOk);
            selectForm.Controls.Add(pnlSearch);

            selectForm.ShowDialog(this);
        }

        private void OnLoanTypeChanged()
        {
            bool isAdvance = cmbLoanType.SelectedItem?.ToString() == "مساعده";
            numInstallments.Enabled = !isAdvance;
            if (isAdvance) numInstallments.Value = 1;
            CalculateInstallment();
        }

        private void CalculateInstallment()
        {
            decimal total = NumberHelper.ParseFormatted(txtAmount.Text);
            bool isAdvance = cmbLoanType.SelectedItem?.ToString() == "مساعده";
            int installments = (int)numInstallments.Value;

            if (total > 0)
            {
                if (isAdvance) lblMonthlyInstallment.Text = $"کسر یک‌جا: {NumberHelper.FormatThousands(total, usePersianDigits: true)} ریال";
                else if (installments > 0)
                {
                    decimal monthly = NumberHelper.Round(total / installments, 0);
                    lblMonthlyInstallment.Text = $"مبلغ هر قسط: {NumberHelper.FormatThousands(monthly, usePersianDigits: true)} ریال";
                }
            }
            else lblMonthlyInstallment.Text = isAdvance ? "کسر یک‌جا: 0 ریال" : "مبلغ هر قسط: 0 ریال";
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
                    _allEmployees = await _empRepository.GetEmployeesWithDetailsAsync("", false);

                    var loans = await _repository.GetActiveLoansAsync();
                    dgvLoans.DataSource = loans;
                }, "در حال بارگذاری اطلاعات...");
                if (!success || IsDisposed) return;
                btnDelete.Enabled = dgvLoans.CurrentRow != null;
            }
            finally { _isInitializing = false; try { _loadLock.Release(); } catch { } }
        }

        private void DgvLoans_SelectionChanged(object? sender, EventArgs e)
        {
            if (_isInitializing || IsDisposed) return;
            if (dgvLoans.CurrentRow?.DataBoundItem is not EmployeeLoan selectedLoan) { _editingLoan = null; btnDelete.Enabled = false; return; }
            _editingLoan = selectedLoan;

            var emp = _allEmployees.FirstOrDefault(x => x.Id == selectedLoan.EmployeeId);
            if (emp != null)
            {
                _selectedEmployeeId = emp.Id;
                txtEmployee.Text = emp.FullName;
            }
            else
            {
                _selectedEmployeeId = null;
                txtEmployee.Text = "کارمند نامشخص";
            }

            cmbLoanType.SelectedItem = selectedLoan.LoanType;
            txtAmount.Text = NumberHelper.FormatThousands(selectedLoan.TotalAmount);

            decimal calcInst = selectedLoan.MonthlyInstallment > 0 ? (int)Math.Round(selectedLoan.TotalAmount / selectedLoan.MonthlyInstallment) : 1;
            calcInst = Math.Max(numInstallments.Minimum, Math.Min(numInstallments.Maximum, calcInst));
            numInstallments.Value = calcInst;

            decimal startYear = selectedLoan.StartYear > 0 ? selectedLoan.StartYear : 1403;
            startYear = Math.Max(numStartYear.Minimum, Math.Min(numStartYear.Maximum, startYear));
            numStartYear.Value = startYear;

            if (selectedLoan.StartMonth >= 1 && selectedLoan.StartMonth <= 12) cmbStartMonth.SelectedItem = selectedLoan.StartMonth;
            txtDescription.Text = selectedLoan.Description;

            btnSave.Text = "💾 ذخیره تغییرات";
            btnSave.BackColor = UIHelper.SectionBlue;
            btnDelete.Enabled = true;
        }

        private void ResetForm()
        {
            _editingLoan = null;
            _selectedEmployeeId = null;
            txtEmployee.Clear();

            cmbLoanType.SelectedIndex = 0;
            txtAmount.Clear();
            numInstallments.Value = 12;
            numInstallments.Enabled = true;
            txtDescription.Clear();

            var pc = new PersianCalendar();
            numStartYear.Value = pc.GetYear(DateTime.Now);
            cmbStartMonth.SelectedItem = pc.GetMonth(DateTime.Now);

            btnSave.Text = "➕ ثبت وام جدید";
            btnSave.BackColor = UIHelper.ActionGreen;
            btnDelete.Enabled = false;

            if (dgvLoans.Rows.Count > 0) { dgvLoans.ClearSelection(); dgvLoans.CurrentCell = null; }
            CalculateInstallment();
        }

        private async Task SaveSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;
            try
            {
                if (!_selectedEmployeeId.HasValue) { ModernMessageBox.ShowWarning("لطفاً یک کارمند را انتخاب کنید."); return; }
                decimal totalAmount = NumberHelper.ParseFormatted(txtAmount.Text);
                if (totalAmount <= 0) { ModernMessageBox.ShowWarning("مبلغ باید بزرگتر از صفر باشد."); return; }
                if (numStartYear.Value <= 0) { ModernMessageBox.ShowWarning("سال شروع را انتخاب کنید."); return; }
                if (cmbStartMonth.SelectedItem == null) { ModernMessageBox.ShowWarning("ماه شروع را انتخاب کنید."); return; }

                string loanType = cmbLoanType.SelectedItem?.ToString() ?? "وام";
                bool isAdvance = loanType == "مساعده";
                int empId = _selectedEmployeeId.Value;
                int startYear = (int)numStartYear.Value;
                int startMonth = (int)cmbStartMonth.SelectedItem;
                decimal monthlyInstallment = isAdvance ? totalAmount : NumberHelper.Round(totalAmount / (int)numInstallments.Value, 0);

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
                        decimal paidSoFar = _editingLoan.TotalAmount - _editingLoan.RemainingAmount;
                        if (paidSoFar < 0) paidSoFar = 0;
                        _editingLoan.LoanType = loanType;
                        _editingLoan.TotalAmount = totalAmount;
                        _editingLoan.MonthlyInstallment = monthlyInstallment;
                        _editingLoan.StartYear = startYear;
                        _editingLoan.StartMonth = startMonth;
                        _editingLoan.Description = txtDescription.Text.Trim();
                        _editingLoan.RemainingAmount = Math.Max(0, totalAmount - paidSoFar);
                        await _repository.UpdateLoanAsync(_editingLoan);
                    }
                }, "در حال ذخیره...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show($"{loanType} با موفقیت ذخیره شد.", isSuccess: true);
                    ResetForm();
                    _isInitializing = true;
                    await LoadDataSafeAsync();
                }
            }
            catch (Exception ex) { LogHelper.LogException(ex, "LoanManagementForm.SaveSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError("خطا در ذخیره وام:\n" + ex.Message); }
            finally { try { _actionLock.Release(); } catch { } }
        }

        private async Task DeleteSafeAsync()
        {
            if (IsDisposed) return;
            if (_editingLoan == null) { ModernMessageBox.ShowWarning("لطفاً ابتدا یک رکورد را از جدول انتخاب کنید."); return; }
            if (!ModernMessageBox.AskConfirm($"آیا از حذف {_editingLoan.LoanType} «{_editingLoan.EmployeeName}» مطمئن هستید؟\n\nمبلغ: {NumberHelper.FormatThousands(_editingLoan.TotalAmount, usePersianDigits: true)} ریال\nمانده: {NumberHelper.FormatThousands(_editingLoan.RemainingAmount, usePersianDigits: true)} ریال", "حذف")) return;

            bool success = false;
            string errorMessage = "";
            try
            {
                success = await AsyncHelper.RunWithLoading(this, async () => { await _repository.DeleteLoanAsync(_editingLoan.Id); }, "در حال حذف...");
            }
            catch (Exception ex) { errorMessage = ex.Message; }

            if (success && !IsDisposed)
            {
                ToastHelper.Show("با موفقیت حذف شد.", isSuccess: true);
                ResetForm();
                _isInitializing = true;
                await LoadDataSafeAsync();
            }
            else if (!string.IsNullOrEmpty(errorMessage) && !IsDisposed) ModernMessageBox.ShowError(errorMessage, "عدم امکان حذف");
        }
    }
}