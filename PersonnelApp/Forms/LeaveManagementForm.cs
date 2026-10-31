using PersonnelApp.Data;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonnelApp.Forms
{
    public class LeaveManagementForm : Form
    {
        private readonly LeaveRepository _repository;
        private readonly EmployeeRepository _empRepository;

        private ComboBox cmbEmployee = null!, cmbLeaveType = null!;
        private TextBox txtStartDate = null!, txtEndDate = null!, txtDescription = null!;
        private Button btnSubmit = null!, btnApprove = null!, btnReject = null!;
        private DataGridView dgvLeaves = null!;

        public LeaveManagementForm(string connectionString)
        {
            _repository = new LeaveRepository(connectionString);
            _empRepository = new EmployeeRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("مدیریت مرخصی پرسنل", new Size(1000, 700));
            ShowInTaskbar = false;

            // فعال‌سازی میانبرهای Ctrl+S (ثبت) و Esc (بستن)
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnSubmit);

            // بارگذاری داده‌ها پس از نمایش فرم برای جلوگیری از فریز
            Shown += async (s, e) => await LoadDataSafeAsync();
        }

        private void BuildUI()
        {
            // ==========================================
            // ۱. فرم ثبت درخواست جدید (پنل بالا)
            // ==========================================
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 170, Padding = new Padding(10) };

            var formLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3 };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));

            for (int i = 0; i < 3; i++) formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            // ردیف اول: کارمند
            cmbEmployee = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "FullName", ValueMember = "Id" };
            UIHelper.AddField(formLayout, "کارمند:", cmbEmployee, 0, 0, 1, 3); // اسپن ۳ ستون

            // ردیف دوم: نوع مرخصی و تاریخ شروع
            cmbLeaveType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbLeaveType.Items.AddRange(new object[] { "استحقاقی", "استعلاجی", "بدون حقوق" });
            cmbLeaveType.SelectedIndex = 0;

            txtStartDate = new TextBox();
            DateHelper.ApplyDateMask(txtStartDate);
            txtStartDate.Text = DateHelper.ToPersianDate(DateTime.Now);
            PersianDatePicker.AttachTo(txtStartDate); // افزودن تقویم شمسی

            UIHelper.AddField(formLayout, "نوع مرخصی:", cmbLeaveType, 1, 0, 1);
            UIHelper.AddField(formLayout, "از تاریخ:", txtStartDate, 1, 2, 3);

            // ردیف سوم: تاریخ پایان و توضیحات
            txtEndDate = new TextBox();
            DateHelper.ApplyDateMask(txtEndDate);
            txtEndDate.Text = DateHelper.ToPersianDate(DateTime.Now);
            PersianDatePicker.AttachTo(txtEndDate); // افزودن تقویم شمسی

            txtDescription = new TextBox();

            var row2Layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
            row2Layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            row2Layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            row2Layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            row2Layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));

            UIHelper.AddField(row2Layout, "تا تاریخ:", txtEndDate, 0, 0, 1);
            UIHelper.AddField(row2Layout, "توضیحات:", txtDescription, 0, 2, 3);

            formLayout.Controls.Add(row2Layout, 0, 2);
            formLayout.SetColumnSpan(row2Layout, 4);

            // دکمه ثبت
            var btnPanelTop = new Panel { Dock = DockStyle.Bottom, Height = 50 };
            btnSubmit = UIHelper.CreateButton("➕ ثبت درخواست", UIHelper.SectionBlue, 150, 40);
            btnSubmit.Dock = DockStyle.Right;
            btnSubmit.Click += BtnSubmit_Click;
            btnPanelTop.Controls.Add(btnSubmit);

            topPanel.Controls.Add(btnPanelTop);
            topPanel.Controls.Add(formLayout);

            // ==========================================
            // ۲. گرید نمایش درخواست‌ها (وسط)
            // ==========================================
            dgvLeaves = new DataGridView { Dock = DockStyle.Fill };
            dgvLeaves.ApplyModernStyle("EmployeeId", "Id");

            // فرمت‌دهی تاریخ شمسی در گرید
            dgvLeaves.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                string colName = dgvLeaves.Columns[e.ColumnIndex].Name;

                if (colName == "StartDate" || colName == "EndDate")
                {
                    if (e.Value is DateTime dt)
                    {
                        e.Value = DateHelper.ToPersianDate(dt);
                        e.FormattingApplied = true;
                    }
                }
            };

            // ==========================================
            // ۳. پنل دکمه‌های تایید/رد (پایین)
            // ==========================================
            var actionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                Padding = new Padding(10),
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            btnApprove = UIHelper.CreateButton("✔️ تایید مرخصی", UIHelper.ActionGreen, 150, 40);
            btnApprove.Click += (s, e) => UpdateStatusAsync("تایید شده");

            btnReject = UIHelper.CreateButton("❌ رد مرخصی", UIHelper.ActionRed, 150, 40);
            btnReject.Click += (s, e) => UpdateStatusAsync("رد شده");

            actionPanel.Controls.Add(btnApprove);
            actionPanel.Controls.Add(btnReject);

            // ترتیب Add مهم است
            this.Controls.Add(dgvLeaves);
            this.Controls.Add(actionPanel);
            this.Controls.Add(topPanel);
        }

        // ==========================================================
        // 📥 بارگذاری داده‌ها (Async)
        // ==========================================================
        private async Task LoadDataSafeAsync()
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                // دریافت همزمان لیست کارمندان و مرخصی‌ها
                var employeesTask = Task.Run(() => _empRepository.GetEmployeesWithDetailsAsync("", false));
                var leavesTask = Task.Run(() => _repository.GetAllRequestsAsync());

                await Task.WhenAll(employeesTask, leavesTask);

                cmbEmployee.DataSource = employeesTask.Result;
                dgvLeaves.DataSource = leavesTask.Result;
            }, "در حال بارگذاری اطلاعات...");
        }

        // ==========================================================
        // 💾 ثبت درخواست جدید
        // ==========================================================
        private async void BtnSubmit_Click(object? sender, EventArgs e)
        {
            if (cmbEmployee.SelectedValue == null)
            {
                ModernMessageBox.ShowWarning("لطفاً یک کارمند را انتخاب کنید.");
                return;
            }

            DateTime? startDate = DateHelper.ParsePersianDate(txtStartDate.Text);
            DateTime? endDate = DateHelper.ParsePersianDate(txtEndDate.Text);

            if (startDate == null || endDate == null)
            {
                ModernMessageBox.ShowWarning("تاریخ شروع یا پایان معتبر نیست.");
                return;
            }

            if (endDate.Value < startDate.Value)
            {
                ModernMessageBox.ShowWarning("تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد.");
                return;
            }

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

            DateStampHelper.StampCreated(request); // مهر زمانی

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _repository.AddRequestAsync(request);
            }, "در حال ثبت درخواست...");

            if (success)
            {
                ToastHelper.Show("درخواست مرخصی با موفقیت ثبت شد و در انتظار تایید است.", isSuccess: true);
                txtDescription.Text = "";
                txtStartDate.Text = DateHelper.ToPersianDate(DateTime.Now);
                txtEndDate.Text = DateHelper.ToPersianDate(DateTime.Now);
                await LoadDataSafeAsync();
            }
        }

        // ==========================================================
        // 🔄 تغییر وضعیت (تایید/رد)
        // ==========================================================
        private async void UpdateStatusAsync(string status)
        {
            if (dgvLeaves.CurrentRow?.DataBoundItem is not LeaveRequest req)
            {
                ModernMessageBox.ShowWarning("لطفاً یک درخواست را از لیست انتخاب کنید.");
                return;
            }

            if (req.Status != "در انتظار")
            {
                ModernMessageBox.ShowWarning("این درخواست قبلاً بررسی شده است.");
                return;
            }

            if (!ModernMessageBox.AskConfirm($"آیا از {status} کردن درخواست مرخصی این کارمند مطمئن هستید؟", "تایید"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await _repository.UpdateStatusAsync(req.Id, status);
            }, "در حال بروزرسانی وضعیت...");

            if (success)
            {
                ToastHelper.Show($"درخواست با وضعیت «{status}» ثبت شد.", isSuccess: true);
                await LoadDataSafeAsync();
            }
        }
    }
}