using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClosedXML.Excel;
using Smp.Data;
using Smp.Helpers;
using Smp.Models;

namespace Smp.Forms;

public class DecreeSalaryManagementForm : Form
{
    private readonly string _connectionString;
    private readonly EmployeeRepository _empRepo;
    private readonly EmployeeContractRepository _contractRepo;
    private readonly DailyWageRateRepository _rateRepo;

    // کش نرخ روزمزدها: سال شمسی → (نام طبقه‌ی نرمال‌شده → نرخ روزانه) از «تنظیمات حقوق سالانه»
    private readonly Dictionary<int, Dictionary<string, decimal>> _rateCache = new();
    private static readonly PersianCalendar _persianCal = new PersianCalendar();

    private TextBox txtSearch = null!;
    private CheckBox chkOnlyActiveContracts = null!;
    private ModernTabHost tabControl = null!;

    private DataGridView dgvContractual = null!;
    private DataGridView dgvDaily = null!;
    private DataGridView dgvHourly = null!;

    private List<Employee> _allEmployees = new();
    private bool _isLoading = false;

    public DecreeSalaryManagementForm(string connectionString)
    {
        _connectionString = connectionString;
        _empRepo = new EmployeeRepository(connectionString);
        _contractRepo = new EmployeeContractRepository(connectionString);
        _rateRepo = new DailyWageRateRepository(connectionString);

        BuildUI();
        this.ApplyStandardHeader("ورود گروهی مبالغ حکم حقوقی", new Size(1300, 800));
        ShowInTaskbar = false;

        Shown += async (s, e) => await LoadDataSafeAsync();
    }

    private void BuildUI()
    {
        // ── نوار جستجو و فیلتر ──
        var filter = ModernReport.CreateFilterBar();

        txtSearch = new TextBox { PlaceholderText = "جستجوی نام یا کد پرسنلی..." };
        txtSearch.TextChanged += (s, e) => ApplyFilter();
        ModernReport.AddFilter(filter, "جستجو", txtSearch, 260);

        var chk = ModernUI.Check("فقط نمایش افرادی که حکم/قرارداد فعال دارند", ModernTheme.Brand);
        chk.Width = 360;
        chk.Margin = new Padding(16, 8, 4, 0);
        chk.CheckedChanged += async (s, e) => await LoadDataSafeAsync();
        chkOnlyActiveContracts = chk;
        filter.Controls.Add(chk);

        var btnRefresh = ModernButton.Soft("🔄 بروزرسانی", ModernTheme.Neutral, 130, 36);
        btnRefresh.Margin = new Padding(8, 4, 0, 0);
        btnRefresh.Click += async (s, e) => await LoadDataSafeAsync();
        filter.Controls.Add(btnRefresh);

        // ── تب‌ها (هر تب یک جدول) ──
        tabControl = new ModernTabHost { Dock = DockStyle.Fill, Margin = new Padding(16, 4, 16, 0) };

        dgvContractual = CreateGrid("contractual");
        dgvDaily = CreateGrid("daily");
        dgvHourly = CreateGrid("hourly");

        void AddTab(string title, DataGridView grid)
        {
            var page = tabControl.AddPage(title);
            page.Padding = new Padding(0, 12, 0, 8);
            page.Controls.Add(ModernReport.WrapGrid(grid));
        }
        AddTab("پرسنل قراردادی / ماهانه", dgvContractual);
        AddTab("پرسنل روزمزد", dgvDaily);
        AddTab("پرسنل ساعتی", dgvHourly);

        // ── نوار دکمه‌ها (اولین دکمه در راست) ──
        var btnSave = ModernButton.Success("💾 ذخیره همه تغییرات", 200, 40);
        btnSave.Click += async (s, e) => await SaveAllSafeAsync();

        var btnClear = ModernButton.Danger("🗑️ حذف اطلاعات انتخاب شده", 220, 40);
        btnClear.Click += (s, e) => ClearSelectedRows();

        var btnImport = ModernButton.Create("📥 ایمپورت از اکسل", ModernTheme.Teal, 170, 40);
        btnImport.Click += async (s, e) => await ImportExcelSafeAsync();

        var btnDownload = ModernButton.Primary("📤 دانلود قالب اکسل", 170, 40);
        btnDownload.Click += (s, e) => DownloadTemplate();

        var bar = ModernForm.CreateButtonBar(btnSave, btnClear, btnImport, btnDownload);
        bar.Paint += (s, e) =>
        {
            using var pen = new Pen(ModernTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = ModernTheme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        root.Controls.Add(filter, 0, 0);
        root.Controls.Add(tabControl, 0, 1);
        root.Controls.Add(bar, 0, 2);
        Controls.Add(root);
    }

    // kind: "contractual" | "daily" | "hourly"
    private DataGridView CreateGrid(string kind)
    {
        var dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            EditMode = DataGridViewEditMode.EditOnEnter
        };
        dgv.ApplyModernStyle();
        dgv.ReadOnly = false;

        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeId", DataPropertyName = "EmployeeId", Visible = false });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "PersonnelType", DataPropertyName = "PersonnelType", Visible = false });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractNumber", DataPropertyName = "ContractNumber", Visible = false });

        dgv.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsSelected", DataPropertyName = "IsSelected", HeaderText = "انتخاب", Width = 50 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "PersonnelCode", DataPropertyName = "PersonnelCode", HeaderText = "کد پرسنلی", ReadOnly = true, Width = 80 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "نام و نام خانوادگی", ReadOnly = true, Width = 150 });

        if (kind == "daily")
        {
            // روزمزد: نوع کارگر و حقوق روزانه فقط نمایشی هستند؛ فقط تاریخ شروع/پایان ورودی است
            var colCat = new DataGridViewTextBoxColumn { Name = "WageCategory", DataPropertyName = "WageCategory", HeaderText = "نوع کارگر", ReadOnly = true, Width = 160 };
            var colDaily = new DataGridViewTextBoxColumn { Name = "DailyWage", DataPropertyName = "DailyWage", HeaderText = "حقوق روزانه (جدول سالانه)", ReadOnly = true, Width = 180 };
            colCat.DefaultCellStyle.BackColor = Color.FromArgb(240, 237, 230);
            colDaily.DefaultCellStyle.BackColor = Color.FromArgb(240, 237, 230);
            dgv.Columns.Add(colCat);
            dgv.Columns.Add(colDaily);
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "RateFound", DataPropertyName = "RateFound", Visible = false });

            // اگر نرخ طبقه در «تنظیمات حقوق سالانه» آن سال ثبت نشده باشد، عدد قرمز نمایش داده می‌شود
            dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0 || dgv.Columns[e.ColumnIndex].Name != "DailyWage") return;

                var gridRow = dgv.Rows[e.RowIndex];
                bool found = Convert.ToBoolean(gridRow.Cells["RateFound"].Value ?? false);
                if (!found) e.CellStyle.ForeColor = Color.Firebrick;
                gridRow.Cells[e.ColumnIndex].ToolTipText = found
                    ? "نرخ طبقه از «تنظیمات حقوق سالانه» (سالِ تاریخ شروع قرارداد)"
                    : "نرخ این طبقه برای سالِ تاریخ شروع در «تنظیمات حقوق سالانه» ثبت نشده؛ نرخ ذخیره‌شده‌ی پرسنل نمایش داده می‌شود";
            };
        }
        else if (kind == "hourly")
        {
            // ساعتی: فقط مبلغ ساعتی + تاریخ شروع/پایان
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "HourlyWage", DataPropertyName = "HourlyWage", HeaderText = "مبلغ ساعتی", Width = 140 });
        }
        else
        {
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "BaseSalary", DataPropertyName = "BaseSalary", HeaderText = "حقوق پایه", Width = 110 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "YearsBonus", DataPropertyName = "YearsBonus", HeaderText = "پایه سنوات", Width = 100 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "AttractionAllowance", DataPropertyName = "AttractionAllowance", HeaderText = "حق جذب", Width = 100 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ResponsibilityAllowance", DataPropertyName = "ResponsibilityAllowance", HeaderText = "حق مسئولیت", Width = 100 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ManagementAllowance", DataPropertyName = "ManagementAllowance", HeaderText = "حق مدیریت", Width = 100 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "TechnicalAllowance", DataPropertyName = "TechnicalAllowance", HeaderText = "حق فنی", Width = 100 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "TransportationAllowance", DataPropertyName = "TransportationAllowance", HeaderText = "حق ایاب و ذهاب", Width = 100 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "OtherAllowances", DataPropertyName = "OtherAllowances", HeaderText = "سایر مزایا", Width = 100 });
        }

        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractStartDate", DataPropertyName = "ContractStartDate", HeaderText = "شروع قرارداد", Width = 100 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContractEndDate", DataPropertyName = "ContractEndDate", HeaderText = "پایان قرارداد", Width = 100 });

        dgv.EditingControlShowing += (s, e) =>
        {
            if (e.Control is TextBox tb)
            {
                tb.KeyPress -= AllowOnlyNumbers;
                tb.KeyPress -= AllowOnlyDate;

                string? colName = dgv.CurrentCell?.OwningColumn?.Name;
                bool isDateCol = colName == "ContractStartDate" || colName == "ContractEndDate";
                tb.KeyPress += isDateCol ? AllowOnlyDate : AllowOnlyNumbers;

                // تاریخ: «/» بین سال/ماه/روز هنگام تایپ خودکار گذاشته می‌شود
                tb.TextChanged -= DateHelper.LiveDateTextChanged;
                if (isDateCol) tb.TextChanged += DateHelper.LiveDateTextChanged;
            }
        };

        dgv.CellEndEdit += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgv.Columns[e.ColumnIndex].Name;

            // روزمزد: با تغییر تاریخ شروع، نرخ روزانه برای سال شمسیِ جدید از جدول خوانده می‌شود
            if (kind == "daily" && colName == "ContractStartDate")
            {
                if (dgv.Rows[e.RowIndex].DataBoundItem is DataRowView drv) ApplyDailyRate(drv.Row);
                return;
            }

            if (colName != "PersonnelCode" && colName != "EmployeeName" && colName != "IsSelected" &&
                colName != "WageCategory" && colName != "DailyWage" &&
                colName != "ContractStartDate" && colName != "ContractEndDate")
            {
                var cell = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex];
                decimal val = NumberHelper.ParseFormatted(cell.Value?.ToString());
                cell.Value = NumberHelper.FormatThousands(val, usePersianDigits: true);
            }
        };

        return dgv;
    }

    private void AllowOnlyNumbers(object? sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != ',' && e.KeyChar != '٫')
        {
            e.Handled = true;
        }
    }

    // ستون‌های تاریخ: رقم + «/» (و «-») مجاز است
    private void AllowOnlyDate(object? sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '/' && e.KeyChar != '-')
        {
            e.Handled = true;
        }
    }

    private static int PersianYearOf(DateTime d) => _persianCal.GetYear(d);

    private Dictionary<string, decimal> GetYearRates(int year)
    {
        if (_rateCache.TryGetValue(year, out var cached)) return cached;

        var map = new Dictionary<string, decimal>();
        try
        {
            foreach (var r in _rateRepo.GetRates(year))
            {
                string key = StringHelper.NormalizePersianText(r.Category).Trim();
                if (key.Length > 0 && r.Amount > 0) map[key] = r.Amount;
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "DecreeSalaryManagementForm.GetYearRates");
            return map; // در صورت خطا کش نمی‌شود تا دفعه‌ی بعد دوباره تلاش شود
        }

        _rateCache[year] = map;
        return map;
    }

    // نرخ روزانه‌ی طبقه‌ی کارگری از «تنظیمات حقوق سالانه ← حقوق روزمزدها» برای سال شمسیِ همان تاریخ
    private bool TryGetDailyRate(string? category, DateTime date, out decimal rate)
    {
        rate = 0;
        string key = StringHelper.NormalizePersianText(category ?? "").Trim();
        if (key.Length == 0) return false;
        return GetYearRates(PersianYearOf(date)).TryGetValue(key, out rate) && rate > 0;
    }

    // حقوق روزانه‌ی نمایشی یک ردیف روزمزد را بر اساس طبقه و سال تاریخ شروع قرارداد دوباره حساب می‌کند
    private void ApplyDailyRate(DataRow r)
    {
        int empId = Convert.ToInt32(r["EmployeeId"]);
        var emp = _allEmployees.FirstOrDefault(x => x.Id == empId);
        DateTime date = SafeParseDate(r["ContractStartDate"]?.ToString()) ?? DateTime.Now.Date;

        decimal wage = emp?.DailyWage ?? 0;   // اگر نرخی در جدول نبود، نرخ ذخیره‌شده‌ی پرسنل
        bool found = false;
        if (TryGetDailyRate(emp?.DailyWageCategory, date, out var rate))
        {
            wage = rate;
            found = true;
        }

        r["DailyWage"] = NumberHelper.FormatThousands(wage, usePersianDigits: true);
        r["RateFound"] = found;
    }

    // مقدار عددی یک ستون؛ اگر ستون در این جدول نباشد (مثلاً مزایا در تب روزمزد) مقدار پیش‌فرض برمی‌گردد
    private static decimal ReadMoney(DataGridView dgv, DataGridViewRow row, string col, decimal fallback)
    {
        if (!dgv.Columns.Contains(col)) return fallback;
        return NumberHelper.ParseFormatted(row.Cells[col].Value?.ToString());
    }

    private async Task LoadDataSafeAsync()
    {
        if (IsDisposed || _isLoading) return;
        _isLoading = true;
        try
        {
            await AsyncHelper.RunWithLoading(this, async () =>
            {
                _allEmployees = await _empRepo.GetEmployeesWithDetailsAsync("", true, chkOnlyActiveContracts.Checked);
                if (IsDisposed) return;
                _rateCache.Clear(); // تا نرخ‌های تازه‌ی «تنظیمات حقوق سالانه» خوانده شود
                ApplyFilter();
            }, "در حال بارگذاری لیست پرسنل...");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "DecreeSalaryManagementForm.LoadDataSafeAsync");
            if (!IsDisposed) ModernMessageBox.ShowError("خطا در بارگذاری:\n" + ex.Message);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ApplyFilter()
    {
        if (IsDisposed) return;

        string search = txtSearch.Text.Trim();
        var filtered = _allEmployees.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(e =>
                (e.FullName ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (e.PersonnelCode ?? "").Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var contractualList = filtered.Where(e => e.PersonnelType != "روزمزد" && e.PersonnelType != "ساعتی").ToList();
        var dailyList = filtered.Where(e => e.PersonnelType == "روزمزد").ToList();
        var hourlyList = filtered.Where(e => e.PersonnelType == "ساعتی").ToList();

        dgvContractual.DataSource = BuildDataTable(contractualList);
        dgvDaily.DataSource = BuildDataTable(dailyList);
        dgvHourly.DataSource = BuildDataTable(hourlyList);
    }

    private DataTable BuildDataTable(List<Employee> employees)
    {
        var dt = new DataTable();
        dt.Columns.Add("EmployeeId", typeof(int));
        dt.Columns.Add("IsSelected", typeof(bool));
        dt.Columns.Add("PersonnelCode", typeof(string));
        dt.Columns.Add("EmployeeName", typeof(string));
        dt.Columns.Add("BaseSalary", typeof(string));
        dt.Columns.Add("YearsBonus", typeof(string));
        dt.Columns.Add("AttractionAllowance", typeof(string));
        dt.Columns.Add("ResponsibilityAllowance", typeof(string));
        dt.Columns.Add("ManagementAllowance", typeof(string));
        dt.Columns.Add("TechnicalAllowance", typeof(string));
        dt.Columns.Add("TransportationAllowance", typeof(string));
        dt.Columns.Add("OtherAllowances", typeof(string));
        dt.Columns.Add("ContractStartDate", typeof(string));
        dt.Columns.Add("ContractEndDate", typeof(string));
        dt.Columns.Add("PersonnelType", typeof(string));
        dt.Columns.Add("ContractNumber", typeof(string));
        dt.Columns.Add("WageCategory", typeof(string));
        dt.Columns.Add("DailyWage", typeof(string));
        dt.Columns.Add("HourlyWage", typeof(string));
        dt.Columns.Add("RateFound", typeof(bool));

        foreach (var emp in employees)
        {
            // روزمزد: حقوق روزانه از جدول «تنظیمات حقوق سالانه» (طبقه + سال تاریخ شروع) خوانده می‌شود
            decimal shownDaily = emp.DailyWage;
            bool rateFound = false;
            if (emp.PersonnelType == "روزمزد" &&
                TryGetDailyRate(emp.DailyWageCategory, emp.ContractStartDate ?? DateTime.Now, out var tableRate))
            {
                shownDaily = tableRate;
                rateFound = true;
            }

            dt.Rows.Add(
                emp.Id,
                false,
                emp.PersonnelCode,
                emp.FullName,
                NumberHelper.FormatThousands(emp.BaseSalary, usePersianDigits: true),
                NumberHelper.FormatThousands(emp.YearsBonus, usePersianDigits: true),
                NumberHelper.FormatThousands(emp.AttractionAllowance, usePersianDigits: true),
                NumberHelper.FormatThousands(emp.ResponsibilityAllowance, usePersianDigits: true),
                NumberHelper.FormatThousands(emp.ManagementAllowance, usePersianDigits: true),
                NumberHelper.FormatThousands(emp.TechnicalAllowance, usePersianDigits: true),
                NumberHelper.FormatThousands(emp.TransportationAllowance, usePersianDigits: true),
                NumberHelper.FormatThousands(emp.OtherAllowances, usePersianDigits: true),
                DateHelper.ToPersianDate(emp.ContractStartDate ?? DateTime.Now),
                DateHelper.ToPersianDate(emp.ContractEndDate),
                emp.PersonnelType,
                emp.ContractNumber,
                string.IsNullOrWhiteSpace(emp.DailyWageCategory) ? "---" : emp.DailyWageCategory,
                NumberHelper.FormatThousands(shownDaily, usePersianDigits: true),
                NumberHelper.FormatThousands(emp.HourlyWage, usePersianDigits: true),
                rateFound
            );
        }
        dt.AcceptChanges();   // مبنای تشخیص «ردیف ویرایش‌شده» برای ذخیره بدون تیک
        return dt;
    }

    private void ClearSelectedRows()
    {
        var grids = new[] { dgvContractual, dgvDaily, dgvHourly };
        foreach (var dgv in grids)
        {
            if (dgv.DataSource is not DataTable dt) continue;

            foreach (DataRow row in dt.Rows)
            {
                bool isSelected = Convert.ToBoolean(row["IsSelected"]);
                if (isSelected)
                {
                    if (dgv == dgvContractual)
                    {
                        row["BaseSalary"] = "0";
                        row["YearsBonus"] = "0";
                        row["AttractionAllowance"] = "0";
                        row["ResponsibilityAllowance"] = "0";
                        row["ManagementAllowance"] = "0";
                        row["TechnicalAllowance"] = "0";
                        row["TransportationAllowance"] = "0";
                        row["OtherAllowances"] = "0";
                    }
                    else if (dgv == dgvHourly)
                    {
                        row["HourlyWage"] = "0";
                    }
                    // روزمزد: حقوق روزانه و نوع کارگر نمایشی‌اند و پاک نمی‌شوند

                    row["IsSelected"] = false;
                }
            }
        }
        ToastHelper.Show("اطلاعات ردیف‌های انتخاب شده پاک شد.", true);
    }

    private DateTime? SafeParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;

        string cleanStr = StringHelper.ConvertToEnglishDigits(dateStr.Trim()).Replace("-", "/");
        if (cleanStr.Contains(" ")) cleanStr = cleanStr.Split(' ')[0];
        if (cleanStr.Contains("T")) cleanStr = cleanStr.Split('T')[0];

        return DateHelper.ParsePersianDate(cleanStr);
    }

    private async Task SaveAllSafeAsync()
    {
        var grids = new[] { dgvContractual, dgvDaily, dgvHourly };
        int savedCount = 0;
        var errors = new List<string>();

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            foreach (var dgv in grids)
            {
                dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
                dgv.EndEdit();

                if (dgv.DataSource is not DataTable dt) continue;

                foreach (DataGridViewRow dgvRow in dgv.Rows)
                {
                    // ✅ برای ذخیره، تیک‌زدن لازم نیست: هر ردیفی که ویرایش شده (تاریخ‌ها/مبالغ) ذخیره می‌شود؛ تیک فقط اختیاری است
                    bool isSelected = Convert.ToBoolean(dgvRow.Cells["IsSelected"].Value ?? false);
                    bool isEdited = false;
                    if (dgvRow.DataBoundItem is DataRowView drvRow)
                    {
                        drvRow.EndEdit();
                        isEdited = IsRowEdited(drvRow.Row);
                    }
                    if (!isSelected && !isEdited) continue;

                    int empId = Convert.ToInt32(dgvRow.Cells["EmployeeId"].Value);
                    string fullName = dgvRow.Cells["EmployeeName"].Value?.ToString() ?? "";

                    try
                    {
                        DateTime startDate = SafeParseDate(dgvRow.Cells["ContractStartDate"].Value?.ToString()) ?? DateTime.Now.Date;
                        DateTime? endDate = SafeParseDate(dgvRow.Cells["ContractEndDate"].Value?.ToString());

                        string pType = dgvRow.Cells["PersonnelType"].Value?.ToString() ?? "ماهانه";

                        var emp = _allEmployees.FirstOrDefault(x => x.Id == empId);

                        // تاریخ‌ها باید معتبر باشند (قبلاً تاریخ نامعتبر بی‌صدا نادیده گرفته می‌شد)
                        string startText = dgvRow.Cells["ContractStartDate"].Value?.ToString() ?? "";
                        string endText = dgvRow.Cells["ContractEndDate"].Value?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(startText) || SafeParseDate(startText) is null)
                        {
                            errors.Add($"{fullName}: تاریخ شروع قرارداد وارد نشده یا نامعتبر است (نمونه: 1405/01/01).");
                            continue;
                        }
                        if (!string.IsNullOrWhiteSpace(endText) && endDate is null)
                        {
                            errors.Add($"{fullName}: تاریخ پایان قرارداد نامعتبر است (نمونه: 1405/12/29).");
                            continue;
                        }
                        if (endDate.HasValue && endDate.Value.Date < startDate.Date)
                        {
                            errors.Add($"{fullName}: تاریخ پایان قرارداد نمی‌تواند قبل از تاریخ شروع باشد.");
                            continue;
                        }

                        // اگر «شروع قرارداد» همان شروع قرارداد فعال فعلی است، قرارداد جدید ساخته نمی‌شود؛
                        // فقط تاریخ پایان همان قرارداد فعال ثبت/اصلاح می‌شود.
                        var activeContract = await _contractRepo.GetActiveContractByEmployeeIdAsync(empId);
                        bool reviseSameStart = false;   // شروع = شروع قرارداد فعال، ولی مبلغ تغییر کرده → اصلاح همان قرارداد (با بایگانی نسخه‌ی قبلی)
                        if (activeContract is not null && activeContract.StartDate.Date == startDate.Date)
                        {
                            bool rateChanged = false;
                            if (pType == "روزمزد" && TryGetDailyRate(emp?.DailyWageCategory, startDate, out var newRate))
                                rateChanged = newRate != activeContract.DailyWage;

                            if (HasMoneyChanged(dgv, dgvRow, emp) || rateChanged)
                            {
                                reviseSameStart = true;   // ادامه می‌دهد؛ اعتبارسنجی‌های پایین هم اجرا می‌شوند
                            }
                            else
                            {
                                if (activeContract.EndDate?.Date != endDate?.Date)
                                {
                                    // 🐞 قبلاً اینجا empId پاس می‌شد ولی متد شناسه‌ی «قرارداد» می‌خواهد
                                    await _contractRepo.SetActiveContractEndDateAsync(activeContract.Id, endDate);
                                    savedCount++;
                                }
                                else
                                {
                                    errors.Add($"{fullName}: چیزی تغییر نکرده است (شروع {DateHelper.ToPersianDate(startDate)} همان قرارداد فعلی است). برای قرارداد جدید، تاریخ شروع را بعد از آن بگذارید.");
                                }
                                dgvRow.Cells["IsSelected"].Value = false;
                                continue;
                            }
                        }

                        bool isHourly = pType == "ساعتی";
                        bool isDaily = pType == "روزمزد";

                        // روزمزد: نرخ روزانه = نرخ «نوع کارگر» از تنظیمات حقوق سالانه برای سال شمسیِ تاریخ شروع
                        decimal dailyWage = emp?.DailyWage ?? 0;
                        if (isDaily)
                        {
                            string wageCat = emp?.DailyWageCategory ?? "";
                            if (string.IsNullOrWhiteSpace(wageCat))
                            {
                                errors.Add($"{fullName}: نوع کارگر (طبقه) برای این پرسنل تعیین نشده است.");
                                continue;
                            }

                            if (TryGetDailyRate(wageCat, startDate, out var tableRate))
                            {
                                dailyWage = tableRate;
                            }
                            else if (dailyWage <= 0)
                            {
                                errors.Add($"{fullName}: نرخ طبقه «{wageCat}» برای سال {PersianYearOf(startDate)} در تنظیمات حقوق سالانه ثبت نشده است.");
                                continue;
                            }
                            // اگر نرخی در جدول نبود ولی نرخ ذخیره‌شده‌ی پرسنل هست، همان می‌ماند (مثل محاسبه‌ی حقوق)
                        }

                        // ماهانه: حقوق پایه‌ی صفر ثبت نمی‌شود
                        if (!isDaily && !isHourly &&
                            ReadMoney(dgv, dgvRow, "BaseSalary", emp?.BaseSalary ?? 0) <= 0)
                        {
                            errors.Add($"{fullName}: حقوق پایه باید بزرگ‌تر از صفر باشد.");
                            continue;
                        }

                        // ساعتی: مبلغ ساعتی الزامی است
                        decimal hourlyWage = ReadMoney(dgv, dgvRow, "HourlyWage", emp?.HourlyWage ?? 0);
                        if (isHourly && hourlyWage <= 0)
                        {
                            errors.Add($"{fullName}: مبلغ ساعتی وارد نشده است.");
                            continue;
                        }

                        // مزایا فقط در تب قراردادی/ماهانه ورودی‌اند؛ برای روزمزد و ساعتی مقدار فعلی پرسنل حفظ می‌شود
                        var contract = new EmployeeContract
                        {
                            EmployeeId = empId,
                            ContractNumber = $"C-{DateTime.Now:yyyyMMddHHmmss}-{empId}",   // ✅ FIX: هر حکم جدید شماره‌ی یکتا دارد
                            StartDate = startDate,
                            EndDate = endDate,
                            PersonnelType = pType,
                            Position = emp?.Position,
                            DailyWageCategory = emp?.DailyWageCategory,   // نوع کارگر (نمایشی)
                            HasInsurance = emp?.HasInsurance ?? true,
                            DailyWage = dailyWage,                         // حقوق روزانه (از جدول تنظیم حقوق سالانه)
                            HourlyWage = hourlyWage,
                            BaseSalary = ReadMoney(dgv, dgvRow, "BaseSalary", emp?.BaseSalary ?? 0),
                            YearsBonus = ReadMoney(dgv, dgvRow, "YearsBonus", emp?.YearsBonus ?? 0),
                            AttractionAllowance = ReadMoney(dgv, dgvRow, "AttractionAllowance", emp?.AttractionAllowance ?? 0),
                            ResponsibilityAllowance = ReadMoney(dgv, dgvRow, "ResponsibilityAllowance", emp?.ResponsibilityAllowance ?? 0),
                            ManagementAllowance = ReadMoney(dgv, dgvRow, "ManagementAllowance", emp?.ManagementAllowance ?? 0),
                            TechnicalAllowance = ReadMoney(dgv, dgvRow, "TechnicalAllowance", emp?.TechnicalAllowance ?? 0),
                            TransportationAllowance = ReadMoney(dgv, dgvRow, "TransportationAllowance", emp?.TransportationAllowance ?? 0),
                            OtherAllowances = ReadMoney(dgv, dgvRow, "OtherAllowances", emp?.OtherAllowances ?? 0),
                            Status = "فعال"
                        };

                        if (reviseSameStart)
                            await _contractRepo.ReviseActiveContractAsync(contract);
                        else
                            await _contractRepo.AddNewContractAsync(contract);
                        savedCount++;
                        dgvRow.Cells["IsSelected"].Value = false;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"خطا برای {fullName}: {ex.Message}");
                    }
                }
            }
        }, "در حال ذخیره و انتقال به آرشیو...");

        if (success && !IsDisposed)
        {
            foreach (var er in errors) LogHelper.LogWarning(er, "DecreeSalaryManagementForm.SaveAll");

            if (errors.Count > 0)
            {
                string more = errors.Count > 15 ? $"\n… و {errors.Count - 15} مورد دیگر (در فایل لاگ)" : "";
                ModernMessageBox.ShowWarning($"تعداد {savedCount} ردیف با موفقیت ذخیره شد.\n\n{errors.Count} مورد:\n" + string.Join("\n", errors.Take(15)) + more);
            }
            else if (savedCount > 0)
            {
                ToastHelper.Show($"{savedCount} ردیف با موفقیت ذخیره شد.", true);
            }
            else
            {
                ToastHelper.Show("تغییری برای ذخیره وجود نداشت (ردیف‌های ویرایش‌شده یا تیک‌خورده ذخیره می‌شوند).", false);
            }

            await LoadDataSafeAsync();
        }
    }

    private static readonly string[] EditableMoneyColumns =
    {
        "BaseSalary", "YearsBonus", "AttractionAllowance", "ResponsibilityAllowance",
        "ManagementAllowance", "TechnicalAllowance", "TransportationAllowance", "OtherAllowances", "HourlyWage"
    };

    private static string NormalizeDateText(object? v) =>
        StringHelper.ConvertToEnglishDigits((Convert.ToString(v) ?? "").Trim()).Replace("-", "/");

    /// <summary>آیا کاربر در این ردیف مبلغی یا تاریخ شروع/پایان را نسبت به مقدار بارگذاری‌شده تغییر داده است؟</summary>
    private bool IsRowEdited(DataRow r)
    {
        if (r.RowState != DataRowState.Modified) return false;

        foreach (var col in EditableMoneyColumns)
        {
            if (!r.Table.Columns.Contains(col)) continue;
            decimal o = NumberHelper.ParseFormatted(Convert.ToString(r[col, DataRowVersion.Original]));
            decimal c = NumberHelper.ParseFormatted(Convert.ToString(r[col, DataRowVersion.Current]));
            if (o != c) return true;
        }

        foreach (var col in new[] { "ContractStartDate", "ContractEndDate" })
        {
            string o = NormalizeDateText(r[col, DataRowVersion.Original]);
            string c = NormalizeDateText(r[col, DataRowVersion.Current]);
            if (o == c) continue;
            // تغییر واقعی، یا متنی که تاریخ معتبر نیست (تا با پیام خطا گزارش شود)
            if (SafeParseDate(o)?.Date != SafeParseDate(c)?.Date || (c.Length > 0 && SafeParseDate(c) is null)) return true;
        }
        return false;
    }

    /// <summary>آیا مبالغ ردیف با مقدار فعلی پرسنل فرق دارد؟ (فقط ستون‌هایی که در همین تب وجود دارند)</summary>
    private bool HasMoneyChanged(DataGridView dgv, DataGridViewRow row, Employee? emp)
    {
        if (emp is null) return false;
        var pairs = new (string Col, decimal Current)[]
        {
            ("BaseSalary", emp.BaseSalary), ("YearsBonus", emp.YearsBonus), ("AttractionAllowance", emp.AttractionAllowance),
            ("ResponsibilityAllowance", emp.ResponsibilityAllowance), ("ManagementAllowance", emp.ManagementAllowance),
            ("TechnicalAllowance", emp.TechnicalAllowance), ("TransportationAllowance", emp.TransportationAllowance),
            ("OtherAllowances", emp.OtherAllowances), ("HourlyWage", emp.HourlyWage)
        };
        foreach (var (col, cur) in pairs)
            if (dgv.Columns.Contains(col) && ReadMoney(dgv, row, col, cur) != cur) return true;
        return false;
    }

    private void DownloadTemplate()
    {
        using var sfd = new SaveFileDialog { Filter = "Excel Files|*.xlsx", FileName = "DecreeTemplate.xlsx", Title = "ذخیره قالب" };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("حکم حقوقی");
        ws.RightToLeft = true;
        string[] headers = { "کد پرسنلی", "نام", "حقوق پایه", "پایه سنوات", "حق جذب", "حق مسئولیت", "حق مدیریت", "حق فنی", "حق ایاب و ذهاب", "سایر مزایا", "مبلغ ساعتی", "شروع قرارداد", "پایان قرارداد" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        ws.Columns().AdjustToContents();
        wb.SaveAs(sfd.FileName);
        ToastHelper.Show("قالب دانلود شد.", true);
    }

    private string GetExcelDateString(IXLCell cell)
    {
        try
        {
            string formatted = cell.GetFormattedString().Trim();

            var parts = formatted.Split('/', '-');
            if (parts.Length >= 3)
            {
                string yearStr = StringHelper.ConvertToEnglishDigits(parts[0]);
                if (int.TryParse(yearStr, out int year) && year >= 1300 && year <= 1500)
                {
                    return formatted.Split(' ')[0];
                }
            }

            if (cell.DataType == XLDataType.DateTime)
            {
                return DateHelper.ToPersianDate(cell.GetDateTime());
            }

            if (formatted.Contains(" ")) formatted = formatted.Split(' ')[0];
            if (formatted.Contains("T")) formatted = formatted.Split('T')[0];
            return formatted;
        }
        catch
        {
            return "";
        }
    }

    private async Task ImportExcelSafeAsync()
    {
        using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx", Title = "انتخاب فایل اکسل حکم حقوقی" };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        int updatedCount = 0;
        var errors = new List<string>();

        bool success = await AsyncHelper.RunWithLoading(this, async () =>
        {
            using var wb = new XLWorkbook(ofd.FileName);
            var ws = wb.Worksheet(1);
            var rows = ws.RowsUsed().Skip(1);

            var headerRow = ws.Row(1);
            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in headerRow.CellsUsed())
            {
                string headerName = cell.GetString().Trim();
                if (!string.IsNullOrEmpty(headerName))
                    headerMap[headerName] = cell.Address.ColumnNumber;
            }

            int GetColIndex(params string[] names)
            {
                foreach (var name in names)
                {
                    var match = headerMap.FirstOrDefault(h => h.Key.Contains(name));
                    if (match.Value > 0) return match.Value;
                }
                return -1;
            }

            int codeCol = GetColIndex("کد پرسنلی");
            if (codeCol == -1) throw new Exception("ستون «کد پرسنلی» در فایل اکسل یافت نشد. لطفاً از قالب دانلود شده استفاده کنید.");

            int baseSalCol = GetColIndex("حقوق پایه");
            int yearsBonusCol = GetColIndex("پایه سنوات");
            int attrCol = GetColIndex("حق جذب");
            int respCol = GetColIndex("حق مسئولیت");
            int mgmtCol = GetColIndex("حق مدیریت");
            int techCol = GetColIndex("حق فنی");
            int transCol = GetColIndex("حق ایاب و ذهاب", "ایاب و ذهاب");
            int otherCol = GetColIndex("سایر مزایا");
            int hourlyCol = GetColIndex("مبلغ ساعتی", "دستمزد ساعتی");

            int startDateCol = GetColIndex("شروع قرارداد", "شروع");
            int endDateCol = GetColIndex("پایان قرارداد", "پایان");
            if (startDateCol <= 0)
                errors.Add("ستون «شروع قرارداد» در فایل اکسل پیدا نشد؛ تاریخ شروع اعمال نمی‌شود (از «دانلود قالب اکسل» استفاده کنید).");

            var dtContractual = dgvContractual.DataSource as DataTable;
            var dtDaily = dgvDaily.DataSource as DataTable;
            var dtHourly = dgvHourly.DataSource as DataTable;

            foreach (var row in rows)
            {
                try
                {
                    string pCode = row.Cell(codeCol).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(pCode)) continue;

                    var emp = _allEmployees.FirstOrDefault(e => e.PersonnelCode == pCode);
                    if (emp is null)
                    {
                        errors.Add($"کد پرسنلی {pCode} در سیستم یافت نشد.");
                        continue;
                    }

                    DataTable? targetDt = null;
                    if (emp.PersonnelType == "روزمزد") targetDt = dtDaily;
                    else if (emp.PersonnelType == "ساعتی") targetDt = dtHourly;
                    else targetDt = dtContractual;

                    if (targetDt is null)
                    {
                        errors.Add($"کد پرسنلی {pCode}: جدول نوع پرسنل «{emp.PersonnelType}» آماده نیست.");
                        continue;
                    }

                    var targetRow = targetDt.AsEnumerable().FirstOrDefault(r => r.Field<int>("EmployeeId") == emp.Id);
                    if (targetRow is null)
                    {
                        errors.Add($"کد پرسنلی {pCode} ({emp.FullName}): در جدول تب «{emp.PersonnelType}» نیست (جستجو یا فیلتر «فقط افرادی که حکم فعال دارند» فعال است؟).");
                        continue;
                    }

                    string Fmt(int col) => NumberHelper.FormatThousands(NumberHelper.ParseFormatted(row.Cell(col).GetString()), usePersianDigits: true);

                    if (targetDt == dtContractual)
                    {
                        if (baseSalCol > 0) targetRow["BaseSalary"] = Fmt(baseSalCol);
                        if (yearsBonusCol > 0) targetRow["YearsBonus"] = Fmt(yearsBonusCol);
                        if (attrCol > 0) targetRow["AttractionAllowance"] = Fmt(attrCol);
                        if (respCol > 0) targetRow["ResponsibilityAllowance"] = Fmt(respCol);
                        if (mgmtCol > 0) targetRow["ManagementAllowance"] = Fmt(mgmtCol);
                        if (techCol > 0) targetRow["TechnicalAllowance"] = Fmt(techCol);
                        if (transCol > 0) targetRow["TransportationAllowance"] = Fmt(transCol);
                        if (otherCol > 0) targetRow["OtherAllowances"] = Fmt(otherCol);
                    }
                    else if (targetDt == dtHourly)
                    {
                        if (hourlyCol > 0) targetRow["HourlyWage"] = Fmt(hourlyCol);
                    }
                    // روزمزد: فقط تاریخ‌ها (حقوق روزانه و نوع کارگر نمایشی‌اند)

                    // اعمال تاریخ‌ها
                    if (startDateCol > 0)
                    {
                        var cell = row.Cell(startDateCol);
                        if (cell.IsEmpty())
                        {
                            errors.Add($"کد پرسنلی {pCode} ({emp.FullName}): تاریخ شروع قرارداد در اکسل خالی است.");
                        }
                        else
                        {
                            string val = GetExcelDateString(cell);
                            if (SafeParseDate(val) is null)
                                errors.Add($"کد پرسنلی {pCode} ({emp.FullName}): تاریخ شروع «{val}» نامعتبر است (نمونه: 1405/07/01).");
                            else
                                targetRow["ContractStartDate"] = val;
                        }
                    }

                    if (endDateCol > 0)
                    {
                        var cell = row.Cell(endDateCol);
                        if (!cell.IsEmpty())
                        {
                            string val = GetExcelDateString(cell);
                            if (!string.IsNullOrWhiteSpace(val) && SafeParseDate(val) is null)
                                errors.Add($"کد پرسنلی {pCode} ({emp.FullName}): تاریخ پایان «{val}» نامعتبر است (نمونه: 1405/12/29).");
                            else if (!string.IsNullOrWhiteSpace(val))
                                targetRow["ContractEndDate"] = val;
                        }
                    }

                    // شروع برابر با شروع قرارداد فعلی → ذخیره قرارداد جدید نمی‌سازد؛ همین‌جا هشدار بده
                    var importedStart = SafeParseDate(targetRow["ContractStartDate"]?.ToString());
                    if (importedStart.HasValue && emp.ContractStartDate.HasValue
                        && importedStart.Value.Date == emp.ContractStartDate.Value.Date)
                    {
                        errors.Add($"هشدار — کد پرسنلی {pCode} ({emp.FullName}): تاریخ شروع ({DateHelper.ToPersianDate(importedStart.Value)}) همان شروع قرارداد فعلی است؛ برای ثبت قرارداد جدید باید تاریخ شروع بعد از آن باشد.");
                    }

                    if (targetDt == dtDaily) ApplyDailyRate(targetRow);

                    targetRow["IsSelected"] = true;
                    updatedCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"سطر {row.RowNumber()}: {ex.Message}");
                }
            }
        }, "در حال خواندن و اعمال اطلاعات از اکسل...");

        if (success && !IsDisposed)
        {
            if (errors.Count > 0)
            {
                ModernMessageBox.ShowWarning($"تعداد {updatedCount} ردیف با موفقیت از اکسل خوانده و در جدول اعمال شد.\n\n{errors.Count} مورد:\n" + string.Join("\n", errors.Take(15)) + (errors.Count > 15 ? $"\n… و {errors.Count - 15} مورد دیگر" : ""), "نتیجه ایمپورت");
            }
            else
            {
                ModernMessageBox.ShowInfo($"تعداد {updatedCount} ردیف با موفقیت از اکسل خوانده شد و تیک انتخاب برای آن‌ها زده شد.\n\nحالا می‌توانید دکمه «ذخیره همه تغییرات» را بزنید.", "نتیجه ایمپورت");
            }
        }
    }
}
