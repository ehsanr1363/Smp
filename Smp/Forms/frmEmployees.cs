using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using SMP.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace SMP.Forms
{
    public class frmEmployees : Form
    {
        private readonly EmployeeService _employeeService;
        private readonly DepartmentService _departmentService;
        private readonly LookupService _lookupService;
        private readonly AuditLogRepository _auditLogRepository;
        private readonly User? _loggedInUser;

        private DataGridView dgvEmployees = null!;
        private TextBox txtSearch = null!;
        private CheckBox chkShowInactive = null!;
        private ComboBox cmbPersonnelType = null!; // 👈 فیلتر نوع پرسنلی
        private Label lblStatus = null!;
        private Label lblCount = null!;
        private Panel pnlAlerts = null!;
        private Label lblAlerts = null!;
        private Button btnAdd = null!, btnEdit = null!, btnDelete = null!, btnRefresh = null!, btnBackup = null!;

        private readonly Timer _searchTimer;
        private BindingList<Employee>? _employeeBindingList;
        private string? _lastSortedColumn;
        private bool _sortAscending = true;

        private int _loadSequence = 0;

        public Employee? SelectedEmployee => dgvEmployees?.CurrentRow?.DataBoundItem as Employee;

        public frmEmployees(
            EmployeeService employeeService,
            DepartmentService departmentService,
            LookupService lookupService,
            AuditLogRepository auditLogRepository,
            User? loggedInUser = null)
        {
            _employeeService = employeeService;
            _departmentService = departmentService;
            _lookupService = lookupService;
            _auditLogRepository = auditLogRepository;
            _loggedInUser = loggedInUser;

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            BuildUI();

            _searchTimer = new Timer { Interval = 300 };
            _searchTimer.Tick += async (s, e) =>
            {
                _searchTimer.Stop();
                if (!IsDisposed) await LoadEmployeesAsync();
            };

            FormClosing += (s, e) => _searchTimer?.Stop();

            this.Disposed += (s, e) =>
            {
                try { _searchTimer?.Dispose(); } catch { }
            };

            this.Shown += async (s, e) => await OnShownAsync();
        }

        private async Task OnShownAsync()
        {
            try
            {
                await LoadEmployeesAsync();
                if (_loggedInUser != null && lblStatus != null && !IsDisposed)
                    lblStatus.Text = $"کاربر: {_loggedInUser.EmployeeFullName}  |  ✓ آماده";
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "frmEmployees.OnShownAsync");
                if (!IsDisposed)
                    lblStatus.Text = "⚠ خطا در بارگذاری اولیه — دکمه «بروزرسانی» را بزنید";
            }
        }

        private async Task LoadEmployeesAsync(int? selectedIdToKeep = null)
        {
            int mySeq = ++_loadSequence;

            try
            {
                var list = await _employeeService.GetEmployeesWithDetailsAsync(txtSearch.Text, chkShowInactive.Checked);

                if (mySeq != _loadSequence) return;
                if (IsDisposed || dgvEmployees.IsDisposed) return;

                // 👈 اعمال فیلتر نوع پرسنلی
                if (cmbPersonnelType.SelectedIndex > 0)
                {
                    string selectedType = cmbPersonnelType.SelectedItem?.ToString() ?? "";

                    if (selectedType == "قراردادی / ماهانه")
                    {
                        list = list.Where(x => x.PersonnelType != "روزمزد" && x.PersonnelType != "ساعتی").ToList();
                    }
                    else
                    {
                        list = list.Where(x => x.PersonnelType == selectedType).ToList();
                    }
                }

                _employeeBindingList = new BindingList<Employee>(list);
                dgvEmployees.DataSource = _employeeBindingList;

                foreach (DataGridViewColumn c in dgvEmployees.Columns) c.SortMode = DataGridViewColumnSortMode.Programmatic;

                if (lblCount != null && !IsDisposed)
                {
                    int a = list.Count(x => x.IsActive);
                    int i = list.Count(x => !x.IsActive);
                    lblCount.Text = $"مجموع: {NumberHelper.FormatThousands(list.Count, usePersianDigits: true)}  |  فعال: {NumberHelper.FormatThousands(a, usePersianDigits: true)}  |  غیرفعال: {NumberHelper.FormatThousands(i, usePersianDigits: true)}";
                }

                if (selectedIdToKeep.HasValue && _employeeBindingList != null && !IsDisposed)
                {
                    var target = _employeeBindingList.FirstOrDefault(x => x.Id == selectedIdToKeep.Value);
                    if (target != null)
                    {
                        int index = _employeeBindingList.IndexOf(target);
                        if (index >= 0 && index < dgvEmployees.Rows.Count)
                        {
                            dgvEmployees.ClearSelection();
                            dgvEmployees.Rows[index].Selected = true;
                            var firstVisible = dgvEmployees.Rows[index].Cells.Cast<DataGridViewCell>().FirstOrDefault(c => c.Visible);
                            if (firstVisible != null) dgvEmployees.CurrentCell = firstVisible;
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "frmEmployees.LoadEmployeesAsync");
                if (!IsDisposed)
                    ModernMessageBox.ShowError("خطا در بارگذاری لیست:\n" + ex.Message);
            }
        }

        private void btnAdd_Click(object? sender, EventArgs e)
        {
            try
            {
                using var form = new EmployeeEditForm(_employeeService, _departmentService, _lookupService);
                if (form.ShowDialog(this) == DialogResult.OK) _ = LoadEmployeesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "frmEmployees.btnAdd_Click");
                ModernMessageBox.ShowError("خطا در باز کردن فرم افزودن:\n" + ex.Message);
            }
        }

        private void btnEdit_Click(object? sender, EventArgs e)
        {
            if (dgvEmployees.CurrentRow?.DataBoundItem is not Employee emp)
            {
                ModernMessageBox.ShowWarning("لطفاً یک کارمند را انتخاب کنید.");
                return;
            }
            try
            {
                using var form = new EmployeeEditForm(_employeeService, _departmentService, _lookupService, emp);
                if (form.ShowDialog(this) == DialogResult.OK) _ = LoadEmployeesAsync(emp.Id);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "frmEmployees.btnEdit_Click");
                ModernMessageBox.ShowError("خطا در باز کردن فرم ویرایش:\n" + ex.Message);
            }
        }

        private async void btnDelete_Click(object? sender, EventArgs e)
        {
            if (dgvEmployees.CurrentRow?.DataBoundItem is not Employee emp) return;
            if (!ModernMessageBox.AskConfirm($"آیا از حذف {emp.FullName} مطمئن هستید؟", "تأیید حذف")) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await Task.Run(() => _employeeService.Delete(emp.Id));
                _ = _auditLogRepository.AddLogAsync(
                    _loggedInUser?.Username ?? "نامشخص",
                    "حذف",
                    "مدیریت پرسنل",
                    $"کارمند با کد {emp.PersonnelCode} حذف شد.");
            }, "در حال حذف...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("کارمند با موفقیت حذف شد.", isSuccess: true);
                await LoadEmployeesAsync();
            }
        }

        private async void btnRefresh_Click(object? sender, EventArgs e)
        {
            if (!IsDisposed) await LoadEmployeesAsync(SelectedEmployee?.Id);
        }

        private async void btnBackup_Click(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"Backup_Employees_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                Title = "ذخیره نسخه پشتیبان"
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            int count = 0;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                count = await Task.Run(() => ExcelExportHelper.BackupEmployees(AppConfig.ConnectionString, sfd.FileName));
            }, "در حال تهیه نسخه پشتیبان...");

            if (success && !IsDisposed)
                ToastHelper.Show($"پشتیبان ذخیره شد. تعداد: {count}", isSuccess: true);
        }

        private void txtSearch_TextChanged(object? sender, EventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void dgvEmployees_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvEmployees.Columns[e.ColumnIndex]?.Name ?? "";

            if (colName == "Gender" && e.Value != null)
            {
                e.Value = StringHelper.GenderToPersian(e.Value.ToString());
                e.FormattingApplied = true;
                return;
            }

            if (colName is "BirthDate" or "ContractStartDate" or "ContractEndDate")
            {
                if (e.Value is DateTime dt) { e.Value = DateHelper.ToPersianDate(dt); e.FormattingApplied = true; }
                else if (e.Value == null || e.Value == DBNull.Value) { e.Value = "---"; e.FormattingApplied = true; }
                return;
            }

            if (colName is "BaseSalary" or "YearsBonus" or "AttractionAllowance" or "ResponsibilityAllowance"
                or "HousingAllowance" or "GroceryAllowance" or "ChildAllowance" or "ManagementAllowance"
                or "TechnicalAllowance" or "MarriageAllowance" or "OtherAllowances" or "TransportationAllowance")
            {
                if (e.Value is decimal dec) { e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true); e.FormattingApplied = true; }
                else if (e.Value is int i) { e.Value = NumberHelper.FormatThousands(i, usePersianDigits: true); e.FormattingApplied = true; }
            }
        }

        private void dgvEmployees_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (_employeeBindingList == null || e.ColumnIndex < 0) return;
            var column = dgvEmployees.Columns[e.ColumnIndex];
            string prop = string.IsNullOrWhiteSpace(column.DataPropertyName) ? column.Name : column.DataPropertyName;
            if (prop == nameof(Employee.IsActive)) return;

            if (_lastSortedColumn == prop) _sortAscending = !_sortAscending;
            else { _lastSortedColumn = prop; _sortAscending = true; }

            var property = typeof(Employee).GetProperty(prop);
            if (property == null) return;

            var sorted = _employeeBindingList
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => property.GetValue(x), new EmployeePropertyComparer(_sortAscending))
                .ThenBy(x => x.Id)
                .ToList();

            _employeeBindingList.RaiseListChangedEvents = false;
            try
            {
                _employeeBindingList.Clear();
                foreach (var emp in sorted) _employeeBindingList.Add(emp);
            }
            finally
            {
                _employeeBindingList.RaiseListChangedEvents = true;
                _employeeBindingList.ResetBindings();
            }
        }

        private sealed class EmployeePropertyComparer : IComparer<object?>
        {
            private readonly bool _ascending;
            public EmployeePropertyComparer(bool asc) => _ascending = asc;

            public int Compare(object? x, object? y)
            {
                int r;
                if (ReferenceEquals(x, y)) r = 0;
                else if (x == null) r = -1;
                else if (y == null) r = 1;
                else if (x is string xs && y is string ys) r = StringComparer.CurrentCultureIgnoreCase.Compare(xs, ys);
                else if (x is IComparable c)
                {
                    try { r = c.CompareTo(y); }
                    catch { r = StringComparer.CurrentCultureIgnoreCase.Compare(x.ToString(), y.ToString()); }
                }
                else r = StringComparer.CurrentCultureIgnoreCase.Compare(x.ToString(), y.ToString());
                return _ascending ? r : -r;
            }
        }

        private void BuildUI()
        {
            SuspendLayout();
            Text = "مدیریت پرسنل";
            Size = new Size(1350, 750);
            MinimumSize = new Size(1200, 600);
            StartPosition = FormStartPosition.CenterScreen;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UIHelper.WarmBackground;
            Font = UIHelper.PersianFont(10f);
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedSingle;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = UIHelper.WarmBackground };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            root.Controls.Add(BuildToolbar(), 0, 0);
            root.Controls.Add(BuildAlerts(), 0, 1);
            root.Controls.Add(BuildGrid(), 0, 2);
            root.Controls.Add(BuildStatus(), 0, 3);
            Controls.Add(root);
            ResumeLayout();
        }

        private Panel BuildToolbar()
        {
            // 👈 تغییر به 3 ستون برای جدا کردن کامل عنوان از فیلترها
            var bar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(10, 5, 10, 5) };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // ستون راست: عنوان
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // ستون وسط: فیلترها
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // ستون چپ: دکمه‌ها

            // ═══════ ۱. ستون راست (عنوان) ═══════
            var lblTitle = new Label { Text = "📋 لیست پرسنل", Font = UIHelper.PersianFont(13f, FontStyle.Bold), ForeColor = UIHelper.WarmText, AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(5, 10, 20, 0) };
            bar.Controls.Add(lblTitle, 0, 0);

            // ═══════ ۲. ستون وسط (فیلترها) ═══════
            var pnlFilters = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, AutoSize = true, Padding = new Padding(0, 8, 0, 0) };

            var lblSearch = new Label { Text = "جستجو:", Font = UIHelper.PersianFont(10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(5, 10, 5, 0) };
            txtSearch = new TextBox { Width = 150, Font = UIHelper.PersianFont(11f), BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 6, 15, 0), PlaceholderText = "نام یا کد..." };
            txtSearch.TextChanged += txtSearch_TextChanged;
            pnlFilters.Controls.Add(lblSearch);
            pnlFilters.Controls.Add(txtSearch);

            var lblFilter = new Label { Text = "نوع قرارداد:", Font = UIHelper.PersianFont(10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(5, 10, 5, 0) };
            cmbPersonnelType = new ComboBox { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(10f), Margin = new Padding(0, 5, 15, 0), BackColor = Color.White };
            cmbPersonnelType.Items.AddRange(new object[] { "همه", "قراردادی / ماهانه", "روزمزد", "ساعتی" });
            cmbPersonnelType.SelectedIndex = 0;
            cmbPersonnelType.SelectedIndexChanged += async (s, e) => { if (!IsDisposed) await LoadEmployeesAsync(SelectedEmployee?.Id); };
            pnlFilters.Controls.Add(lblFilter);
            pnlFilters.Controls.Add(cmbPersonnelType);

            chkShowInactive = new CheckBox { Text = "نمایش غیرفعال‌ها", Font = UIHelper.PersianFont(10f), ForeColor = UIHelper.WarmText, AutoSize = true, Margin = new Padding(0, 8, 10, 0) };
            UIHelper.StyleToggle(chkShowInactive);
            chkShowInactive.CheckedChanged += async (s, e) => { if (!IsDisposed) await LoadEmployeesAsync(SelectedEmployee?.Id); };
            pnlFilters.Controls.Add(chkShowInactive);

            bar.Controls.Add(pnlFilters, 1, 0);

            // ═══════ ۳. ستون چپ (دکمه‌ها) ═══════
            var pnlButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Padding = new Padding(0, 5, 0, 0) };

            btnAdd = MakeBtn("➕ افزودن", UIHelper.ActionGreen, btnAdd_Click);
            btnEdit = MakeBtn("✏️ ویرایش", UIHelper.SectionBlue, btnEdit_Click);
            btnDelete = MakeBtn("🗑️ حذف", UIHelper.ActionRed, btnDelete_Click);
            btnRefresh = MakeBtn("🔄 بروزرسانی", UIHelper.SectionGray, btnRefresh_Click);
            btnBackup = MakeBtn("💾 بک‌آپ", UIHelper.SectionTeal, btnBackup_Click);

            pnlButtons.Controls.AddRange(new Control[] { btnAdd, btnEdit, btnDelete, btnRefresh, btnBackup });
            bar.Controls.Add(pnlButtons, 2, 0);

            return bar;
        }

        private Button MakeBtn(string text, Color color, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                Font = UIHelper.PersianFont(9.5f, FontStyle.Bold),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                AutoSize = true,
                MinimumSize = new Size(100, 32),
                Padding = new Padding(10, 2, 10, 2),
                Margin = new Padding(4, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            return btn;
        }

        private Panel BuildAlerts()
        {
            pnlAlerts = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmWarningBackground, Visible = false, Padding = new Padding(12, 4, 12, 4) };
            lblAlerts = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(10.5f, FontStyle.Bold), ForeColor = UIHelper.WarmWarningText };
            pnlAlerts.Controls.Add(lblAlerts);
            return pnlAlerts;
        }

        private DataGridView BuildGrid()
        {
            dgvEmployees = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = true,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AllowUserToResizeRows = false,
                Padding = new Padding(12, 4, 12, 4),
                BackgroundColor = UIHelper.WarmBackground
            };
            dgvEmployees.ApplyModernStyle("Photo");

            dgvEmployees.DataBindingComplete += (s, e) =>
            {
                if (IsDisposed) return;

                if (dgvEmployees.Columns["AgeNumber"] != null)
                    dgvEmployees.Columns["AgeNumber"].Visible = false;

                foreach (DataGridViewColumn col in dgvEmployees.Columns)
                {
                    col.HeaderText = TranslationHelper.GetColumnName(col.Name);
                    // 👈 اطمینان از اینکه ستون نوع پرسنلی حتماً نمایش داده می‌شود
                    if (col.Name == "PersonnelType") col.Visible = true;
                }

                foreach (DataGridViewRow r in dgvEmployees.Rows)
                    if (r.Height != 38) r.Height = 38;
            };

            dgvEmployees.ColumnHeaderMouseClick += dgvEmployees_ColumnHeaderMouseClick;
            dgvEmployees.CellFormatting += dgvEmployees_CellFormatting;
            dgvEmployees.SelectionChanged += (s, e) =>
            {
                if (IsDisposed || lblStatus == null) return;
                if (dgvEmployees.CurrentRow?.DataBoundItem is Employee emp)
                    lblStatus.Text = $"کارمند انتخاب‌شده: {emp.FullName}";
                else
                    lblStatus.Text = "✓  آماده";
            };

            dgvEmployees.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgvEmployees.Rows[e.RowIndex].DataBoundItem is Employee emp)
                {
                    try
                    {
                        using var form = new EmployeeEditForm(_employeeService, _departmentService, _lookupService, emp);
                        if (form.ShowDialog(this) == DialogResult.OK) _ = LoadEmployeesAsync(emp.Id);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogException(ex, "frmEmployees.CellDoubleClick");
                        ModernMessageBox.ShowError("خطا در باز کردن فرم ویرایش:\n" + ex.Message);
                    }
                }
            };

            dgvEmployees.RowPrePaint += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgvEmployees.Rows[e.RowIndex].DataBoundItem is Employee emp && !emp.IsActive)
                {
                    dgvEmployees.Rows[e.RowIndex].DefaultCellStyle.ForeColor = UIHelper.WarmErrorText;
                    dgvEmployees.Rows[e.RowIndex].DefaultCellStyle.BackColor = UIHelper.WarmErrorBackground;
                }
            };

            return dgvEmployees;
        }

        private Panel BuildStatus()
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground, ColumnCount = 2, Padding = new Padding(16, 0, 16, 0) };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            lblStatus = new Label { Text = "✓  آماده", Font = UIHelper.PersianFont(10f), ForeColor = UIHelper.WarmText, AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 8, 0, 0) };
            panel.Controls.Add(lblStatus, 0, 0); // در حالت RTL این ستون سمت راست است

            lblCount = new Label { Text = "مجموع: 0", Font = UIHelper.PersianFont(10f), ForeColor = UIHelper.WarmText, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 0, 0) };
            panel.Controls.Add(lblCount, 1, 0); // این ستون سمت چپ است

            return panel;
        }
    }
}