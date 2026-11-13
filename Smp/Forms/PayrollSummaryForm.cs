using ClosedXML.Excel;
using SMP.Data;
using SMP.Helpers;
using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class PayrollSummaryForm : Form
    {
        private readonly PayrollRepository _repository;

        private ComboBox cmbYear = null!, cmbMonth = null!;
        private Button btnLoad = null!, btnExport = null!;
        private DataGridView dgvDetails = null!;
        private DataTable? _currentData;

        private Label lblTotalGross = null!, lblTotalInsuranceEmp = null!, lblTotalInsuranceEmpr = null!, lblTotalTax = null!, lblTotalNet = null!;

        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public PayrollSummaryForm(string connectionString)
        {
            _repository = new PayrollRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("داشبورد مالی و بیمه", new Size(1100, 750));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnExport);
            LoadDefaults();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _loadLock?.Dispose(); } catch { } }
            base.Dispose(disposing);
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
            // این متد از هلپر جدید استفاده می‌کند
            var (topRightPanel, topLeftPanel, grid, bottomPanel) = StandardFormHelper.SetupStandardListLayout(
                this, "داشبورد مالی و بیمه", new Size(1100, 750));

            dgvDetails = grid;
            dgvDetails.AutoGenerateColumns = true;

            dgvDetails.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (e.Value is decimal dec) { e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true); e.FormattingApplied = true; }
                else if (e.Value is int i) { e.Value = NumberHelper.FormatThousands(i, usePersianDigits: true); e.FormattingApplied = true; }
            };

            // پنل KPI (کارت‌های بالای جدول)
            var mainTable = this.Controls.OfType<TableLayoutPanel>().FirstOrDefault();
            if (mainTable != null)
            {
                var kpiPanel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 90, ColumnCount = 5, Padding = new Padding(10) };
                for (int i = 0; i < 5; i++) kpiPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

                lblTotalGross = CreateSummaryCard(kpiPanel, "جمع کل حقوق و مزایا", UIHelper.SectionBlue, 0);
                lblTotalInsuranceEmp = CreateSummaryCard(kpiPanel, "بیمه سهم کارمند", UIHelper.SectionOrange, 1);
                lblTotalInsuranceEmpr = CreateSummaryCard(kpiPanel, "بیمه سهم کارفرما", UIHelper.SectionPurple, 2);
                lblTotalTax = CreateSummaryCard(kpiPanel, "جمع مالیات", UIHelper.ActionRed, 3);
                lblTotalNet = CreateSummaryCard(kpiPanel, "خالص پرداختی", UIHelper.ActionGreen, 4);

                mainTable.RowCount = 4;
                mainTable.RowStyles.Clear();
                mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 55F)); // toolbar
                mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F)); // KPI
                mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // grid
                mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F)); // bottom

                mainTable.Controls.Add(kpiPanel, 0, 1);
                mainTable.SetRow(dgvDetails, 2);
                mainTable.SetRow(bottomPanel, 3);
            }

            // نوار بالای جدول (سال، ماه و بارگذاری)
            cmbYear = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 8, 10, 5) };
            for (int i = 1400; i <= 1410; i++) cmbYear.Items.Add(i);

            cmbMonth = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 8, 10, 5) };
            for (int i = 1; i <= 12; i++) cmbMonth.Items.Add(i);

            btnLoad = UIHelper.CreateButton("نمایش داشبورد", UIHelper.SectionBlue, 130, 35);
            btnLoad.Margin = new Padding(5, 3, 10, 5);
            btnLoad.Click += async (s, e) => await LoadDataSafeAsync();

            topRightPanel.Controls.Add(new Label { Text = "سال:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topRightPanel.Controls.Add(cmbYear);
            topRightPanel.Controls.Add(new Label { Text = "ماه:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            topRightPanel.Controls.Add(cmbMonth);
            topRightPanel.Controls.Add(btnLoad);

            // دکمه خروجی اکسل در نوار پایین
            btnExport = UIHelper.CreateButton("📥 خروجی اکسل", UIHelper.ActionGreen, 130, 42);
            btnExport.Enabled = false;
            btnExport.Click += async (s, e) => await ExportDataSafeAsync();

            // ✅ اصلاح: bottomPanel یک FlowLayoutPanel است، پس بدون آرگومان‌های اضافی آیتم اضافه می‌شود
            bottomPanel.Controls.Add(btnExport);
        }

        private Label CreateSummaryCard(TableLayoutPanel parent, string title, Color color, int col)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5) };
            card.Paint += (s, e) => { ControlPaint.DrawBorder(e.Graphics, card.ClientRectangle, color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid); };

            var lblTitle = new Label { Text = title, Dock = DockStyle.Top, Height = 25, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(9.5f, FontStyle.Bold), ForeColor = color };
            var lblValue = new Label { Text = "0", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(13f, FontStyle.Bold), ForeColor = UIHelper.WarmText };

            card.Controls.Add(lblValue); card.Controls.Add(lblTitle); parent.Controls.Add(card, col, 0);
            return lblValue;
        }

        private async Task LoadDataSafeAsync()
        {
            if (cmbYear.SelectedItem == null || cmbMonth.SelectedItem == null || IsDisposed) return;
            if (!await _loadLock.WaitAsync(0)) return;

            try
            {
                int year = Convert.ToInt32(cmbYear.SelectedItem);
                int month = Convert.ToInt32(cmbMonth.SelectedItem);

                DataTable? result = null;
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    result = await _repository.GetSavedPayrollsAsync(year, month);
                }, "در حال بارگذاری اطلاعات...");

                if (!success || result == null || IsDisposed) return;

                _currentData = result;

                if (_currentData.Rows.Count == 0)
                {
                    ModernMessageBox.ShowWarning("برای این ماه اطلاعاتی ذخیره نشده است.");
                    ResetLabels(); btnExport.Enabled = false; dgvDetails.DataSource = null;
                }
                else
                {
                    dgvDetails.DataSource = _currentData;

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
            catch (Exception ex) { LogHelper.LogException(ex, "PayrollSummaryForm.LoadDataSafeAsync"); if (!IsDisposed) ModernMessageBox.ShowError(ex.Message); }
            finally { try { _loadLock.Release(); } catch { } }
        }

        private void ResetLabels()
        {
            lblTotalGross.Text = "0"; lblTotalInsuranceEmp.Text = "0";
            lblTotalInsuranceEmpr.Text = "0"; lblTotalTax.Text = "0"; lblTotalNet.Text = "0";
        }

        private async Task ExportDataSafeAsync()
        {
            if (_currentData == null || _currentData.Rows.Count == 0 || IsDisposed) return;

            using var sfd = new SaveFileDialog { Filter = "Excel Files|*.xlsx", FileName = $"PayrollSummary_{cmbYear.SelectedItem}_{cmbMonth.SelectedItem}.xlsx", Title = "ذخیره گزارش مالی", OverwritePrompt = true };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await Task.Yield();
                ExcelExportHelper.ExportDataGridViewToExcel(dgvDetails, sfd.FileName, "گزارش مالی");
            }, "در حال تولید فایل اکسل...");

            if (success && !IsDisposed) { ToastHelper.Show("فایل اکسل با موفقیت ذخیره شد.", isSuccess: true); }
        }
    }
}