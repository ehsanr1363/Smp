using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class LeaveManagementForm : Form
    {
        private readonly LeaveRepository _repository;
        private readonly EmployeeRepository _empRepository;

        private ComboBox cmbEmployee = null!, cmbLeaveType = null!;
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
                try { _loadLock?.Dispose(); } catch { }
                try { _actionLock?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private void BuildUI()
        {
            var (formLayout, grid, bottomPanel) = StandardFormHelper.SetupMasterDetailLayout(
                this, "مدیریت مرخصی پرسنل", new Size(1000, 700));

            dgvLeaves = grid;
            // ✅ تعریف دستی ستون‌ها
            dgvLeaves.AutoGenerateColumns = false;

            dgvLeaves.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            dgvLeaves.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "نام کارمند", FillWeight = 25 });
            dgvLeaves.Columns.Add(new DataGridViewTextBoxColumn { Name = "LeaveType", DataPropertyName = "LeaveType", HeaderText = "نوع مرخصی", FillWeight = 15 });
            dgvLeaves.Columns.Add(new DataGridViewTextBoxColumn { Name = "StartDate", DataPropertyName = "StartDate", HeaderText = "از تاریخ", FillWeight = 15 });
            dgvLeaves.Columns.Add(new DataGridViewTextBoxColumn { Name = "EndDate", DataPropertyName = "EndDate", HeaderText = "تا تاریخ", FillWeight = 15 });
            dgvLeaves.Columns.Add(new DataGridViewTextBoxColumn { Name = "DaysCount", DataPropertyName = "DaysCount", HeaderText = "تعداد روز", FillWeight = 10 });
            dgvLeaves.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", DataPropertyName = "Status", HeaderText = "وضعیت", FillWeight = 10 });
            dgvLeaves.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "توضیحات", FillWeight = 20 });

            dgvLeaves.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                string colName = dgvLeaves.Columns[e.ColumnIndex].Name;
                if ((colName == "StartDate" || colName == "EndDate") && e.Value is DateTime dt)
                {
                    e.Value = DateHelper.ToPersianDate(dt);
                    e.FormattingApplied = true;
                }
            };

            cmbEmployee = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "FullName", ValueMember = "Id" };
            cmbLeaveType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbLeaveType.Items.AddRange(new object[] { "استحقاقی", "استعلاجی", "بدون حقوق" });
            cmbLeaveType.SelectedIndex = 0;

            txtStartDate = new TextBox(); DateHelper.ApplyDateMask(txtStartDate); txtStartDate.Text = DateHelper.ToPersianDate(DateTime.Now); PersianDatePicker.AttachTo(txtStartDate);
            txtEndDate = new TextBox(); DateHelper.ApplyDateMask(txtEndDate); txtEndDate.Text = DateHelper.ToPersianDate(DateTime.Now); PersianDatePicker.AttachTo(txtEndDate);
            txtDescription = new TextBox();

            // چیدمان فیلدها
            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "کارمند:", cmbEmployee, 0, 0, 1);
            formLayout.SetColumnSpan(cmbEmployee, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "نوع مرخصی:", cmbLeaveType, 1, 0, 1);
            UIHelper.AddField(formLayout, "از تاریخ:", txtStartDate, 1, 2, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            UIHelper.AddField(formLayout, "تا تاریخ:", txtEndDate, 2, 0, 1);
            UIHelper.AddField(formLayout, "توضیحات:", txtDescription, 2, 2, 3);

            StandardFormHelper.AddRow(formLayout, 50F);
            btnSubmit = UIHelper.CreateButton("➕ ثبت درخواست", UIHelper.SectionBlue, 200, 42);
            btnSubmit.Click += async (s, e) => await SubmitSafeAsync();
            var pnlSubmit = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            pnlSubmit.Controls.Add(btnSubmit);
            formLayout.Controls.Add(pnlSubmit, 3, 3);

            // دکمه‌های پایین
            btnApprove = UIHelper.CreateButton("✔️ تایید مرخصی", UIHelper.ActionGreen, 150, 42);
            btnApprove.Click += async (s, e) => await UpdateStatusSafeAsync("تایید شده");
            btnReject = UIHelper.CreateButton("❌ رد مرخصی", UIHelper.ActionRed, 150, 42);
            btnReject.Click += async (s, e) => await UpdateStatusSafeAsync("رد شده");

            var btnContainer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            btnContainer.Controls.Add(btnApprove);
            btnContainer.Controls.Add(btnReject);
            bottomPanel.Controls.Add(btnContainer);
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
                    cmbEmployee.DataSource = employeesTask.Result;
                    dgvLeaves.DataSource = leavesTask.Result;
                }, "در حال بارگذاری اطلاعات...");
            }
            catch (Exception ex) { LogHelper.LogException(ex, "LeaveManagementForm.LoadDataSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError(ex.Message); }
            finally { try { _loadLock.Release(); } catch { } }
        }

        private async Task SubmitSafeAsync()
        {
            if (IsDisposed) return;
            if (!await _actionLock.WaitAsync(0)) return;
            try
            {
                if (cmbEmployee.SelectedValue == null) { ModernMessageBox.ShowWarning("لطفاً یک کارمند را انتخاب کنید."); return; }
                DateTime? startDate = DateHelper.ParsePersianDate(txtStartDate.Text);
                DateTime? endDate = DateHelper.ParsePersianDate(txtEndDate.Text);

                if (startDate == null || endDate == null) { ModernMessageBox.ShowWarning("تاریخ شروع یا پایان معتبر نیست."); return; }
                if (endDate.Value < startDate.Value) { ModernMessageBox.ShowWarning("تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد."); return; }

                int days = (endDate.Value.Date - startDate.Value.Date).Days + 1;
                var request = new LeaveRequest
                {
                    EmployeeId = Convert.ToInt32(cmbEmployee.SelectedValue),
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
                    txtDescription.Clear(); txtStartDate.Text = DateHelper.ToPersianDate(DateTime.Now); txtEndDate.Text = DateHelper.ToPersianDate(DateTime.Now);
                    await LoadDataSafeAsync();
                }
            }
            catch (Exception ex) { LogHelper.LogException(ex, "LeaveManagementForm.SubmitSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError(ex.Message); }
            finally { try { _actionLock.Release(); } catch { } }
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
            finally { try { _actionLock.Release(); } catch { } }
        }
    }
}