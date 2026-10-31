using ClosedXML.Excel;
using PersonnelApp.Data;
using PersonnelApp.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonnelApp.Forms
{
    public class PayrollSummaryForm : Form
    {
        private readonly PayrollRepository _repository;

        private ComboBox cmbYear = null!, cmbMonth = null!;
        private Button btnLoad = null!, btnExport = null!;
        private DataGridView dgvDetails = null!;
        private DataTable? _currentData;

        private Label lblTotalGross = null!, lblTotalInsuranceEmp = null!, lblTotalInsuranceEmpr = null!, lblTotalTax = null!, lblTotalNet = null!;

        public PayrollSummaryForm(string connectionString)
        {
            _repository = new PayrollRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("داشبورد مالی و بیمه", new Size(1100, 700));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnExport);

            LoadDefaults();
        }

        private void LoadDefaults()
        {
            var pc = new PersianCalendar();
            int currentYear = pc.GetYear(DateTime.Now);
            int currentMonth = pc.GetMonth(DateTime.Now);

            if (cmbYear.Items.Contains(currentYear)) cmbYear.SelectedItem = currentYear;
            else if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;

            if (cmbMonth.Items.Contains(currentMonth)) cmbMonth.SelectedItem = currentMonth;
            else if (cmbMonth.Items.Count > 0) cmbMonth.SelectedIndex = 0;
        }

        private void BuildUI()
        {
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };

            var topFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            cmbYear = new ComboBox
            {
                Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UIHelper.PersianFont(11f),
                Margin = new Padding(5, 8, 15, 5)
            };
            for (int i = 1400; i <= 1410; i++) cmbYear.Items.Add(i);

            cmbMonth = new ComboBox
            {
                Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UIHelper.PersianFont(11f),
                Margin = new Padding(5, 8, 15, 5)
            };
            for (int i = 1; i <= 12; i++) cmbMonth.Items.Add(i);

            btnLoad = UIHelper.CreateButton("نمایش داشبورد", UIHelper.SectionBlue, 130, 35);
            btnLoad.Margin = new Padding(5, 3, 15, 5);
            btnLoad.Click += async (s, e) => await LoadDataSafeAsync();

            btnExport = UIHelper.CreateButton("📥 خروجی اکسل", UIHelper.ActionGreen, 130, 35);
            btnExport.Margin = new Padding(5, 3, 5, 5);
            btnExport.Enabled = false;
            btnExport.Click += async (s, e) => await ExportDataSafeAsync();

            topFlow.Controls.Add(new Label { Text = "سال:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(cmbYear);
            topFlow.Controls.Add(new Label { Text = "ماه:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topFlow.Controls.Add(cmbMonth);
            topFlow.Controls.Add(btnLoad);
            topFlow.Controls.Add(btnExport);

            topPanel.Controls.Add(topFlow);

            var summaryPanel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 120, ColumnCount = 5, Padding = new Padding(10) };
            for (int i = 0; i < 5; i++) summaryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

            lblTotalGross = CreateSummaryCard(summaryPanel, "جمع کل حقوق و مزایا", UIHelper.SectionBlue, 0);
            lblTotalInsuranceEmp = CreateSummaryCard(summaryPanel, "بیمه سهم کارمند", UIHelper.SectionOrange, 1);
            lblTotalInsuranceEmpr = CreateSummaryCard(summaryPanel, "بیمه سهم کارفرما", UIHelper.SectionPurple, 2);
            lblTotalTax = CreateSummaryCard(summaryPanel, "جمع مالیات", UIHelper.ActionRed, 3);
            lblTotalNet = CreateSummaryCard(summaryPanel, "خالص پرداختی", UIHelper.ActionGreen, 4);

            dgvDetails = new DataGridView { Dock = DockStyle.Fill };
            dgvDetails.ApplyModernStyle();

            dgvDetails.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

                if (e.Value is decimal dec)
                {
                    e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
                else if (e.Value is int i)
                {
                    e.Value = NumberHelper.FormatThousands(i, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
            };

            this.Controls.Add(dgvDetails);
            this.Controls.Add(summaryPanel);
            this.Controls.Add(topPanel);
        }

        private Label CreateSummaryCard(TableLayoutPanel parent, string title, Color color, int col)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5) };
            card.Paint += (s, e) => {
                ControlPaint.DrawBorder(e.Graphics, card.ClientRectangle, color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid);
            };

            var lblTitle = new Label { Text = title, Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(10f, FontStyle.Bold), ForeColor = color };
            var lblValue = new Label { Text = "0", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(14f, FontStyle.Bold), ForeColor = UIHelper.WarmText };

            card.Controls.Add(lblValue);
            card.Controls.Add(lblTitle);
            parent.Controls.Add(card, col, 0);

            return lblValue;
        }

        // ✅ FIX: استفاده از نام ستون‌های انگلیسی برگشتی از GetSavedPayrollsAsync
        private async Task LoadDataSafeAsync()
        {
            if (cmbYear.SelectedItem == null || cmbMonth.SelectedItem == null)
            {
                ModernMessageBox.ShowWarning("لطفاً سال و ماه را انتخاب کنید.");
                return;
            }

            int year = Convert.ToInt32(cmbYear.SelectedItem);
            int month = Convert.ToInt32(cmbMonth.SelectedItem);

            DataTable? result = null;
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                result = await _repository.GetSavedPayrollsAsync(year, month);
            }, "در حال بارگذاری اطلاعات...");

            if (!success || result == null) return;

            _currentData = result;

            if (_currentData.Rows.Count == 0)
            {
                ModernMessageBox.ShowWarning("برای این ماه اطلاعاتی ذخیره نشده است.");
                ResetLabels();
                btnExport.Enabled = false;
                dgvDetails.DataSource = null;
            }
            else
            {
                dgvDetails.DataSource = _currentData;

                // ✅ FIX: نام ستون‌ها = نام‌های واقعی از دیتابیس (چون کوئری p.* است)
                decimal totalGross = _currentData.AsEnumerable().Sum(r => r.Field<decimal?>("TotalEarnings") ?? 0m);
                decimal totalInsEmp = _currentData.AsEnumerable().Sum(r => r.Field<decimal?>("InsuranceEmployee") ?? 0m);
                decimal totalInsEmpr = _currentData.AsEnumerable().Sum(r => r.Field<decimal?>("InsuranceEmployer") ?? 0m);
                decimal totalTax = _currentData.AsEnumerable().Sum(r => r.Field<decimal?>("IncomeTax") ?? 0m);
                decimal totalNet = _currentData.AsEnumerable().Sum(r => r.Field<decimal?>("NetPay") ?? 0m);

                lblTotalGross.Text = NumberHelper.FormatThousands(totalGross, usePersianDigits: true);
                lblTotalInsuranceEmp.Text = NumberHelper.FormatThousands(totalInsEmp, usePersianDigits: true);
                lblTotalInsuranceEmpr.Text = NumberHelper.FormatThousands(totalInsEmpr, usePersianDigits: true);
                lblTotalTax.Text = NumberHelper.FormatThousands(totalTax, usePersianDigits: true);
                lblTotalNet.Text = NumberHelper.FormatThousands(totalNet, usePersianDigits: true);

                btnExport.Enabled = true;
            }
        }

        private void ResetLabels()
        {
            lblTotalGross.Text = "0"; lblTotalInsuranceEmp.Text = "0";
            lblTotalInsuranceEmpr.Text = "0"; lblTotalTax.Text = "0"; lblTotalNet.Text = "0";
        }

        private async Task ExportDataSafeAsync()
        {
            if (_currentData == null || _currentData.Rows.Count == 0) return;

            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                FileName = $"PayrollSummary_{cmbYear.SelectedItem}_{cmbMonth.SelectedItem}.xlsx",
                Title = "ذخیره گزارش مالی",
                OverwritePrompt = true
            };

            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await Task.Yield();
                ExcelExportHelper.ExportDataGridViewToExcel(dgvDetails, sfd.FileName, "گزارش مالی");
            }, "در حال تولید فایل اکسل...");

            if (success)
            {
                ToastHelper.Show("فایل اکسل با موفقیت ذخیره شد.", isSuccess: true);
            }
        }
    }
}