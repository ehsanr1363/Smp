using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class AttendanceForm : Form
{
    private readonly string _connectionString;
    private readonly AttendanceRepository _repository;
    private readonly SalarySettingsRepository _settingsRepo;
    private SalarySettings? _settings;

    private ComboBox cmbYear = null!;
    private ComboBox cmbMonth = null!;
    private Button btnLoad = null!;
    private Button btnSave = null!;
    private Button btnPrint = null!;
    private Button btnPrintAll = null!;
    private Button btnExportExcel = null!;
    private Button btnImportExcel = null!;
    private ModernTabHost tabControl = null!;

    private DataGridView dgvContractual = null!;
    private DataGridView dgvDaily = null!;
    private DataGridView dgvHourly = null!;

    private List<Attendance> _allAttendances = new();
    private List<Attendance> _contractualList = new(), _dailyList = new(), _hourlyList = new();

    private ModernTextBox txtSearch = null!;
    private Label lblDirty = null!;
    private readonly Label[] _emptyLabels = new Label[3];
    private readonly DataGridView[] _grids = new DataGridView[3];
    private readonly System.Windows.Forms.Timer _searchTimer = new() { Interval = 250 };

    private bool _initializing = true, _loading, _suppressPeriod, _dirty;
    private int _loadedYear, _loadedMonth;

    public AttendanceForm(string connectionString)
    {
        _connectionString = connectionString;
        _repository = new AttendanceRepository(connectionString);
        _settingsRepo = new SalarySettingsRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader("ثبت کارکرد ماهانه", new Size(1300, 750), isDialog: false);
        LoadDefaults();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave);

        _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); ApplyFilter(); };
        FormClosed += (s, e) => { _searchTimer.Stop(); _searchTimer.Dispose(); };

        // به‌جای اینکه کاربر با یک صفحه‌ی خالی روبه‌رو شود و باید «بارگذاری» بزند، لیست ماه جاری خودکار بارگذاری می‌شود
        Shown += async (s, e) =>
        {
            _initializing = false;
            await LoadAsync(showToast: false);
        };
    }

    // ───────────────────────── تغییرات ذخیره‌نشده ─────────────────────────
    private void MarkDirty()
    {
        if (_initializing || _loading || _dirty) return;
        _dirty = true;
        UpdateDirtyUi();
    }

    private void ClearDirty() { _dirty = false; UpdateDirtyUi(); }

    private void UpdateDirtyUi()
    {
        if (IsDisposed || lblDirty is null) return;
        lblDirty.Text = _dirty ? "● تغییرات ذخیره‌نشده دارید" : "";
    }

    private bool ConfirmDiscard()
        => !_dirty || ModernMessageBox.AskConfirm("تغییرات ذخیره‌نشده‌ی کارکرد از بین می‌رود. ادامه می‌دهید؟");

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !ConfirmDiscard()) { e.Cancel = true; return; }
        base.OnFormClosing(e);
    }

    private void OnPeriodChanged()
    {
        if (_initializing || _suppressPeriod) return;
        if (!ConfirmDiscard())
        {
            _suppressPeriod = true;
            if (_loadedYear > 0 && cmbYear.Items.Contains(_loadedYear)) cmbYear.SelectedItem = _loadedYear;
            if (_loadedMonth > 0 && cmbMonth.Items.Contains(_loadedMonth)) cmbMonth.SelectedItem = _loadedMonth;
            _suppressPeriod = false;
            return;
        }
        _ = LoadAsync(showToast: false);
    }

    // ───────────────────────── جستجو ─────────────────────────
    private static string Normalize(string? s) => PersianText.NormalizeForSearch(s);

    /// <summary>فیلتر جستجو روی هر سه لیست؛ ردیف‌ها همان آبجکت‌های اصلی‌اند پس ویرایش‌ها هنگام فیلتر حفظ می‌شوند</summary>
    private void ApplyFilter()
    {
        if (IsDisposed) return;
        string q = Normalize(txtSearch?.Text);
        bool Match(Attendance a) => q.Length == 0 || Normalize(a.EmployeeName).Contains(q) || Normalize(a.PersonnelCode).Contains(q);

        dgvContractual.EndEdit(); dgvDaily.EndEdit(); dgvHourly.EndEdit();
        dgvContractual.DataSource = new BindingList<Attendance>(_contractualList.Where(Match).ToList());
        dgvDaily.DataSource = new BindingList<Attendance>(_dailyList.Where(Match).ToList());
        dgvHourly.DataSource = new BindingList<Attendance>(_hourlyList.Where(Match).ToList());

        for (int i = 0; i < _grids.Length; i++)
        {
            if (_emptyLabels[i] is null) continue;
            _emptyLabels[i].Text = q.Length > 0 ? "موردی با این جستجو پیدا نشد." : "برای این ماه پرسنلی در این گروه ثبت نشده است.";
            _emptyLabels[i].Visible = _grids[i].RowCount == 0;
        }
    }

    /// <summary>جدول را مدرن می‌کند، ستون‌ها را با عرض پنجره هماهنگ می‌کند (بدون اسکرول افقی) و پیام «خالی» می‌گذارد</summary>
    private void AddGridPage(Panel page, DataGridView grid, int index)
    {
        var host = ModernReport.WrapGrid(grid);

        // عرض‌های ثابت قبلی به وزن نسبی تبدیل می‌شوند: اسکرول افقی حذف می‌شود و در پنجره‌ی کوچک فقط تا حداقل عرض فشرده می‌شود
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        foreach (DataGridViewColumn c in grid.Columns)
        {
            c.FillWeight = c.Width;
            c.MinimumWidth = c.Name == "EmployeeName" ? 150 : 78;
            c.SortMode = DataGridViewColumnSortMode.NotSortable;
        }
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        var empty = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ModernTheme.GetFont(11.5f),
            ForeColor = ModernTheme.TextMuted,
            BackColor = ModernTheme.Surface,
            Text = "در حال بارگذاری…",
            Visible = false
        };
        host.Controls.Add(empty);
        empty.BringToFront();

        _grids[index] = grid;
        _emptyLabels[index] = empty;

        grid.CellValueChanged += (s, e) => { if (e.RowIndex >= 0) MarkDirty(); };
        page.Controls.Add(host);
    }

    private void LoadDefaults()
    {
        try
        {
            var pc = new PersianCalendar();
            int y = pc.GetYear(DateTime.Now);
            int m = pc.GetMonth(DateTime.Now);

            if (cmbYear.Items.Contains(y)) cmbYear.SelectedItem = y;
            else if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;

            if (cmbMonth.Items.Contains(m)) cmbMonth.SelectedItem = m;
            else if (cmbMonth.Items.Count > 0) cmbMonth.SelectedIndex = 0;
        }
        catch
        {
            if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;
            if (cmbMonth.Items.Count > 0) cmbMonth.SelectedIndex = 0;
        }
    }

    private void BuildUI()
    {
        cmbYear = ModernUI.Combo();
        ComboBoxHelper.FillYears(cmbYear);
        cmbMonth = ModernUI.Combo();
        ComboBoxHelper.FillMonths(cmbMonth);

        cmbYear.SelectedIndexChanged += (s, e) => OnPeriodChanged();
        cmbMonth.SelectedIndexChanged += (s, e) => OnPeriodChanged();

        btnLoad = ModernButton.Soft("بارگذاری مجدد", ModernTheme.Primary, 150, 38);
        btnLoad.Margin = new Padding(14, 4, 14, 0);
        btnLoad.Click += BtnLoad_Click;

        txtSearch = ModernUI.Input(ModernInputKind.Text, "جستجوی نام یا کد پرسنلی…   (Ctrl+F)");
        txtSearch.Width = 300;
        txtSearch.Height = ModernUI.InputHeight;
        txtSearch.Margin = new Padding(0, 4, 0, 0);
        txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };

        // فرم Mirror شده است: با LeftToRight اولین کنترل در راست‌ترین جا می‌نشیند
        // (قبلاً «سال» در چپ‌ترین نقطه بود و دکمه در راست؛ و ارتفاع ردیف کم بود و پایین کمبوها بریده می‌شد)
        var topBar = ModernReport.CreateFilterBar();
        topBar.Dock = DockStyle.Fill;
        topBar.FlowDirection = FlowDirection.LeftToRight;
        topBar.WrapContents = false;
        topBar.Padding = new Padding(16, 12, 16, 8);
        ModernReport.AddFilter(topBar, "سال", cmbYear, 120);
        ModernReport.AddFilter(topBar, "ماه", cmbMonth, 160);
        topBar.Controls.Add(btnLoad);
        topBar.Controls.Add(txtSearch);

        tabControl = new ModernTabHost { Dock = DockStyle.Fill };

        // ─── تب ۱: قراردادی / ماهانه ───
        var pageContractual = tabControl.AddPage("پرسنل قراردادی / ماهانه");
        dgvContractual = CreateGrid();
        dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "PersonnelCode", DataPropertyName = "PersonnelCode", HeaderText = "کد پرسنلی", ReadOnly = true, Width = 90 });
        dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "نام و نام خانوادگی", ReadOnly = true, Width = 180 });
        dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "WorkingDays", DataPropertyName = "WorkingDays", HeaderText = "کل کارکرد (روز)", Width = 100 });

        dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalPresenceStr", DataPropertyName = "TotalPresenceStr", HeaderText = "حضور کل (ساعت)", Width = 110 });
        dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "GrossOvertimeStr", DataPropertyName = "GrossOvertimeStr", HeaderText = "اضافه کار ناخالص", Width = 110 });
        dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "AbsenceHoursStr", DataPropertyName = "AbsenceHoursStr", HeaderText = "کسر کار (ساعت)", Width = 100 });
        dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "EarlyOvertimeStr", DataPropertyName = "EarlyOvertimeStr", HeaderText = "اضافه کار اول وقت", Width = 110 });
        dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "OtherOvertimeStr", DataPropertyName = "OtherOvertimeStr", HeaderText = "سایر اضافه کار", Width = 100 });
        dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "NetOvertimeStr", DataPropertyName = "NetOvertimeStr", HeaderText = "اضافه کار (کسر کار)", ReadOnly = true, Width = 110 });
        dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "PayableOvertimeStr", DataPropertyName = "PayableOvertimeStr", HeaderText = "اضافه کار قابل پرداخت", Width = 110 });

        dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "HourlyLeave", DataPropertyName = "HourlyLeave", HeaderText = "مرخصی ساعتی", Width = 100 });
        dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "DailyLeave", DataPropertyName = "DailyLeave", HeaderText = "مرخصی روزانه", Width = 100 });
        dgvContractual.Columns.Add(new DataGridViewTextBoxColumn { Name = "LunchDays", DataPropertyName = "LunchDays", HeaderText = "روزهای نهاری", Width = 100 });

        // 👈 محاسبه خودکار خالص اضافه کار هنگام تایپ
        dgvContractual.CellEndEdit += (s, e) =>
        {
            if (e.RowIndex >= 0 && dgvContractual.Rows[e.RowIndex].DataBoundItem is Attendance att)
            {
                att.NetOvertime = att.GrossOvertime - att.AbsenceHours + att.EarlyOvertime + att.OtherOvertime;
                dgvContractual.InvalidateRow(e.RowIndex);
            }
        };

        pageContractual.Padding = new Padding(14, 12, 14, 8);
        AddGridPage(pageContractual, dgvContractual, 0);

        // ─── تب ۲: روزمزد ───
        var pageDaily = tabControl.AddPage("پرسنل روزمزد");
        dgvDaily = CreateGrid();
        dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "PersonnelCode", DataPropertyName = "PersonnelCode", HeaderText = "کد پرسنلی", ReadOnly = true, Width = 80 });
        dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "نام و نام خانوادگی", ReadOnly = true, Width = 160 });
        dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "WorkingDays", DataPropertyName = "WorkingDays", HeaderText = "روزهای عادی", Width = 80 });
        dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "HolidayDays", DataPropertyName = "HolidayDays", HeaderText = "روزهای تعطیل", Width = 80 });
        dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalDaysStr", DataPropertyName = "TotalDaysStr", HeaderText = "جمع کارکرد", ReadOnly = true, Width = 80 });
        dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "ExpectedHoursStr", DataPropertyName = "ExpectedHoursStr", HeaderText = "موظفی", ReadOnly = true, Width = 100 });
        dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalPresenceStr", DataPropertyName = "TotalPresenceStr", HeaderText = "حضور کل", Width = 120 });
        dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "EarlyOvertimeStr", DataPropertyName = "EarlyOvertimeStr", HeaderText = "اضافه کار اول وقت", Width = 110 });
        dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "NetOvertimeStr", DataPropertyName = "NetOvertimeStr", HeaderText = "اضافه کار (کسر کار)", ReadOnly = true, Width = 110 });
        dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "NormalDayRate", DataPropertyName = "NormalDayRate", HeaderText = "روز عادی", ReadOnly = true, Width = 80 });
        dgvDaily.Columns.Add(new DataGridViewTextBoxColumn { Name = "HolidayDayRate", DataPropertyName = "HolidayDayRate", HeaderText = "روز تعطیل", ReadOnly = true, Width = 80 });

        dgvDaily.CellEndEdit += (s, e) =>
        {
            if (dgvDaily.Rows[e.RowIndex].DataBoundItem is Attendance att && _settings is not null)
            {
                decimal expectedHours = (att.WorkingDays * _settings.NormalDayHours) + (att.HolidayDays * _settings.HolidayDayHours);
                att.NetOvertime = att.TotalPresence - expectedHours - att.EarlyOvertime;
                dgvDaily.InvalidateRow(e.RowIndex);
            }
        };
        pageDaily.Padding = new Padding(14, 12, 14, 8);
        AddGridPage(pageDaily, dgvDaily, 1);

        // ─── تب ۳: ساعتی ───
        var pageHourly = tabControl.AddPage("پرسنل ساعتی");
        dgvHourly = CreateGrid();
        dgvHourly.Columns.Add(new DataGridViewTextBoxColumn { Name = "PersonnelCode", DataPropertyName = "PersonnelCode", HeaderText = "کد پرسنلی", ReadOnly = true, Width = 120 });
        dgvHourly.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "نام و نام خانوادگی", ReadOnly = true, Width = 250 });
        dgvHourly.Columns.Add(new DataGridViewTextBoxColumn { Name = "HoursWorked", DataPropertyName = "HoursWorked", HeaderText = "تعداد ساعات کارکرد", Width = 150 });
        pageHourly.Padding = new Padding(14, 12, 14, 8);
        AddGridPage(pageHourly, dgvHourly, 2);

        btnSave = ModernButton.Success("ذخیره کارکردها", 210, 44);
        btnSave.Click += BtnSave_Click;
        lblDirty = ModernUI.Caption("", 10f, true, ModernTheme.Warning);
        lblDirty.AutoSize = true;
        lblDirty.Margin = new Padding(14, 14, 0, 0);
        btnPrint = ModernButton.Outline("چاپ این لیست", ModernTheme.Primary, 150, 44);
        btnPrint.Margin = new Padding(12, 3, 0, 3);
        btnPrint.Click += (s, e) => PrintAttendance(all: false);
        btnPrintAll = ModernButton.Outline("چاپ همه لیست‌ها (مجزا)", ModernTheme.Primary, 200, 44);
        btnPrintAll.Margin = new Padding(12, 3, 0, 3);
        btnPrintAll.Click += (s, e) => PrintAttendance(all: true);

        btnExportExcel = ModernButton.Outline("دریافت قالب اکسل", ModernTheme.Primary, 160, 44);
        btnExportExcel.Margin = new Padding(12, 3, 0, 3);
        btnExportExcel.Click += BtnExportExcel_Click;
        btnImportExcel = ModernButton.Outline("ورود کارکرد از اکسل", ModernTheme.Primary, 180, 44);
        btnImportExcel.Margin = new Padding(12, 3, 0, 3);
        btnImportExcel.Click += BtnImportExcel_Click;

        var bar = ModernForm.CreateButtonBar(btnSave, btnPrint, btnPrintAll, btnExportExcel, btnImportExcel, lblDirty);
        bar.FlowDirection = FlowDirection.LeftToRight;   // دکمه‌ی ذخیره در راست، وضعیت کنارش

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = ModernTheme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        root.Controls.Add(topBar, 0, 0);
        root.Controls.Add(tabControl, 0, 1);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);
    }

    private DataGridView CreateGrid()
    {
        var dgv = ModernGrid.Create();
        dgv.AllowUserToAddRows = false;
        dgv.AllowUserToDeleteRows = false;
        dgv.AutoGenerateColumns = false;
        dgv.SelectionMode = DataGridViewSelectionMode.CellSelect;
        dgv.EditMode = DataGridViewEditMode.EditOnEnter;
        dgv.ReadOnly = false;
        dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        return dgv;
    }

    private async void BtnLoad_Click(object? sender, EventArgs e)
    {
        if (!ConfirmDiscard()) return;
        await LoadAsync(showToast: true);
    }

    // ───────────────────────── اکسل: قالب و ورود کارکرد ─────────────────────────
    /// <summary>قالب اکسل با پرسنل و مقادیر فعلی همین ماه؛ کاربر مقادیر را پر/اصلاح می‌کند و دوباره وارد می‌کند</summary>
    private void BtnExportExcel_Click(object? sender, EventArgs e)
    {
        if (_loading || IsDisposed) return;
        if (_allAttendances.Count == 0)
        {
            ModernMessageBox.ShowWarning("ابتدا لیست را بارگذاری کنید.");
            return;
        }

        dgvContractual.EndEdit(); dgvDaily.EndEdit(); dgvHourly.EndEdit();

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"Attendance_{_loadedYear}_{_loadedMonth:00}.xlsx",
            Title = "ذخیره قالب اکسل کارکرد"
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            AttendanceExcelHelper.ExportTemplate(sfd.FileName, _loadedYear, _loadedMonth,
                _contractualList, _dailyList, _hourlyList,
                _settings?.NormalDayHours ?? 8.5m, _settings?.HolidayDayHours ?? 7m);
            ToastHelper.Show("قالب اکسل کارکرد ساخته شد.", true);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AttendanceForm.ExportExcel");
            ModernMessageBox.ShowError("خطا در ساخت فایل اکسل:\n" + ex.Message);
        }
    }

    /// <summary>
    /// مقادیر اکسل (بر اساس کد پرسنلی) روی لیست همین ماه می‌نشیند. چیزی در دیتابیس نوشته نمی‌شود تا کاربر
    /// نتیجه را در جدول ببیند و با «ذخیره کارکردها» تأیید کند.
    /// </summary>
    private void BtnImportExcel_Click(object? sender, EventArgs e)
    {
        if (_loading || IsDisposed) return;
        if (_allAttendances.Count == 0)
        {
            ModernMessageBox.ShowWarning("ابتدا لیست را بارگذاری کنید.");
            return;
        }

        using var ofd = new OpenFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            Title = $"انتخاب فایل اکسل کارکرد — {_loadedYear}/{_loadedMonth:00}"
        };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        dgvContractual.EndEdit(); dgvDaily.EndEdit(); dgvHourly.EndEdit();

        AttendanceExcelHelper.ImportResult result;
        try
        {
            result = AttendanceExcelHelper.ImportInto(ofd.FileName, _allAttendances);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AttendanceForm.ImportExcel");
            ModernMessageBox.ShowError("خطا در خواندن فایل اکسل:\n" + ex.Message);
            return;
        }

        foreach (var att in result.Updated) RecalcDerived(att);

        ApplyFilter();
        if (result.Updated.Count > 0) MarkDirty();

        var sb = new StringBuilder();
        sb.AppendLine($"کارکرد {result.Updated.Count} نفر از فایل خوانده شد (از {result.RowsRead} ردیف).");
        if (result.Updated.Count > 0) sb.AppendLine("برای ثبت نهایی، «ذخیره کارکردها» را بزنید.");
        if (result.NotFound.Count > 0)
            sb.AppendLine($"\nکد پرسنلی {result.NotFound.Count} ردیف در لیست این ماه پیدا نشد:\n" +
                          string.Join("، ", result.NotFound.Take(15)) + (result.NotFound.Count > 15 ? " …" : ""));
        if (result.Warnings.Count > 0)
            sb.AppendLine("\nهشدارها:\n" + string.Join("\n", result.Warnings.Take(10)) + (result.Warnings.Count > 10 ? "\n…" : ""));

        if (result.Updated.Count == 0 || result.NotFound.Count > 0 || result.Warnings.Count > 0)
            ModernMessageBox.ShowWarning(sb.ToString());
        else
            ToastHelper.Show(sb.ToString().Trim(), true);
    }

    /// <summary>مقادیر محاسباتی (خالص اضافه‌کار و موظفی) را مثل بارگذاری لیست دوباره حساب می‌کند</summary>
    private void RecalcDerived(Attendance att)
    {
        if (att.PersonnelType == "ساعتی") return;

        if (att.PersonnelType == "روزمزد")
        {
            if (_settings is null) return;
            decimal expectedHours = (att.WorkingDays * _settings.NormalDayHours) + (att.HolidayDays * _settings.HolidayDayHours);
            att.ExpectedHoursStr = TimeHelper.FormatToTimeString(expectedHours);
            if (att.TotalPresence > 0 || att.EarlyOvertime > 0)
                att.NetOvertime = att.TotalPresence - expectedHours - att.EarlyOvertime;
        }
        else
        {
            att.NetOvertime = att.GrossOvertime - att.AbsenceHours + att.EarlyOvertime + att.OtherOvertime;
        }
    }

    // ───────────────────────── چاپ مجزای کارکرد (با لوگو) ─────────────────────────
    /// <summary>
    /// all=false: فقط لیست تب فعال (قراردادی / روزمزد / ساعتی) چاپ می‌شود.
    /// all=true : هر سه لیست به‌صورت بخش‌های مجزا (هرکدام از صفحه‌ی جدید) در یک پیش‌نمایش.
    /// همیشه کل لیست ماه بارگذاری‌شده چاپ می‌شود (فیلتر جستجو روی چاپ اثر ندارد).
    /// </summary>
    private void PrintAttendance(bool all)
    {
        if (_loading || IsDisposed) return;
        if (_allAttendances.Count == 0)
        {
            ModernMessageBox.ShowWarning("ابتدا لیست را بارگذاری کنید.");
            return;
        }

        dgvContractual.EndEdit(); dgvDaily.EndEdit(); dgvHourly.EndEdit();

        string companyName = "شرکت";
        Image? logo = null;
        try
        {
            var companies = new CompanyRepository(_connectionString).GetAll();
            var company = companies.FirstOrDefault(c => c.Logo is not null && c.Logo.Length > 0) ?? companies.FirstOrDefault();
            if (company is not null)
            {
                companyName = company.Name;
                logo = ImageHelper.ByteArrayToImage(company.Logo);
            }
        }
        catch (Exception ex)
        {
            // نبود لوگو/شرکت نباید مانع چاپ شود
            LogHelper.LogWarning("خطا در بارگذاری اطلاعات شرکت برای چاپ کارکرد: " + ex.Message, "AttendanceForm");
        }

        try
        {
            var printer = new AttendancePrinter(companyName, logo, _loadedYear, _loadedMonth);

            if (all)
            {
                printer.AddSection(AttendanceGroup.Contractual, _contractualList);
                printer.AddSection(AttendanceGroup.Daily, _dailyList);
                printer.AddSection(AttendanceGroup.Hourly, _hourlyList);
            }
            else
            {
                var group = (AttendanceGroup)Math.Clamp(tabControl.SelectedIndex, 0, 2);
                var list = group switch
                {
                    AttendanceGroup.Daily => _dailyList,
                    AttendanceGroup.Hourly => _hourlyList,
                    _ => _contractualList
                };
                printer.AddSection(group, list);
            }

            if (!printer.HasRows)
            {
                ModernMessageBox.ShowWarning("برای چاپ، پرسنلی در این لیست ثبت نشده است.");
                return;
            }

            printer.ShowPreview();
        }
        finally
        {
            logo?.Dispose();
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.F)) { txtSearch.Focus(); return true; }
        if (keyData == (Keys.Control | Keys.P)) { PrintAttendance(all: false); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async Task LoadAsync(bool showToast)
    {
        if (_loading || IsDisposed) return;
        if (cmbYear.SelectedItem is null || cmbMonth.SelectedItem is null)
        {
            ModernMessageBox.ShowWarning("لطفاً سال و ماه را انتخاب کنید.");
            return;
        }

        int year = Convert.ToInt32(cmbYear.SelectedItem);
        int month = Convert.ToInt32(cmbMonth.SelectedItem);

        _loading = true;
        try
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                _settings = await _settingsRepo.GetByYearAsync(year);
                if (_settings is null)
                    throw new InvalidOperationException($"تنظیمات حقوق سال {year} یافت نشد. لطفاً ابتدا تنظیمات را ذخیره کنید.");

                _allAttendances = await _repository.GetAttendanceForMonthAsync(year, month);

                _contractualList = _allAttendances.Where(a => a.PersonnelType != "روزمزد" && a.PersonnelType != "ساعتی").ToList();
                _dailyList = _allAttendances.Where(a => a.PersonnelType == "روزمزد").ToList();
                _hourlyList = _allAttendances.Where(a => a.PersonnelType == "ساعتی").ToList();

                foreach (var att in _contractualList)
                {
                    att.NetOvertime = att.GrossOvertime - att.AbsenceHours + att.EarlyOvertime + att.OtherOvertime;
                }

                foreach (var att in _dailyList)
                {
                    decimal expectedHours = (att.WorkingDays * _settings.NormalDayHours) + (att.HolidayDays * _settings.HolidayDayHours);
                    att.ExpectedHoursStr = TimeHelper.FormatToTimeString(expectedHours);
                    att.NormalDayRate = TimeHelper.FormatToTimeString(_settings.NormalDayHours);
                    att.HolidayDayRate = TimeHelper.FormatToTimeString(_settings.HolidayDayHours);

                    if (att.TotalPresence > 0 || att.EarlyOvertime > 0)
                    {
                        att.NetOvertime = att.TotalPresence - expectedHours - att.EarlyOvertime;
                    }
                }
            }, "در حال بارگذاری لیست...");

            if (success)
            {
                _loadedYear = year;
                _loadedMonth = month;
                ApplyFilter();
                ClearDirty();
                if (showToast) ToastHelper.Show("لیست کارکردها بارگذاری شد.", true);
            }
            else
            {
                // بارگذاری شکست خورد (مثلاً تنظیمات سال نیست): لیست قبلی با دوره‌ی جدید اشتباه گرفته نشود
                foreach (var l in _emptyLabels) if (l is not null) { l.Text = "لیست بارگذاری نشد."; l.Visible = true; }
            }
        }
        finally
        {
            _loading = false;
        }
    }

    private async void BtnSave_Click(object? sender, EventArgs e)
    {
        if (_allAttendances.Count == 0)
        {
            ModernMessageBox.ShowWarning("ابتدا لیست را بارگذاری کنید.");
            return;
        }
        if (!btnSave.Enabled || _loading) return;

        dgvContractual.EndEdit();
        dgvDaily.EndEdit();
        dgvHourly.EndEdit();

        btnSave.Enabled = false;
        bool success;
        try
        {
            success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _repository.SaveAttendanceBatchAsync(_allAttendances);
            }, "در حال ذخیره کارکردها...");
        }
        finally
        {
            if (!IsDisposed) btnSave.Enabled = true;
        }

        if (success && !IsDisposed)
        {
            ClearDirty();
            ToastHelper.Show("کارکردها با موفقیت ذخیره شد.", true);
        }
    }
}
