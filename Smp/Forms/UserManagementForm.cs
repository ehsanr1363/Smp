using Microsoft.Data.SqlClient;
using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class UserManagementForm : Form
{
    private readonly string _connectionString;
    private readonly UserRepository _userRepository;

    private DataGridView dgvUsers = null!;
    private ModernTextBox txtUsername = null!, txtPassword = null!;
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
        // ── جدول ──
        dgvUsers = ModernGrid.Create();
        dgvUsers.AutoGenerateColumns = false;
        dgvUsers.FillColumns();
        dgvUsers.UseAiryLook(50);
        dgvUsers.UseSelectionAccent(ModernTheme.Primary);

        dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "Username", DataPropertyName = "Username", HeaderText = "نام کاربری", FillWeight = 22, MinimumWidth = 150 });
        dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "Role", DataPropertyName = "Role", HeaderText = "سطح دسترسی", FillWeight = 20, MinimumWidth = 150 });
        dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeFullName", DataPropertyName = "EmployeeFullName", HeaderText = "کارمند متصل", FillWeight = 26, MinimumWidth = 140 });
        dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsActive", DataPropertyName = "IsActive", HeaderText = "وضعیت", FillWeight = 12, MinimumWidth = 100 });
        dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "LastLogin", DataPropertyName = "LastLogin", HeaderText = "آخرین ورود", FillWeight = 20, MinimumWidth = 140, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        ModernListKit.PinHeaders(dgvUsers);   // «EmployeeFullName» و «LastLogin» دیگر انگلیسی نمایش داده نمی‌شوند

        dgvUsers.UsePersonColumn("Username");
        dgvUsers.UseBadges("IsActive", new Dictionary<string, Color>
        {
            ["فعال"] = ModernTheme.Success,
            ["غیرفعال"] = ModernTheme.Danger
        });
        dgvUsers.UseBadges("Role", new Dictionary<string, Color>
        {
            ["مدیر سیستم"] = ModernTheme.Purple,
            ["کارشناس منابع انسانی"] = ModernTheme.Teal,
            ["کاربر عادی"] = ModernTheme.Primary,
            ["بازدیدکننده"] = ModernTheme.Neutral
        });

        dgvUsers.SelectionChanged += DgvUsers_SelectionChanged;

        dgvUsers.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvUsers.Columns[e.ColumnIndex].Name;

            if (colName == "IsActive" && e.Value is not null)
            {
                e.Value = Convert.ToBoolean(e.Value) ? "فعال" : "غیرفعال";
                e.FormattingApplied = true;
            }
            else if (colName == "Role" && e.Value is not null)
            {
                string role = e.Value.ToString()!;
                if (_roleNames.ContainsKey(role)) e.Value = _roleNames[role];
                e.FormattingApplied = true;
            }
            else if (colName == "LastLogin")
            {
                if (e.Value is DateTime dt) e.Value = DateHelper.ToPersianDateTime(dt);
                else { e.Value = "—"; e.CellStyle.ForeColor = ModernTheme.Placeholder; }
                e.FormattingApplied = true;
            }
            else if (colName == "EmployeeFullName")
                e.CellStyle.ForeColor = ModernTheme.TextMuted;
        };

        // ── فرم ──
        txtUsername = ModernUI.Input();
        txtPassword = new ModernTextBox { Kind = ModernInputKind.Password, PlaceholderText = "برای عدم تغییر، خالی بگذارید" };
        cmbRole = ModernUI.Combo();
        cmbRole.Items.AddRange(new object[] { "مدیر سیستم", "کارشناس منابع انسانی", "کاربر عادی", "بازدیدکننده" });
        cmbRole.SelectedIndex = 2;
        cmbEmployee = ModernUI.Combo();
        cmbEmployee.DisplayMember = "FullName";
        cmbEmployee.ValueMember = "Id";
        chkIsActive = ModernUI.Check("کاربر فعال است");
        chkIsActive.Checked = true;

        int rh = ModernUI.RowHeight;
        var card = new ModernCard("اطلاعات کاربر", ModernTheme.Primary, 2, rh, rh, rh);
        card.Body.Controls.Add(ModernUI.Row("نام کاربری", txtUsername), 0, 0);
        card.Body.Controls.Add(ModernUI.Row("رمز عبور", txtPassword), 1, 0);
        card.Body.Controls.Add(ModernUI.Row("سطح دسترسی", cmbRole), 0, 1);
        card.Body.Controls.Add(ModernUI.Row("اتصال به کارمند", cmbEmployee), 1, 1);
        card.Body.Controls.Add(ModernUI.CenterInCell(chkIsActive, 12), 0, 2);

        // ── دکمه‌ها ──
        btnSave = ModernButton.Success("ثبت کاربر جدید", 170, 36);
        btnNew = ModernButton.Soft("جدید / پاک‌سازی فرم", ModernTheme.Neutral, 190, 36);
        btnDelete = ModernButton.Soft("حذف کاربر انتخاب‌شده", ModernTheme.Danger, 200, 36);
        btnDelete.Enabled = false;

        btnSave.Click += BtnSave_Click;
        btnNew.Click += (s, e) => ResetForm();
        btnDelete.Click += BtnDelete_Click;

        var actions = ModernListKit.BuildActionBar(new Control[] { btnSave, btnNew }, new Control[] { btnDelete });

        ModernListKit.ComposeRows(this, "مدیریت کاربران و سطوح دسترسی", "تعریف کاربران، نقش‌ها و اتصال آن‌ها به کارمندان", new Size(950, 700),
            (card, card.Height + 12), (ModernReport.WrapGrid(dgvUsers), 0), (actions, 64));
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

            if (success) btnDelete.Enabled = dgvUsers.CurrentRow is not null;
        }
        finally { _isInitializing = false; }
    }

    private void DgvUsers_SelectionChanged(object? sender, EventArgs e)
    {
        if (_isInitializing) return;
        if (dgvUsers.CurrentRow?.DataBoundItem is not User selectedUser) return;

        _editingUserId = selectedUser.Id;
        txtUsername.Text = selectedUser.Username;
        txtPassword.Text = string.Empty;
        cmbRole.SelectedItem = _roleNames.ContainsKey(selectedUser.Role) ? _roleNames[selectedUser.Role] : "بازدیدکننده";
        chkIsActive.Checked = selectedUser.IsActive;

        if (selectedUser.EmployeeId.HasValue)
        {
            foreach (Employee emp in cmbEmployee.Items)
                if (emp.Id == selectedUser.EmployeeId.Value) { cmbEmployee.SelectedItem = emp; break; }
        }
        else cmbEmployee.SelectedIndex = 0;

        btnSave.Text = "ذخیره تغییرات";
        btnSave.BackColor = ModernTheme.Primary;
        btnDelete.Enabled = selectedUser.Username.ToLower() != "admin";
    }

    private void ResetForm()
    {
        _editingUserId = null;
        txtUsername.Text = string.Empty;
        txtPassword.Text = string.Empty;
        cmbRole.SelectedIndex = 2;
        cmbEmployee.SelectedIndex = 0;
        chkIsActive.Checked = true;
        btnSave.Text = "ثبت کاربر جدید";
        btnSave.BackColor = ModernTheme.Success;
        btnDelete.Enabled = false;
        if (dgvUsers.Rows.Count > 0) { dgvUsers.ClearSelection(); dgvUsers.CurrentCell = null; }
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtUsername.Text)) { ModernMessageBox.ShowWarning("نام کاربری الزامی است."); return; }
        if (_editingUserId is null && string.IsNullOrWhiteSpace(txtPassword.Text)) { ModernMessageBox.ShowWarning("رمز عبور برای کاربر جدید الزامی است."); return; }

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
            if (_editingUserId is null)
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
