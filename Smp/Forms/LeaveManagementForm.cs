using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Smp.Forms;

public class LeaveManagementForm : Form
{
    private readonly LeaveRepository _repository;
    private readonly EmployeeRepository _empRepository;

    // تغییر نام کلاس به EmployeePickerField
    private EmployeePickerField employeePicker = null!;
    private ComboBox cmbLeaveType = null!;
    private TextBox txtStartDate = null!, txtEndDate = null!, txtDescription = null!;
    private Button btnSubmit = null!, btnApprove = null!, btnReject = null!;
    private DataGridView dgvLeaves = null!;

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly SemaphoreSlim _actionLock = new(1, 1);

    public LeaveManagementForm(string connectionString)
    {
        _repository = new LeaveRepository(connectionString);
        _empRepository = new EmployeeRepository(connectionString);

        BuildUI();
        ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnSubmit);

        Shown += async (s, e) => await LoadDataSafeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { _loadLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "LeaveManagementForm"); }
            try { _actionLock?.Dispose(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "LeaveManagementForm"); }
        }
        base.Dispose(disposing);
    }

    private void BuildUI()
    {
        // ── ورودی‌ها ──
        // تغییر نام کلاس در اینجا نیز
        employeePicker = new EmployeePickerField();
        cmbLeaveType = ModernUI.Combo();
        cmbLeaveType.Items.AddRange(new object[] { "استحقاقی", "استعلاجی", "بدون حقوق" });
        cmbLeaveType.SelectedIndex = 0;

        txtStartDate = new TextBox { TextAlign = HorizontalAlignment.Center }; DateHelper.ApplyDateMask(txtStartDate); txtStartDate.Text = DateHelper.ToPersianDate(DateTime.Now); PersianDatePicker.AttachTo(txtStartDate);
        txtEndDate = new TextBox { TextAlign = HorizontalAlignment.Center }; DateHelper.ApplyDateMask(txtEndDate); txtEndDate.Text = DateHelper.ToPersianDate(DateTime.Now); PersianDatePicker.AttachTo(txtEndDate);
        txtDescription = new TextBox();

        int rh = ModernUI.RowHeight;
        var formCard = new ModernCard("ثبت درخواست مرخصی", ModernTheme.Primary, 2, rh, rh, rh);
        var empRow = ModernUI.Row("کارمند", employeePicker);
        formCard.Body.Controls.Add(empRow, 0, 0);
        formCard.Body.SetColumnSpan(empRow, 2);
        formCard.Body.Controls.Add(ModernUI.Row("نوع مرخصی", cmbLeaveType), 0, 1);
        formCard.Body.Controls.Add(ModernUI.Row("از تاریخ", txtStartDate), 1, 1);
        formCard.Body.Controls.Add(ModernUI.Row("تا تاریخ", txtEndDate), 0, 2);
        formCard.Body.Controls.Add(ModernUI.Row("توضیحات", txtDescription), 1, 2);
        formCard.Dock = DockStyle.Fill;

        // ── جدول ──
        dgvLeaves = ModernGrid.Create("Id");
        dgvLeaves.AutoGenerateColumns = false;
        dgvLeaves.FillColumns();
        dgvLeaves.ReadOnly = true;
        dgvLeaves.AllowUserToAddRows = false;
        dgvLeaves.MultiSelect = false;
        dgvLeaves.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

        dgvLeaves.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        dgvLeaves.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "نام کارمند", FillWeight = 25 });
        dgvLeaves.Columns.Add(new DataGridViewTextBoxColumn { Name = "LeaveType", DataPropertyName = "LeaveType", HeaderText = "نوع مرخصی", FillWeight = 15 });
        dgvLeaves.Columns.Add(new DataGridViewTextBoxColumn { Name = "StartDate", DataPropertyName = "StartDate", HeaderText = "از تاریخ", FillWeight = 15 });
        dgvLeaves.Columns.Add(new DataGridViewTextBoxColumn { Name = "EndDate", DataPropertyName = "EndDate", HeaderText = "تا تاریخ", FillWeight = 15 });
        dgvLeaves.Columns.Add(new DataGridViewTextBoxColumn { Name = "DaysCount", DataPropertyName = "DaysCount", HeaderText = "تعداد روز", FillWeight = 10 });
        dgvLeaves.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", DataPropertyName = "Status", HeaderText = "وضعیت", FillWeight = 12 });
        dgvLeaves.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "توضیحات", FillWeight = 20 });
        foreach (var c in new[] { "LeaveType", "StartDate", "EndDate", "DaysCount", "Status" })
            dgvLeaves.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        dgvLeaves.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvLeaves.Columns[e.ColumnIndex].Name;
            if ((colName == "StartDate" || colName == "EndDate") && e.Value is DateTime dt)
            {
                e.Value = DateHelper.ToPersianDate(dt);
                e.FormattingApplied = true;
            }
            else if (colName == "Status" && e.Value is string st)
            {
                e.CellStyle.Font = ModernTheme.GetFont(10.5f, FontStyle.Bold);
                e.CellStyle.ForeColor = st == "تایید شده" ? ModernTheme.Success
                                      : st == "رد شده" ? ModernTheme.Danger
                                      : ModernTheme.Warning;
            }
        };

        var gridCard = new ModernCard("درخواست‌های مرخصی", ModernTheme.Teal, new[] { new ColumnStyle(SizeType.Percent, 100F) });
        dgvLeaves.Dock = DockStyle.Fill;
        dgvLeaves.Margin = Padding.Empty;
        gridCard.Body.Controls.Add(dgvLeaves, 0, 0);

        // ── دکمه‌ها ──
        btnSubmit = ModernButton.Primary("ثبت درخواست", 170, 44);
        btnSubmit.Click += async (s, e) => await SubmitSafeAsync();
        btnApprove = ModernButton.Success("تایید مرخصی", 150, 44);
        btnApprove.Click += async (s, e) => await UpdateStatusSafeAsync("تایید شده");
        btnReject = ModernButton.Danger("رد مرخصی", 140, 44);
        btnReject.Click += async (s, e) => await UpdateStatusSafeAsync("رد شده");
        var bar = ModernForm.CreateButtonBar(btnSubmit, btnApprove, btnReject);

        // ── چیدمان ──
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12, 10, 12, 0), BackColor = ModernTheme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, formCard.Height + 8));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        root.Controls.Add(formCard, 0, 0);
        root.Controls.Add(gridCard, 0, 1);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);

        this.ApplyStandardHeader("مدیریت مرخصی پرسنل", new Size(1100, 760), isDialog: false);
    }

    private async Task LoadDataSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _loadLock.WaitAsync(0)) return;
        try
        {
            await AsyncHelper.RunWithLoading(this, async () =>
            {
                var employeesTask = Task.Run(() => _empRepository.GetEmployeesWithDetailsAsync("", false));
                var leavesTask = Task.Run(() => _repository.GetAllRequestsAsync());
                await Task.WhenAll(employeesTask, leavesTask);
                if (IsDisposed) return;
                employeePicker.SetEmployees(employeesTask.Result);
                dgvLeaves.DataSource = leavesTask.Result;
            }, "در حال بارگذاری اطلاعات...");
        }
        catch (Exception ex) { LogHelper.LogException(ex, "LeaveManagementForm.LoadDataSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError(ex.Message); }
        finally { try { _loadLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "LeaveManagementForm"); } }
    }

    private async Task SubmitSafeAsync()
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;
        try
        {
            if (employeePicker.SelectedId is null) { ModernMessageBox.ShowWarning("لطفاً یک کارمند را انتخاب کنید."); return; }
            DateTime? startDate = DateHelper.ParsePersianDate(txtStartDate.Text);
            DateTime? endDate = DateHelper.ParsePersianDate(txtEndDate.Text);

            if (startDate is null || endDate is null) { ModernMessageBox.ShowWarning("تاریخ شروع یا پایان معتبر نیست."); return; }
            if (endDate.Value < startDate.Value) { ModernMessageBox.ShowWarning("تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد."); return; }

            int days = (endDate.Value.Date - startDate.Value.Date).Days + 1;
            var request = new LeaveRequest
            {
                EmployeeId = employeePicker.SelectedId!.Value,
                LeaveType = cmbLeaveType.SelectedItem?.ToString() ?? "",
                StartDate = startDate.Value.Date,
                EndDate = endDate.Value.Date,
                DaysCount = days,
                Description = txtDescription.Text.Trim()
            };
            DateStampHelper.StampCreated(request);

            bool success = await AsyncHelper.RunWithLoading(this, async () => { await _repository.AddRequestAsync(request); }, "در حال ثبت...");
            if (success && !IsDisposed)
            {
                ToastHelper.Show("درخواست ثبت شد.", isSuccess: true);
                employeePicker.Clear(); txtDescription.Clear(); txtStartDate.Text = DateHelper.ToPersianDate(DateTime.Now); txtEndDate.Text = DateHelper.ToPersianDate(DateTime.Now);
                await LoadDataSafeAsync();
            }
        }
        catch (Exception ex) { LogHelper.LogException(ex, "LeaveManagementForm.SubmitSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError(ex.Message); }
        finally { try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "LeaveManagementForm"); } }
    }

    private async Task UpdateStatusSafeAsync(string status)
    {
        if (IsDisposed) return;
        if (!await _actionLock.WaitAsync(0)) return;
        try
        {
            if (dgvLeaves.CurrentRow?.DataBoundItem is not LeaveRequest req) { ModernMessageBox.ShowWarning("لطفاً یک درخواست را از لیست انتخاب کنید."); return; }
            if (req.Status != "در انتظار") { ModernMessageBox.ShowWarning("این درخواست قبلاً بررسی شده است."); return; }
            if (!ModernMessageBox.AskConfirm($"آیا از {status} کردن درخواست مطمئن هستید؟", "تایید")) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () => { await _repository.UpdateStatusAsync(req.Id, status); }, "در حال بروزرسانی...");
            if (success && !IsDisposed) { ToastHelper.Show($"وضعیت به «{status}» تغییر کرد.", isSuccess: true); await LoadDataSafeAsync(); }
        }
        catch (Exception ex) { LogHelper.LogException(ex, "LeaveManagementForm.UpdateStatusSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError(ex.Message); }
        finally { try { _actionLock.Release(); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "LeaveManagementForm"); } }
    }
}
