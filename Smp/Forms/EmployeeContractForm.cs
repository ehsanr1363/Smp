using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using SMP.Services;

namespace SMP.Forms
{
    public class EmployeeContractForm : Form
    {
        private readonly string _connectionString;
        private Employee _employee;
        private readonly EmployeeContractRepository _repository;
        private readonly LookupService _lookupService;

        private TabControl tabControl = null!;

        // --- تب ۱: ماهانه / قراردادی ---
        private DataGridView dgvMonthly = null!;
        private TextBox txtMonthlyContractNum = null!, txtMonthlyPosition = null!, txtMonthlyStart = null!, txtMonthlyEnd = null!;
        private TextBox txtMonthlyBase = null!, txtMonthlyYears = null!, txtMonthlyAttr = null!, txtMonthlyResp = null!;
        private TextBox txtMonthlyMgmt = null!, txtMonthlyTech = null!, txtMonthlyTransport = null!, txtMonthlyOther = null!;
        private CheckBox chkMonthlyHasEnd = null!, chkMonthlyIns = null!;
        private Button btnSaveMonthly = null!, btnNewMonthly = null!, btnDelMonthly = null!, btnPrintMonthly = null!;
        private int? _editMonthlyId = null;

        // --- تب ۲: روزمزد ---
        private DataGridView dgvDaily = null!;
        private TextBox txtDailyContractNum = null!, txtDailyPosition = null!, txtDailyStart = null!, txtDailyEnd = null!;
        private ComboBox cmbDailyCategory = null!;
        private CheckBox chkDailyHasEnd = null!, chkDailyIns = null!;
        private Button btnSaveDaily = null!, btnNewDaily = null!, btnDelDaily = null!, btnPrintDaily = null!;
        private int? _editDailyId = null;

        // --- تب ۳: ساعتی ---
        private DataGridView dgvHourly = null!;
        private TextBox txtHourlyContractNum = null!, txtHourlyPosition = null!, txtHourlyStart = null!, txtHourlyEnd = null!, txtHourlyRate = null!;
        private CheckBox chkHourlyHasEnd = null!, chkHourlyIns = null!;
        private Button btnSaveHourly = null!, btnNewHourly = null!, btnDelHourly = null!, btnPrintHourly = null!;
        private int? _editHourlyId = null;

        private bool _isSaving = false;

        public EmployeeContractForm(string connectionString, Employee employee)
        {
            _connectionString = connectionString;
            _employee = employee;
            _repository = new EmployeeContractRepository(connectionString);
            _lookupService = new LookupService(new LookupRepository(connectionString));

            BuildUI();
            this.ApplyStandardHeader($"مدیریت قراردادها - {employee.FullName}", new Size(1100, 850));
            ShowInTaskbar = false;

            ShortcutHelper.ApplyStandardShortcuts(this);

            Shown += async (s, e) =>
            {
                await LoadCategoriesAsync();
                await LoadAllContractsAsync();
                RestrictTabsBasedOnPersonnelType();
            };
        }

        private async Task LoadCategoriesAsync()
        {
            try
            {
                var list = await _lookupService.GetByCategoryAsync("DailyWageCategory");
                cmbDailyCategory.DataSource = list;
                cmbDailyCategory.DisplayMember = "Value";
                cmbDailyCategory.ValueMember = "Value";
                cmbDailyCategory.SelectedIndex = -1;
            }
            catch { }
        }

        private void RestrictTabsBasedOnPersonnelType()
        {
            string empType = _employee.PersonnelType?.Trim() ?? "ماهانه";

            if (empType == "روزمزد")
            {
                tabControl.TabPages.RemoveByKey("tabMonthly");
                tabControl.TabPages.RemoveByKey("tabHourly");
                if (tabControl.TabPages.ContainsKey("tabDaily")) tabControl.SelectedTab = tabControl.TabPages["tabDaily"];
            }
            else if (empType == "ساعتی")
            {
                tabControl.TabPages.RemoveByKey("tabMonthly");
                tabControl.TabPages.RemoveByKey("tabDaily");
                if (tabControl.TabPages.ContainsKey("tabHourly")) tabControl.SelectedTab = tabControl.TabPages["tabHourly"];
            }
            else
            {
                tabControl.TabPages.RemoveByKey("tabDaily");
                tabControl.TabPages.RemoveByKey("tabHourly");
                if (tabControl.TabPages.ContainsKey("tabMonthly")) tabControl.SelectedTab = tabControl.TabPages["tabMonthly"];
            }
        }

        private void BuildUI()
        {
            // ✅ ۱. پنل بالایی برای دکمه آرشیو
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10), BackColor = UIHelper.WarmPanelBackground };
            var btnViewArchive = UIHelper.CreateButton("📂 آرشیو قراردادهای این پرسنل", UIHelper.SectionTeal, 220, 35);
            btnViewArchive.Dock = DockStyle.Right;
            btnViewArchive.Click += (s, e) =>
            {
                using var f = new ContractArchiveViewForm(_connectionString, _employee.Id, _employee.FullName);
                f.ShowDialog(this);
            };
            topPanel.Controls.Add(btnViewArchive);

            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = UIHelper.PersianFont(12f, FontStyle.Bold),
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(200, 40)
            };

            // ==========================================
            // تب ۱: ماهانه / قراردادی
            // ==========================================
            var pageMonthly = new TabPage("قرارداد ماهانه / قراردادی") { Name = "tabMonthly", BackColor = UIHelper.WarmBackground, Padding = new Padding(10) };
            var layoutMonthly = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layoutMonthly.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));
            layoutMonthly.RowStyles.Add(new RowStyle(SizeType.Percent, 70F));

            dgvMonthly = CreateGrid();
            layoutMonthly.Controls.Add(CreateGridPanel(dgvMonthly, "سوابق قراردادهای ماهانه (دابل کلیک برای ویرایش):"), 0, 0);

            var formMonthly = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 7, Padding = new Padding(0, 10, 0, 10) };
            formMonthly.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            formMonthly.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            formMonthly.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            formMonthly.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            for (int i = 0; i < 7; i++) formMonthly.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));

            txtMonthlyContractNum = new TextBox(); txtMonthlyPosition = new TextBox();
            txtMonthlyStart = new TextBox(); DateHelper.ApplyDateMask(txtMonthlyStart);
            txtMonthlyEnd = new TextBox(); DateHelper.ApplyDateMask(txtMonthlyEnd);

            txtMonthlyBase = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtMonthlyBase);
            txtMonthlyYears = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtMonthlyYears);
            txtMonthlyAttr = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtMonthlyAttr);
            txtMonthlyResp = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtMonthlyResp);
            txtMonthlyMgmt = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtMonthlyMgmt);
            txtMonthlyTech = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtMonthlyTech);
            txtMonthlyTransport = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtMonthlyTransport);
            txtMonthlyOther = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtMonthlyOther);

            chkMonthlyHasEnd = new CheckBox { Text = "دارای تاریخ پایان", Checked = true };
            chkMonthlyHasEnd.CheckedChanged += (s, e) => txtMonthlyEnd.Enabled = chkMonthlyHasEnd.Checked;
            chkMonthlyIns = new CheckBox { Text = "بیمه دارد", Checked = true };

            UIHelper.AddField(formMonthly, "شماره قرارداد:", txtMonthlyContractNum, 0, 0, 1);
            UIHelper.AddField(formMonthly, "سمت:", txtMonthlyPosition, 0, 2, 3);

            UIHelper.AddField(formMonthly, "تاریخ شروع:", txtMonthlyStart, 1, 0, 1);
            var pnlEndM = new Panel { Dock = DockStyle.Fill };
            txtMonthlyEnd.Location = new Point(0, 5); txtMonthlyEnd.Width = 150;
            chkMonthlyHasEnd.Location = new Point(160, 8);
            pnlEndM.Controls.Add(txtMonthlyEnd); pnlEndM.Controls.Add(chkMonthlyHasEnd);
            formMonthly.Controls.Add(new Label { Text = "تاریخ پایان:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f) }, 2, 1);
            formMonthly.Controls.Add(pnlEndM, 3, 1);

            UIHelper.AddField(formMonthly, "حقوق پایه:", txtMonthlyBase, 2, 0, 1);
            UIHelper.AddField(formMonthly, "پایه سنوات:", txtMonthlyYears, 2, 2, 3);
            UIHelper.AddField(formMonthly, "حق جذب:", txtMonthlyAttr, 3, 0, 1);
            UIHelper.AddField(formMonthly, "حق مسئولیت:", txtMonthlyResp, 3, 2, 3);
            UIHelper.AddField(formMonthly, "حق مدیریت:", txtMonthlyMgmt, 4, 0, 1);
            UIHelper.AddField(formMonthly, "حق فنی:", txtMonthlyTech, 4, 2, 3);
            UIHelper.AddField(formMonthly, "حق ایاب و ذهاب:", txtMonthlyTransport, 5, 0, 1);
            UIHelper.AddField(formMonthly, "سایر مزایا:", txtMonthlyOther, 5, 2, 3);

            formMonthly.Controls.Add(chkMonthlyIns, 0, 6);
            formMonthly.SetColumnSpan(chkMonthlyIns, 2);

            layoutMonthly.Controls.Add(CreateBottomPanel(formMonthly, out btnSaveMonthly, out btnNewMonthly, out btnDelMonthly, out btnPrintMonthly), 0, 1);

            btnSaveMonthly.Click += async (s, e) => await SaveMonthlyContractAsync();
            btnNewMonthly.Click += (s, e) => ClearMonthlyForm();
            btnDelMonthly.Click += async (s, e) => await DeleteContractAsync(dgvMonthly);
            btnPrintMonthly.Click += (s, e) => { using var f = new ContractPrintForm(_employee); f.ShowDialog(this); };
            dgvMonthly.CellDoubleClick += (s, e) => LoadMonthlyContractForEdit(e.RowIndex);
            dgvMonthly.SelectionChanged += (s, e) => btnDelMonthly.Enabled = dgvMonthly.CurrentRow != null;

            pageMonthly.Controls.Add(layoutMonthly);

            // ==========================================
            // تب ۲: روزمزد
            // ==========================================
            var pageDaily = new TabPage("قرارداد روزمزد") { Name = "tabDaily", BackColor = UIHelper.WarmBackground, Padding = new Padding(10) };
            var layoutDaily = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layoutDaily.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
            layoutDaily.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));

            dgvDaily = CreateGrid();
            layoutDaily.Controls.Add(CreateGridPanel(dgvDaily, "سوابق قراردادهای روزمزد (دابل کلیک برای ویرایش):"), 0, 0);

            var formDaily = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3, Padding = new Padding(0, 10, 0, 10) };
            formDaily.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            formDaily.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            formDaily.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            formDaily.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            for (int i = 0; i < 3; i++) formDaily.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));

            txtDailyContractNum = new TextBox(); txtDailyPosition = new TextBox();
            txtDailyStart = new TextBox(); DateHelper.ApplyDateMask(txtDailyStart);
            txtDailyEnd = new TextBox(); DateHelper.ApplyDateMask(txtDailyEnd);
            cmbDailyCategory = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            chkDailyHasEnd = new CheckBox { Text = "دارای تاریخ پایان", Checked = true };
            chkDailyHasEnd.CheckedChanged += (s, e) => txtDailyEnd.Enabled = chkDailyHasEnd.Checked;
            chkDailyIns = new CheckBox { Text = "بیمه دارد", Checked = true };

            UIHelper.AddField(formDaily, "شماره قرارداد:", txtDailyContractNum, 0, 0, 1);
            UIHelper.AddField(formDaily, "سمت:", txtDailyPosition, 0, 2, 3);
            UIHelper.AddField(formDaily, "تاریخ شروع:", txtDailyStart, 1, 0, 1);

            var pnlEndD = new Panel { Dock = DockStyle.Fill };
            txtDailyEnd.Location = new Point(0, 5); txtDailyEnd.Width = 150;
            chkDailyHasEnd.Location = new Point(160, 8);
            pnlEndD.Controls.Add(txtDailyEnd); pnlEndD.Controls.Add(chkDailyHasEnd);
            formDaily.Controls.Add(new Label { Text = "تاریخ پایان:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f) }, 2, 1);
            formDaily.Controls.Add(pnlEndD, 3, 1);

            UIHelper.AddField(formDaily, "دسته کارگر:", cmbDailyCategory, 2, 0, 3);
            formDaily.SetColumnSpan(cmbDailyCategory, 3);
            formDaily.Controls.Add(chkDailyIns, 0, 2);

            layoutDaily.Controls.Add(CreateBottomPanel(formDaily, out btnSaveDaily, out btnNewDaily, out btnDelDaily, out btnPrintDaily), 0, 1);

            btnSaveDaily.Click += async (s, e) => await SaveDailyContractAsync();
            btnNewDaily.Click += (s, e) => ClearDailyForm();
            btnDelDaily.Click += async (s, e) => await DeleteContractAsync(dgvDaily);
            btnPrintDaily.Click += (s, e) => { using var f = new ContractPrintForm(_employee); f.ShowDialog(this); };
            dgvDaily.CellDoubleClick += (s, e) => LoadDailyContractForEdit(e.RowIndex);
            dgvDaily.SelectionChanged += (s, e) => btnDelDaily.Enabled = dgvDaily.CurrentRow != null;

            pageDaily.Controls.Add(layoutDaily);

            // ==========================================
            // تب ۳: ساعتی
            // ==========================================
            var pageHourly = new TabPage("قرارداد ساعتی") { Name = "tabHourly", BackColor = UIHelper.WarmBackground, Padding = new Padding(10) };
            var layoutHourly = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layoutHourly.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
            layoutHourly.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));

            dgvHourly = CreateGrid();
            layoutHourly.Controls.Add(CreateGridPanel(dgvHourly, "سوابق قراردادهای ساعتی (دابل کلیک برای ویرایش):"), 0, 0);

            var formHourly = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3, Padding = new Padding(0, 10, 0, 10) };
            formHourly.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            formHourly.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            formHourly.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
            formHourly.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            for (int i = 0; i < 3; i++) formHourly.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));

            txtHourlyContractNum = new TextBox(); txtHourlyPosition = new TextBox();
            txtHourlyStart = new TextBox(); DateHelper.ApplyDateMask(txtHourlyStart);
            txtHourlyEnd = new TextBox(); DateHelper.ApplyDateMask(txtHourlyEnd);
            txtHourlyRate = new TextBox(); SmartInputHelper.EnableSmartAmountTextBox(txtHourlyRate);
            chkHourlyHasEnd = new CheckBox { Text = "دارای تاریخ پایان", Checked = true };
            chkHourlyHasEnd.CheckedChanged += (s, e) => txtHourlyEnd.Enabled = chkHourlyHasEnd.Checked;
            chkHourlyIns = new CheckBox { Text = "بیمه دارد", Checked = true };

            UIHelper.AddField(formHourly, "شماره قرارداد:", txtHourlyContractNum, 0, 0, 1);
            UIHelper.AddField(formHourly, "سمت:", txtHourlyPosition, 0, 2, 3);
            UIHelper.AddField(formHourly, "تاریخ شروع:", txtHourlyStart, 1, 0, 1);

            var pnlEndH = new Panel { Dock = DockStyle.Fill };
            txtHourlyEnd.Location = new Point(0, 5); txtHourlyEnd.Width = 150;
            chkHourlyHasEnd.Location = new Point(160, 8);
            pnlEndH.Controls.Add(txtHourlyEnd); pnlEndH.Controls.Add(chkHourlyHasEnd);
            formHourly.Controls.Add(new Label { Text = "تاریخ پایان:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UIHelper.PersianFont(11f) }, 2, 1);
            formHourly.Controls.Add(pnlEndH, 3, 1);

            UIHelper.AddField(formHourly, "حقوق ساعتی:", txtHourlyRate, 2, 0, 3);
            formHourly.SetColumnSpan(txtHourlyRate, 3);
            formHourly.Controls.Add(chkHourlyIns, 0, 2);

            layoutHourly.Controls.Add(CreateBottomPanel(formHourly, out btnSaveHourly, out btnNewHourly, out btnDelHourly, out btnPrintHourly), 0, 1);

            btnSaveHourly.Click += async (s, e) => await SaveHourlyContractAsync();
            btnNewHourly.Click += (s, e) => ClearHourlyForm();
            btnDelHourly.Click += async (s, e) => await DeleteContractAsync(dgvHourly);
            btnPrintHourly.Click += (s, e) => { using var f = new ContractPrintForm(_employee); f.ShowDialog(this); };
            dgvHourly.CellDoubleClick += (s, e) => LoadHourlyContractForEdit(e.RowIndex);
            dgvHourly.SelectionChanged += (s, e) => btnDelHourly.Enabled = dgvHourly.CurrentRow != null;

            pageHourly.Controls.Add(layoutHourly);

            tabControl.TabPages.Add(pageMonthly);
            tabControl.TabPages.Add(pageDaily);
            tabControl.TabPages.Add(pageHourly);

            this.Controls.Add(tabControl);
            this.Controls.Add(topPanel); // ✅ اضافه شدن پنل دکمه آرشیو
        }

        private DataGridView CreateGrid()
        {
            var dgv = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false };
            dgv.ApplyModernStyle();
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Id", Visible = false });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ContractNumber", HeaderText = "شماره قرارداد", FillWeight = 15 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StartDate", HeaderText = "تاریخ شروع", FillWeight = 15 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EndDate", HeaderText = "تاریخ پایان", FillWeight = 15 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Position", HeaderText = "سمت", FillWeight = 15 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "وضعیت", FillWeight = 10 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DisplaySalary", HeaderText = "حقوق/دستمزد", FillWeight = 15 });
            dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (dgv.Columns[e.ColumnIndex].DataPropertyName == "StartDate" || dgv.Columns[e.ColumnIndex].DataPropertyName == "EndDate")
                {
                    if (e.Value is DateTime dt) { e.Value = DateHelper.ToPersianDate(dt); e.FormattingApplied = true; }
                    else { e.Value = "---"; e.FormattingApplied = true; }
                }
                else if (dgv.Columns[e.ColumnIndex].DataPropertyName == "DisplaySalary" && e.Value is decimal dec)
                {
                    e.Value = NumberHelper.FormatThousands(dec, usePersianDigits: true); e.FormattingApplied = true;
                }
            };
            return dgv;
        }

        private Panel CreateGridPanel(DataGridView dgv, string title)
        {
            var pnl = new Panel { Dock = DockStyle.Fill };
            pnl.Controls.Add(dgv);
            pnl.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Font = UIHelper.PersianFont(11f, FontStyle.Bold), Height = 25 });
            return pnl;
        }

        private Panel CreateBottomPanel(TableLayoutPanel formTable, out Button btnSave, out Button btnNew, out Button btnDel, out Button btnPrint)
        {
            var pnl = new Panel { Dock = DockStyle.Fill };

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
            btnPrint = UIHelper.CreateButton("🖨️ چاپ", UIHelper.SectionPurple, 120, 45);
            btnNew = UIHelper.CreateButton("📄 جدید", UIHelper.SectionGray, 120, 45);
            btnDel = UIHelper.CreateButton("🗑️ حذف", UIHelper.ActionRed, 120, 45);
            btnDel.Enabled = false;
            btnSave = UIHelper.CreateButton("💾 ثبت قرارداد", UIHelper.ActionGreen, 180, 45);
            btnPanel.Controls.Add(btnPrint); btnPanel.Controls.Add(btnNew); btnPanel.Controls.Add(btnDel); btnPanel.Controls.Add(btnSave);

            var lblNew = new Label { Text = "اطلاعات قرارداد جدید:", Dock = DockStyle.Top, Font = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.SectionBlue, Height = 25 };

            pnl.Controls.Add(btnPanel);
            pnl.Controls.Add(formTable);
            pnl.Controls.Add(lblNew);
            return pnl;
        }

        private async Task LoadAllContractsAsync()
        {
            var loadingResult = await AsyncHelper.RunWithLoading(this, () => _repository.GetContractsByEmployeeIdAsync(_employee.Id), "در حال بارگذاری...");

            if (!loadingResult.Success || loadingResult.Result == null) return;

            var contracts = loadingResult.Result;

            var monthlyList = contracts.Where(c =>
            {
                string t = (c.PersonnelType ?? "ماهانه").Trim();
                return t != "روزمزد" && t != "ساعتی";
            }).ToList();
            var dailyList = contracts.Where(c => (c.PersonnelType ?? "").Trim() == "روزمزد").ToList();
            var hourlyList = contracts.Where(c => (c.PersonnelType ?? "").Trim() == "ساعتی").ToList();

            foreach (var c in monthlyList) c.DisplaySalary = c.BaseSalary;
            foreach (var c in dailyList) c.DisplaySalary = c.DailyWage;
            foreach (var c in hourlyList) c.DisplaySalary = c.HourlyWage;

            dgvMonthly.DataSource = monthlyList;
            dgvDaily.DataSource = dailyList;
            dgvHourly.DataSource = hourlyList;
        }

        private void LoadMonthlyContractForEdit(int rowIndex)
        {
            if (rowIndex < 0 || dgvMonthly.Rows[rowIndex].DataBoundItem is not EmployeeContract c) return;
            _editMonthlyId = c.Id;
            txtMonthlyContractNum.Text = c.ContractNumber;
            txtMonthlyPosition.Text = c.Position;
            txtMonthlyStart.Text = DateHelper.ToPersianDate(c.StartDate);
            if (c.EndDate.HasValue) { chkMonthlyHasEnd.Checked = true; txtMonthlyEnd.Text = DateHelper.ToPersianDate(c.EndDate.Value); }
            else { chkMonthlyHasEnd.Checked = false; txtMonthlyEnd.Text = ""; }
            chkMonthlyIns.Checked = c.HasInsurance;

            txtMonthlyBase.Text = NumberHelper.FormatThousands(c.BaseSalary);
            txtMonthlyYears.Text = NumberHelper.FormatThousands(c.YearsBonus);
            txtMonthlyAttr.Text = NumberHelper.FormatThousands(c.AttractionAllowance);
            txtMonthlyResp.Text = NumberHelper.FormatThousands(c.ResponsibilityAllowance);
            txtMonthlyMgmt.Text = NumberHelper.FormatThousands(c.ManagementAllowance);
            txtMonthlyTech.Text = NumberHelper.FormatThousands(c.TechnicalAllowance);
            txtMonthlyTransport.Text = NumberHelper.FormatThousands(c.TransportationAllowance);
            txtMonthlyOther.Text = NumberHelper.FormatThousands(c.OtherAllowances);

            btnSaveMonthly.Text = "💾 ذخیره ویرایش"; btnSaveMonthly.BackColor = UIHelper.SectionBlue;
            txtMonthlyContractNum.Focus();
        }

        private void ClearMonthlyForm()
        {
            _editMonthlyId = null;
            txtMonthlyContractNum.Text = $"CR-{_employee.PersonnelCode}-{DateTime.Now:yyyyMMdd}";
            txtMonthlyPosition.Text = _employee.Position ?? "";
            txtMonthlyStart.Text = DateHelper.ToPersianDate(DateTime.Now);
            txtMonthlyEnd.Text = DateHelper.ToPersianDate(DateTime.Now.AddYears(1));
            chkMonthlyHasEnd.Checked = true; txtMonthlyEnd.Enabled = true; chkMonthlyIns.Checked = _employee.HasInsurance;

            txtMonthlyBase.Text = "0"; txtMonthlyYears.Text = "0"; txtMonthlyAttr.Text = "0"; txtMonthlyResp.Text = "0";
            txtMonthlyMgmt.Text = "0"; txtMonthlyTech.Text = "0"; txtMonthlyTransport.Text = "0"; txtMonthlyOther.Text = "0";

            btnSaveMonthly.Text = "➕ ثبت قرارداد جدید"; btnSaveMonthly.BackColor = UIHelper.ActionGreen;
        }

        private async Task SaveMonthlyContractAsync()
        {
            if (_isSaving) return;
            if (string.IsNullOrWhiteSpace(txtMonthlyContractNum.Text)) { ModernMessageBox.ShowWarning("شماره قرارداد الزامی است."); return; }

            DateTime? start = DateHelper.ParsePersianDate(txtMonthlyStart.Text);
            if (start == null) { ModernMessageBox.ShowWarning("تاریخ شروع نامعتبر است."); return; }

            DateTime? end = null;
            if (chkMonthlyHasEnd.Checked)
            {
                if (string.IsNullOrWhiteSpace(txtMonthlyEnd.Text))
                {
                    ModernMessageBox.ShowWarning("تاریخ پایان را وارد کنید یا تیک «دارای تاریخ پایان» را بردارید.");
                    return;
                }
                end = DateHelper.ParsePersianDate(txtMonthlyEnd.Text);
                if (end == null) { ModernMessageBox.ShowWarning("تاریخ پایان نامعتبر است."); return; }
            }

            int? editSnapshot = _editMonthlyId;

            _isSaving = true;
            try
            {
                var contract = new EmployeeContract
                {
                    Id = editSnapshot ?? 0,
                    EmployeeId = _employee.Id,
                    ContractNumber = txtMonthlyContractNum.Text.Trim(),
                    StartDate = start.Value,
                    EndDate = end,
                    Position = txtMonthlyPosition.Text.Trim(),
                    PersonnelType = _employee.PersonnelType ?? "ماهانه",
                    HasInsurance = chkMonthlyIns.Checked,
                    BaseSalary = NumberHelper.ParseFormatted(txtMonthlyBase.Text),
                    YearsBonus = NumberHelper.ParseFormatted(txtMonthlyYears.Text),
                    AttractionAllowance = NumberHelper.ParseFormatted(txtMonthlyAttr.Text),
                    ResponsibilityAllowance = NumberHelper.ParseFormatted(txtMonthlyResp.Text),
                    ManagementAllowance = NumberHelper.ParseFormatted(txtMonthlyMgmt.Text),
                    TechnicalAllowance = NumberHelper.ParseFormatted(txtMonthlyTech.Text),
                    TransportationAllowance = NumberHelper.ParseFormatted(txtMonthlyTransport.Text),
                    OtherAllowances = NumberHelper.ParseFormatted(txtMonthlyOther.Text)
                };

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    if (editSnapshot.HasValue) await _repository.UpdateContractAsync(contract);
                    else await _repository.AddNewContractAsync(contract);
                }, "در حال ذخیره...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("قرارداد ذخیره شد.", true);
                    var updatedEmp = await Task.Run(() => new EmployeeRepository(_connectionString).GetById(_employee.Id));
                    if (updatedEmp != null) _employee = updatedEmp;
                    await LoadAllContractsAsync();
                    if (!IsDisposed) ClearMonthlyForm();
                }
            }
            finally
            {
                _isSaving = false;
            }
        }

        private void LoadDailyContractForEdit(int rowIndex)
        {
            if (rowIndex < 0 || dgvDaily.Rows[rowIndex].DataBoundItem is not EmployeeContract c) return;
            _editDailyId = c.Id;
            txtDailyContractNum.Text = c.ContractNumber;
            txtDailyPosition.Text = c.Position;
            txtDailyStart.Text = DateHelper.ToPersianDate(c.StartDate);
            if (c.EndDate.HasValue) { chkDailyHasEnd.Checked = true; txtDailyEnd.Text = DateHelper.ToPersianDate(c.EndDate.Value); }
            else { chkDailyHasEnd.Checked = false; txtDailyEnd.Text = ""; }
            chkDailyIns.Checked = c.HasInsurance;
            cmbDailyCategory.Text = c.DailyWageCategory;

            btnSaveDaily.Text = "💾 ذخیره ویرایش"; btnSaveDaily.BackColor = UIHelper.SectionBlue;
            txtDailyContractNum.Focus();
        }

        private void ClearDailyForm()
        {
            _editDailyId = null;
            txtDailyContractNum.Text = $"CR-{_employee.PersonnelCode}-{DateTime.Now:yyyyMMdd}";
            txtDailyPosition.Text = _employee.Position ?? "";
            txtDailyStart.Text = DateHelper.ToPersianDate(DateTime.Now);
            txtDailyEnd.Text = DateHelper.ToPersianDate(DateTime.Now.AddYears(1));
            chkDailyHasEnd.Checked = true; txtDailyEnd.Enabled = true; chkDailyIns.Checked = _employee.HasInsurance;
            cmbDailyCategory.SelectedIndex = -1;
            btnSaveDaily.Text = "➕ ثبت قرارداد جدید"; btnSaveDaily.BackColor = UIHelper.ActionGreen;
        }

        private async Task SaveDailyContractAsync()
        {
            if (_isSaving) return;
            if (string.IsNullOrWhiteSpace(txtDailyContractNum.Text)) { ModernMessageBox.ShowWarning("شماره قرارداد الزامی است."); return; }
            if (string.IsNullOrWhiteSpace(cmbDailyCategory.Text)) { ModernMessageBox.ShowWarning("انتخاب دسته کارگر الزامی است."); return; }

            DateTime? start = DateHelper.ParsePersianDate(txtDailyStart.Text);
            if (start == null) { ModernMessageBox.ShowWarning("تاریخ شروع نامعتبر است."); return; }

            DateTime? end = null;
            if (chkDailyHasEnd.Checked)
            {
                if (string.IsNullOrWhiteSpace(txtDailyEnd.Text))
                {
                    ModernMessageBox.ShowWarning("تاریخ پایان را وارد کنید یا تیک «دارای تاریخ پایان» را بردارید.");
                    return;
                }
                end = DateHelper.ParsePersianDate(txtDailyEnd.Text);
                if (end == null) { ModernMessageBox.ShowWarning("تاریخ پایان نامعتبر است."); return; }
            }

            int? editSnapshot = _editDailyId;

            _isSaving = true;
            try
            {
                var contract = new EmployeeContract
                {
                    Id = editSnapshot ?? 0,
                    EmployeeId = _employee.Id,
                    ContractNumber = txtDailyContractNum.Text.Trim(),
                    StartDate = start.Value,
                    EndDate = end,
                    Position = txtDailyPosition.Text.Trim(),
                    PersonnelType = "روزمزد",
                    HasInsurance = chkDailyIns.Checked,
                    DailyWageCategory = cmbDailyCategory.Text
                };

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    if (editSnapshot.HasValue) await _repository.UpdateContractAsync(contract);
                    else await _repository.AddNewContractAsync(contract);
                }, "در حال ذخیره...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("قرارداد ذخیره شد.", true);
                    var updatedEmp = await Task.Run(() => new EmployeeRepository(_connectionString).GetById(_employee.Id));
                    if (updatedEmp != null) _employee = updatedEmp;
                    await LoadAllContractsAsync();
                    if (!IsDisposed) ClearDailyForm();
                }
            }
            finally
            {
                _isSaving = false;
            }
        }

        private void LoadHourlyContractForEdit(int rowIndex)
        {
            if (rowIndex < 0 || dgvHourly.Rows[rowIndex].DataBoundItem is not EmployeeContract c) return;
            _editHourlyId = c.Id;
            txtHourlyContractNum.Text = c.ContractNumber;
            txtHourlyPosition.Text = c.Position;
            txtHourlyStart.Text = DateHelper.ToPersianDate(c.StartDate);
            if (c.EndDate.HasValue) { chkHourlyHasEnd.Checked = true; txtHourlyEnd.Text = DateHelper.ToPersianDate(c.EndDate.Value); }
            else { chkHourlyHasEnd.Checked = false; txtHourlyEnd.Text = ""; }
            chkHourlyIns.Checked = c.HasInsurance;
            txtHourlyRate.Text = NumberHelper.FormatThousands(c.HourlyWage);

            btnSaveHourly.Text = "💾 ذخیره ویرایش"; btnSaveHourly.BackColor = UIHelper.SectionBlue;
            txtHourlyContractNum.Focus();
        }

        private void ClearHourlyForm()
        {
            _editHourlyId = null;
            txtHourlyContractNum.Text = $"CR-{_employee.PersonnelCode}-{DateTime.Now:yyyyMMdd}";
            txtHourlyPosition.Text = _employee.Position ?? "";
            txtHourlyStart.Text = DateHelper.ToPersianDate(DateTime.Now);
            txtHourlyEnd.Text = DateHelper.ToPersianDate(DateTime.Now.AddYears(1));
            chkHourlyHasEnd.Checked = true; txtHourlyEnd.Enabled = true; chkHourlyIns.Checked = _employee.HasInsurance;
            txtHourlyRate.Text = "0";
            btnSaveHourly.Text = "➕ ثبت قرارداد جدید"; btnSaveHourly.BackColor = UIHelper.ActionGreen;
        }

        private async Task SaveHourlyContractAsync()
        {
            if (_isSaving) return;
            if (string.IsNullOrWhiteSpace(txtHourlyContractNum.Text)) { ModernMessageBox.ShowWarning("شماره قرارداد الزامی است."); return; }

            DateTime? start = DateHelper.ParsePersianDate(txtHourlyStart.Text);
            if (start == null) { ModernMessageBox.ShowWarning("تاریخ شروع نامعتبر است."); return; }

            DateTime? end = null;
            if (chkHourlyHasEnd.Checked)
            {
                if (string.IsNullOrWhiteSpace(txtHourlyEnd.Text))
                {
                    ModernMessageBox.ShowWarning("تاریخ پایان را وارد کنید یا تیک «دارای تاریخ پایان» را بردارید.");
                    return;
                }
                end = DateHelper.ParsePersianDate(txtHourlyEnd.Text);
                if (end == null) { ModernMessageBox.ShowWarning("تاریخ پایان نامعتبر است."); return; }
            }

            decimal rate = NumberHelper.ParseFormatted(txtHourlyRate.Text);
            if (rate <= 0) { ModernMessageBox.ShowWarning("حقوق ساعتی الزامی است."); return; }

            int? editSnapshot = _editHourlyId;

            _isSaving = true;
            try
            {
                var contract = new EmployeeContract
                {
                    Id = editSnapshot ?? 0,
                    EmployeeId = _employee.Id,
                    ContractNumber = txtHourlyContractNum.Text.Trim(),
                    StartDate = start.Value,
                    EndDate = end,
                    Position = txtHourlyPosition.Text.Trim(),
                    PersonnelType = "ساعتی",
                    HasInsurance = chkHourlyIns.Checked,
                    HourlyWage = rate
                };

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    if (editSnapshot.HasValue) await _repository.UpdateContractAsync(contract);
                    else await _repository.AddNewContractAsync(contract);
                }, "در حال ذخیره...");

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("قرارداد ذخیره شد.", true);
                    var updatedEmp = await Task.Run(() => new EmployeeRepository(_connectionString).GetById(_employee.Id));
                    if (updatedEmp != null) _employee = updatedEmp;
                    await LoadAllContractsAsync();
                    if (!IsDisposed) ClearHourlyForm();
                }
            }
            finally
            {
                _isSaving = false;
            }
        }

        private async Task DeleteContractAsync(DataGridView dgv)
        {
            if (dgv.CurrentRow?.DataBoundItem is not EmployeeContract c) return;
            if (!ModernMessageBox.AskConfirm($"حذف قرارداد {c.ContractNumber}؟", "تایید")) return;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                await Task.Run(() => _repository.Delete(c.Id));
            }, "در حال حذف...");

            if (success && !IsDisposed)
            {
                ToastHelper.Show("حذف شد.", true);
                await LoadAllContractsAsync();
            }
        }
    }
}