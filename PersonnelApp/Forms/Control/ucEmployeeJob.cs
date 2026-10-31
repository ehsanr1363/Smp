using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PersonnelApp.Data;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using PersonnelApp.Services;

namespace PersonnelApp.Forms.Controls
{
    public class ucEmployeeJob : UserControl
    {
        private readonly CompanyService _companyService;
        private readonly DepartmentService _departmentService;
        private readonly LookupService _lookupService;

        private ComboBox cmbCompany = null!, cmbDepartment = null!;
        private TextBox txtDepartmentAddress = null!, txtPosition = null!;
        private ComboBox cmbPersonnelType = null!, cmbWorkLocation = null!;
        private Label lblDailyWageCategory = null!;
        private ComboBox cmbDailyWageCategory = null!;
        private TableLayoutPanel tlpJob = null!;

        public ucEmployeeJob(CompanyService companyService, DepartmentService departmentService, LookupService lookupService)
        {
            this.Dock = DockStyle.Fill;
            this.RightToLeft = RightToLeft.Yes;

            _companyService = companyService;
            _departmentService = departmentService;
            _lookupService = lookupService;

            BuildUI();

            // 👈 خط this.Load حذف شد تا از دوبار اجرا شدن و تداخل جلوگیری شود
        }

        // 👈 این متد از private به public تغییر کرد تا از فرم اصلی صداش بزنیم
        public async Task LoadCombosAsync()
        {
            await AsyncHelper.RunWithLoading(this.FindForm()!, async () =>
            {
                var personnelTypeTask = _lookupService.GetByCategoryAsync("PersonnelType");
                var workLocationTask = _lookupService.GetByCategoryAsync("WorkLocation");
                var dailyWageCategoryTask = _lookupService.GetByCategoryAsync("DailyWageCategory");
                var companiesTask = Task.Run(() => _companyService.GetAll());
                var departmentsTask = Task.Run(() => _departmentService.GetAll());

                await Task.WhenAll(personnelTypeTask, workLocationTask, dailyWageCategoryTask, companiesTask, departmentsTask);

                cmbPersonnelType.DisplayMember = "Value"; cmbPersonnelType.ValueMember = "Id"; cmbPersonnelType.DataSource = personnelTypeTask.Result;
                cmbWorkLocation.DisplayMember = "Value"; cmbWorkLocation.ValueMember = "Id"; cmbWorkLocation.DataSource = workLocationTask.Result;
                cmbDailyWageCategory.DisplayMember = "Value"; cmbDailyWageCategory.ValueMember = "Id"; cmbDailyWageCategory.DataSource = dailyWageCategoryTask.Result;

                cmbCompany.DataSource = companiesTask.Result; cmbCompany.DisplayMember = "Name"; cmbCompany.ValueMember = "Id"; cmbCompany.SelectedIndex = -1;
                cmbDepartment.DataSource = departmentsTask.Result; cmbDepartment.DisplayMember = "Name"; cmbDepartment.ValueMember = "Id";
            }, "در حال بارگذاری اطلاعات شغلی...");
        }

        public void LoadData(Employee e)
        {
            if (e.CompanyId.HasValue && cmbCompany != null) try { cmbCompany.SelectedValue = e.CompanyId.Value; } catch { }
            if (e.DepartmentId.HasValue && cmbDepartment != null) try { cmbDepartment.SelectedValue = e.DepartmentId.Value; } catch { }

            txtPosition.Text = e.Position ?? "";

            SelectComboByText(cmbPersonnelType, e.PersonnelType);
            SelectComboByText(cmbWorkLocation, e.WorkLocation);

            if (e.PersonnelType == "روزمزد" && !string.IsNullOrEmpty(e.DailyWageCategory))
            {
                lblDailyWageCategory.Visible = true;
                cmbDailyWageCategory.Visible = true;
                tlpJob.RowStyles[4].Height = 50f;
                SelectComboByText(cmbDailyWageCategory, e.DailyWageCategory);
            }
            else
            {
                lblDailyWageCategory.Visible = false;
                cmbDailyWageCategory.Visible = false;
                tlpJob.RowStyles[4].Height = 0f;
                cmbDailyWageCategory.SelectedIndex = -1;
            }
        }

        public void SaveData(Employee emp)
        {
            emp.CompanyId = cmbCompany.SelectedValue != null ? Convert.ToInt32(cmbCompany.SelectedValue) : (int?)null;
            emp.DepartmentId = GetSelectedDepartmentId();
            emp.Position = txtPosition.Text.Trim();
            emp.PersonnelType = cmbPersonnelType.Text;
            emp.WorkLocation = cmbWorkLocation.Text;
            emp.DailyWageCategory = cmbDailyWageCategory.Visible ? cmbDailyWageCategory.Text : null;
        }

        private void BuildUI()
        {
            tlpJob = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 6, Padding = new Padding(15), AutoScroll = true };
            tlpJob.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15f));
            tlpJob.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));
            tlpJob.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15f));
            tlpJob.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));

            tlpJob.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
            tlpJob.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
            tlpJob.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
            tlpJob.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
            tlpJob.RowStyles.Add(new RowStyle(SizeType.Absolute, 0f)); // دسته کارگر
            tlpJob.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));

            UIHelper.AddField(tlpJob, "شرکت:", cmbCompany = new ComboBox(), row: 0, labelCol: 0, controlCol: 1);
            UIHelper.AddField(tlpJob, "واحد سازمانی:", cmbDepartment = new ComboBox(), row: 0, labelCol: 2, controlCol: 3);
            UIHelper.AddField(tlpJob, "آدرس واحد:", txtDepartmentAddress = new TextBox(), row: 1, labelCol: 0, controlCol: 1);
            tlpJob.SetColumnSpan(txtDepartmentAddress, 3);

            UIHelper.AddField(tlpJob, "سمت:", txtPosition = new TextBox(), row: 2, labelCol: 0, controlCol: 1);
            UIHelper.AddField(tlpJob, "نوع پرسنلی:", cmbPersonnelType = new ComboBox(), row: 2, labelCol: 2, controlCol: 3);

            UIHelper.AddField(tlpJob, "محل خدمت:", cmbWorkLocation = new ComboBox(), row: 3, labelCol: 0, controlCol: 1);
            tlpJob.SetColumnSpan(cmbWorkLocation, 3);
            cmbWorkLocation.Dock = DockStyle.Right;
            cmbWorkLocation.Width = 300;

            lblDailyWageCategory = new Label { Text = "دسته کارگر (روزمزد):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(13f), ForeColor = UIHelper.WarmText, Visible = false };
            cmbDailyWageCategory = new ComboBox { Dock = DockStyle.Right, Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, Visible = false, BackColor = UIHelper.WarmInputBackground, Font = UIHelper.PersianFont(13f) };

            tlpJob.Controls.Add(lblDailyWageCategory, 0, 4);
            tlpJob.Controls.Add(cmbDailyWageCategory, 1, 4);
            tlpJob.SetColumnSpan(cmbDailyWageCategory, 3);

            cmbPersonnelType.SelectedIndexChanged += (s, e) =>
            {
                bool isDaily = cmbPersonnelType.Text.Trim() == "روزمزد";
                lblDailyWageCategory.Visible = isDaily;
                cmbDailyWageCategory.Visible = isDaily;
                tlpJob.RowStyles[4].Height = isDaily ? 50f : 0f;
                if (!isDaily) cmbDailyWageCategory.SelectedIndex = -1;
            };

            txtDepartmentAddress.ReadOnly = true;
            txtDepartmentAddress.BackColor = UIHelper.WarmInputBackground;
            txtDepartmentAddress.RightToLeft = RightToLeft.No;
            txtDepartmentAddress.TextAlign = HorizontalAlignment.Right;

            cmbDepartment.SelectedIndexChanged += (s, e) =>
            {
                if (cmbDepartment.SelectedItem is Department dept) txtDepartmentAddress.Text = dept.Address ?? "";
                else txtDepartmentAddress.Text = "";
            };

            this.Controls.Add(tlpJob);
        }

        private int? GetSelectedDepartmentId()
        {
            object? val = cmbDepartment.SelectedValue;
            if (val == null) return null;
            if (val is int i) return i;
            try { return Convert.ToInt32(val); } catch { return null; }
        }

        private static void SelectComboByText(ComboBox cmb, string? text)
        {
            if (cmb == null || cmb.Items.Count == 0) return;
            if (string.IsNullOrWhiteSpace(text)) { cmb.SelectedIndex = -1; return; }
            string cleanText = StringHelper.NormalizePersianText(text);
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                string itemText = StringHelper.NormalizePersianText(cmb.GetItemText(cmb.Items[i]));
                if (itemText == cleanText || itemText.Contains(cleanText) || cleanText.Contains(itemText)) { cmb.SelectedIndex = i; return; }
            }
            cmb.SelectedIndex = -1;
        }
    }
}