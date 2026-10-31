using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using PersonnelApp.Helpers;
using PersonnelApp.Models;

namespace PersonnelApp.Forms.Controls
{
    public class ucEmployeeSalary : UserControl
    {
        private DataGridView dgvSalary = null!;
        private Label lblSumSalary = null!, lblSumBenefits = null!, lblGrandTotal = null!;
        private DataTable _dataTable = null!;

        public ucEmployeeSalary()
        {
            this.Dock = DockStyle.Fill;
            this.RightToLeft = RightToLeft.Yes;
            BuildUI();
        }

        public void LoadData(Employee e, SalarySettings? settings = null)
        {
            _dataTable.Rows.Clear();

            // ✅ افزودن تمام آیتم‌های حقوقی همراه با تعیین وضعیت مشمول بیمه و مالیات
            AddRow("BaseSalary", "حقوق پایه", e.BaseSalary, settings?.TaxIncludeBaseSalary ?? true, true);
            AddRow("YearsBonus", "پایه سنوات", e.YearsBonus, settings?.TaxIncludeYearsBonus ?? true, true);
            AddRow("AttractionAllowance", "حق جذب", e.AttractionAllowance, settings?.TaxIncludeAttraction ?? true, true);
            AddRow("ResponsibilityAllowance", "حق مسئولیت", e.ResponsibilityAllowance, settings?.TaxIncludeResponsibility ?? true, true);
            AddRow("ManagementAllowance", "حق مدیریت", e.ManagementAllowance, settings?.TaxIncludeManagement ?? true, true);
            AddRow("TechnicalAllowance", "حق فنی", e.TechnicalAllowance, settings?.TaxIncludeTechnical ?? true, true);
            AddRow("TransportationAllowance", "حق ایاب و ذهاب", e.TransportationAllowance, settings?.TaxIncludeTransportation ?? false, false);

            AddRow("HousingAllowance", "حق مسکن", e.HousingAllowance, settings?.TaxIncludeHousing ?? false, true);
            AddRow("GroceryAllowance", "بن و خواربار", e.GroceryAllowance, settings?.TaxIncludeGrocery ?? false, true);
            AddRow("ChildAllowance", "حق اولاد", e.ChildAllowance, settings?.TaxIncludeChild ?? false, false);
            AddRow("MarriageAllowance", "حق تاهل", e.MarriageAllowance, settings?.TaxIncludeMarriage ?? true, false);
            AddRow("OtherAllowances", "سایر مزایا", e.OtherAllowances, settings?.TaxIncludeOtherAllowances ?? true, true);

            CalculateTotals();
        }

        public void SaveData(Employee e)
        {
            foreach (DataRow row in _dataTable.Rows)
            {
                string key = row["Key"].ToString()!;
                decimal amount = Convert.ToDecimal(row["Amount"]);
                switch (key)
                {
                    case "BaseSalary": e.BaseSalary = amount; break;
                    case "YearsBonus": e.YearsBonus = amount; break;
                    case "AttractionAllowance": e.AttractionAllowance = amount; break;
                    case "ResponsibilityAllowance": e.ResponsibilityAllowance = amount; break;
                    case "ManagementAllowance": e.ManagementAllowance = amount; break;
                    case "TechnicalAllowance": e.TechnicalAllowance = amount; break;
                    case "TransportationAllowance": e.TransportationAllowance = amount; break;
                    case "HousingAllowance": e.HousingAllowance = amount; break;
                    case "GroceryAllowance": e.GroceryAllowance = amount; break;
                    case "ChildAllowance": e.ChildAllowance = amount; break;
                    case "MarriageAllowance": e.MarriageAllowance = amount; break;
                    case "OtherAllowances": e.OtherAllowances = amount; break;
                }
            }
        }

        private void AddRow(string key, string title, decimal amount, bool isTaxable, bool isInsurable)
        {
            _dataTable.Rows.Add(key, title, amount, isTaxable, isInsurable);
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

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
                EditMode = DataGridViewEditMode.EditOnEnter,
                RowHeadersVisible = false
            };
            dgvSalary.ApplyModernStyle();
            dgvSalary.ReadOnly = false;

            dgvSalary.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Title", HeaderText = "شرح آیتم", ReadOnly = true, FillWeight = 30 });
            dgvSalary.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Amount", HeaderText = "مبلغ (ریال)", FillWeight = 30, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" } });
            dgvSalary.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "IsInsurable", HeaderText = "مشمول بیمه", ReadOnly = true, FillWeight = 20 });
            dgvSalary.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "IsTaxable", HeaderText = "مشمول مالیات", ReadOnly = true, FillWeight = 20 });

            dgvSalary.DataSource = _dataTable;

            dgvSalary.CellEndEdit += (s, ev) => { CalculateTotals(); };

            var pnlTotals = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };
            lblSumSalary = new Label { Text = "جمع حقوق: 0", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText, Margin = new Padding(15, 5, 15, 5) };
            lblSumBenefits = new Label { Text = "جمع مزایا: 0", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText, Margin = new Padding(15, 5, 15, 5) };
            lblGrandTotal = new Label { Text = "جمع کل: 0", AutoSize = true, Font = UIHelper.PersianFont(12f, FontStyle.Bold), ForeColor = UIHelper.ActionGreen, Margin = new Padding(15, 5, 15, 5) };

            pnlTotals.Controls.Add(lblSumSalary);
            pnlTotals.Controls.Add(lblSumBenefits);
            pnlTotals.Controls.Add(lblGrandTotal);

            mainLayout.Controls.Add(dgvSalary, 0, 0);
            mainLayout.Controls.Add(pnlTotals, 0, 1);

            this.Controls.Add(mainLayout);
        }

        private void CalculateTotals()
        {
            decimal totalSalary = 0;
            decimal totalBenefits = 0;

            foreach (DataRow row in _dataTable.Rows)
            {
                string key = row["Key"].ToString()!;
                decimal amount = Convert.ToDecimal(row["Amount"]);

                if (key == "BaseSalary" || key == "YearsBonus" || key == "AttractionAllowance" ||
                    key == "ResponsibilityAllowance" || key == "ManagementAllowance" ||
                    key == "TechnicalAllowance" || key == "TransportationAllowance")
                {
                    totalSalary += amount;
                }
                else
                {
                    totalBenefits += amount;
                }
            }

            lblSumSalary.Text = "جمع حقوق: " + NumberHelper.FormatThousands(totalSalary, usePersianDigits: true);
            lblSumBenefits.Text = "جمع مزایا: " + NumberHelper.FormatThousands(totalBenefits, usePersianDigits: true);
            lblGrandTotal.Text = "جمع کل: " + NumberHelper.FormatThousands(totalSalary + totalBenefits, usePersianDigits: true);
        }
    }
}