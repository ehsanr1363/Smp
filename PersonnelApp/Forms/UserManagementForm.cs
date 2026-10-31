using Microsoft.Data.SqlClient;
using PersonnelApp.Data;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonnelApp.Forms
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

        public UserManagementForm(string connectionString)
        {
            _connectionString = connectionString;
            _userRepository = new UserRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("مدیریت کاربران و سطوح دسترسی", new Size(900, 750));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

            Shown += async (s, e) => await LoadDataSafeAsync();
        }

        private void BuildUI()
        {
            // استفاده از TableLayoutPanel اصلی برای جلوگیری از به هم ریختگی
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200F)); // پنل فرم
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // جدول
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));  // دکمه‌ها

            // ==========================================
            // ۱. پنل ورودی فرم
            // ==========================================
            var topPanel = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(15) };

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            for (int i = 0; i < 3; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));

            // ساخت کنترل‌ها
            txtUsername = new TextBox();
            txtPassword = new TextBox { UseSystemPasswordChar = true, PlaceholderText = "برای عدم تغییر، خالی بگذارید" };

            cmbRole = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbRole.Items.AddRange(new object[] { "Admin", "HR", "Viewer", "User" });
            cmbRole.SelectedIndex = 2;

            cmbEmployee = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbEmployee.DisplayMember = "FullName";
            cmbEmployee.ValueMember = "Id";

            chkIsActive = new CheckBox { Text = "کاربر فعال است", Checked = true, AutoSize = true };
            UIHelper.StyleToggle(chkIsActive);

            // اضافه کردن به جدول
            UIHelper.AddField(layout, "نام کاربری:", txtUsername, 0, 0, 1);
            UIHelper.AddField(layout, "رمز عبور:", txtPassword, 0, 2, 3);
            UIHelper.AddField(layout, "سطح دسترسی:", cmbRole, 1, 0, 1);
            UIHelper.AddField(layout, "اتصال به کارمند:", cmbEmployee, 1, 2, 3);

            // چک‌باکس وضعیت (در ردیف سوم)
            var flpStatus = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 0) };
            flpStatus.Controls.Add(chkIsActive);
            layout.Controls.Add(flpStatus, 0, 2);
            layout.SetColumnSpan(flpStatus, 4);

            topPanel.Controls.Add(layout);
            mainLayout.Controls.Add(topPanel, 0, 0);

            // ==========================================
            // ۲. جدول کاربران
            // ==========================================
            dgvUsers = new DataGridView { Dock = DockStyle.Fill };
            dgvUsers.ApplyModernStyle("PasswordHash");
            dgvUsers.SelectionChanged += DgvUsers_SelectionChanged;

            dgvUsers.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (dgvUsers.Columns[e.ColumnIndex].Name == "IsActive" && e.Value != null)
                {
                    bool isActive = Convert.ToBoolean(e.Value);
                    e.Value = isActive ? "✔ فعال" : "✖ غیرفعال";
                    e.CellStyle.ForeColor = isActive ? UIHelper.ActionGreen : UIHelper.WarmDanger;
                    e.FormattingApplied = true;
                }
            };

            mainLayout.Controls.Add(dgvUsers, 0, 1);

            // ==========================================
            // ۳. پنل دکمه‌ها (پایین)
            // ==========================================
            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            btnSave = UIHelper.CreateButton("➕ ثبت کاربر جدید", UIHelper.ActionGreen, 180, 40);
            btnSave.Click += BtnSave_Click;

            btnNew = UIHelper.CreateButton("🔄 جدید / پاک‌سازی فرم", UIHelper.SectionGray, 180, 40);
            btnNew.Click += (s, e) => ResetForm();

            btnDelete = UIHelper.CreateButton("🗑️ حذف کاربر انتخاب‌شده", UIHelper.ActionRed, 200, 40);
            btnDelete.Enabled = false;
            btnDelete.Click += BtnDelete_Click;

            bottomPanel.Controls.Add(btnSave);
            bottomPanel.Controls.Add(btnNew);
            bottomPanel.Controls.Add(btnDelete);

            mainLayout.Controls.Add(bottomPanel, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private async Task LoadDataSafeAsync()
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var employees = await new EmployeeRepository(_connectionString).GetEmployeesWithDetailsAsync("", false);
                employees.Insert(0, new Employee { Id = 0, FirstName = "--- بدون اتصال ---", LastName = "" });
                cmbEmployee.DataSource = employees;
                cmbEmployee.SelectedIndex = 0;

                var users = await _userRepository.GetAllUsersAsync();
                dgvUsers.DataSource = users;

                if (dgvUsers.Columns.Contains("Id")) dgvUsers.Columns["Id"].Visible = false;
                if (dgvUsers.Columns.Contains("EmployeeId")) dgvUsers.Columns["EmployeeId"].Visible = false;
                if (dgvUsers.Columns.Contains("PasswordHash")) dgvUsers.Columns["PasswordHash"].Visible = false;
                if (dgvUsers.Columns.Contains("CreatedAt")) dgvUsers.Columns["CreatedAt"].Visible = false;

                if (dgvUsers.Columns.Contains("Username")) dgvUsers.Columns["Username"].HeaderText = "نام کاربری";
                if (dgvUsers.Columns.Contains("Role")) dgvUsers.Columns["Role"].HeaderText = "سطح دسترسی";
                if (dgvUsers.Columns.Contains("EmployeeFullName")) dgvUsers.Columns["EmployeeFullName"].HeaderText = "کارمند متصل";
                if (dgvUsers.Columns.Contains("IsActive")) dgvUsers.Columns["IsActive"].HeaderText = "وضعیت";
                if (dgvUsers.Columns.Contains("LastLogin")) dgvUsers.Columns["LastLogin"].HeaderText = "آخرین ورود";
            }, "در حال بارگذاری اطلاعات...");

            _isInitializing = false;
            btnDelete.Enabled = dgvUsers.CurrentRow != null;
        }

        private void DgvUsers_SelectionChanged(object? sender, EventArgs e)
        {
            if (_isInitializing) return;
            if (dgvUsers.CurrentRow?.DataBoundItem is not User selectedUser) return;

            _editingUserId = selectedUser.Id;
            txtUsername.Text = selectedUser.Username;
            txtPassword.Clear();
            cmbRole.SelectedItem = selectedUser.Role;
            chkIsActive.Checked = selectedUser.IsActive;

            if (selectedUser.EmployeeId.HasValue)
            {
                foreach (Employee emp in cmbEmployee.Items)
                {
                    if (emp.Id == selectedUser.EmployeeId.Value)
                    {
                        cmbEmployee.SelectedItem = emp;
                        break;
                    }
                }
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
            if (string.IsNullOrWhiteSpace(txtUsername.Text))
            {
                ModernMessageBox.ShowWarning("نام کاربری الزامی است.");
                return;
            }

            if (_editingUserId == null && string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                ModernMessageBox.ShowWarning("رمز عبور برای کاربر جدید الزامی است.");
                return;
            }

            int? empId = null;
            if (cmbEmployee.SelectedItem is Employee selectedEmp && selectedEmp.Id > 0)
                empId = selectedEmp.Id;

            if (empId.HasValue)
            {
                bool isAssigned = await _userRepository.IsEmployeeAssignedToAnotherUserAsync(empId.Value, _editingUserId);
                if (isAssigned)
                {
                    ModernMessageBox.ShowWarning("این کارمند قبلاً به یک کاربر دیگر متصل شده است.\nهر کارمند فقط می‌تواند یک حساب کاربری داشته باشد.");
                    return;
                }
            }

            string username = txtUsername.Text.Trim();
            string role = cmbRole.SelectedItem?.ToString() ?? "Viewer";
            string hash = string.IsNullOrWhiteSpace(txtPassword.Text) ? "" : SecurityHelper.HashPassword(txtPassword.Text);

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                if (_editingUserId == null)
                {
                    var existing = await _userRepository.GetAllUsersAsync();
                    if (existing.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException("این نام کاربری قبلاً ثبت شده است.");

                    var newUser = new User
                    {
                        Username = username,
                        PasswordHash = hash,
                        Role = role,
                        EmployeeId = empId,
                        IsActive = chkIsActive.Checked
                    };
                    await _userRepository.AddAsync(newUser);
                }
                else
                {
                    var existing = await _userRepository.GetAllUsersAsync();
                    if (existing.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && u.Id != _editingUserId))
                        throw new InvalidOperationException("این نام کاربری قبلاً ثبت شده است.");

                    var updatedUser = new User
                    {
                        Id = _editingUserId.Value,
                        Username = username,
                        PasswordHash = hash,
                        Role = role,
                        EmployeeId = empId,
                        IsActive = chkIsActive.Checked
                    };
                    await _userRepository.UpdateAsync(updatedUser);
                }
            }, "در حال ذخیره اطلاعات کاربر...");

            if (success)
            {
                ToastHelper.Show("اطلاعات کاربر با موفقیت ذخیره شد.", isSuccess: true);
                ResetForm();
                _isInitializing = true;
                await LoadDataSafeAsync();
            }
        }

        private async void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (dgvUsers.CurrentRow?.DataBoundItem is not User selectedUser) return;

            if (selectedUser.Username.ToLower() == "admin")
            {
                ModernMessageBox.ShowWarning("کاربر اصلی سیستم (admin) قابل حذف نیست.");
                return;
            }

            if (!ModernMessageBox.AskConfirm($"آیا از حذف کاربر «{selectedUser.Username}» مطمئن هستید؟", "حذف کاربر"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await Task.Run(() => _userRepository.Delete(selectedUser.Id));
            }, "در حال حذف کاربر...");

            if (success)
            {
                ToastHelper.Show("کاربر با موفقیت حذف شد.", isSuccess: true);
                ResetForm();
                _isInitializing = true;
                await LoadDataSafeAsync();
            }
        }
    }
}