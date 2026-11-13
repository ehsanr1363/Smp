using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class SalarySettingsForm : Form
    {
        private readonly string _connectionString;

        private ComboBox cmbYear = null!;
        private Button btnReload = null!, btnSave = null!;
        private TabControl tabControl = null!;

        private TextBox txtMinimumWage = null!, txtHousing = null!, txtFood = null!, txtMarriage = null!, txtChildRate = null!;
        private TextBox txtInsuranceEmp = null!, txtInsuranceEmpr = null!;
        // 🔴 فیلد txtTaxExemption به طور کامل حذف شد
        private NumericUpDown numAbsenceMultiplier = null!;
        private TextBox txtNormalDayHours = null!, txtHolidayDayHours = null!, txtMonthlyWorkingHours = null!;

        private DataGridView dgvTaxBrackets = null!;
        private Button btnAddBracket = null!, btnDeleteBracket = null!;

        private CheckBox chkTaxBase = null!, chkTaxYears = null!, chkTaxAttr = null!, chkTaxResp = null!;
        private CheckBox chkTaxMgmt = null!, chkTaxTech = null!, chkTaxHouse = null!, chkTaxGroc = null!;
        private CheckBox chkTaxChild = null!, chkTaxMarr = null!, chkTaxTrans = null!, chkTaxOther = null!;

        private CheckBox chkInsBase = null!, chkInsYears = null!, chkInsAttr = null!, chkInsResp = null!;
        private CheckBox chkInsMgmt = null!, chkInsTech = null!, chkInsHouse = null!, chkInsGroc = null!;
        private CheckBox chkInsChild = null!, chkInsMarr = null!, chkInsTrans = null!, chkInsOther = null!;

        private CheckBox chkOvtBase = null!, chkOvtYears = null!, chkOvtAttr = null!, chkOvtResp = null!;
        private CheckBox chkOvtMgmt = null!, chkOvtTech = null!, chkOvtHouse = null!, chkOvtGroc = null!;
        private CheckBox chkOvtChild = null!, chkOvtMarr = null!, chkOvtTrans = null!, chkOvtOther = null!;

        private bool _isInitializing = true;

        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public SalarySettingsForm(string connectionString)
        {
            _connectionString = connectionString;

            BuildUI();
            this.ApplyStandardHeader("تنظیمات حقوق سالانه", new Size(950, 780));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this, btnSave);
            LoadDefaults();

            Shown += async (s, e) =>
            {
                _isInitializing = false;
                await LoadAllAsync();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _loadLock.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private void LoadDefaults()
        {
            try
            {
                var pc = new PersianCalendar();
                int currentYear = pc.GetYear(DateTime.Now);
                if (cmbYear.Items.Contains(currentYear)) cmbYear.SelectedItem = currentYear;
                else if (cmbYear.Items.Count > 0) cmbYear.SelectedIndex = 0;
            }
            catch { }
        }

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));

            var yearPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            cmbYear = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f) };
            for (int i = 1400; i <= 1420; i++) cmbYear.Items.Add(i);
            cmbYear.SelectedIndexChanged += async (s, e) => { if (!_isInitializing) await LoadAllAsync(); };

            btnReload = UIHelper.CreateButton("🔄 بارگذاری", UIHelper.SectionBlue, 120, 34);
            btnReload.Click += async (s, e) => await LoadAllAsync();

            yearPanel.Controls.Add(new Label { Text = "سال مالی:", AutoSize = true, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Margin = new Padding(5, 10, 5, 5) });
            yearPanel.Controls.Add(cmbYear);
            yearPanel.Controls.Add(btnReload);

            tabControl = new TabControl { Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f, FontStyle.Bold) };
            ModernLayoutHelper.StyleModernTabControl(tabControl);

            // ==========================================
            // تب ۱: تنظیمات عمومی
            // ==========================================
            var pageGeneral = new TabPage("تنظیمات عمومی") { BackColor = UIHelper.WarmBackground, Padding = new Padding(15) };
            var layoutGeneral = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 8 };
            layoutGeneral.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            layoutGeneral.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            layoutGeneral.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            layoutGeneral.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            for (int i = 0; i < 8; i++) layoutGeneral.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            txtMinimumWage = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtMinimumWage);
            txtHousing = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtHousing);
            txtFood = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtFood);
            txtMarriage = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtMarriage);
            txtChildRate = new TextBox(); SmartInputHelper.EnableSmartPercentageTextBox(txtChildRate);
            txtInsuranceEmp = new TextBox(); SmartInputHelper.EnableSmartPercentageTextBox(txtInsuranceEmp);
            txtInsuranceEmpr = new TextBox(); SmartInputHelper.EnableSmartPercentageTextBox(txtInsuranceEmpr);

            numAbsenceMultiplier = new NumericUpDown { Minimum = 1, Maximum = 10, DecimalPlaces = 1, Value = 1, TextAlign = HorizontalAlignment.Center, Font = UIHelper.PersianFont(11f) };

            txtNormalDayHours = new TextBox();
            txtHolidayDayHours = new TextBox();
            txtMonthlyWorkingHours = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtMonthlyWorkingHours);

            UIHelper.AddField(layoutGeneral, "حداقل حقوق ماهانه (ریال):", txtMinimumWage, 0, 0, 1);
            UIHelper.AddField(layoutGeneral, "حق مسکن (ریال):", txtHousing, 0, 2, 3);
            UIHelper.AddField(layoutGeneral, "حق بن خواربار (ریال):", txtFood, 1, 0, 1);
            UIHelper.AddField(layoutGeneral, "حق تاهل (ریال):", txtMarriage, 1, 2, 3);
            UIHelper.AddField(layoutGeneral, "نرخ حق اولاد (%):", txtChildRate, 2, 0, 1);
            UIHelper.AddField(layoutGeneral, "بیمه سهم کارمند (%):", txtInsuranceEmp, 2, 2, 3);
            UIHelper.AddField(layoutGeneral, "بیمه سهم کارفرما (%):", txtInsuranceEmpr, 3, 0, 1);
            UIHelper.AddField(layoutGeneral, "ضریب کسر کار (مثلاً 2 برای ۲ برابر):", numAbsenceMultiplier, 3, 2, 3);
            UIHelper.AddField(layoutGeneral, "ساعت موظفی روز عادی (روزمزد):", txtNormalDayHours, 4, 0, 1);
            UIHelper.AddField(layoutGeneral, "ساعت موظفی روز تعطیل (روزمزد):", txtHolidayDayHours, 4, 2, 3);
            UIHelper.AddField(layoutGeneral, "ساعت کارکرد ماهانه (مبنای اضافه کار):", txtMonthlyWorkingHours, 5, 0, 1, 3);

            pageGeneral.Controls.Add(layoutGeneral);

            // ==========================================
            // تب ۲: پله‌های مالیات
            // ==========================================
            var pageBrackets = new TabPage("پله‌های مالیات") { BackColor = UIHelper.WarmBackground, Padding = new Padding(10) };
            var layoutBrackets = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layoutBrackets.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layoutBrackets.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var bracketBtns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            btnAddBracket = UIHelper.CreateButton("➕ افزودن پله", UIHelper.ActionGreen, 120, 30);
            btnAddBracket.Click += (s, e) => { if (dgvTaxBrackets.Columns.Count > 0) dgvTaxBrackets.Rows.Add(); };
            btnDeleteBracket = UIHelper.CreateButton("🗑️ حذف پله", UIHelper.ActionRed, 110, 30);
            btnDeleteBracket.Click += (s, e) => { if (dgvTaxBrackets.CurrentRow != null && !dgvTaxBrackets.CurrentRow.IsNewRow) dgvTaxBrackets.Rows.Remove(dgvTaxBrackets.CurrentRow); };
            bracketBtns.Controls.Add(btnAddBracket);
            bracketBtns.Controls.Add(btnDeleteBracket);

            dgvTaxBrackets = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = true, AllowUserToDeleteRows = true, EditMode = DataGridViewEditMode.EditOnEnter, SelectionMode = DataGridViewSelectionMode.CellSelect };
            dgvTaxBrackets.ApplyModernStyle();
            dgvTaxBrackets.ReadOnly = false;
            dgvTaxBrackets.Columns.Add(new DataGridViewTextBoxColumn { Name = "FromAmount", HeaderText = "از مبلغ (ریال)", FillWeight = 30 });
            dgvTaxBrackets.Columns.Add(new DataGridViewTextBoxColumn { Name = "ToAmount", HeaderText = "تا مبلغ (ریال) - خالی = بدون سقف", FillWeight = 35 });
            dgvTaxBrackets.Columns.Add(new DataGridViewTextBoxColumn { Name = "TaxRate", HeaderText = "نرخ (مثلاً 10 یا 0.10)", FillWeight = 35 });
            SmartInputHelper.EnableSmartGridInput(dgvTaxBrackets, amountColumns: new[] { "FromAmount", "ToAmount" }, percentageColumns: new[] { "TaxRate" });

            layoutBrackets.Controls.Add(bracketBtns, 0, 0);
            layoutBrackets.Controls.Add(dgvTaxBrackets, 0, 1);
            pageBrackets.Controls.Add(layoutBrackets);

            // ==========================================
            // تب ۳: مشمولین مالیات
            // ==========================================
            var pageTax = new TabPage("مشمولین مالیات") { BackColor = UIHelper.WarmBackground, Padding = new Padding(15) };
            var layoutTax = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 4 };
            for (int i = 0; i < 3; i++) layoutTax.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            for (int i = 0; i < 4; i++) layoutTax.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            chkTaxBase = CreateCheckBox("حقوق پایه"); chkTaxYears = CreateCheckBox("پایه سنوات"); chkTaxAttr = CreateCheckBox("حق جذب");
            chkTaxResp = CreateCheckBox("حق مسئولیت"); chkTaxMgmt = CreateCheckBox("حق مدیریت"); chkTaxTech = CreateCheckBox("حق فنی");
            chkTaxHouse = CreateCheckBox("حق مسکن"); chkTaxGroc = CreateCheckBox("حق خواربار"); chkTaxChild = CreateCheckBox("حق اولاد");
            chkTaxMarr = CreateCheckBox("حق تاهل"); chkTaxTrans = CreateCheckBox("حق ایاب و ذهاب"); chkTaxOther = CreateCheckBox("سایر مزایا");

            layoutTax.Controls.Add(chkTaxBase, 0, 0); layoutTax.Controls.Add(chkTaxYears, 1, 0); layoutTax.Controls.Add(chkTaxAttr, 2, 0);
            layoutTax.Controls.Add(chkTaxResp, 0, 1); layoutTax.Controls.Add(chkTaxMgmt, 1, 1); layoutTax.Controls.Add(chkTaxTech, 2, 1);
            layoutTax.Controls.Add(chkTaxHouse, 0, 2); layoutTax.Controls.Add(chkTaxGroc, 1, 2); layoutTax.Controls.Add(chkTaxChild, 2, 2);
            layoutTax.Controls.Add(chkTaxMarr, 0, 3); layoutTax.Controls.Add(chkTaxTrans, 1, 3); layoutTax.Controls.Add(chkTaxOther, 2, 3);

            pageTax.Controls.Add(layoutTax);

            // ==========================================
            // تب ۴: مشمولین بیمه
            // ==========================================
            var pageIns = new TabPage("مشمولین بیمه") { BackColor = UIHelper.WarmBackground, Padding = new Padding(15) };
            var layoutIns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 4 };
            for (int i = 0; i < 3; i++) layoutIns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            for (int i = 0; i < 4; i++) layoutIns.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            chkInsBase = CreateCheckBox("حقوق پایه"); chkInsYears = CreateCheckBox("پایه سنوات"); chkInsAttr = CreateCheckBox("حق جذب");
            chkInsResp = CreateCheckBox("حق مسئولیت"); chkInsMgmt = CreateCheckBox("حق مدیریت"); chkInsTech = CreateCheckBox("حق فنی");
            chkInsHouse = CreateCheckBox("حق مسکن"); chkInsGroc = CreateCheckBox("حق خواربار"); chkInsChild = CreateCheckBox("حق اولاد");
            chkInsMarr = CreateCheckBox("حق تاهل"); chkInsTrans = CreateCheckBox("حق ایاب و ذهاب"); chkInsOther = CreateCheckBox("سایر مزایا");

            layoutIns.Controls.Add(chkInsBase, 0, 0); layoutIns.Controls.Add(chkInsYears, 1, 0); layoutIns.Controls.Add(chkInsAttr, 2, 0);
            layoutIns.Controls.Add(chkInsResp, 0, 1); layoutIns.Controls.Add(chkInsMgmt, 1, 1); layoutIns.Controls.Add(chkInsTech, 2, 1);
            layoutIns.Controls.Add(chkInsHouse, 0, 2); layoutIns.Controls.Add(chkInsGroc, 1, 2); layoutIns.Controls.Add(chkInsChild, 2, 2);
            layoutIns.Controls.Add(chkInsMarr, 0, 3); layoutIns.Controls.Add(chkInsTrans, 1, 3); layoutIns.Controls.Add(chkInsOther, 2, 3);

            pageIns.Controls.Add(layoutIns);

            // ==========================================
            // تب ۵: مشمولین اضافه کاری
            // ==========================================
            var pageOvt = new TabPage("مشمولین اضافه کاری") { BackColor = UIHelper.WarmBackground, Padding = new Padding(15) };
            var layoutOvt = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 4 };
            for (int i = 0; i < 3; i++) layoutOvt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            for (int i = 0; i < 4; i++) layoutOvt.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            chkOvtBase = CreateCheckBox("حقوق پایه"); chkOvtYears = CreateCheckBox("پایه سنوات"); chkOvtAttr = CreateCheckBox("حق جذب");
            chkOvtResp = CreateCheckBox("حق مسئولیت"); chkOvtMgmt = CreateCheckBox("حق مدیریت"); chkOvtTech = CreateCheckBox("حق فنی");
            chkOvtHouse = CreateCheckBox("حق مسکن"); chkOvtGroc = CreateCheckBox("حق خواربار"); chkOvtChild = CreateCheckBox("حق اولاد");
            chkOvtMarr = CreateCheckBox("حق تاهل"); chkOvtTrans = CreateCheckBox("حق ایاب و ذهاب"); chkOvtOther = CreateCheckBox("سایر مزایا");

            layoutOvt.Controls.Add(chkOvtBase, 0, 0); layoutOvt.Controls.Add(chkOvtYears, 1, 0); layoutOvt.Controls.Add(chkOvtAttr, 2, 0);
            layoutOvt.Controls.Add(chkOvtResp, 0, 1); layoutOvt.Controls.Add(chkOvtMgmt, 1, 1); layoutOvt.Controls.Add(chkOvtTech, 2, 1);
            layoutOvt.Controls.Add(chkOvtHouse, 0, 2); layoutOvt.Controls.Add(chkOvtGroc, 1, 2); layoutOvt.Controls.Add(chkOvtChild, 2, 2);
            layoutOvt.Controls.Add(chkOvtMarr, 0, 3); layoutOvt.Controls.Add(chkOvtTrans, 1, 3); layoutOvt.Controls.Add(chkOvtOther, 2, 3);

            pageOvt.Controls.Add(layoutOvt);

            tabControl.TabPages.Add(pageGeneral);
            tabControl.TabPages.Add(pageBrackets);
            tabControl.TabPages.Add(pageTax);
            tabControl.TabPages.Add(pageIns);
            tabControl.TabPages.Add(pageOvt);

            var savePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };
            btnSave = UIHelper.CreateButton("💾 ذخیره تنظیمات", UIHelper.ActionGreen, 200, 45);
            btnSave.Font = UIHelper.PersianFont(12f, FontStyle.Bold);
            btnSave.Click += BtnSave_Click;
            savePanel.Controls.Add(btnSave);

            mainLayout.Controls.Add(yearPanel, 0, 0);
            mainLayout.Controls.Add(tabControl, 0, 1);
            mainLayout.Controls.Add(savePanel, 0, 2);

            this.Controls.Add(mainLayout);
        }

        private CheckBox CreateCheckBox(string text)
        {
            return new CheckBox { Text = text, Dock = DockStyle.Fill, Font = UIHelper.PersianFont(11f), ForeColor = UIHelper.WarmText };
        }

        private static string FormatToHoursMinutes(decimal hours)
        {
            int h = (int)hours;
            int m = (int)Math.Round((hours - h) * 60);
            if (m == 60) { m = 0; h++; }
            return $"{h}:{m:00}";
        }

        private static decimal ParseHoursMinutes(string text)
        {
            return TimeHelper.ParseTimeString(text);
        }

        private async Task LoadAllAsync()
        {
            if (cmbYear.SelectedItem == null || IsDisposed) return;

            try
            {
                await _loadLock.WaitAsync();
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            try
            {
                if (IsDisposed || cmbYear.SelectedItem == null) return;
                int year = Convert.ToInt32(cmbYear.SelectedItem);

                await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var repo = new SalarySettingsRepository(_connectionString);
                    var settings = await repo.GetByYearAsync(year);

                    if (settings != null)
                    {
                        txtMinimumWage.Text = NumberHelper.FormatThousands(settings.MinimumWage, usePersianDigits: true);
                        txtHousing.Text = NumberHelper.FormatThousands(settings.HousingAllowance, usePersianDigits: true);
                        txtFood.Text = NumberHelper.FormatThousands(settings.FoodAllowance, usePersianDigits: true);
                        txtMarriage.Text = NumberHelper.FormatThousands(settings.MarriageAllowance, usePersianDigits: true);
                        txtChildRate.Text = StringHelper.ConvertToPersianDigits(ToDisplayPercent(settings.ChildAllowanceRate).ToString("0.##"));
                        txtInsuranceEmp.Text = StringHelper.ConvertToPersianDigits(ToDisplayPercent(settings.InsuranceEmployeeRate).ToString("0.##"));
                        txtInsuranceEmpr.Text = StringHelper.ConvertToPersianDigits(ToDisplayPercent(settings.InsuranceEmployerRate).ToString("0.##"));

                        // 🔴 خط مربوط به TaxExemptionLimit کاملاً حذف شده است

                        numAbsenceMultiplier.Value = settings.AbsenceMultiplier > 0 ? settings.AbsenceMultiplier : 1.0m;

                        txtNormalDayHours.Text = FormatToHoursMinutes(settings.NormalDayHours);
                        txtHolidayDayHours.Text = FormatToHoursMinutes(settings.HolidayDayHours);
                        txtMonthlyWorkingHours.Text = settings.MonthlyWorkingHours.ToString("0.##");

                        chkTaxBase.Checked = settings.TaxIncludeBaseSalary; chkTaxYears.Checked = settings.TaxIncludeYearsBonus; chkTaxAttr.Checked = settings.TaxIncludeAttraction;
                        chkTaxResp.Checked = settings.TaxIncludeResponsibility; chkTaxMgmt.Checked = settings.TaxIncludeManagement; chkTaxTech.Checked = settings.TaxIncludeTechnical;
                        chkTaxHouse.Checked = settings.TaxIncludeHousing; chkTaxGroc.Checked = settings.TaxIncludeGrocery; chkTaxChild.Checked = settings.TaxIncludeChild;
                        chkTaxMarr.Checked = settings.TaxIncludeMarriage; chkTaxTrans.Checked = settings.TaxIncludeTransportation; chkTaxOther.Checked = settings.TaxIncludeOtherAllowances;

                        chkInsBase.Checked = settings.InsuranceIncludeBaseSalary; chkInsYears.Checked = settings.InsuranceIncludeYearsBonus; chkInsAttr.Checked = settings.InsuranceIncludeAttraction;
                        chkInsResp.Checked = settings.InsuranceIncludeResponsibility; chkInsMgmt.Checked = settings.InsuranceIncludeManagement; chkInsTech.Checked = settings.InsuranceIncludeTechnical;
                        chkInsHouse.Checked = settings.InsuranceIncludeHousing; chkInsGroc.Checked = settings.InsuranceIncludeGrocery; chkInsChild.Checked = settings.InsuranceIncludeChild;
                        chkInsMarr.Checked = settings.InsuranceIncludeMarriage; chkInsTrans.Checked = settings.InsuranceIncludeTransportation; chkInsOther.Checked = settings.InsuranceIncludeOther;

                        chkOvtBase.Checked = settings.OvertimeIncludeBaseSalary; chkOvtYears.Checked = settings.OvertimeIncludeYearsBonus; chkOvtAttr.Checked = settings.OvertimeIncludeAttraction;
                        chkOvtResp.Checked = settings.OvertimeIncludeResponsibility; chkOvtMgmt.Checked = settings.OvertimeIncludeManagement; chkOvtTech.Checked = settings.OvertimeIncludeTechnical;
                        chkOvtHouse.Checked = settings.OvertimeIncludeHousing; chkOvtGroc.Checked = settings.OvertimeIncludeGrocery; chkOvtChild.Checked = settings.OvertimeIncludeChild;
                        chkOvtMarr.Checked = settings.OvertimeIncludeMarriage; chkOvtTrans.Checked = settings.OvertimeIncludeTransportation; chkOvtOther.Checked = settings.OvertimeIncludeOther;
                    }
                    else
                    {
                        txtMinimumWage.Text = "0"; txtHousing.Text = "0"; txtFood.Text = "0"; txtMarriage.Text = "0";
                        txtChildRate.Text = "10"; txtInsuranceEmp.Text = "7"; txtInsuranceEmpr.Text = "23";
                        txtNormalDayHours.Text = "8:30"; txtHolidayDayHours.Text = "7:00"; txtMonthlyWorkingHours.Text = "220";

                        numAbsenceMultiplier.Value = 1;

                        chkTaxBase.Checked = true; chkTaxYears.Checked = true; chkTaxAttr.Checked = true; chkTaxResp.Checked = true; chkTaxMgmt.Checked = true; chkTaxTech.Checked = true;
                        chkTaxHouse.Checked = false; chkTaxGroc.Checked = false; chkTaxChild.Checked = false; chkTaxMarr.Checked = false; chkTaxTrans.Checked = true; chkTaxOther.Checked = true;

                        chkInsBase.Checked = true; chkInsYears.Checked = true; chkInsAttr.Checked = true; chkInsResp.Checked = true; chkInsMgmt.Checked = true; chkInsTech.Checked = true;
                        chkInsHouse.Checked = true; chkInsGroc.Checked = true; chkInsChild.Checked = true; chkInsMarr.Checked = false; chkInsTrans.Checked = false; chkInsOther.Checked = true;

                        chkOvtBase.Checked = true; chkOvtYears.Checked = true; chkOvtAttr.Checked = false; chkOvtResp.Checked = false; chkOvtMgmt.Checked = false; chkOvtTech.Checked = false;
                        chkOvtHouse.Checked = false; chkOvtGroc.Checked = false; chkOvtChild.Checked = false; chkOvtMarr.Checked = false; chkOvtTrans.Checked = false; chkOvtOther.Checked = false;
                    }

                    dgvTaxBrackets.AllowUserToAddRows = false;
                    dgvTaxBrackets.Rows.Clear();
                    var brackets = await repo.GetBracketsAsync(year);
                    foreach (var b in brackets)
                    {
                        string toStr = b.ToAmount.HasValue ? NumberHelper.FormatThousands(b.ToAmount.Value, usePersianDigits: true) : "";
                        dgvTaxBrackets.Rows.Add(NumberHelper.FormatThousands(b.FromAmount, usePersianDigits: true), toStr, ToDisplayPercent(b.TaxRate).ToString("0.##", CultureInfo.InvariantCulture));
                    }
                    dgvTaxBrackets.AllowUserToAddRows = true;
                }, "در حال بارگذاری تنظیمات...");
            }
            finally
            {
                try { _loadLock.Release(); } catch { }
            }
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (cmbYear.SelectedItem == null) return;
            int year = Convert.ToInt32(cmbYear.SelectedItem);

            var settings = new SalarySettings
            {
                Year = year,
                MinimumWage = NumberHelper.ParseFormatted(txtMinimumWage.Text),
                HousingAllowance = NumberHelper.ParseFormatted(txtHousing.Text),
                FoodAllowance = NumberHelper.ParseFormatted(txtFood.Text),
                MarriageAllowance = NumberHelper.ParseFormatted(txtMarriage.Text),
                ChildAllowanceRate = ParsePercent(txtChildRate.Text),
                InsuranceEmployeeRate = ParsePercent(txtInsuranceEmp.Text),
                InsuranceEmployerRate = ParsePercent(txtInsuranceEmpr.Text),

                // 🔴 خط مربوط به TaxExemptionLimit کاملاً حذف شده است

                AbsenceMultiplier = numAbsenceMultiplier.Value,

                NormalDayHours = ParseHoursMinutes(txtNormalDayHours.Text),
                HolidayDayHours = ParseHoursMinutes(txtHolidayDayHours.Text),
                MonthlyWorkingHours = NumberHelper.ParseFormatted(txtMonthlyWorkingHours.Text),

                TaxIncludeBaseSalary = chkTaxBase.Checked,
                TaxIncludeYearsBonus = chkTaxYears.Checked,
                TaxIncludeAttraction = chkTaxAttr.Checked,
                TaxIncludeResponsibility = chkTaxResp.Checked,
                TaxIncludeManagement = chkTaxMgmt.Checked,
                TaxIncludeTechnical = chkTaxTech.Checked,
                TaxIncludeHousing = chkTaxHouse.Checked,
                TaxIncludeGrocery = chkTaxGroc.Checked,
                TaxIncludeChild = chkTaxChild.Checked,
                TaxIncludeMarriage = chkTaxMarr.Checked,
                TaxIncludeTransportation = chkTaxTrans.Checked,
                TaxIncludeOtherAllowances = chkTaxOther.Checked,

                InsuranceIncludeBaseSalary = chkInsBase.Checked,
                InsuranceIncludeYearsBonus = chkInsYears.Checked,
                InsuranceIncludeAttraction = chkInsAttr.Checked,
                InsuranceIncludeResponsibility = chkInsResp.Checked,
                InsuranceIncludeManagement = chkInsMgmt.Checked,
                InsuranceIncludeTechnical = chkInsTech.Checked,
                InsuranceIncludeHousing = chkInsHouse.Checked,
                InsuranceIncludeGrocery = chkInsGroc.Checked,
                InsuranceIncludeChild = chkInsChild.Checked,
                InsuranceIncludeMarriage = chkInsMarr.Checked,
                InsuranceIncludeTransportation = chkInsTrans.Checked,
                InsuranceIncludeOther = chkInsOther.Checked,

                OvertimeIncludeBaseSalary = chkOvtBase.Checked,
                OvertimeIncludeYearsBonus = chkOvtYears.Checked,
                OvertimeIncludeAttraction = chkOvtAttr.Checked,
                OvertimeIncludeResponsibility = chkOvtResp.Checked,
                OvertimeIncludeManagement = chkOvtMgmt.Checked,
                OvertimeIncludeTechnical = chkOvtTech.Checked,
                OvertimeIncludeHousing = chkOvtHouse.Checked,
                OvertimeIncludeGrocery = chkOvtGroc.Checked,
                OvertimeIncludeChild = chkOvtChild.Checked,
                OvertimeIncludeMarriage = chkOvtMarr.Checked,
                OvertimeIncludeTransportation = chkOvtTrans.Checked,
                OvertimeIncludeOther = chkOvtOther.Checked
            };

            if (settings.NormalDayHours <= 0 || settings.HolidayDayHours <= 0 || settings.MonthlyWorkingHours <= 0)
            {
                ModernMessageBox.ShowWarning("ساعت موظفی و ساعت کارکرد ماهانه باید بزرگتر از صفر باشند.");
                return;
            }

            var brackets = new List<TaxBracketDto>();
            foreach (DataGridViewRow row in dgvTaxBrackets.Rows)
            {
                if (row.IsNewRow) continue;
                string fromStr = row.Cells[0].Value?.ToString() ?? "";
                string toStr = row.Cells[1].Value?.ToString() ?? "";
                string rateStr = row.Cells[2].Value?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(fromStr) || string.IsNullOrWhiteSpace(rateStr)) continue;

                brackets.Add(new TaxBracketDto
                {
                    FromAmount = NumberHelper.ParseFormatted(fromStr),
                    ToAmount = string.IsNullOrWhiteSpace(toStr) ? (decimal?)null : NumberHelper.ParseFormatted(toStr),
                    TaxRate = ToDecimalRate(ParsePercent(rateStr))
                });
            }

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var repo = new SalarySettingsRepository(_connectionString);
                await repo.SaveWithBracketsAsync(settings, brackets);
            }, "در حال ذخیره تنظیمات...");

            if (success)
            {
                ToastHelper.Show("تنظیمات حقوق با موفقیت ذخیره شد.", isSuccess: true);
                await LoadAllAsync();
            }
        }

        private static decimal ParsePercent(string text)
        {
            string clean = StringHelper.ConvertToEnglishDigits(text ?? "").Replace("%", "").Replace("٫", ".").Replace(",", "").Replace("٬", "").Trim();
            if (!decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v)) return 0m;
            return v > 0 && v <= 1m ? v * 100m : v;
        }

        private static decimal ToDisplayPercent(decimal rate) => rate > 0 && rate <= 1m ? rate * 100m : rate;
        private static decimal ToDecimalRate(decimal rate) => rate > 1m ? rate / 100m : rate;
    }
}