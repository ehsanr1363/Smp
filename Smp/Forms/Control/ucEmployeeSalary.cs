using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using SMP.Helpers;
using SMP.Models;
using SMP.Data;

namespace SMP.Forms.Controls
{
    public class ucEmployeeSalary : UserControl
    {
        private DataGridView dgvSalary = null!;
        private Label lblGrandTotal = null!;
        private DataTable _dataTable = null!;

        private Employee? _currentEmployee;
        private SalarySettings? _currentSettings;

        public ucEmployeeSalary()
        {
            this.Dock = DockStyle.Fill;
            this.RightToLeft = RightToLeft.Yes;
            BuildUI();
        }

        public void LoadData(Employee e, SalarySettings? settings = null)
        {
            _currentEmployee = e;
            _currentSettings = settings;

            _dataTable.Rows.Clear();

            string type = (e.PersonnelType ?? "ماهانه").Trim();
            bool isContractual = type != "روزمزد" && type != "ساعتی";

            decimal housing = e.HousingAllowance;
            decimal grocery = e.GroceryAllowance;
            decimal marriage = e.MarriageAllowance;
            decimal child = e.ChildAllowance;

            if (isContractual && settings != null)
            {
                housing = settings.HousingAllowance;
                grocery = settings.FoodAllowance;

                string marital = StringHelper.NormalizePersianText(e.MaritalStatus ?? "");
                marriage = (marital.Contains("متاهل") || marital.Contains("متأهل")) ? settings.MarriageAllowance : 0;

                decimal childRate = settings.ChildAllowanceRate > 1 ? settings.ChildAllowanceRate / 100m : settings.ChildAllowanceRate;
                child = Math.Round(settings.MinimumWage * childRate * e.ChildrenCount, 0);
            }

            AddRow("BaseSalary", "حقوق پایه", e.BaseSalary, settings?.TaxIncludeBaseSalary ?? true, true);
            AddRow("YearsBonus", "پایه سنوات", e.YearsBonus, settings?.TaxIncludeYearsBonus ?? true, true);
            AddRow("AttractionAllowance", "حق جذب", e.AttractionAllowance, settings?.TaxIncludeAttraction ?? true, true);
            AddRow("ResponsibilityAllowance", "حق مسئولیت", e.ResponsibilityAllowance, settings?.TaxIncludeResponsibility ?? true, true);
            AddRow("ManagementAllowance", "حق مدیریت", e.ManagementAllowance, settings?.TaxIncludeManagement ?? true, true);
            AddRow("TechnicalAllowance", "حق فنی", e.TechnicalAllowance, settings?.TaxIncludeTechnical ?? true, true);
            AddRow("TransportationAllowance", "حق ایاب و ذهاب", e.TransportationAllowance, settings?.TaxIncludeTransportation ?? false, false);

            AddRow("HousingAllowance", "حق مسکن", housing, settings?.TaxIncludeHousing ?? false, true);
            AddRow("GroceryAllowance", "بن و خواربار", grocery, settings?.TaxIncludeGrocery ?? false, true);
            AddRow("ChildAllowance", "حق اولاد", child, settings?.TaxIncludeChild ?? false, false);
            AddRow("MarriageAllowance", "حق تاهل", marriage, settings?.TaxIncludeMarriage ?? true, false);
            AddRow("OtherAllowances", "سایر مزایا", e.OtherAllowances, settings?.TaxIncludeOtherAllowances ?? true, true);

            CalculateTotals();
        }

        // 👈 متد SaveData که باعث خطا شده بود، دوباره اضافه شد (خالی است چون جدول فقط خواندنی است)
        public void SaveData(Employee e)
        {
            // چون فرم ReadOnly شده، دیگر نیازی به ذخیره از اینجا نیست
        }

        private void AddRow(string key, string title, decimal amount, bool isTaxable, bool isInsurable)
        {
            _dataTable.Rows.Add(key, title, amount, isTaxable, isInsurable);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

            _dataTable = new DataTable();
            _dataTable.Columns.Add("Key", typeof(string));
            _dataTable.Columns.Add("Title", typeof(string));
            _dataTable.Columns.Add("Amount", typeof(decimal));
            _dataTable.Columns.Add("IsTaxable", typeof(bool));
            _dataTable.Columns.Add("IsInsurable", typeof(bool));

            dgvSalary = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true
            };
            dgvSalary.ApplyModernStyle();
            dgvSalary.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            dgvSalary.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Title", HeaderText = "شرح آیتم", FillWeight = 30 });
            dgvSalary.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 30, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" } });
            dgvSalary.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "IsInsurable", HeaderText = "مشمول بیمه", FillWeight = 20 });
            dgvSalary.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "IsTaxable", HeaderText = "مشمول مالیات", FillWeight = 20 });

            dgvSalary.DataSource = _dataTable;

            var pnlTotals = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground };
            lblGrandTotal = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(13f, FontStyle.Bold),
                ForeColor = UIHelper.ActionGreen,
                Text = "جمع کل حقوق و مزایا: 0 ریال"
            };
            pnlTotals.Controls.Add(lblGrandTotal);

            var pnlButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 10, 0, 0)
            };

            var btnManageContract = UIHelper.CreateButton("📝 تنظیم مبالغ قرارداد", UIHelper.SectionBlue, 200, 42);
            btnManageContract.Click += BtnManageContract_Click;

            var btnArchive = UIHelper.CreateButton("📂 آرشیو قراردادها", UIHelper.SectionTeal, 180, 42);
            btnArchive.Click += BtnArchive_Click;

            pnlButtons.Controls.Add(btnManageContract);
            pnlButtons.Controls.Add(btnArchive);

            mainLayout.Controls.Add(dgvSalary, 0, 0);
            mainLayout.Controls.Add(pnlTotals, 0, 1);
            mainLayout.Controls.Add(pnlButtons, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private void BtnManageContract_Click(object? sender, EventArgs e)
        {
            if (_currentEmployee == null || _currentEmployee.Id <= 0)
            {
                ModernMessageBox.ShowWarning("ابتدا اطلاعات هویتی کارمند را ذخیره کنید.");
                return;
            }

            using var form = new EmployeeContractForm(AppConfig.ConnectionString, _currentEmployee);
            if (form.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                var empRepo = new EmployeeRepository(AppConfig.ConnectionString);
                var updatedEmp = empRepo.GetById(_currentEmployee.Id);
                if (updatedEmp != null)
                {
                    LoadData(updatedEmp, _currentSettings);
                }
            }
        }

        private void BtnArchive_Click(object? sender, EventArgs e)
        {
            if (_currentEmployee == null || _currentEmployee.Id <= 0) return;
            using var f = new ContractArchiveViewForm(AppConfig.ConnectionString, _currentEmployee.Id, _currentEmployee.FullName);
            f.ShowDialog(this.FindForm());
        }

        private void CalculateTotals()
        {
            decimal grandTotal = 0;
            foreach (DataRow row in _dataTable.Rows)
            {
                grandTotal += Convert.ToDecimal(row["Amount"]);
            }
            lblGrandTotal.Text = "جمع کل حقوق و مزایا: " + NumberHelper.FormatThousands(grandTotal, usePersianDigits: true) + " ریال";
        }
    }
}