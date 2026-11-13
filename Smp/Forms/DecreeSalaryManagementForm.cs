using ClosedXML.Excel;
using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class DecreeSalaryManagementForm : Form
    {
        private readonly EmployeeRepository _empRepo;
        private readonly SalarySettingsRepository _settingsRepo;

        private TabControl tabControl = null!;
        private DataGridView dgvContractual = null!, dgvDaily = null!, dgvHourly = null!;
        private TextBox txtSearch = null!;
        private Button btnSave = null!, btnDownloadTemplate = null!, btnImportExcel = null!;

        private BindingList<Employee> _contractualList = new();
        private BindingList<Employee> _dailyList = new();
        private BindingList<Employee> _hourlyList = new();

        public DecreeSalaryManagementForm(string connectionString)
        {
            _empRepo = new EmployeeRepository(connectionString);
            _settingsRepo = new SalarySettingsRepository(connectionString);

            BuildUI();
            this.ApplyStandardHeader("ورود گروهی مبالغ حکم حقوقی", new Size(1300, 750));
            Shown += async (s, e) => await LoadDataAsync();
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F)); // جستجو
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // تب‌ها
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F)); // دکمه‌ها

            // نوار جستجو
            var pnlTop = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground };
            var flowTop = new FlowLayoutPanel { Dock = DockStyle.Right, Padding = new Padding(0, 10, 10, 0), WrapContents = false };

            txtSearch = new TextBox { Width = 250, Font = UIHelper.PersianFont(11f), PlaceholderText = "جستجو نام یا کد...", Margin = new Padding(0, 2, 5, 0) };
            txtSearch.TextChanged += async (s, e) => await LoadDataAsync();

            flowTop.Controls.Add(new Label { Text = "🔍 جستجو:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Margin = new Padding(0, 5, 5, 0) });
            flowTop.Controls.Add(txtSearch);
            pnlTop.Controls.Add(flowTop);

            // تب‌ها
            tabControl = new TabControl { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(12f, FontStyle.Bold) };
            ModernLayoutHelper.StyleModernTabControl(tabControl);

            var tabContractual = new TabPage("قراردادی / ماهانه") { BackColor = UIHelper.WarmBackground, Padding = new Padding(5) };
            dgvContractual = CreateGrid();
            AddMonthlyColumns(dgvContractual);
            tabContractual.Controls.Add(dgvContractual);

            var tabDaily = new TabPage("روزمزد") { BackColor = UIHelper.WarmBackground, Padding = new Padding(5) };
            dgvDaily = CreateGrid();
            AddDailyColumns(dgvDaily);
            tabDaily.Controls.Add(dgvDaily);

            var tabHourly = new TabPage("ساعتی") { BackColor = UIHelper.WarmBackground, Padding = new Padding(5) };
            dgvHourly = CreateGrid();
            AddHourlyColumns(dgvHourly);
            tabHourly.Controls.Add(dgvHourly);

            tabControl.TabPages.Add(tabContractual);
            tabControl.TabPages.Add(tabDaily);
            tabControl.TabPages.Add(tabHourly);

            // دکمه‌ها
            var pnlBottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };

            btnSave = UIHelper.CreateButton("💾 ذخیره همه تغییرات", UIHelper.ActionGreen, 180, 42);
            btnSave.Click += async (s, e) => await SaveDataAsync();

            btnDownloadTemplate = UIHelper.CreateButton("📥 دانلود قالب اکسل", UIHelper.SectionTeal, 160, 42);
            btnDownloadTemplate.Click += BtnDownloadTemplate_Click;

            btnImportExcel = UIHelper.CreateButton("📤 ایمپورت از اکسل", UIHelper.SectionBlue, 150, 42);
            btnImportExcel.Click += async (s, e) => await BtnImportExcel_Click(s, e);

            pnlBottom.Controls.Add(btnSave);
            pnlBottom.Controls.Add(btnImportExcel);
            pnlBottom.Controls.Add(btnDownloadTemplate);

            mainLayout.Controls.Add(pnlTop, 0, 0);
            mainLayout.Controls.Add(tabControl, 0, 1);
            mainLayout.Controls.Add(pnlBottom, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private DataGridView CreateGrid()
        {
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                ReadOnly = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect
            };
            dgv.ApplyModernStyle();

            dgv.CellFormatting += (s, e) =>
            {
                if (e.Value is decimal dec) { e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true); e.FormattingApplied = true; }
            };
            return dgv;
        }

        private void AddCommonColumns(DataGridView dgv)
        {
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PersonnelCode", HeaderText = "کد پرسنلی", ReadOnly = true, FillWeight = 10 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FullName", HeaderText = "نام و نام خانوادگی", ReadOnly = true, FillWeight = 20 });
        }

        private void AddMoneyCol(DataGridView dgv, string prop, string header, int weight = 12, bool isAutoFilled = false)
        {
            var cellStyle = new DataGridViewCellStyle { Format = "N0" };
            if (isAutoFilled)
            {
                cellStyle.BackColor = Color.FromArgb(240, 240, 240); // خاکستری برای فیلدهای قفل شده
            }
            else
            {
                cellStyle.BackColor = UIHelper.WarmWarningBackground; // زرد برای فیلدهای قابل ویرایش
            }

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = prop,
                HeaderText = header,
                FillWeight = weight,
                ReadOnly = isAutoFilled,
                DefaultCellStyle = cellStyle
            });
        }

        private void AddMonthlyColumns(DataGridView dgv)
        {
            AddCommonColumns(dgv);
            AddMoneyCol(dgv, "BaseSalary", "حقوق پایه", 15);
            AddMoneyCol(dgv, "YearsBonus", "پایه سنوات");

            // 👈 این ۴ ستون به صورت خودکار از تنظیمات سالانه می‌آیند و قفل (خاکستری) هستند
            AddMoneyCol(dgv, "HousingAllowance", "حق مسکن", 12, isAutoFilled: true);
            AddMoneyCol(dgv, "GroceryAllowance", "حق خواربار", 12, isAutoFilled: true);
            AddMoneyCol(dgv, "ChildAllowance", "حق اولاد", 12, isAutoFilled: true);
            AddMoneyCol(dgv, "MarriageAllowance", "حق تاهل", 12, isAutoFilled: true);

            AddMoneyCol(dgv, "AttractionAllowance", "حق جذب");
            AddMoneyCol(dgv, "ResponsibilityAllowance", "حق مسئولیت");
            AddMoneyCol(dgv, "ManagementAllowance", "حق مدیریت");
            AddMoneyCol(dgv, "TechnicalAllowance", "حق فنی");
            AddMoneyCol(dgv, "TransportationAllowance", "حق ایاب و ذهاب");
            AddMoneyCol(dgv, "OtherAllowances", "سایر مزایا");
        }

        private void AddDailyColumns(DataGridView dgv)
        {
            AddCommonColumns(dgv);
            AddMoneyCol(dgv, "DailyWage", "دستمزد روزانه (ریال)", 30);
        }

        private void AddHourlyColumns(DataGridView dgv)
        {
            AddCommonColumns(dgv);
            AddMoneyCol(dgv, "HourlyWage", "دستمزد ساعتی (ریال)", 30);
        }

        private async Task LoadDataAsync()
        {
            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                // گرفتن سال جاری شمسی برای خواندن تنظیمات
                var pc = new PersianCalendar();
                int currentYear = pc.GetYear(DateTime.Now);

                var settings = await _settingsRepo.GetByYearAsync(currentYear);
                var list = await _empRepo.GetEmployeesWithDetailsAsync(txtSearch.Text.Trim(), false);

                var contractual = list.Where(e => e.PersonnelType != "روزمزد" && e.PersonnelType != "ساعتی").ToList();
                var daily = list.Where(e => e.PersonnelType == "روزمزد").ToList();
                var hourly = list.Where(e => e.PersonnelType == "ساعتی").ToList();

                // 👈 فراخوانی خودکار مبالغ از تنظیمات سالانه برای پرسنل قراردادی
                if (settings != null)
                {
                    foreach (var emp in contractual)
                    {
                        emp.HousingAllowance = settings.HousingAllowance;
                        emp.GroceryAllowance = settings.FoodAllowance;

                        string marital = StringHelper.NormalizePersianText(emp.MaritalStatus ?? "");
                        emp.MarriageAllowance = (marital.Contains("متاهل") || marital.Contains("متأهل")) ? settings.MarriageAllowance : 0;

                        decimal childRate = settings.ChildAllowanceRate > 1 ? settings.ChildAllowanceRate / 100m : settings.ChildAllowanceRate;
                        emp.ChildAllowance = Math.Round(settings.MinimumWage * childRate * emp.ChildrenCount, 0);
                    }
                }

                _contractualList = new BindingList<Employee>(contractual);
                _dailyList = new BindingList<Employee>(daily);
                _hourlyList = new BindingList<Employee>(hourly);

                dgvContractual.DataSource = _contractualList;
                dgvDaily.DataSource = _dailyList;
                dgvHourly.DataSource = _hourlyList;

            }, "در حال بارگذاری لیست پرسنل...");

            if (!success) ModernMessageBox.ShowError("خطا در بارگذاری اطلاعات.");
        }

        private async Task SaveDataAsync()
        {
            dgvContractual.EndEdit();
            dgvDaily.EndEdit();
            dgvHourly.EndEdit();

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var allToUpdate = _contractualList.Concat(_dailyList).Concat(_hourlyList).ToList();
                foreach (var emp in allToUpdate)
                {
                    await _empRepo.UpdateSalaryDecreeAsync(emp);
                }
            }, "در حال ذخیره تغییرات...");

            if (success) ToastHelper.Show("تغییرات با موفقیت ذخیره شد.", true);
        }

        private void BtnDownloadTemplate_Click(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog { Filter = "Excel Files|*.xlsx", FileName = "SalaryDecree_Template.xlsx", Title = "ذخیره قالب" };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("حکم حقوقی");
                ws.RightToLeft = true;

                // 👈 ستون‌های مسکن، خواربار، اولاد و تاهل از اکسل حذف شدند
                string[] headers = {
                    "کد پرسنلی",
                    "حقوق پایه", "پایه سنوات", "حق جذب", "حق مسئولیت", "حق مدیریت", "حق فنی", "حق ایاب و ذهاب", "سایر مزایا",
                    "دستمزد روزانه", "دستمزد ساعتی"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                ws.Cell(2, 1).Value = "1001"; // نمونه
                ws.Columns().AdjustToContents();
                wb.SaveAs(sfd.FileName);
                ToastHelper.Show("قالب اکسل ذخیره شد.", true);
            }
            catch (Exception ex) { ModernMessageBox.ShowError("خطا:\n" + ex.Message); }
        }

        private async Task BtnImportExcel_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls", Title = "انتخاب فایل اکسل" };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                using var wb = new XLWorkbook(ofd.FileName);
                var ws = wb.Worksheet(1);
                var rows = ws.RowsUsed().Skip(1);

                var allEmps = _contractualList.Concat(_dailyList).Concat(_hourlyList).ToList();

                foreach (var row in rows)
                {
                    string code = row.Cell(1).GetString().Trim();
                    if (string.IsNullOrEmpty(code)) continue;

                    var emp = allEmps.FirstOrDefault(x => x.PersonnelCode == code);
                    if (emp != null)
                    {
                        // 👈 ایندکس ستون‌ها به دلیل حذف ۴ ستون تغییر کرده است
                        emp.BaseSalary = NumberHelper.ParseFormatted(row.Cell(2).GetString());
                        emp.YearsBonus = NumberHelper.ParseFormatted(row.Cell(3).GetString());
                        emp.AttractionAllowance = NumberHelper.ParseFormatted(row.Cell(4).GetString());
                        emp.ResponsibilityAllowance = NumberHelper.ParseFormatted(row.Cell(5).GetString());
                        emp.ManagementAllowance = NumberHelper.ParseFormatted(row.Cell(6).GetString());
                        emp.TechnicalAllowance = NumberHelper.ParseFormatted(row.Cell(7).GetString());
                        emp.TransportationAllowance = NumberHelper.ParseFormatted(row.Cell(8).GetString());
                        emp.OtherAllowances = NumberHelper.ParseFormatted(row.Cell(9).GetString());

                        emp.DailyWage = NumberHelper.ParseFormatted(row.Cell(10).GetString());
                        emp.HourlyWage = NumberHelper.ParseFormatted(row.Cell(11).GetString());
                    }
                }

                dgvContractual.Refresh();
                dgvDaily.Refresh();
                dgvHourly.Refresh();

            }, "در حال خواندن فایل اکسل...");

            if (success) ModernMessageBox.ShowInfo("اطلاعات از اکسل خوانده شد.\nحالا دکمه «ذخیره همه تغییرات» را بزنید.", "موفق");
        }
    }
}