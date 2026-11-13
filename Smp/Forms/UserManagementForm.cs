using Microsoft.Data.SqlClient;
using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class UserManagementForm : Form
    {
        private readonly string _connectionString;
        private readonly UserRepository _userRepository;

        private DataGridView dgvUsers = null!;
        private TextBox txtUsername = null!, txtPassword = null!;
        private ComboBox cmbRole = null!, cmbEmployee = null!;
        private Button btnSave = null!, btnDelete = null!, btnNew = null!;
        private CheckBox chkIsActive = null!;

        private int? _editingUserId = null;
        private bool _isInitializing = true;

        private readonly Dictionary<string, string> _roleNames = new()
        {
            { "Admin", "مدیر سیستم" },
            { "HR", "کارشناس منابع انسانی" },
            { "User", "کاربر عادی" },
            { "Viewer", "بازدیدکننده" }
        };

        public UserManagementForm(string connectionString)
        {
            _connectionString = connectionString;
            _userRepository = new UserRepository(connectionString);

            BuildUI();
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            Shown += async (s, e) => await LoadDataSafeAsync();
        }

        private void BuildUI()
        {
            var (formLayout, grid, bottomPanel) = StandardFormHelper.SetupMasterDetailLayout(
                this, "مدیریت کاربران و سطوح دسترسی", new Size(950, 700));

            dgvUsers = grid;
            // ✅ تعریف دستی ستون‌ها
            dgvUsers.AutoGenerateColumns = false;

            dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "Username", DataPropertyName = "Username", HeaderText = "نام کاربری", FillWeight = 20 });
            dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "Role", DataPropertyName = "Role", HeaderText = "سطح دسترسی", FillWeight = 20 });
            dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeFullName", DataPropertyName = "EmployeeFullName", HeaderText = "کارمند متصل", FillWeight = 30 });
            dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsActive", DataPropertyName = "IsActive", HeaderText = "وضعیت", FillWeight = 15 });
            dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "LastLogin", DataPropertyName = "LastLogin", HeaderText = "آخرین ورود", FillWeight = 15 });

            dgvUsers.SelectionChanged += DgvUsers_SelectionChanged;

            dgvUsers.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                string colName = dgvUsers.Columns[e.ColumnIndex].Name;

                if (colName == "IsActive" && e.Value != null)
                {
                    bool isActive = Convert.ToBoolean(e.Value);
                    e.Value = isActive ? "✔ فعال" : "✖ غیرفعال";
                    e.CellStyle.ForeColor = isActive ? UIHelper.ActionGreen : UIHelper.WarmDanger;
                    e.FormattingApplied = true;
                }
                else if (colName == "Role" && e.Value != null)
                {
                    string role = e.Value.ToString()!;
                    if (_roleNames.ContainsKey(role)) e.Value = _roleNames[role];
                    e.FormattingApplied = true;
                }
                else if (colName == "LastLogin" && e.Value is DateTime dt)
                {
                    e.Value = DateHelper.ToPersianDateTime(dt);
                    e.FormattingApplied = true;
                }
            };

            // فیلدهای فرم
            txtUsername = new TextBox();
            txtPassword = new TextBox { UseSystemPasswordChar = true, PlaceholderText = "برای عدم تغییر، خالی بگذارید" };
            cmbRole = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbRole.Items.AddRange(new object[] { "مدیر سیستم", "کارشناس منابع انسانی", "کاربر عادی", "بازدیدکننده" });
            cmbRole.SelectedIndex = 2;
            cmbEmployee = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "FullName", ValueMember = "Id" };
            chkIsActive = new CheckBox { Text = "کاربر فعال است", Checked = true, AutoSize = true };
            UIHelper.StyleToggle(chkIsActive);

            // چیدمان فیلدها
            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "نام کاربری:", txtUsername, 0, 0, 1);
            UIHelper.AddField(formLayout, "رمز عبور:", txtPassword, 0, 2, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "سطح دسترسی:", cmbRole, 1, 0, 1);
            UIHelper.AddField(formLayout, "اتصال به کارمند:", cmbEmployee, 1, 2, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            formLayout.Controls.Add(chkIsActive, 0, 2);

            // دکمه‌ها
            btnSave = UIHelper.CreateButton("➕ ثبت کاربر جدید", UIHelper.ActionGreen, 180, 42);
            btnNew = UIHelper.CreateButton("🔄 جدید / پاک‌سازی فرم", UIHelper.SectionGray, 180, 42);
            btnDelete = UIHelper.CreateButton("🗑️ حذف کاربر انتخاب‌شده", UIHelper.ActionRed, 200, 42);
            btnDelete.Enabled = false;

            btnSave.Click += BtnSave_Click;
            btnNew.Click += (s, e) => ResetForm();
            btnDelete.Click += BtnDelete_Click;

            var btnContainer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            btnContainer.Controls.Add(btnSave);
            btnContainer.Controls.Add(btnNew);
            btnContainer.Controls.Add(btnDelete);
            bottomPanel.Controls.Add(btnContainer);
        }

        private async Task LoadDataSafeAsync()
        {
            _isInitializing = true;
            try
            {
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var employees = await new EmployeeRepository(_connectionString).GetEmployeesWithDetailsAsync("", false);
                    employees.Insert(0, new Employee { Id = 0, FirstName = "--- بدون اتصال ---", LastName = "" });
                    cmbEmployee.DataSource = employees;
                    cmbEmployee.SelectedIndex = 0;

                    var users = await _userRepository.GetAllUsersAsync();
                    dgvUsers.DataSource = users;
                }, "در حال بارگذاری اطلاعات...");

                if (success) btnDelete.Enabled = dgvUsers.CurrentRow != null;
            }
            finally { _isInitializing = false; }
        }

        private void DgvUsers_SelectionChanged(object? sender, EventArgs e)
        {
            if (_isInitializing) return;
            if (dgvUsers.CurrentRow?.DataBoundItem is not User selectedUser) return;

            _editingUserId = selectedUser.Id;
            txtUsername.Text = selectedUser.Username;
            txtPassword.Clear();
            cmbRole.SelectedItem = _roleNames.ContainsKey(selectedUser.Role) ? _roleNames[selectedUser.Role] : "بازدیدکننده";
            chkIsActive.Checked = selectedUser.IsActive;

            if (selectedUser.EmployeeId.HasValue)
            {
                foreach (Employee emp in cmbEmployee.Items)
                    if (emp.Id == selectedUser.EmployeeId.Value) { cmbEmployee.SelectedItem = emp; break; }
            }
            else cmbEmployee.SelectedIndex = 0;

            btnSave.Text = "💾 ذخیره تغییرات";
            btnSave.BackColor = UIHelper.SectionBlue;
            btnDelete.Enabled = selectedUser.Username.ToLower() != "admin";
        }

        private void ResetForm()
        {
            _editingUserId = null;
            txtUsername.Clear();
            txtPassword.Clear();
            cmbRole.SelectedIndex = 2;
            cmbEmployee.SelectedIndex = 0;
            chkIsActive.Checked = true;
            btnSave.Text = "➕ ثبت کاربر جدید";
            btnSave.BackColor = UIHelper.ActionGreen;
            btnDelete.Enabled = false;
            if (dgvUsers.Rows.Count > 0) { dgvUsers.ClearSelection(); dgvUsers.CurrentCell = null; }
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text)) { ModernMessageBox.ShowWarning("نام کاربری الزامی است."); return; }
            if (_editingUserId == null && string.IsNullOrWhiteSpace(txtPassword.Text)) { ModernMessageBox.ShowWarning("رمز عبور برای کاربر جدید الزامی است."); return; }

            int? empId = null;
            if (cmbEmployee.SelectedItem is Employee selectedEmp && selectedEmp.Id > 0) empId = selectedEmp.Id;

            if (empId.HasValue)
            {
                bool isAssigned = await _userRepository.IsEmployeeAssignedToAnotherUserAsync(empId.Value, _editingUserId);
                if (isAssigned) { ModernMessageBox.ShowWarning("این کارمند قبلاً به یک کاربر دیگر متصل شده است."); return; }
            }

            string username = txtUsername.Text.Trim();
            string selectedRole = cmbRole.SelectedItem?.ToString() ?? "بازدیدکننده";
            string role = _roleNames.FirstOrDefault(x => x.Value == selectedRole).Key ?? "Viewer";
            string hash = string.IsNullOrWhiteSpace(txtPassword.Text) ? "" : SecurityHelper.HashPassword(txtPassword.Text);

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                if (_editingUserId == null)
                {
                    var existing = await _userRepository.GetAllUsersAsync();
                    if (existing.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException("این نام کاربری قبلاً ثبت شده است.");
                    await _userRepository.AddAsync(new User { Username = username, PasswordHash = hash, Role = role, EmployeeId = empId, IsActive = chkIsActive.Checked });
                }
                else
                {
                    var existing = await _userRepository.GetAllUsersAsync();
                    if (existing.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && u.Id != _editingUserId))
                        throw new InvalidOperationException("این نام کاربری قبلاً ثبت شده است.");
                    await _userRepository.UpdateAsync(new User { Id = _editingUserId.Value, Username = username, PasswordHash = hash, Role = role, EmployeeId = empId, IsActive = chkIsActive.Checked });
                }
            }, "در حال ذخیره اطلاعات...");

            if (success) { ToastHelper.Show("اطلاعات کاربر با موفقیت ذخیره شد.", isSuccess: true); ResetForm(); await LoadDataSafeAsync(); }
        }

        private async void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (dgvUsers.CurrentRow?.DataBoundItem is not User selectedUser) return;
            if (selectedUser.Username.ToLower() == "admin") { ModernMessageBox.ShowWarning("کاربر اصلی سیستم (admin) قابل حذف نیست."); return; }
            if (!ModernMessageBox.AskConfirm($"آیا از حذف کاربر «{selectedUser.Username}» مطمئن هستید؟", "حذف کاربر")) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () => { await Task.Run(() => _userRepository.Delete(selectedUser.Id)); }, "در حال حذف...");
            if (success) { ToastHelper.Show("کاربر حذف شد.", isSuccess: true); ResetForm(); await LoadDataSafeAsync(); }
        }
    }
}