using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PersonnelApp.Data;
using PersonnelApp.Models;
using PersonnelApp.Helpers;

namespace PersonnelApp.Forms
{
    public class DepartmentManagementForm : Form
    {
        private DataGridView dgv = null!;
        private TextBox txtCode = null!;
        private TextBox txtName = null!;
        private TextBox txtAddress = null!;
        private Button btnSave = null!, btnDelete = null!, btnNew = null!;

        private readonly DepartmentRepository _repo;
        private int _selectedId = 0;

        public DepartmentManagementForm(string connectionString)
        {
            _repo = new DepartmentRepository(connectionString);

            BuildUI();

            // فعال‌سازی میانبرهای Ctrl+S (ذخیره) و Esc (بستن) و Ctrl+N (جدید)
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave, btnNew);
            ShowInTaskbar = false;

            // بارگذاری داده‌ها پس از نمایش فرم
            Shown += async (s, e) => await LoadDataSafeAsync();
        }

        private void BuildUI()
        {
            UIHelper.SetupForm(this, "مدیریت واحدهای سازمانی", new Size(850, 550));

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170F));

            // ==========================================
            // ۱. جدول (DataGridView)
            // ==========================================
            var dgvContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            dgv = new DataGridView { Dock = DockStyle.Fill };

            dgv.ApplyModernStyle();
            dgv.CellClick += Dgv_CellClick;
            dgv.SelectionChanged += (s, e) => UpdateButtonsState();

            dgvContainer.Controls.Add(dgv);
            mainLayout.Controls.Add(dgvContainer, 0, 0);

            // ==========================================
            // ۲. فرم ورودی
            // ==========================================
            var pnlBottom = UIHelper.CreateWarmPanel();

            var formLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3 };
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));

            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            txtCode = new TextBox();
            txtName = new TextBox();
            txtAddress = new TextBox();

            UIHelper.AddField(formLayout, "کد واحد:", txtCode, 0, 0, 1);
            UIHelper.AddField(formLayout, "عنوان واحد:", txtName, 0, 2, 3);
            UIHelper.AddField(formLayout, "آدرس واحد:", txtAddress, 1, 0, 1, 4);

            // دکمه‌ها
            var flpBtns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };

            btnSave = UIHelper.CreateButton("💾 ذخیره / ویرایش", UIHelper.WarmPrimary, 150);
            btnSave.Click += BtnSave_Click;

            btnDelete = UIHelper.CreateButton("🗑️ حذف", UIHelper.WarmDanger, 100);
            btnDelete.Enabled = false; // در ابتدا غیرفعال
            btnDelete.Click += BtnDelete_Click;

            btnNew = UIHelper.CreateButton("➕ جدید", UIHelper.WarmSecondary, 100);
            btnNew.Click += BtnNew_Click;

            flpBtns.Controls.Add(btnSave);
            flpBtns.Controls.Add(btnDelete);
            flpBtns.Controls.Add(btnNew);

            formLayout.Controls.Add(flpBtns, 0, 2);
            formLayout.SetColumnSpan(flpBtns, 4);

            pnlBottom.Controls.Add(formLayout);
            mainLayout.Controls.Add(pnlBottom, 0, 1);

            Controls.Add(mainLayout);
        }

        // ==========================================================
        // 📥 بارگذاری داده‌ها (Async)
        // ==========================================================
        private async Task LoadDataSafeAsync()
        {
            var (success, list) = await AsyncHelper.RunWithLoading(
                this,
                () => Task.Run(() => _repo.GetAll()),
                "در حال بارگذاری واحدهای سازمانی...");

            if (!success || list == null) return;

            dgv.DataSource = list;

            // تنظیم ستون‌ها
            if (dgv.Columns["Id"] != null) dgv.Columns["Id"].Visible = false;
            if (dgv.Columns["Code"] != null) dgv.Columns["Code"].HeaderText = "کد واحد";
            if (dgv.Columns["Name"] != null) dgv.Columns["Name"].HeaderText = "عنوان واحد";
            if (dgv.Columns["Address"] != null) dgv.Columns["Address"].HeaderText = "آدرس";

            UpdateButtonsState();
        }

        private void UpdateButtonsState()
        {
            btnDelete.Enabled = _selectedId > 0;
        }

        // ==========================================================
        // 🖱️ انتخاب ردیف
        // ==========================================================
        private void Dgv_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dgv.Rows[e.RowIndex].DataBoundItem is Department dept)
            {
                _selectedId = dept.Id;
                txtCode.Text = dept.Code ?? "";
                txtName.Text = dept.Name ?? "";
                txtAddress.Text = dept.Address ?? "";

                UpdateButtonsState();
            }
        }

        // ==========================================================
        // 💾 ذخیره / ویرایش
        // ==========================================================
        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                ModernMessageBox.ShowWarning("عنوان واحد الزامی است.");
                txtName.Focus();
                return;
            }

            var dept = new Department
            {
                Id = _selectedId,
                Code = txtCode.Text.Trim(),
                Name = txtName.Text.Trim(),
                Address = txtAddress.Text.Trim()
            };

            // مهر زمانی سیستم
            if (_selectedId == 0) DateStampHelper.StampCreated(dept);
            else DateStampHelper.StampUpdated(dept);

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                if (_selectedId == 0)
                    await Task.Run(() => _repo.Add(dept));
                else
                    await Task.Run(() => _repo.Update(dept));
            }, "در حال ذخیره اطلاعات...");

            if (success)
            {
                ToastHelper.Show("اطلاعات با موفقیت ذخیره شد.", isSuccess: true);
                BtnNew_Click(null, EventArgs.Empty);
                await LoadDataSafeAsync();
            }
        }

        // ==========================================================
        // 🗑️ حذف
        // ==========================================================
        private async void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (_selectedId == 0)
            {
                ModernMessageBox.ShowWarning("لطفاً ابتدا یک واحد را انتخاب کنید.");
                return;
            }

            if (!ModernMessageBox.AskConfirm("آیا از حذف این واحد مطمئن هستید؟", "حذف واحد سازمانی"))
                return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await Task.Run(() => _repo.Delete(_selectedId));
            }, "در حال حذف...");

            if (success)
            {
                ToastHelper.Show("واحد سازمانی با موفقیت حذف شد.", isSuccess: true);
                BtnNew_Click(null, EventArgs.Empty);
                await LoadDataSafeAsync();
            }
        }

        // ==========================================================
        // ➕ جدید (پاکسازی فرم)
        // ==========================================================
        private void BtnNew_Click(object? sender, EventArgs e)
        {
            _selectedId = 0;
            txtCode.Clear();
            txtName.Clear();
            txtAddress.Clear();

            dgv.ClearSelection();
            UpdateButtonsState();

            txtName.Focus();
        }
    }
}