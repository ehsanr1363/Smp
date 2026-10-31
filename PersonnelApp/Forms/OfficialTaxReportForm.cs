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
    public class OfficialTaxReportForm : Form
    {
        private readonly PayrollRepository _repository;

        private ComboBox cmbYear = null!, cmbMonth = null!;
        private Button btnLoad = null!, btnExport = null!;
        private DataGridView dgvTax = null!;
        private DataTable? _currentData;

        private Label lblTotalCount = null!, lblTotalGross = null!, lblTotalTaxable = null!, lblTotalTax = null!;

        public OfficialTaxReportForm(string connectionString)
        {
            _repository = new PayrollRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("گزارش رسمی مالیات بر درآمد حقوق", new Size(1200, 750));
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

            btnLoad = UIHelper.CreateButton("نمایش لیست", UIHelper.SectionBlue, 130, 35);
            btnLoad.Margin = new Padding(5, 3, 5, 5);
            btnLoad.Click += async (s, e) => await LoadDataSafeAsync();

            btnExport = UIHelper.CreateButton("📥 خروجی اکسل رسمی", UIHelper.ActionGreen, 180, 35);
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

            var summaryPanel = new TableLayoutPanel { Dock = DockStyle.Top, Height = 90, ColumnCount = 4, Padding = new Padding(10) };
            for (int i = 0; i < 4; i++) summaryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            lblTotalCount = CreateSummaryCard(summaryPanel, "تعداد پرسنل", UIHelper.SectionDark, 0);
            lblTotalGross = CreateSummaryCard(summaryPanel, "جمع مزایا (ریال)", UIHelper.SectionBlue, 1);
            lblTotalTaxable = CreateSummaryCard(summaryPanel, "جمع درآمد مشمول", UIHelper.SectionPurple, 2);
            lblTotalTax = CreateSummaryCard(summaryPanel, "جمع مالیات پرداختی", UIHelper.ActionRed, 3);

            dgvTax = new DataGridView { Dock = DockStyle.Fill };
            dgvTax.ApplyModernStyle();

            dgvTax.CellFormatting += (s, e) =>
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

            this.Controls.Add(dgvTax);
            this.Controls.Add(summaryPanel);
            this.Controls.Add(topPanel);
        }

        private Label CreateSummaryCard(TableLayoutPanel parent, string title, Color color, int col)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5) };
            card.Paint += (s, e) => {
                ControlPaint.DrawBorder(e.Graphics, card.ClientRectangle, color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid, color, 2, ButtonBorderStyle.Solid);
            };

            var lblTitle = new Label { Text = title, Dock = DockStyle.Top, Height = 25, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(9.5f, FontStyle.Bold), ForeColor = color };
            var lblValue = new Label { Text = "0", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(14f, FontStyle.Bold), ForeColor = UIHelper.WarmText };

            card.Controls.Add(lblValue);
            card.Controls.Add(lblTitle);
            parent.Controls.Add(card, col, 0);

            return lblValue;
        }

        // ✅ FIX: نام ستون‌ها با alias‌های GetOfficialTaxReportAsync هم‌تراز شد
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
                result = await _repository.GetOfficialTaxReportAsync(year, month);
            }, "در حال بارگذاری لیست مالیات...");

            if (!success || result == null) return;

            _currentData = result;

            if (_currentData.Rows.Count == 0)
            {
                ModernMessageBox.ShowWarning("برای این ماه هیچ فیش حقوقی ذخیره نشده است. ابتدا از منوی «پردازش حقوق»، محاسبه و ذخیره کنید.");
                ResetLabels();
                btnExport.Enabled = false;
                dgvTax.DataSource = null;
            }
            else
            {
                dgvTax.DataSource = _currentData;

                int count = _currentData.Rows.Count;
                // ✅ FIX: نام ستون‌ها = alias‌های داخل کوئری
                decimal totalGross = _currentData.AsEnumerable().Sum(r => r.Field<decimal?>("جمع حقوق و مزایا") ?? 0m);
                decimal totalTaxable = _currentData.AsEnumerable().Sum(r => r.Field<decimal?>("درآمد مشمول مالیات") ?? 0m);
                decimal totalTax = _currentData.AsEnumerable().Sum(r => r.Field<decimal?>("مالیات محاسبه شده") ?? 0m);

                lblTotalCount.Text = NumberHelper.FormatThousands(count, usePersianDigits: true);
                lblTotalGross.Text = NumberHelper.FormatThousands(totalGross, usePersianDigits: true);
                lblTotalTaxable.Text = NumberHelper.FormatThousands(totalTaxable, usePersianDigits: true);
                lblTotalTax.Text = NumberHelper.FormatThousands(totalTax, usePersianDigits: true);

                btnExport.Enabled = true;
            }
        }

        private void ResetLabels()
        {
            lblTotalCount.Text = "0"; lblTotalGross.Text = "0";
            lblTotalTaxable.Text = "0"; lblTotalTax.Text = "0";
        }

        private async Task ExportDataSafeAsync()
        {
            if (_currentData == null || _currentData.Rows.Count == 0) return;

            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                FileName = $"Tax_List_{cmbYear.SelectedItem}_{cmbMonth.SelectedItem}.xlsx",
                Title = "ذخیره لیست رسمی مالیات",
                OverwritePrompt = true
            };

            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await Task.Run(() =>
                {
                    using var workbook = new XLWorkbook();
                    var ws = workbook.Worksheets.Add("لیست مالیات");
                    ws.RightToLeft = true;

                    string title = $"لیست مالیات بر درآمد حقوق - {TranslationHelper.GetMonthName(Convert.ToInt32(cmbMonth.SelectedItem))} {cmbYear.SelectedItem}";
                    ws.Cell(1, 1).Value = title;
                    ws.Range(1, 1, 1, _currentData.Columns.Count).Merge().Style.Font.Bold = true;
                    ws.Cell(1, 1).Style.Font.FontSize = 14;
                    ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    for (int i = 0; i < _currentData.Columns.Count; i++)
                    {
                        var cell = ws.Cell(3, i + 1);
                        cell.Value = _currentData.Columns[i].ColumnName;
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                        cell.Style.Font.FontColor = XLColor.White;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }

                    for (int row = 0; row < _currentData.Rows.Count; row++)
                    {
                        for (int col = 0; col < _currentData.Columns.Count; col++)
                        {
                            var cell = ws.Cell(row + 4, col + 1);
                            var value = _currentData.Rows[row][col];
                            if (value is decimal decValue)
                            {
                                cell.Value = decValue;
                                cell.Style.NumberFormat.Format = "#,##0";
                            }
                            else
                            {
                                cell.Value = value?.ToString() ?? "";
                            }
                            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }
                    }

                    int totalRow = _currentData.Rows.Count + 4;
                    ws.Cell(totalRow, 1).Value = "جمع کل";
                    ws.Range(totalRow, 1, totalRow, _currentData.Columns.Count).Style.Fill.BackgroundColor = XLColor.FromArgb(240, 230, 220);
                    ws.Cell(totalRow, 1).Style.Font.Bold = true;

                    ws.Columns().AdjustToContents();
                    workbook.SaveAs(sfd.FileName);
                });
            }, "در حال تولید فایل اکسل...");

            if (success)
            {
                ToastHelper.Show("فایل اکسل لیست مالیات با موفقیت ذخیره شد.", isSuccess: true);
            }
        }
    }
}