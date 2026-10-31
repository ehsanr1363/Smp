using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PersonnelApp.Helpers;

namespace PersonnelApp.Forms
{
    public class NonPersonnelInsuranceReportForm : Form
    {
        private readonly string _connectionString;
        private DataGridView dgvReport = null!;
        private Label lblSummary = null!;
        private Button btnExport = null!;
        private DataTable _dataTable = null!;

        public NonPersonnelInsuranceReportForm(string connectionString)
        {
            _connectionString = connectionString;

            BuildUI();
            this.ApplyStandardHeader("گزارش آماری بیمه افراد غیرفعال (غیر پرسنل)", new Size(1300, 800));
            ShowInTaskbar = false;

            // فعال‌سازی میانبرهای Ctrl+S (خروجی) و Esc (بستن)
            ShortcutHelper.ApplyStandardShortcuts(this, btnSave: btnExport);

            // بارگذاری پس از نمایش فرم برای جلوگیری از فریز
            Shown += async (s, e) => await LoadReportSafeAsync();
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

            dgvReport = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
            dgvReport.ApplyModernStyle();

            // 👈 حذف Format = "N0" از ستون‌ها (چون در CellFormatting خودمان فرمت‌دهی می‌کنیم)
            dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "NationalCode", DataPropertyName = "NationalCode", HeaderText = "کد ملی", FillWeight = 15 });
            dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "FullName", DataPropertyName = "FullName", HeaderText = "نام و نام خانوادگی", FillWeight = 20 });
            dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "MonthsCount", DataPropertyName = "MonthsCount", HeaderText = "تعداد ماه‌های بیمه", FillWeight = 10 });
            dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "WageTotal", DataPropertyName = "WageTotal", HeaderText = "مجموع حقوق و مزایا", FillWeight = 15 });
            dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "WorkerInsurance", DataPropertyName = "WorkerInsurance", HeaderText = "سهم کارگر (۷٪)", FillWeight = 10 });
            dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployerInsurance", DataPropertyName = "EmployerInsurance", HeaderText = "سهم کارفرما (۲۰٪)", FillWeight = 10 });
            dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "Unemployment", DataPropertyName = "Unemployment", HeaderText = "بیمه بیکاری (۳٪)", FillWeight = 10 });
            dgvReport.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalInsurance", DataPropertyName = "TotalInsurance", HeaderText = "جمع کل پرداختی", FillWeight = 10 });

            // 👈 فرمت‌دهی اعداد با ارقام فارسی
            dgvReport.CellFormatting += DgvReport_CellFormatting;

            mainLayout.Controls.Add(dgvReport, 0, 0);

            lblSummary = new Label
            {
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(11.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = UIHelper.WarmWarningBackground,
                ForeColor = UIHelper.WarmWarningText,
                Text = ""
            };
            mainLayout.Controls.Add(lblSummary, 0, 1);

            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(10),
                BackColor = UIHelper.WarmPanelBackground
            };

            btnExport = UIHelper.CreateButton("📁 خروجی اکسل", UIHelper.WarmPrimary, 170, 40);
            btnExport.Click += BtnExport_Click;

            bottomPanel.Controls.Add(btnExport);
            mainLayout.Controls.Add(bottomPanel, 0, 2);

            Controls.Add(mainLayout);
        }

        private void DgvReport_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string colName = dgvReport.Columns[e.ColumnIndex].Name;

            if (colName != "NationalCode" && colName != "FullName")
            {
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
            }
        }

        // ==========================================================
        // 📥 بارگذاری گزارش (Async)
        // ==========================================================
        private async Task LoadReportSafeAsync()
        {
            _dataTable = new DataTable();
            _dataTable.Columns.Add("NationalCode", typeof(string));
            _dataTable.Columns.Add("FullName", typeof(string));
            _dataTable.Columns.Add("MonthsCount", typeof(int));
            _dataTable.Columns.Add("WageTotal", typeof(decimal));
            _dataTable.Columns.Add("WorkerInsurance", typeof(decimal));
            _dataTable.Columns.Add("EmployerInsurance", typeof(decimal));
            _dataTable.Columns.Add("Unemployment", typeof(decimal));
            _dataTable.Columns.Add("TotalInsurance", typeof(decimal));

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                string sql = @"
                    SELECT 
                        LTRIM(RTRIM(i.NationalCode)) AS NationalCode,
                        MAX(i.FirstName + N' ' + i.LastName) AS FullName,
                        COUNT(DISTINCT i.Year + N'-' + i.Month) AS MonthsCount,
                        SUM(TRY_CAST(REPLACE(REPLACE(REPLACE(NULLIF(LTRIM(RTRIM(i.WageAndBenefitsIncluded)), ''), ',', ''), '٬', ''), ' ', '') AS FLOAT)) AS WageTotal,
                        SUM(TRY_CAST(REPLACE(REPLACE(REPLACE(NULLIF(LTRIM(RTRIM(i.WorkerInsurance)), ''), ',', ''), '٬', ''), ' ', '') AS FLOAT)) AS WorkerInsurance
                    FROM InsuranceRecords i
                    INNER JOIN Employees e ON i.EmployeeId = e.Id
                    WHERE e.IsActive = 0 AND i.NationalCode IS NOT NULL AND LTRIM(RTRIM(i.NationalCode)) <> ''
                    GROUP BY LTRIM(RTRIM(i.NationalCode))
                    ORDER BY WageTotal DESC;";

                var dtResult = await Task.Run(() => DbHelper.ExecuteQuery(_connectionString, sql));

                foreach (DataRow row in dtResult.Rows)
                {
                    var newRow = _dataTable.NewRow();
                    newRow["NationalCode"] = row["NationalCode"].ToString();
                    newRow["FullName"] = row["FullName"].ToString();
                    newRow["MonthsCount"] = Convert.ToInt32(row["MonthsCount"]);

                    decimal wageTotal = row["WageTotal"] != DBNull.Value ? Convert.ToDecimal(row["WageTotal"]) : 0;
                    decimal workerIns = row["WorkerInsurance"] != DBNull.Value ? Convert.ToDecimal(row["WorkerInsurance"]) : 0;

                    decimal employerIns = wageTotal * 0.20m;
                    decimal unemployment = wageTotal * 0.03m;
                    decimal totalIns = workerIns + employerIns + unemployment;

                    newRow["WageTotal"] = wageTotal;
                    newRow["WorkerInsurance"] = workerIns;
                    newRow["EmployerInsurance"] = employerIns;
                    newRow["Unemployment"] = unemployment;
                    newRow["TotalInsurance"] = totalIns;

                    _dataTable.Rows.Add(newRow);
                }
            }, "در حال بارگذاری گزارش...");

            if (success)
            {
                dgvReport.DataSource = _dataTable;
                UpdateSummary();
            }
        }

        private void UpdateSummary()
        {
            if (_dataTable.Rows.Count == 0)
            {
                lblSummary.Text = "داده‌ای برای نمایش وجود ندارد.";
                return;
            }

            int totalPeople = _dataTable.Rows.Count;
            decimal totalWage = 0, totalWorkerIns = 0, totalEmployerIns = 0, totalUnemployment = 0, totalIns = 0;

            foreach (DataRow row in _dataTable.Rows)
            {
                totalWage += Convert.ToDecimal(row["WageTotal"]);
                totalWorkerIns += Convert.ToDecimal(row["WorkerInsurance"]);
                totalEmployerIns += Convert.ToDecimal(row["EmployerInsurance"]);
                totalUnemployment += Convert.ToDecimal(row["Unemployment"]);
                totalIns += Convert.ToDecimal(row["TotalInsurance"]);
            }

            // 👈 استفاده از NumberHelper برای نمایش ارقام فارسی در لیبل خلاصه
            lblSummary.Text =
                $"تعداد افراد غیرفعال: {NumberHelper.FormatThousands(totalPeople, usePersianDigits: true)} نفر  |  " +
                $"حقوق و مزایا: {NumberHelper.FormatThousands(totalWage, usePersianDigits: true)}  |  " +
                $"سهم کارگر: {NumberHelper.FormatThousands(totalWorkerIns, usePersianDigits: true)}  |  " +
                $"سهم کارفرما: {NumberHelper.FormatThousands(totalEmployerIns, usePersianDigits: true)}  |  " +
                $"بیکاری: {NumberHelper.FormatThousands(totalUnemployment, usePersianDigits: true)}  |  " +
                $"جمع کل حق بیمه پرداختی: {NumberHelper.FormatThousands(totalIns, usePersianDigits: true)} ریال";
        }

        // ==========================================================
        // 📤 خروجی اکسل (Async)
        // ==========================================================
        private async void BtnExport_Click(object? sender, EventArgs e)
        {
            if (_dataTable.Rows.Count == 0)
            {
                ModernMessageBox.ShowWarning("داده‌ای برای خروجی وجود ندارد.");
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                FileName = $"گزارش_بیمه_غیرفعال_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                OverwritePrompt = true,
                Title = "ذخیره خروجی اکسل"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await Task.Run(() =>
                {
                    using var workbook = new XLWorkbook();
                    var ws = workbook.Worksheets.Add("غیرفعال");

                    string[] colHeaders = { "کد ملی", "نام و نام خانوادگی", "تعداد ماه‌های بیمه", "مجموع حقوق و مزایا", "سهم کارگر", "سهم کارفرما (۲۰٪)", "بیمه بیکاری (۳٪)", "جمع کل حق بیمه" };
                    for (int c = 0; c < colHeaders.Length; c++)
                        ws.Cell(1, c + 1).Value = colHeaders[c];

                    var headerRange = ws.Range(1, 1, 1, colHeaders.Length);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(248, 237, 227);
                    headerRange.Style.Font.FontColor = XLColor.FromArgb(90, 60, 40);
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                    for (int i = 0; i < _dataTable.Rows.Count; i++)
                    {
                        var r = _dataTable.Rows[i];
                        int er = i + 2;
                        ws.Cell(er, 1).Value = r["NationalCode"].ToString();
                        ws.Cell(er, 2).Value = r["FullName"].ToString();
                        ws.Cell(er, 3).Value = Convert.ToInt32(r["MonthsCount"]);
                        ws.Cell(er, 4).Value = Convert.ToDecimal(r["WageTotal"]);
                        ws.Cell(er, 5).Value = Convert.ToDecimal(r["WorkerInsurance"]);
                        ws.Cell(er, 6).Value = Convert.ToDecimal(r["EmployerInsurance"]);
                        ws.Cell(er, 7).Value = Convert.ToDecimal(r["Unemployment"]);
                        ws.Cell(er, 8).Value = Convert.ToDecimal(r["TotalInsurance"]);
                    }

                    int lastRow = _dataTable.Rows.Count + 1;
                    var dataRange = ws.Range(2, 1, lastRow, colHeaders.Length);
                    dataRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                    ws.Column(4).Style.NumberFormat.Format = "#,##0";
                    ws.Column(5).Style.NumberFormat.Format = "#,##0";
                    ws.Column(6).Style.NumberFormat.Format = "#,##0";
                    ws.Column(7).Style.NumberFormat.Format = "#,##0";
                    ws.Column(8).Style.NumberFormat.Format = "#,##0";

                    int totalRow = lastRow + 1;
                    ws.Cell(totalRow, 1).Value = "جمع کل";
                    ws.Range(totalRow, 1, totalRow, 3).Merge();
                    ws.Cell(totalRow, 4).Value = _dataTable.Rows.Cast<DataRow>().Sum(x => Convert.ToDecimal(x["WageTotal"]));
                    ws.Cell(totalRow, 5).Value = _dataTable.Rows.Cast<DataRow>().Sum(x => Convert.ToDecimal(x["WorkerInsurance"]));
                    ws.Cell(totalRow, 6).Value = _dataTable.Rows.Cast<DataRow>().Sum(x => Convert.ToDecimal(x["EmployerInsurance"]));
                    ws.Cell(totalRow, 7).Value = _dataTable.Rows.Cast<DataRow>().Sum(x => Convert.ToDecimal(x["Unemployment"]));
                    ws.Cell(totalRow, 8).Value = _dataTable.Rows.Cast<DataRow>().Sum(x => Convert.ToDecimal(x["TotalInsurance"]));

                    var totalRange = ws.Range(totalRow, 1, totalRow, colHeaders.Length);
                    totalRange.Style.Font.Bold = true;
                    totalRange.Style.Fill.BackgroundColor = XLColor.FromArgb(245, 238, 230);
                    totalRange.Style.Font.FontColor = XLColor.FromArgb(60, 50, 45);
                    totalRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Columns().AdjustToContents();
                    ws.RightToLeft = true;
                    ws.SheetView.FreezeRows(1);

                    workbook.SaveAs(sfd.FileName);
                });
            }, "در حال تولید فایل اکسل...");

            if (success)
            {
                ToastHelper.Show("فایل اکسل با موفقیت ذخیره شد.", isSuccess: true);
            }
        }
    }
}