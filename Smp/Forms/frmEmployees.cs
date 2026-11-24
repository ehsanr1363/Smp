using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using Smp.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Smp.Forms;

public class frmEmployees : Form
{
    private readonly EmployeeService _employeeService;
    private readonly DepartmentService _departmentService;
    private readonly LookupService _lookupService;
    private readonly AuditLogRepository _auditLogRepository;
    private readonly User? _loggedInUser;

    private DataGridView dgvEmployees = null!;
    private ModernTextBox txtSearch = null!;
    private CheckBox chkShowInactive = null!;
    private readonly string[] _typeItems = { "همه", "قراردادی / ماهانه", "روزمزد", "ساعتی" };
    private readonly List<ModernTabButton> _typeTabs = new();
    private string _personnelTypeFilter = "همه";   // فیلتر نوع پرسنلی (تب‌های بالای لیست)
    private Label lblStatus = null!;
    private ModernStatCard cardTotal = null!, cardActive = null!, cardInactive = null!;
    private Panel pnlAlerts = null!;
    private Label lblAlerts = null!;
    private ModernButton btnAdd = null!, btnEdit = null!, btnDelete = null!, btnRefresh = null!, btnBackup = null!, btnPrintContracts = null!;

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
            try { _searchTimer?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "frmEmployees"); }
        };

        this.Shown += async (s, e) => await OnShownAsync();
    }

    private async Task OnShownAsync()
    {
        try
        {
            await LoadEmployeesAsync();
            if (_loggedInUser is not null && lblStatus is not null && !IsDisposed)
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
            if (_personnelTypeFilter != _typeItems[0])
            {
                string selectedType = _personnelTypeFilter;

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

            if (cardTotal is not null && !IsDisposed)
            {
                cardTotal.SetCount(list.Count, "نفر");
                cardActive.SetCount(list.Count(x => x.IsActive), "نفر");
                cardInactive.SetCount(list.Count(x => !x.IsActive), "نفر");
            }

            if (selectedIdToKeep.HasValue && _employeeBindingList is not null && !IsDisposed)
            {
                var target = _employeeBindingList.FirstOrDefault(x => x.Id == selectedIdToKeep.Value);
                if (target is not null)
                {
                    int index = _employeeBindingList.IndexOf(target);
                    if (index >= 0 && index < dgvEmployees.Rows.Count)
                    {
                        dgvEmployees.ClearSelection();
                        dgvEmployees.Rows[index].Selected = true;
                        var firstVisible = dgvEmployees.Rows[index].Cells.Cast<DataGridViewCell>().FirstOrDefault(c => c.Visible);
                        if (firstVisible is not null) dgvEmployees.CurrentCell = firstVisible;
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

        if (colName == "Gender" && e.Value is not null)
        {
            e.Value = StringHelper.GenderToPersian(e.Value.ToString());
            e.FormattingApplied = true;
            return;
        }

        if (colName is "BirthDate" or "ContractStartDate" or "ContractEndDate")
        {
            if (e.Value is DateTime dt) { e.Value = DateHelper.ToPersianDate(dt); e.FormattingApplied = true; }
            else if (e.Value is null || e.Value == DBNull.Value) { e.Value = "---"; e.FormattingApplied = true; }
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
        if (_employeeBindingList is null || e.ColumnIndex < 0) return;
        var column = dgvEmployees.Columns[e.ColumnIndex];
        string prop = string.IsNullOrWhiteSpace(column.DataPropertyName) ? column.Name : column.DataPropertyName;
        if (prop == nameof(Employee.IsActive)) return;

        if (_lastSortedColumn == prop) _sortAscending = !_sortAscending;
        else { _lastSortedColumn = prop; _sortAscending = true; }

        var property = typeof(Employee).GetProperty(prop);
        if (property is null) return;

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
            else if (x is null) r = -1;
            else if (y is null) r = 1;
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
        BackColor = ModernTheme.Background;
        Font = ModernTheme.GetFont(10f);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;

        var root = new BufferedTable
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = ModernTheme.Background,
            Padding = new Padding(14, 12, 14, 0)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));    // نوار عنوان و دکمه‌ها
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));    // جستجو و تب‌های نوع پرسنل
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));         // هشدارها (خالی = ارتفاع صفر)
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));    // کارت‌های خلاصه
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));    // جدول
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));    // نوار وضعیت
        root.Controls.Add(BuildToolbar(), 0, 0);
        root.Controls.Add(BuildFilters(), 0, 1);
        root.Controls.Add(BuildAlerts(), 0, 2);
        root.Controls.Add(BuildStats(), 0, 3);
        root.Controls.Add(BuildGrid(), 0, 4);
        root.Controls.Add(BuildStatus(), 0, 5);
        Controls.Add(root);
        ResumeLayout();
    }

    // نکته: در فرم‌های Mirror شده (RightToLeftLayout) جهت LeftToRight در FlowLayoutPanel
    // یعنی «اولین کنترل در راست‌ترین جا»؛ پس همه‌جا LeftToRight استفاده شده است.
    private Control BuildToolbar()
    {
        var card = new ModernCard("", ModernTheme.Primary, 1) { Margin = new Padding(0, 0, 0, 10) };
        card.Body.Padding = new Padding(14, 4, 24, 4);

        var bar = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));       // راست: عنوان
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));  // وسط: عملیات اصلی
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));       // چپ: عملیات جانبی
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var lblTitle = new Label
        {
            Text = "📋  لیست پرسنل",
            Font = ModernTheme.GetFont(13f, FontStyle.Bold),
            ForeColor = ModernTheme.TextStrong,
            AutoSize = true,
            Anchor = AnchorStyles.None,
            Margin = new Padding(4, 0, 24, 0),
            BackColor = Color.Transparent
        };
        bar.Controls.Add(lblTitle, 0, 0);

        btnAdd     = MakeBtn("➕  افزودن",    ModernTheme.Success, 125, ModernButtonStyle.Filled,  btnAdd_Click);
        btnEdit    = MakeBtn("✏️  ویرایش",    ModernTheme.Primary, 125, ModernButtonStyle.Soft,    btnEdit_Click);
        btnDelete  = MakeBtn("🗑️  حذف",       ModernTheme.Danger,  110, ModernButtonStyle.Soft,    btnDelete_Click);
        btnRefresh = MakeBtn("🔄  بروزرسانی", ModernTheme.Neutral, 135, ModernButtonStyle.Outline, btnRefresh_Click);
        btnBackup  = MakeBtn("💾  بک‌آپ",     ModernTheme.Teal,    120, ModernButtonStyle.Soft,    btnBackup_Click);

        var main = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = new Padding(0, 3, 0, 0) };
        main.Controls.Add(btnAdd);
        main.Controls.Add(btnEdit);
        main.Controls.Add(btnDelete);
        bar.Controls.Add(main, 1, 0);

        var side = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = new Padding(0, 3, 0, 0) };
        btnPrintContracts = MakeBtn("🖨️  چاپ گروهی قرارداد", ModernTheme.Primary, 170, ModernButtonStyle.Outline, btnPrintContracts_Click);
        side.Controls.Add(btnPrintContracts);
        side.Controls.Add(btnRefresh);
        side.Controls.Add(btnBackup);
        bar.Controls.Add(side, 2, 0);

        card.Body.Controls.Add(bar, 0, 0);
        return card;
    }

    private void btnPrintContracts_Click(object? sender, EventArgs e)
    {
        // نوع انتخاب‌شده در تب‌های بالای لیست، پیش‌فرض فرم چاپ گروهی است
        string type = _personnelTypeFilter == _typeItems[0] ? "قراردادی / ماهانه" : _personnelTypeFilter;
        using var f = new BatchContractPrintForm(AppConfig.ConnectionString, type);
        f.ShowDialog(this);
    }

    private ModernButton MakeBtn(string text, Color color, int width, ModernButtonStyle style, EventHandler onClick)
    {
        var btn = ModernButton.Create(text, color, width, 42, style);
        btn.Margin = new Padding(4, 0, 4, 0);
        btn.Click += onClick;
        return btn;
    }

    private Control BuildFilters()
    {
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(0, 4, 0, 0)
        };

        txtSearch = ModernUI.Input(placeholder: "🔍  جستجوی نام یا کد پرسنلی...");
        txtSearch.Width = 300;
        txtSearch.Height = ModernUI.InputHeight;
        txtSearch.Margin = new Padding(0, 2, 16, 0);
        txtSearch.TextBox.TextChanged += txtSearch_TextChanged;
        flow.Controls.Add(txtSearch);

        // تب‌های نوع پرسنل (جایگزین کمبوباکس «نوع قرارداد»)
        foreach (var item in _typeItems)
        {
            var tab = new ModernTabButton(item, 36, 90) { Margin = new Padding(3, 2, 3, 0), Accent = ModernTheme.Primary, Selected = item == _personnelTypeFilter };
            string value = item;
            tab.Click += async (s, e) =>
            {
                if (_personnelTypeFilter == value) return;
                _personnelTypeFilter = value;
                foreach (var tb in _typeTabs) tb.Selected = tb.Text == value;
                if (!IsDisposed) await LoadEmployeesAsync(SelectedEmployee?.Id);
            };
            _typeTabs.Add(tab);
            flow.Controls.Add(tab);
        }

        chkShowInactive = ModernUI.Check("نمایش غیرفعال‌ها", ModernTheme.Primary);
        chkShowInactive.Width = 170;
        chkShowInactive.Margin = new Padding(18, 4, 0, 0);
        chkShowInactive.CheckedChanged += async (s, e) => { if (!IsDisposed) await LoadEmployeesAsync(SelectedEmployee?.Id); };
        flow.Controls.Add(chkShowInactive);

        return flow;
    }

    private Control BuildAlerts()
    {
        pnlAlerts = new Panel { Dock = DockStyle.Fill, BackColor = ModernTheme.WarningSoft, Visible = false, Padding = new Padding(12, 6, 12, 6), Margin = new Padding(0, 0, 0, 8) };
        lblAlerts = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = ModernTheme.GetFont(10.5f, FontStyle.Bold), ForeColor = ModernTheme.WarningText };
        pnlAlerts.Controls.Add(lblAlerts);
        return pnlAlerts;
    }

    private Control BuildStats()
    {
        var strip = ModernReport.CreateStatStrip(3, 50);
        strip.Dock = DockStyle.Fill;
        cardTotal = strip.Add("مجموع پرسنل", ModernTheme.Primary);
        cardActive = strip.Add("پرسنل فعال", ModernTheme.Success);
        cardInactive = strip.Add("پرسنل غیرفعال", ModernTheme.Danger);
        cardTotal.SetCount(0, "نفر");
        cardActive.SetCount(0, "نفر");
        cardInactive.SetCount(0, "نفر");
        return strip;
    }

    private Control BuildGrid()
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
            AllowUserToResizeRows = false
        };
        dgvEmployees.ApplyModernStyle("Photo");

        // ── ظاهر مدرن ──
        dgvEmployees.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;   // ستون‌ها کل عرض را پر کنند
        dgvEmployees.UseAiryLook(48);                 // هدر سبک، ردیف بلندتر، بدون راه‌راه
        dgvEmployees.UseSelectionAccent();            // نوار رنگی کنار ردیف انتخاب‌شده
        dgvEmployees.UsePersonColumn("FullName", "PersonnelCode");   // آواتار + نام + کد پرسنلی زیر نام
        dgvEmployees.UseTextAlignment("Position", "DepartmentName", "CompanyTitle", "WorkLocation");
        dgvEmployees.UseBadges("Gender", new Dictionary<string, Color>
        {
            ["مرد"] = ModernTheme.Primary,
            ["زن"] = ModernTheme.Purple
        });
        dgvEmployees.UseBadges(new[] { "MaritalStatus" }, new Dictionary<string, Color>
        {
            ["متاهل"] = ModernTheme.Success,
            ["متأهل"] = ModernTheme.Success,
            ["مجرد"] = ModernTheme.Warning
        });
        dgvEmployees.UseBadges("PersonnelType", new Dictionary<string, Color>
        {
            ["روزمزد"] = ModernTheme.Orange,
            ["ساعتی"] = ModernTheme.Teal,
            ["قراردادی"] = ModernTheme.Primary,
            ["ماهانه"] = ModernTheme.Primary
        });

        // فقط ستون‌های مهم در ابتدا؛ بقیه با راست‌کلیک روی هدر قابل‌نمایش‌اند (بدون اسکرول افقی)
        dgvEmployees.UseColumnChooser(
            defaultVisible: new[]
            {
                "FullName", "NationalCode", "PersonnelType", "Position", "DepartmentName", "WorkLocation",
                "PhoneNumber", "Gender", "MaritalStatus", "ContractEndDate"
            },
            neverShow: new[] { "Photo", "AgeNumber", "FirstName", "LastName", "PersonnelCode", "Id" });

        dgvEmployees.DataBindingComplete += (s, e) =>
        {
            if (IsDisposed) return;

            foreach (DataGridViewColumn col in dgvEmployees.Columns)
            {
                col.HeaderText = TranslationHelper.GetColumnName(col.Name);
                if (col.Name == "FullName") { col.HeaderText = "پرسنل"; col.DisplayIndex = 0; col.MinimumWidth = 230; col.FillWeight = 160; }
            }

            // «محل خدمت» درست کنار «واحد سازمانی» قرار بگیرد
            if (dgvEmployees.Columns["WorkLocation"] is { } wl && dgvEmployees.Columns["DepartmentName"] is { } dn)
                wl.DisplayIndex = Math.Min(dn.DisplayIndex + 1, dgvEmployees.Columns.Count - 1);
        };

        dgvEmployees.ColumnHeaderMouseClick += dgvEmployees_ColumnHeaderMouseClick;
        dgvEmployees.CellFormatting += dgvEmployees_CellFormatting;
        dgvEmployees.SelectionChanged += (s, e) =>
        {
            if (IsDisposed || lblStatus is null) return;
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

        // پرسنل غیرفعال: متن خاکستری و کم‌رنگ (به‌جای بلوک قرمز)
        dgvEmployees.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.CellStyle is null) return;
            if (dgvEmployees.Rows[e.RowIndex].DataBoundItem is Employee emp && !emp.IsActive)
            {
                e.CellStyle.ForeColor = ModernTheme.TextDisabled;
                e.CellStyle.BackColor = ModernTheme.SurfaceMuted;
            }
        };

        var wrap = ModernReport.WrapGrid(dgvEmployees);
        wrap.Margin = new Padding(0, 4, 0, 8);
        return wrap;
    }

    private Control BuildStatus()
    {
        var panel = new BufferedTable { Dock = DockStyle.Fill, BackColor = Color.Transparent, ColumnCount = 2, Margin = Padding.Empty };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        lblStatus = new Label { Text = "✓  آماده", Font = ModernTheme.GetFont(10f), ForeColor = ModernTheme.TextMuted, AutoSize = true, Anchor = AnchorStyles.Left, BackColor = Color.Transparent };
        panel.Controls.Add(lblStatus, 0, 0);

        var hint = new Label { Text = "برای ویرایش روی هر ردیف دو بار کلیک کنید", Font = ModernTheme.GetFont(9.5f), ForeColor = ModernTheme.Placeholder, AutoSize = true, Anchor = AnchorStyles.Right, BackColor = Color.Transparent };
        panel.Controls.Add(hint, 1, 0);

        return panel;
    }
}
