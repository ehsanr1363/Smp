using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class DecreeManagementForm : Form
    {
        private readonly EmployeeRepository _empRepo;
        private DataGridView dgvEmployees = null!;
        private TextBox txtSearch = null!;
        private Button btnPrint = null!;

        public DecreeManagementForm(string connectionString)
        {
            _empRepo = new EmployeeRepository(connectionString);
            BuildUI();
            this.ApplyStandardHeader("صدور حکم حقوقی پرسنل", new Size(1000, 700));
            Shown += async (s, e) => await LoadDataAsync();
        }

        private void BuildUI()
        {
            var (topRight, topLeft, grid, bottom) = StandardFormHelper.SetupStandardListLayout(this, "صدور حکم حقوقی پرسنل", new Size(1000, 700));

            dgvEmployees = grid;
            dgvEmployees.AutoGenerateColumns = false;

            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PersonnelCode", HeaderText = "کد پرسنلی", FillWeight = 20 });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FullName", HeaderText = "نام و نام خانوادگی", FillWeight = 30 });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Position", HeaderText = "سمت", FillWeight = 20 });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DepartmentName", HeaderText = "واحد سازمانی", FillWeight = 20 });
            dgvEmployees.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "IsActive", HeaderText = "فعال", FillWeight = 10 });

            // دابل کلیک روی ردیف برای چاپ
            dgvEmployees.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) PrintDecree(); };

            txtSearch = new TextBox { Width = 250, Font = UIHelper.PersianFont(11f), PlaceholderText = "جستجو نام یا کد پرسنلی...", Margin = new Padding(0, 6, 0, 0) };
            var btnSearch = UIHelper.CreateButton("🔍 جستجو", UIHelper.SectionBlue, 100, 35);
            btnSearch.Margin = new Padding(5, 3, 5, 5);
            btnSearch.Click += async (s, e) => await LoadDataAsync();
            txtSearch.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) await LoadDataAsync(); };

            topLeft.Controls.Add(new Label { Text = "جستجو:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Margin = new Padding(0, 10, 5, 0) });
            topLeft.Controls.Add(txtSearch);
            topLeft.Controls.Add(btnSearch);

            btnPrint = UIHelper.CreateButton("🖨️ چاپ حکم حقوقی", UIHelper.ActionGreen, 180, 42);
            btnPrint.Click += (s, e) => PrintDecree();
            bottom.Controls.Add(btnPrint);
        }

        private async Task LoadDataAsync()
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var list = await _empRepo.GetEmployeesWithDetailsAsync(txtSearch.Text.Trim(), false);
                dgvEmployees.DataSource = list;
            }, "در حال بارگذاری لیست پرسنل...");

            if (!success) ModernMessageBox.ShowError("خطا در بارگذاری اطلاعات.");
        }

        private void PrintDecree()
        {
            if (dgvEmployees.CurrentRow?.DataBoundItem is not Employee emp)
            {
                ModernMessageBox.ShowWarning("لطفاً یک کارمند را از جدول انتخاب کنید.");
                return;
            }

            // باز کردن فرم چاپ حکم
            using var form = new PersonnelDecreeForm(emp);
            form.ShowDialog(this);
        }
    }
}