using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using SMP.Data;
using SMP.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class PayrollReportsForm : Form
    {
        private readonly PayrollRepository _repository;
        private ComboBox cmbYear = null!, cmbMonth = null!;
        private TabControl tabMain = null!;

        private TabControl tabListTypes = null!;
        private DataGridView dgvContractual = null!, dgvDaily = null!, dgvHourly = null!;

        private TabControl tabPayslipTypes = null!;
        private DataGridView dgvPayslipContractual = null!, dgvPayslipDaily = null!, dgvPayslipHourly = null!;

        private Button btnLoad = null!, btnPrintList = null!, btnExcel = null!, btnPrintPayslip = null!;

        private CheckBox chkShowEarnings = null!;
        private CheckBox chkShowDeductions = null!;
        private CheckBox chkHideEmptyColumns = null!;

        private DataTable? _currentData;

        private readonly string[] _earningKeys = {
            "WorkingDays", "OvertimeHours", "AbsenceHours",
            "BaseSalary", "OvertimePay",
            "YearsBonus", "AttractionAllowance", "ResponsibilityAllowance",
            "ManagementAllowance", "TechnicalAllowance", "HousingAllowance", "GroceryAllowance",
            "ChildAllowance", "MarriageAllowance", "TransportationAllowance", "OtherAllowances"
        };

        private readonly string[] _deductionKeys = {
            "AbsenceDeduction", "InsuranceEmployee", "IncomeTax",
            "LoanDeduction", "SupplementaryInsuranceDeduction", "CarInsuranceDeduction", "OtherDeductions"
        };

        private readonly Dictionary<string, string> _columnNames = new Dictionary<string, string>
        {
            { "PersonnelCode", "کد پرسنلی" },
            { "EmployeeName", "نام و نام خانوادگی" },
            { "WorkingDays", "روزهای کارکرد" },
            { "OvertimeHours", "ساعت اضافه کار" },
            { "AbsenceHours", "ساعت کسر کار" },
            { "BaseSalary", "حقوق پایه" },
            { "OvertimePay", "مبلغ اضافه کار" },
            { "YearsBonus", "پایه سنوات" },
            { "AttractionAllowance", "حق جذب" },
            { "ResponsibilityAllowance", "حق مسئولیت" },
            { "ManagementAllowance", "حق مدیریت" },
            { "TechnicalAllowance", "حق فنی" },
            { "HousingAllowance", "مسکن" },
            { "GroceryAllowance", "مزایای رفاهی" },
            { "ChildAllowance", "حق اولاد" },
            { "MarriageAllowance", "حق تاهل" },
            { "TransportationAllowance", "حق ایاب و ذهاب" },
            { "OtherAllowances", "سایر مزایا" },
            { "TotalEarnings", "جمع حقوق و مزایا" },
            { "AbsenceDeduction", "کسر غیبت" },
            { "InsuranceEmployee", "بیمه سهم کارمند" },
            { "IncomeTax", "مالیات" },
            { "LoanDeduction", "کسر وام" },
            { "SupplementaryInsuranceDeduction", "بیمه تکمیلی" },
            { "CarInsuranceDeduction", "بیمه خودرو" },
            { "OtherDeductions", "سایر کسورات" },
            { "TotalDeductions", "جمع کسورات" },
            { "NetPay", "مانده قابل پرداخت" }
        };

        private readonly HashSet<string> _numericColumns = new HashSet<string>
        {
            "WorkingDays", "OvertimeHours", "AbsenceHours", "BaseSalary", "OvertimePay",
            "YearsBonus", "AttractionAllowance", "ResponsibilityAllowance", "ManagementAllowance", "TechnicalAllowance",
            "HousingAllowance", "GroceryAllowance", "ChildAllowance", "MarriageAllowance",
            "TransportationAllowance", "OtherAllowances", "TotalEarnings", "AbsenceDeduction",
            "InsuranceEmployee", "IncomeTax", "LoanDeduction",
            "SupplementaryInsuranceDeduction", "CarInsuranceDeduction", "OtherDeductions", "TotalDeductions", "NetPay"
        };

        public PayrollReportsForm(string connectionString)
        {
            _repository = new PayrollRepository(connectionString);
            BuildUI();
            this.ApplyStandardHeader("گزارش لیست حقوق و فیش حقوقی", new Size(1300, 800));
            LoadDefaults();
            Shown += async (s, e) => await LoadDataAsync();
        }

        private void LoadDefaults()
        {
            var pc = new PersianCalendar();
            int currentYear = pc.GetYear(DateTime.Now);
            int currentMonth = pc.GetMonth(DateTime.Now);

            for (int i = 1400; i <= 1410; i++) cmbYear.Items.Add(i);
            for (int i = 1; i <= 12; i++) cmbMonth.Items.Add(i);

            cmbYear.SelectedItem = currentYear;
            cmbMonth.SelectedItem = currentMonth;
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

            var pnlTop = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            cmbYear = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 8, 5, 5) };
            cmbMonth = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 8, 15, 5) };
            btnLoad = UIHelper.CreateButton("نمایش لیست", UIHelper.SectionBlue, 120, 35);
            btnLoad.Margin = new Padding(5, 3, 5, 5);
            btnLoad.Click += async (s, e) => await LoadDataAsync();

            pnlTop.Controls.Add(new Label { Text = "سال:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            pnlTop.Controls.Add(cmbYear);
            pnlTop.Controls.Add(new Label { Text = "ماه:", AutoSize = true, Font = UIHelper.PersianFont(11f), Margin = new Padding(5, 13, 5, 5) });
            pnlTop.Controls.Add(cmbMonth);
            pnlTop.Controls.Add(btnLoad);

            var splitLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            splitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
            splitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var pnlFilters = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(15) };
            var lblFilters = new Label { Text = "نمایش اطلاعات:", Dock = DockStyle.Top, Font = UIHelper.PersianFont(12f, FontStyle.Bold), Height = 35, TextAlign = ContentAlignment.MiddleCenter, ForeColor = UIHelper.SectionBlue };

            var flpFilters = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 10, 0, 0) };

            chkShowEarnings = new CheckBox { Text = "نمایش ستون‌های مزایا", Checked = true, AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText, Margin = new Padding(5, 10, 5, 10) };
            chkShowEarnings.CheckedChanged += (s, e) => ApplyGridFilter();
            chkShowDeductions = new CheckBox { Text = "نمایش ستون‌های کسورات", Checked = true, AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText, Margin = new Padding(5, 10, 5, 10) };
            chkShowDeductions.CheckedChanged += (s, e) => ApplyGridFilter();
            chkHideEmptyColumns = new CheckBox { Text = "مخفیکردن ستون‌های خالی", Checked = true, AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmPrimary, Margin = new Padding(5, 20, 5, 10) };
            chkHideEmptyColumns.CheckedChanged += (s, e) => ApplyGridFilter();

            flpFilters.Controls.Add(chkShowEarnings);
            flpFilters.Controls.Add(chkShowDeductions);
            flpFilters.Controls.Add(chkHideEmptyColumns);

            pnlFilters.Controls.Add(flpFilters);
            pnlFilters.Controls.Add(lblFilters);
            splitLayout.Controls.Add(pnlFilters, 0, 0);

            tabMain = new TabControl { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold) };
            ModernLayoutHelper.StyleModernTabControl(tabMain);

            var tabList = new TabPage("📊 لیست حقوق ماهانه");
            tabListTypes = new TabControl { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold) };

            var tabContractual = new TabPage("قراردادی / ماهانه") { BackColor = UIHelper.WarmBackground };
            dgvContractual = CreateListGrid(); tabContractual.Controls.Add(dgvContractual);

            var tabDaily = new TabPage("روزمزد") { BackColor = UIHelper.WarmBackground };
            dgvDaily = CreateListGrid(); tabDaily.Controls.Add(dgvDaily);

            var tabHourly = new TabPage("ساعتی") { BackColor = UIHelper.WarmBackground };
            dgvHourly = CreateListGrid(); tabHourly.Controls.Add(dgvHourly);

            tabListTypes.TabPages.Add(tabContractual);
            tabListTypes.TabPages.Add(tabDaily);
            tabListTypes.TabPages.Add(tabHourly);
            tabList.Controls.Add(tabListTypes);

            var tabPayslip = new TabPage("🧾 چاپ فیش حقوقی");
            tabPayslipTypes = new TabControl { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold) };

            var tabPsContractual = new TabPage("فیش قراردادی / ماهانه") { BackColor = UIHelper.WarmBackground };
            dgvPayslipContractual = CreatePayslipGrid(); tabPsContractual.Controls.Add(dgvPayslipContractual);

            var tabPsDaily = new TabPage("فیش روزمزد") { BackColor = UIHelper.WarmBackground };
            dgvPayslipDaily = CreatePayslipGrid(); tabPsDaily.Controls.Add(dgvPayslipDaily);

            var tabPsHourly = new TabPage("فیش ساعتی") { BackColor = UIHelper.WarmBackground };
            dgvPayslipHourly = CreatePayslipGrid(); tabPsHourly.Controls.Add(dgvPayslipHourly);

            tabPayslipTypes.TabPages.Add(tabPsContractual);
            tabPayslipTypes.TabPages.Add(tabPsDaily);
            tabPayslipTypes.TabPages.Add(tabPsHourly);
            tabPayslip.Controls.Add(tabPayslipTypes);

            tabMain.TabPages.Add(tabList);
            tabMain.TabPages.Add(tabPayslip);
            splitLayout.Controls.Add(tabMain, 1, 0);

            var pnlBottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };
            btnPrintList = UIHelper.CreateButton("🖨️ چاپ لیست حقوق", UIHelper.SectionBlue, 160, 40);
            btnPrintList.Click += (s, e) => PrintList();
            btnExcel = UIHelper.CreateButton("📥 خروجی اکسل", UIHelper.ActionGreen, 140, 40);
            btnExcel.Click += (s, e) => ExportExcel();
            btnPrintPayslip = UIHelper.CreateButton("🖨️ چاپ فیش (۲ نفر در A4)", UIHelper.SectionPurple, 220, 40);
            btnPrintPayslip.Click += (s, e) => PrintPayslip();

            pnlBottom.Controls.Add(btnPrintList);
            pnlBottom.Controls.Add(btnExcel);
            pnlBottom.Controls.Add(btnPrintPayslip);

            mainLayout.Controls.Add(pnlTop, 0, 0);
            mainLayout.Controls.Add(splitLayout, 0, 1);
            mainLayout.Controls.Add(pnlBottom, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private bool IsColumnEmpty(DataTable dt, string colName)
        {
            if (colName == "EmployeeName") return false;
            if (colName == "WorkingDays" || colName == "OvertimeHours" || colName == "AbsenceHours" || colName == "OvertimePay") return false;
            if (!dt.Columns.Contains(colName)) return true;

            foreach (DataRow row in dt.Rows)
            {
                if (row[colName] != DBNull.Value && row[colName] != null)
                {
                    if (decimal.TryParse(row[colName].ToString(), out decimal decVal))
                    {
                        if (decVal != 0) return false;
                    }
                }
            }
            return true;
        }

        private List<KeyValuePair<string, string>> GetActiveColumns(DataTable dt)
        {
            var active = new List<KeyValuePair<string, string>>();
            active.Add(new KeyValuePair<string, string>("EmployeeName", _columnNames["EmployeeName"]));

            if (chkShowEarnings.Checked)
            {
                foreach (var key in _earningKeys)
                {
                    if (!chkHideEmptyColumns.Checked || !IsColumnEmpty(dt, key))
                        active.Add(new KeyValuePair<string, string>(key, _columnNames[key]));
                }
                active.Add(new KeyValuePair<string, string>("TotalEarnings", _columnNames["TotalEarnings"]));
            }

            if (chkShowDeductions.Checked)
            {
                foreach (var key in _deductionKeys)
                {
                    if (!chkHideEmptyColumns.Checked || !IsColumnEmpty(dt, key))
                        active.Add(new KeyValuePair<string, string>(key, _columnNames[key]));
                }
                active.Add(new KeyValuePair<string, string>("TotalDeductions", _columnNames["TotalDeductions"]));
            }

            active.Add(new KeyValuePair<string, string>("NetPay", _columnNames["NetPay"]));
            return active;
        }

        private DataGridView CreateListGrid()
        {
            var dgv = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, ReadOnly = true, AutoGenerateColumns = false };
            dgv.ApplyModernStyle();
            return dgv;
        }

        // ═══════════════════════════════════════════════════════════════
        // ✅ MultiSelect = true اضافه شد تا کاربر بتونه چند ردیف انتخاب کنه
        // ═══════════════════════════════════════════════════════════════
        private DataGridView CreatePayslipGrid()
        {
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true
            };
            dgv.ApplyModernStyle();
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EmployeeName", HeaderText = "نام کارمند", FillWeight = 50 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PersonnelCode", HeaderText = "کد پرسنلی", FillWeight = 20 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "NetPay", HeaderText = "خالص پرداختی", FillWeight = 30 });

            dgv.CellFormatting += (s, e) =>
            {
                if (e.Value is decimal dec && dgv.Columns[e.ColumnIndex].DataPropertyName == "NetPay")
                {
                    e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
            };
            dgv.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) PrintPayslip(); };
            return dgv;
        }

        private async Task LoadDataAsync()
        {
            if (cmbYear.SelectedItem == null || cmbMonth.SelectedItem == null) return;
            int year = Convert.ToInt32(cmbYear.SelectedItem);
            int month = Convert.ToInt32(cmbMonth.SelectedItem);

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                _currentData = await _repository.GetSavedPayrollsAsync(year, month);
            }, "در حال بارگذاری لیست حقوق...");

            if (!success || _currentData == null) return;

            if (_currentData.Rows.Count == 0)
            {
                ModernMessageBox.ShowWarning("برای این ماه هیچ فیش حقوقی ذخیره نشده است.");
                dgvContractual.DataSource = null; dgvDaily.DataSource = null; dgvHourly.DataSource = null;
                dgvPayslipContractual.DataSource = null; dgvPayslipDaily.DataSource = null; dgvPayslipHourly.DataSource = null;
                return;
            }

            ApplyGridFilter();
        }

        // ═══════════════════════════════════════════════════════════════
        // ✅ بعد از بارگذاری، انتخاب خودکار ردیف اول پاک میشه
        //    تا «چاپ همه» به‌طور پیش‌فرض همه رو چاپ کنه، نه یک نفر رو
        // ═══════════════════════════════════════════════════════════════
        private void ApplyGridFilter()
        {
            if (_currentData == null) return;

            var contractual = _currentData.AsEnumerable().Where(r =>
            {
                string type = r.Field<string>("PersonnelType") ?? "ماهانه";
                return type.Trim() != "روزمزد" && type.Trim() != "ساعتی";
            });

            var daily = _currentData.AsEnumerable().Where(r => (r.Field<string>("PersonnelType") ?? "").Trim() == "روزمزد");
            var hourly = _currentData.AsEnumerable().Where(r => (r.Field<string>("PersonnelType") ?? "").Trim() == "ساعتی");

            var dtContractual = contractual.Any() ? contractual.CopyToDataTable() : _currentData.Clone();
            var dtDaily = daily.Any() ? daily.CopyToDataTable() : _currentData.Clone();
            var dtHourly = hourly.Any() ? hourly.CopyToDataTable() : _currentData.Clone();

            UpdateGridColumns(dgvContractual, dtContractual);
            UpdateGridColumns(dgvDaily, dtDaily);
            UpdateGridColumns(dgvHourly, dtHourly);

            dgvPayslipContractual.DataSource = dtContractual;
            dgvPayslipDaily.DataSource = dtDaily;
            dgvPayslipHourly.DataSource = dtHourly;

            dgvPayslipContractual.ClearSelection();
            dgvPayslipDaily.ClearSelection();
            dgvPayslipHourly.ClearSelection();
        }

        private void UpdateGridColumns(DataGridView dgv, DataTable dt)
        {
            dgv.DataSource = null;
            dgv.Columns.Clear();

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "RowNum", HeaderText = "ردیف", FillWeight = 5, ReadOnly = true });

            var activeCols = GetActiveColumns(dt);
            foreach (var col in activeCols)
            {
                dgv.Columns.Add(new DataGridViewTextBoxColumn
                {
                    DataPropertyName = col.Key,
                    HeaderText = col.Value,
                    FillWeight = 15
                });
            }

            dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                string colName = dgv.Columns[e.ColumnIndex].DataPropertyName;

                if (colName != "EmployeeName" && e.Value is decimal dec)
                {
                    e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                    e.FormattingApplied = true;
                }
            };

            dgv.DataSource = dt;

            for (int i = 0; i < dgv.Rows.Count; i++)
            {
                dgv.Rows[i].Cells["RowNum"].Value = i + 1;
            }
        }

        private void PrintList()
        {
            if (tabMain.SelectedIndex != 0) { ModernMessageBox.ShowWarning("لطفاً به تب «لیست حقوق ماهانه» بروید."); return; }

            DataGridView activeGrid = tabListTypes.SelectedIndex switch
            {
                0 => dgvContractual,
                1 => dgvDaily,
                2 => dgvHourly,
                _ => dgvContractual
            };

            if (activeGrid.DataSource is not DataTable dt || dt.Rows.Count == 0) { ModernMessageBox.ShowWarning("داده‌ای برای چاپ وجود ندارد."); return; }

            string yearMonth = $"{TranslationHelper.GetMonthName(Convert.ToInt32(cmbMonth.SelectedItem))} {cmbYear.SelectedItem}";
            string listTitle = "محاسبه حقوق و مزایا و فوق العاده های";

            var selectedCols = GetActiveColumns(dt);

            var companyRepo = new CompanyRepository(AppConfig.ConnectionString);
            var company = companyRepo.GetAll().FirstOrDefault();
            string companyName = company?.Name ?? "شرکت";
            Image? logo = ImageHelper.ByteArrayToImage(company?.Logo);

            var printer = new PayrollListPrinter(dt, selectedCols, yearMonth, listTitle, companyName, logo, _numericColumns);
            printer.ShowPreview();
        }

        // ═══════════════════════════════════════════════════════════════
        // ✅ منطق هوشمند چاپ فیش:
        //    - هیچ ردیفی انتخاب نشده → همه چاپ می‌شن (۲ نفر در A4)
        //    - یک یا چند ردیف انتخاب شده → فقط همون‌ها چاپ می‌شن
        // ═══════════════════════════════════════════════════════════════
        private void PrintPayslip()
        {
            if (tabMain.SelectedIndex != 1)
            {
                ModernMessageBox.ShowWarning("لطفاً به تب «چاپ فیش حقوقی» بروید.");
                return;
            }

            DataGridView activeGrid = tabPayslipTypes.SelectedIndex switch
            {
                0 => dgvPayslipContractual,
                1 => dgvPayslipDaily,
                2 => dgvPayslipHourly,
                _ => dgvPayslipContractual
            };

            if (activeGrid.DataSource is not DataTable dt || dt.Rows.Count == 0)
            {
                ModernMessageBox.ShowWarning("داده‌ای برای چاپ وجود ندارد.");
                return;
            }

            DataTable printData = dt;
            int totalRowsInGrid = dt.Rows.Count;

            if (activeGrid.SelectedRows.Count > 0 && activeGrid.SelectedRows.Count < totalRowsInGrid)
            {
                var selectedEmployeeIds = new HashSet<int>();
                foreach (DataGridViewRow gRow in activeGrid.SelectedRows)
                {
                    if (gRow.DataBoundItem is DataRowView drv && drv.Row.Table.Columns.Contains("EmployeeId"))
                    {
                        selectedEmployeeIds.Add(Convert.ToInt32(drv.Row["EmployeeId"]));
                    }
                }

                if (selectedEmployeeIds.Count > 0)
                {
                    var filtered = dt.Clone();
                    foreach (DataRow row in dt.Rows)
                    {
                        if (selectedEmployeeIds.Contains(Convert.ToInt32(row["EmployeeId"])))
                            filtered.ImportRow(row);
                    }
                    if (filtered.Rows.Count > 0)
                        printData = filtered;
                }
            }

            string yearMonth = $"{TranslationHelper.GetMonthName(Convert.ToInt32(cmbMonth.SelectedItem))} {cmbYear.SelectedItem}";
            var companyRepo = new CompanyRepository(AppConfig.ConnectionString);
            var company = companyRepo.GetAll().FirstOrDefault();
            string companyName = company?.Name ?? "شرکت";
            Image? logo = ImageHelper.ByteArrayToImage(company?.Logo);

            var printer = new PayslipPrinter(printData, yearMonth, companyName, logo);
            printer.ShowPreview();
        }

        private void ExportExcel()
        {
            if (tabMain.SelectedIndex != 0) { ModernMessageBox.ShowWarning("لطفاً به تب «لیست حقوق» بروید."); return; }

            DataGridView activeGrid = tabListTypes.SelectedIndex switch
            {
                0 => dgvContractual,
                1 => dgvDaily,
                2 => dgvHourly,
                _ => dgvContractual
            };

            if (activeGrid.DataSource is not DataTable dt || dt.Rows.Count == 0) { ModernMessageBox.ShowWarning("داده‌ای برای خروجی وجود ندارد."); return; }

            string listTitle = tabListTypes.SelectedTab?.Text ?? "لیست_حقوق";
            using var sfd = new SaveFileDialog { Filter = "Excel Files|*.xlsx", FileName = $"{listTitle.Replace(" ", "_")}_{cmbYear.SelectedItem}_{cmbMonth.SelectedItem}.xlsx" };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add(listTitle);
                ws.RightToLeft = true;

                var selectedCols = GetActiveColumns(dt);

                ws.Cell(1, 1).Value = "ردیف";
                for (int i = 0; i < selectedCols.Count; i++)
                {
                    ws.Cell(1, i + 2).Value = selectedCols[i].Value;
                }

                for (int c = 1; c <= selectedCols.Count + 1; c++)
                {
                    var cell = ws.Cell(1, c);
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                for (int r = 0; r < dt.Rows.Count; r++)
                {
                    var row = dt.Rows[r];
                    ws.Cell(r + 2, 1).Value = r + 1;

                    for (int i = 0; i < selectedCols.Count; i++)
                    {
                        var val = row[selectedCols[i].Key];
                        var cell = ws.Cell(r + 2, i + 2);

                        if (selectedCols[i].Key != "EmployeeName" && decimal.TryParse(val?.ToString(), out decimal decVal))
                        {
                            cell.Value = decVal;
                            cell.Style.NumberFormat.Format = "#,##0";
                        }
                        else
                        {
                            cell.Value = val?.ToString() ?? "";
                        }

                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }
                }

                ws.Columns().AdjustToContents();
                wb.SaveAs(sfd.FileName);
                ToastHelper.Show("فایل اکسل ذخیره شد.", true);
            }
            catch (Exception ex) { ModernMessageBox.ShowError("خطا:\n" + ex.Message); }
        }
    }
}