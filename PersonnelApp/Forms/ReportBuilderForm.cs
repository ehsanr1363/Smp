using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using PersonnelApp.Data;
using PersonnelApp.Models;
using PersonnelApp.Services;
using PersonnelApp.Helpers;

namespace PersonnelApp.Forms
{
    public partial class ReportBuilderForm : Form
    {
        private readonly EmployeeService? _employeeService;
        private readonly DependentService? _dependentService;
        private readonly EmployeeBankAccountRepository _bankAccountRepo;
        private readonly List<Employee> _employees;

        private readonly List<ReportField> _allFields = new();
        private readonly List<ReportField> _selectedFields = new();

        private TreeView treeFields = null!;
        private DataGridView dgvPreview = null!;
        private Label lblStatus = null!;
        private Label lblPreviewTitle = null!;
        private TextBox txtReportTitle = null!;
        private Button btnGenerate = null!, btnExcel = null!, btnPrint = null!, btnCancel = null!, btnSelectAll = null!, btnClearAll = null!;
        private ToolTip _treeToolTip = null!;

        private ComboBox cmbStatusFilter = null!, cmbTypeFilter = null!;
        private Button btnApplyFilter = null!;

        private const int LandscapeThreshold = 8;

        public ReportBuilderForm(EmployeeService? employeeService, DependentService? dependentService, List<Employee>? employees = null)
        {
            _employeeService = employeeService;
            _dependentService = dependentService;
            _bankAccountRepo = new EmployeeBankAccountRepository(AppConfig.ConnectionString);

            _employees = employees ?? new List<Employee>();

            BuildFields();
            BuildUI();
            LoadTree();
            UpdateStatus();

            ShortcutHelper.ApplyStandardShortcuts(this);

            FormClosed += (s, e) => _treeToolTip?.Dispose();

            if (employees == null && employeeService != null)
            {
                Shown += async (s, e) => await LoadEmployeesSafeAsync();
            }
        }

        private async Task LoadEmployeesSafeAsync()
        {
            if (_employeeService == null) return;

            await AsyncHelper.RunWithLoading(this, async () =>
            {
                var list = await _employeeService.GetEmployeesWithDetailsAsync("", true);
                _employees.Clear();
                _employees.AddRange(list);
            }, "در حال بارگذاری لیست پرسنل...");
        }

        private void BuildFields()
        {
            _allFields.Add(new ReportField("اطلاعات فردی", "کد پرسنلی", e => e.IsEmployeeRow ? e.Employee.PersonnelCode : ""));
            _allFields.Add(new ReportField("اطلاعات فردی", "نام", e => e.IsEmployeeRow ? e.Employee.FirstName : ""));
            _allFields.Add(new ReportField("اطلاعات فردی", "نام خانوادگی", e => e.IsEmployeeRow ? e.Employee.LastName : ""));
            _allFields.Add(new ReportField("اطلاعات فردی", "نام پدر", e => e.IsEmployeeRow ? e.Employee.FatherName : ""));
            _allFields.Add(new ReportField("اطلاعات فردی", "کد ملی", e => e.IsEmployeeRow ? e.Employee.NationalCode : ""));
            _allFields.Add(new ReportField("اطلاعات فردی", "شماره شناسنامه", e => e.IsEmployeeRow ? e.Employee.BirthCertificateNumber : ""));
            _allFields.Add(new ReportField("اطلاعات فردی", "تاریخ تولد", e => e.IsEmployeeRow ? DateHelper.ToPersianDate(e.Employee.BirthDate) : ""));
            _allFields.Add(new ReportField("اطلاعات فردی", "محل تولد", e => e.IsEmployeeRow ? e.Employee.BirthPlace : ""));
            _allFields.Add(new ReportField("اطلاعات فردی", "محل صدور", e => e.IsEmployeeRow ? e.Employee.IssuancePlace : ""));
            _allFields.Add(new ReportField("اطلاعات فردی", "جنسیت", e => e.IsEmployeeRow ? StringHelper.GenderToPersian(e.Employee.Gender) : ""));
            _allFields.Add(new ReportField("اطلاعات فردی", "وضعیت تأهل", e => e.IsEmployeeRow ? e.Employee.MaritalStatus : ""));
            _allFields.Add(new ReportField("اطلاعات فردی", "نظام وظیفه", e => e.IsEmployeeRow ? e.Employee.MilitaryStatus : ""));
            _allFields.Add(new ReportField("اطلاعات فردی", "تحصیلات", e => e.IsEmployeeRow ? e.Employee.Education : ""));
            _allFields.Add(new ReportField("اطلاعات فردی", "رشته تخصصی", e => e.IsEmployeeRow ? e.Employee.SpecializedField : ""));
            _allFields.Add(new ReportField("اطلاعات فردی", "تعداد فرزند", e => e.IsEmployeeRow ? (object)e.Employee.ChildrenCount : ""));
            _allFields.Add(new ReportField("اطلاعات فردی", "شماره بیمه", e => e.IsEmployeeRow ? e.Employee.InsuranceNumber : ""));
            _allFields.Add(new ReportField("اطلاعات فردی", "تلفن", e => e.IsEmployeeRow ? e.Employee.PhoneNumber : ""));
            _allFields.Add(new ReportField("اطلاعات فردی", "آدرس", e => e.IsEmployeeRow ? e.Employee.Address : ""));
            _allFields.Add(new ReportField("اطلاعات فردی", "فعال", e => e.IsEmployeeRow ? (e.Employee.IsActive ? "بله" : "خیر") : ""));

            _allFields.Add(new ReportField("اطلاعات شغلی", "شرکت", e => e.IsEmployeeRow ? e.Employee.CompanyTitle : ""));
            _allFields.Add(new ReportField("اطلاعات شغلی", "واحد سازمانی", e => e.IsEmployeeRow ? e.Employee.DepartmentName : ""));
            _allFields.Add(new ReportField("اطلاعات شغلی", "سمت", e => e.IsEmployeeRow ? e.Employee.Position : ""));
            _allFields.Add(new ReportField("اطلاعات شغلی", "نوع پرسنلی", e => e.IsEmployeeRow ? e.Employee.PersonnelType : ""));
            _allFields.Add(new ReportField("اطلاعات شغلی", "تاریخ استخدام", e => e.IsEmployeeRow ? DateHelper.ToPersianDate(e.Employee.HireDate) : ""));

            _allFields.Add(new ReportField("قرارداد", "شماره قرارداد", e => e.IsEmployeeRow ? e.Employee.ContractNumber : ""));
            _allFields.Add(new ReportField("قرارداد", "سال قرارداد", e => e.IsEmployeeRow ? (object)e.Employee.ContractYear : ""));
            _allFields.Add(new ReportField("قرارداد", "شروع قرارداد", e => e.IsEmployeeRow ? DateHelper.ToPersianDate(e.Employee.ContractStartDate) : ""));
            _allFields.Add(new ReportField("قرارداد", "پایان قرارداد", e => e.IsEmployeeRow ? DateHelper.ToPersianDate(e.Employee.ContractEndDate) : ""));

            _allFields.Add(new ReportField("حقوق و مزایا", "حقوق مبنا", e => e.IsEmployeeRow ? (object)e.Employee.BaseSalary : "", null, true));
            _allFields.Add(new ReportField("حقوق و مزایا", "پایه سنوات", e => e.IsEmployeeRow ? (object)e.Employee.YearsBonus : "", null, true));
            _allFields.Add(new ReportField("حقوق و مزایا", "حق جذب", e => e.IsEmployeeRow ? (object)e.Employee.AttractionAllowance : "", null, true));
            _allFields.Add(new ReportField("حقوق و مزایا", "حق مسئولیت", e => e.IsEmployeeRow ? (object)e.Employee.ResponsibilityAllowance : "", null, true));
            _allFields.Add(new ReportField("حقوق و مزایا", "حق مسکن", e => e.IsEmployeeRow ? (object)e.Employee.HousingAllowance : "", null, true));
            _allFields.Add(new ReportField("حقوق و مزایا", "حق خواربار", e => e.IsEmployeeRow ? (object)e.Employee.GroceryAllowance : "", null, true));
            _allFields.Add(new ReportField("حقوق و مزایا", "حق اولاد", e => e.IsEmployeeRow ? (object)e.Employee.ChildAllowance : "", null, true));
            _allFields.Add(new ReportField("حقوق و مزایا", "حق تأهل", e => e.IsEmployeeRow ? (object)e.Employee.MarriageAllowance : "", null, true));
            _allFields.Add(new ReportField("حقوق و مزایا", "حق مدیریت", e => e.IsEmployeeRow ? (object)e.Employee.ManagementAllowance : "", null, true));
            _allFields.Add(new ReportField("حقوق و مزایا", "حق فنی", e => e.IsEmployeeRow ? (object)e.Employee.TechnicalAllowance : "", null, true));
            _allFields.Add(new ReportField("حقوق و مزایا", "سایر مزایا", e => e.IsEmployeeRow ? (object)e.Employee.OtherAllowances : "", null, true));

            _allFields.Add(new ReportField("اطلاعات بانکی", "شماره شبا (اصلی)", e => e.IsEmployeeRow ? e.ActiveSheba : ""));
            _allFields.Add(new ReportField("اطلاعات بانکی", "نام بانک (اصلی)", e => e.IsEmployeeRow ? e.ActiveBankName : ""));

            _allFields.Add(new ReportField("افراد تحت تکفل", "نام تحت تکفل", e => !e.IsEmployeeRow && e.Dependent != null ? e.Dependent.FirstName : "", null, false, true));
            _allFields.Add(new ReportField("افراد تحت تکفل", "نام خانوادگی تحت تکفل", e => !e.IsEmployeeRow && e.Dependent != null ? e.Dependent.LastName : "", null, false, true));
            _allFields.Add(new ReportField("افراد تحت تکفل", "کد ملی تحت تکفل", e => !e.IsEmployeeRow && e.Dependent != null ? e.Dependent.NationalCode : "", null, false, true));
            _allFields.Add(new ReportField("افراد تحت تکفل", "شماره شناسنامه تحت تکفل", e => !e.IsEmployeeRow && e.Dependent != null ? e.Dependent.BirthCertificateNumber : "", null, false, true));
            _allFields.Add(new ReportField("افراد تحت تکفل", "نام پدر تحت تکفل", e => !e.IsEmployeeRow && e.Dependent != null ? e.Dependent.FatherName : "", null, false, true));
            _allFields.Add(new ReportField("افراد تحت تکفل", "تاریخ تولد تحت تکفل", e => !e.IsEmployeeRow && e.Dependent != null ? DateHelper.ToPersianDate(e.Dependent.BirthDate) : "", null, false, true));
            _allFields.Add(new ReportField("افراد تحت تکفل", "محل تولد تحت تکفل", e => !e.IsEmployeeRow && e.Dependent != null ? e.Dependent.BirthPlace : "", null, false, true));
            _allFields.Add(new ReportField("افراد تحت تکفل", "جنسیت تحت تکفل", e => !e.IsEmployeeRow && e.Dependent != null ? StringHelper.GenderToPersian(e.Dependent.Gender) : "", null, false, true));
            _allFields.Add(new ReportField("افراد تحت تکفل", "نسبت", e => !e.IsEmployeeRow && e.Dependent != null ? e.Dependent.Relation : "", null, false, true));
            _allFields.Add(new ReportField("افراد تحت تکفل", "بیمه تکمیلی", e => !e.IsEmployeeRow && e.Dependent != null ? (e.Dependent.HasSupplementaryInsurance ? "بله" : "خیر") : "", null, false, true));

            _allFields.Add(new ReportField("لیست یکپارچه", "ردیف", e => e.IsEmployeeRow ? "سرپرست" : "تحت تکفل", null, false, true));
            _allFields.Add(new ReportField("لیست یکپارچه", "نام", e => e.IsEmployeeRow ? e.Employee.FirstName : (e.Dependent?.FirstName ?? ""), null, false, true));
            _allFields.Add(new ReportField("لیست یکپارچه", "نام خانوادگی", e => e.IsEmployeeRow ? e.Employee.LastName : (e.Dependent?.LastName ?? ""), null, false, true));
            _allFields.Add(new ReportField("لیست یکپارچه", "کد ملی", e => e.IsEmployeeRow ? e.Employee.NationalCode : (e.Dependent?.NationalCode ?? ""), null, false, true));
            _allFields.Add(new ReportField("لیست یکپارچه", "شماره شناسنامه", e => e.IsEmployeeRow ? e.Employee.BirthCertificateNumber : (e.Dependent?.BirthCertificateNumber ?? ""), null, false, true));
            _allFields.Add(new ReportField("لیست یکپارچه", "نام پدر", e => e.IsEmployeeRow ? e.Employee.FatherName : (e.Dependent?.FatherName ?? ""), null, false, true));
            _allFields.Add(new ReportField("لیست یکپارچه", "تاریخ تولد", e => e.IsEmployeeRow ? DateHelper.ToPersianDate(e.Employee.BirthDate) : DateHelper.ToPersianDate(e.Dependent?.BirthDate), null, false, true));
            _allFields.Add(new ReportField("لیست یکپارچه", "محل تولد", e => e.IsEmployeeRow ? e.Employee.BirthPlace : (e.Dependent?.BirthPlace ?? ""), null, false, true));
            _allFields.Add(new ReportField("لیست یکپارچه", "جنسیت", e => e.IsEmployeeRow ? StringHelper.GenderToPersian(e.Employee.Gender) : StringHelper.GenderToPersian(e.Dependent?.Gender), null, false, true));
            _allFields.Add(new ReportField("لیست یکپارچه", "نسبت", e => e.IsEmployeeRow ? "---" : (e.Dependent?.Relation ?? ""), null, false, true));
        }

        private void BuildUI()
        {
            UIHelper.SetupForm(this, "گزارش‌ساز پرسنل", new Size(1250, 780));
            this.MinimumSize = new Size(1000, 600);

            _treeToolTip = new ToolTip();

            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(15, 10, 15, 10) };

            lblStatus = new Label { Text = "فیلدها را از راست انتخاب کنید", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            UIHelper.StyleLabel(lblStatus);

            var flowTitle = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent, WrapContents = false, Margin = new Padding(0, 2, 0, 0) };

            var lblTitle = new Label { Text = "عنوان گزارش:", AutoSize = true, Margin = new Padding(0, 7, 5, 0) };
            UIHelper.StyleLabel(lblTitle);

            txtReportTitle = new TextBox { Text = "گزارش پرسنل", Width = 280, BackColor = UIHelper.WarmInputBackground, BorderStyle = BorderStyle.FixedSingle, Font = UIHelper.PersianFont(11f), Margin = new Padding(0, 4, 0, 0) };

            txtReportTitle.TextChanged += (s, e) =>
            {
                if (lblPreviewTitle != null)
                    lblPreviewTitle.Text = string.IsNullOrWhiteSpace(txtReportTitle.Text) ? "گزارش" : txtReportTitle.Text.Trim();
            };

            flowTitle.Controls.Add(lblTitle);
            flowTitle.Controls.Add(txtReportTitle);

            btnSelectAll = UIHelper.CreateButton("✓ انتخاب همه", UIHelper.WarmSecondary, 110, 34);
            btnSelectAll.Click += (s, e) =>
            {
                foreach (TreeNode cat in treeFields.Nodes) { cat.Checked = true; foreach (TreeNode child in cat.Nodes) child.Checked = true; }
                RebuildSelectedFields(); UpdateStatus();
            };

            btnClearAll = UIHelper.CreateButton("✗ پاک کردن", UIHelper.WarmDanger, 110, 34);
            btnClearAll.Click += (s, e) =>
            {
                foreach (TreeNode cat in treeFields.Nodes) { cat.Checked = false; foreach (TreeNode child in cat.Nodes) child.Checked = false; }
                RebuildSelectedFields(); UpdateStatus();
            };

            var flowTop = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, BackColor = Color.Transparent, WrapContents = false, Margin = new Padding(0, 3, 0, 0) };
            flowTop.Controls.Add(btnSelectAll); flowTop.Controls.Add(btnClearAll);

            pnlTop.Controls.Add(lblStatus); pnlTop.Controls.Add(flowTitle); pnlTop.Controls.Add(flowTop);

            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, RightToLeft = RightToLeft.Yes };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            treeFields = new TreeView
            {
                Dock = DockStyle.Fill,
                CheckBoxes = true,
                Font = UIHelper.PersianFont(11f),
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                ShowLines = false,
                ShowPlusMinus = true,
                ShowRootLines = true,
                Indent = 15,
                BorderStyle = BorderStyle.None,
                BackColor = UIHelper.WarmInputBackground,
                ItemHeight = 28,
                HideSelection = false,
                Scrollable = true,
                Margin = new Padding(5)
            };
            treeFields.AfterCheck += TreeFields_AfterCheck; treeFields.MouseMove += TreeFields_MouseMove;

            var pnlTreeContainer = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(5) };
            pnlTreeContainer.Controls.Add(treeFields);
            mainLayout.Controls.Add(pnlTreeContainer, 0, 0);

            var previewLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55f));
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var pnlFilterContainer = UIHelper.CreateWarmPanel();
            pnlFilterContainer.Padding = new Padding(10, 5, 10, 5);
            pnlFilterContainer.Margin = new Padding(5, 5, 5, 0);

            var flowFilter = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };

            var lblStatusFilter = new Label { Text = "وضعیت:", AutoSize = true, Margin = new Padding(0, 7, 5, 0) };
            UIHelper.StyleLabel(lblStatusFilter);

            cmbStatusFilter = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(0, 3, 15, 0), BackColor = UIHelper.WarmInputBackground };
            cmbStatusFilter.Items.AddRange(new object[] { "همه", "فعال", "غیرفعال" });
            cmbStatusFilter.SelectedIndex = 0;

            var lblTypeFilter = new Label { Text = "نوع پرسنلی:", AutoSize = true, Margin = new Padding(0, 7, 5, 0) };
            UIHelper.StyleLabel(lblTypeFilter);

            cmbTypeFilter = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList, Font = UIHelper.PersianFont(11f), Margin = new Padding(0, 3, 15, 0), BackColor = UIHelper.WarmInputBackground };
            cmbTypeFilter.Items.AddRange(new object[] { "همه", "ماهانه", "قراردادی", "ساعتی", "روزمزد", "رسمی", "پیمانی" });
            cmbTypeFilter.SelectedIndex = 0;

            btnApplyFilter = UIHelper.CreateButton("🔄 اعمال فیلتر", UIHelper.WarmPrimary, 110, 30);
            btnApplyFilter.Margin = new Padding(0, 1, 0, 0);
            btnApplyFilter.Click += async (s, e) => await GeneratePreviewAsync();

            flowFilter.Controls.Add(btnApplyFilter);
            flowFilter.Controls.Add(cmbTypeFilter);
            flowFilter.Controls.Add(lblTypeFilter);
            flowFilter.Controls.Add(cmbStatusFilter);
            flowFilter.Controls.Add(lblStatusFilter);

            pnlFilterContainer.Controls.Add(flowFilter);

            lblPreviewTitle = new Label { Text = "گزارش", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = UIHelper.PersianFont(14f, FontStyle.Bold), ForeColor = UIHelper.WarmText, BackColor = UIHelper.WarmPanelBackground, Margin = new Padding(5, 0, 5, 0) };

            dgvPreview = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, Margin = new Padding(5, 0, 5, 5) };

            dgvPreview.ApplyModernStyle();

            dgvPreview.CellFormatting += DgvPreview_CellFormatting;

            previewLayout.Controls.Add(pnlFilterContainer, 0, 0);
            previewLayout.Controls.Add(lblPreviewTitle, 0, 1);
            previewLayout.Controls.Add(dgvPreview, 0, 2);

            var pnlGridContainer = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(5) };
            pnlGridContainer.Controls.Add(previewLayout);
            mainLayout.Controls.Add(pnlGridContainer, 1, 0);

            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 62, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(10) };
            var flowBottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = Color.Transparent, WrapContents = false };

            btnGenerate = UIHelper.CreateButton("🔄  ساخت پیش‌نمایش", UIHelper.WarmPrimary, 170);
            btnGenerate.Click += async (s, e) => await GeneratePreviewAsync();

            btnExcel = UIHelper.CreateButton("📊  خروجی اکسل", UIHelper.WarmSecondary, 150);
            btnExcel.Click += async (s, e) => await ExportExcelAsync();

            btnPrint = UIHelper.CreateButton("🖨️  چاپ", UIHelper.WarmSecondary, 110);
            btnPrint.Click += async (s, e) => await PrintReportAsync();

            btnCancel = UIHelper.CreateButton("انصراف", UIHelper.WarmDanger, 90);
            btnCancel.Click += (s, e) => Close();

            flowBottom.Controls.Add(btnGenerate); flowBottom.Controls.Add(btnExcel); flowBottom.Controls.Add(btnPrint); flowBottom.Controls.Add(btnCancel);
            pnlBottom.Controls.Add(flowBottom);

            this.Controls.Add(mainLayout); this.Controls.Add(pnlBottom); this.Controls.Add(pnlTop);
        }

        private void DgvPreview_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex >= 0 && dgvPreview.Rows[e.RowIndex].DataBoundItem is DataRowView drv)
            {
                if (drv.Row.Table.Columns.Contains("_IsEmployeeRow") && Convert.ToBoolean(drv["_IsEmployeeRow"]))
                {
                    e.CellStyle.BackColor = UIHelper.WarmPanelBackground;
                    e.CellStyle.Font = UIHelper.PersianFont(10f, FontStyle.Bold);
                }
            }
        }

        private void LoadTree()
        {
            treeFields.BeginUpdate(); treeFields.Nodes.Clear();
            foreach (var group in _allFields.GroupBy(f => f.Category))
            {
                var catNode = new TreeNode(group.Key) { Checked = false, NodeFont = UIHelper.PersianFont(11f, FontStyle.Bold), ForeColor = UIHelper.WarmText };
                foreach (var field in group)
                {
                    var child = new TreeNode(" " + field.DisplayName) { Tag = field, Checked = false };
                    catNode.Nodes.Add(child);
                }
                treeFields.Nodes.Add(catNode); catNode.Expand();
            }
            treeFields.EndUpdate();
        }

        private void TreeFields_MouseMove(object? sender, MouseEventArgs e)
        {
            var node = treeFields.GetNodeAt(e.X, e.Y);
            _treeToolTip.SetToolTip(treeFields, node != null ? node.Text.Trim() : "");
        }

        private void TreeFields_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            treeFields.AfterCheck -= TreeFields_AfterCheck;
            try
            {
                if (e.Node.Tag == null) { foreach (TreeNode child in e.Node.Nodes) child.Checked = e.Node.Checked; }
                else if (e.Node.Parent != null) { var parent = e.Node.Parent; bool all = parent.Nodes.Cast<TreeNode>().All(n => n.Checked); parent.Checked = all; }
            }
            finally { treeFields.AfterCheck += TreeFields_AfterCheck; }

            RebuildSelectedFields(); UpdateStatus();
        }

        private void RebuildSelectedFields()
        {
            _selectedFields.Clear();
            foreach (TreeNode cat in treeFields.Nodes) foreach (TreeNode child in cat.Nodes) if (child.Checked && child.Tag is ReportField f) _selectedFields.Add(f);
        }

        private void UpdateStatus()
        {
            int count = _selectedFields.Count;
            string orient = count > LandscapeThreshold ? "🔄 افقی" : "📄 عمودی";
            bool hasDependentFields = _selectedFields.Any(f => f.IsDependentField);
            string type = hasDependentFields ? "گزارش سلسله مراتبی" : "گزارش پرسنل";
            lblStatus.Text = $"فیلد انتخاب‌شده: {NumberHelper.FormatThousands(count, usePersianDigits: true)}    |    نوع: {type}    |    چیدمان چاپ: {orient}";
        }

        private async Task<List<ReportDataItem>> GetReportDataAsync(List<ReportField> fields)
        {
            var filteredEmployees = _employees.AsEnumerable();

            string status = cmbStatusFilter?.SelectedItem?.ToString() ?? "همه";
            if (status == "فعال") filteredEmployees = filteredEmployees.Where(e => e.IsActive);
            else if (status == "غیرفعال") filteredEmployees = filteredEmployees.Where(e => !e.IsActive);

            string type = cmbTypeFilter?.SelectedItem?.ToString() ?? "همه";
            if (type != "همه") filteredEmployees = filteredEmployees.Where(e => e.PersonnelType == type);

            var items = new List<ReportDataItem>();
            bool hasDependentFields = fields.Any(f => f.IsDependentField);

            var allAccountsTask = _bankAccountRepo.GetAllAsync();
            var allDependentsTask = new DependentRepository(AppConfig.ConnectionString).GetAllAsync();

            await Task.WhenAll(allAccountsTask, allDependentsTask);

            var allAccounts = allAccountsTask.Result ?? new List<EmployeeBankAccount>();
            var allDependents = allDependentsTask.Result ?? new List<Dependent>();

            var accountsByEmp = allAccounts.GroupBy(a => a.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
            var depsByEmp = allDependents.GroupBy(d => d.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var emp in filteredEmployees)
            {
                string activeSheba = "";
                string activeBankName = "";

                if (accountsByEmp.TryGetValue(emp.Id, out var accounts))
                {
                    var mainAcc = accounts.FirstOrDefault(a => a.IsMainAccount) ?? accounts.FirstOrDefault();
                    if (mainAcc != null)
                    {
                        activeSheba = mainAcc.ShebaNumber;
                        activeBankName = mainAcc.BankName;
                    }
                }

                items.Add(new ReportDataItem
                {
                    Employee = emp,
                    Dependent = null,
                    IsEmployeeRow = true,
                    ActiveSheba = activeSheba,
                    ActiveBankName = activeBankName
                });

                if (hasDependentFields && depsByEmp.TryGetValue(emp.Id, out var deps))
                {
                    foreach (var dep in deps)
                    {
                        items.Add(new ReportDataItem
                        {
                            Employee = emp,
                            Dependent = dep,
                            IsEmployeeRow = false
                        });
                    }
                }
            }

            return items;
        }

        private async Task GeneratePreviewAsync()
        {
            RebuildSelectedFields();
            if (_selectedFields.Count == 0) { ModernMessageBox.ShowWarning("حداقل یک فیلد انتخاب کنید."); return; }
            if (_employees == null || _employees.Count == 0) { ModernMessageBox.ShowWarning("هیچ کارمندی برای گزارش وجود ندارد."); return; }

            await AsyncHelper.RunWithLoading(this, async () =>
            {
                var items = await GetReportDataAsync(_selectedFields);
                var dt = BuildDataTable(_selectedFields, items);

                dgvPreview.DataSource = dt;
                if (dgvPreview.Columns["_IsEmployeeRow"] != null) dgvPreview.Columns["_IsEmployeeRow"].Visible = false;
                UpdateStatus();
            }, "در حال ساخت پیش‌نمایش...");
        }

        private DataTable BuildDataTable(List<ReportField> fields, List<ReportDataItem> items)
        {
            var dt = new DataTable();
            var seenNames = new HashSet<string>();

            for (int i = 0; i < fields.Count; i++)
            {
                string colName = fields[i].DisplayName;
                int suffix = 1;
                while (seenNames.Contains(colName))
                {
                    colName = $"{fields[i].DisplayName} ({suffix++})";
                }
                seenNames.Add(colName);
                dt.Columns.Add(colName);
            }
            dt.Columns.Add("_IsEmployeeRow", typeof(bool));

            foreach (var item in items)
            {
                var row = dt.NewRow();
                for (int i = 0; i < fields.Count; i++)
                {
                    object value;
                    try { value = fields[i].GetValue(item); } catch { value = null; }

                    if (value is decimal dec)
                        row[i] = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                    else if (value is int iVal)
                        row[i] = NumberHelper.FormatThousands(iVal, usePersianDigits: true);
                    else if (value is long lVal)
                        row[i] = NumberHelper.FormatThousands(lVal, usePersianDigits: true);
                    else
                        row[i] = value?.ToString() ?? "";
                }
                row["_IsEmployeeRow"] = item.IsEmployeeRow;
                dt.Rows.Add(row);
            }
            return dt;
        }

        private async Task<bool> EnsurePreviewUpToDateAsync()
        {
            RebuildSelectedFields();
            if (_selectedFields.Count == 0) { ModernMessageBox.ShowWarning("حداقل یک فیلد انتخاب کنید."); return false; }
            if (_employees == null || _employees.Count == 0) { ModernMessageBox.ShowWarning("هیچ کارمندی برای گزارش وجود ندارد."); return false; }

            bool needsRegen = dgvPreview.DataSource == null || dgvPreview.Columns.Count != _selectedFields.Count + 1;
            if (!needsRegen)
            {
                for (int i = 0; i < _selectedFields.Count; i++)
                {
                    if (dgvPreview.Columns[i].HeaderText != _selectedFields[i].DisplayName) { needsRegen = true; break; }
                }
            }

            if (needsRegen)
            {
                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var items = await GetReportDataAsync(_selectedFields);
                    var dt = BuildDataTable(_selectedFields, items);

                    dgvPreview.DataSource = dt;
                    if (dgvPreview.Columns["_IsEmployeeRow"] != null) dgvPreview.Columns["_IsEmployeeRow"].Visible = false;
                    UpdateStatus();
                }, "در حال ساخت پیش‌نمایش...");

                if (!success) return false;
            }
            return true;
        }

        private async Task ExportExcelAsync()
        {
            if (!await EnsurePreviewUpToDateAsync()) return;

            using var sfd = new SaveFileDialog
            {
                Filter = "Excel File|*.xlsx",
                FileName = $"گزارش_پرسنل_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                Title = "ذخیره گزارش اکسل",
                OverwritePrompt = true
            };

            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            var selectedFieldsCopy = _selectedFields.ToList();

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                var dataItems = await GetReportDataAsync(selectedFieldsCopy);

                await Task.Run(() =>
                {
                    using var wb = new XLWorkbook();
                    var ws = wb.Worksheets.Add("گزارش");
                    int colCount = selectedFieldsCopy.Count;

                    ws.Cell(1, 1).Value = string.IsNullOrWhiteSpace(txtReportTitle.Text) ? "گزارش" : txtReportTitle.Text.Trim();
                    var titleRange = ws.Range(1, 1, 1, Math.Max(1, colCount));
                    titleRange.Merge(); titleRange.Style.Font.Bold = true; titleRange.Style.Font.FontSize = 14; titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell(2, 1).Value = $"تاریخ: {DateHelper.ToPersianDate(DateTime.Now)}  —  تعداد رکورد: {NumberHelper.FormatThousands(dataItems.Count, usePersianDigits: true)}";
                    var subRange = ws.Range(2, 1, 2, Math.Max(1, colCount));
                    subRange.Merge(); subRange.Style.Font.FontSize = 10; subRange.Style.Font.FontColor = XLColor.FromArgb(100, 116, 139); subRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    for (int i = 0; i < colCount; i++)
                    {
                        var cell = ws.Cell(4, i + 1);
                        cell.Value = selectedFieldsCopy[i].DisplayName;
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.FontColor = XLColor.FromArgb(90, 60, 40);
                        cell.Style.Fill.BackgroundColor = XLColor.FromArgb(248, 237, 227);
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin; cell.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    }
                    ws.Row(4).Height = 24;

                    int row = 5; decimal[] totals = new decimal[colCount];

                    foreach (var item in dataItems)
                    {
                        for (int i = 0; i < colCount; i++)
                        {
                            object value;
                            try { value = selectedFieldsCopy[i].GetValue(item); } catch { value = null; }

                            string text = "";
                            if (value is decimal dec) text = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                            else if (value is int iVal) text = NumberHelper.FormatThousands(iVal, usePersianDigits: true);
                            else if (value is long lVal) text = NumberHelper.FormatThousands(lVal, usePersianDigits: true);
                            else text = value?.ToString() ?? "";

                            var cell = ws.Cell(row, i + 1);
                            cell.Value = text; cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin; cell.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                            if (item.IsEmployeeRow)
                            {
                                cell.Style.Font.Bold = true;
                                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(253, 250, 245);
                            }
                            else if (row % 2 == 0)
                            {
                                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(255, 254, 252);
                            }

                            if (selectedFieldsCopy[i].IsSummable && value != null && decimal.TryParse(value.ToString(), out decimal d)) totals[i] += d;
                        }
                        row++;
                    }

                    if (selectedFieldsCopy.Any(f => f.IsSummable) && dataItems.Count > 0)
                    {
                        var totalRowRange = ws.Range(row, 1, row, colCount);
                        totalRowRange.Style.Font.Bold = true;
                        totalRowRange.Style.Fill.BackgroundColor = XLColor.FromArgb(245, 238, 230);
                        totalRowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin; totalRowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                        ws.Cell(row, 1).Value = "جمع کل"; ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        for (int i = 0; i < colCount; i++)
                        {
                            var cell = ws.Cell(row, i + 1);
                            cell.Value = selectedFieldsCopy[i].IsSummable ? NumberHelper.FormatThousands(totals[i], usePersianDigits: true) : "";
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }
                        row++;
                    }

                    ws.PageSetup.PageOrientation = colCount > LandscapeThreshold ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
                    ws.PageSetup.FitToPages(1, 0);
                    ws.Columns().AdjustToContents();
                    wb.SaveAs(sfd.FileName);
                });
            }, "در حال ذخیره فایل اکسل...");

            if (success)
            {
                ToastHelper.Show("فایل اکسل ذخیره شد.", isSuccess: true);
                if (ModernMessageBox.AskConfirm("فایل ذخیره شد. باز شود؟", "موفق"))
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true }); } catch { }
                }
            }
        }

        private async Task<Image?> GetCompanyLogoAsync()
        {
            try
            {
                using var conn = new SqlConnection(AppConfig.ConnectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("SELECT TOP 1 Logo FROM Companies WHERE Logo IS NOT NULL", conn);
                var res = await cmd.ExecuteScalarAsync();
                if (res != null && res != DBNull.Value)
                {
                    byte[] bytes = (byte[])res;
                    return ImageHelper.ByteArrayToImage(bytes);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ReportBuilderForm.GetCompanyLogo");
            }
            return null;
        }

        private async Task PrintReportAsync()
        {
            if (!await EnsurePreviewUpToDateAsync()) return;

            var selectedFieldsCopy = _selectedFields.ToList();
            List<ReportDataItem>? dataItems = null;

            bool success = await AsyncHelper.RunWithLoading(this, async () =>
            {
                dataItems = await GetReportDataAsync(selectedFieldsCopy);
            }, "در حال آماده‌سازی چاپ...");

            if (!success || dataItems == null) return;

            try
            {
                string reportTitle = string.IsNullOrWhiteSpace(txtReportTitle.Text) ? "گزارش" : txtReportTitle.Text.Trim();
                using var pd = new ReportPrintDocument(selectedFieldsCopy, dataItems, reportTitle);

                pd.CompanyLogo = await GetCompanyLogoAsync();

                using var ppd = new PrintPreviewDialog
                {
                    Document = pd,
                    Width = 1050,
                    Height = 750,
                    StartPosition = FormStartPosition.CenterParent,
                    UseAntiAlias = true,
                    Text = "پیش‌نمایش چاپ",
                    RightToLeft = RightToLeft.Yes,
                    RightToLeftLayout = true
                };

                ppd.ShowDialog(this);

                pd.CompanyLogo?.Dispose();
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ReportBuilderForm.PrintReport");
                ModernMessageBox.ShowError("خطا در چاپ:\n" + ex.Message);
            }
        }
    }

    public class ReportDataItem
    {
        public Employee Employee { get; set; } = null!;
        public Dependent? Dependent { get; set; }
        public bool IsEmployeeRow { get; set; }
        public string ActiveSheba { get; set; } = "";
        public string ActiveBankName { get; set; } = "";
    }

    public class ReportField
    {
        public string Category { get; }
        public string DisplayName { get; }
        public Func<ReportDataItem, object> GetValue { get; }
        public string? Format { get; }
        public bool IsSummable { get; }
        public bool IsDependentField { get; }

        public ReportField(string category, string displayName, Func<ReportDataItem, object> getValue, string? format = null, bool isSummable = false, bool isDependentField = false)
        {
            Category = category;
            DisplayName = displayName;
            GetValue = getValue;
            Format = format;
            IsSummable = isSummable;
            IsDependentField = isDependentField;
        }

        public override string ToString() => DisplayName;
    }

    public class ReportPrintDocument : PrintDocument
    {
        private readonly List<ReportField> _fields;
        private readonly List<ReportDataItem> _dataItems;
        private readonly string _title;
        private int _currentRow;
        private decimal[] _totals = Array.Empty<decimal>();
        private const int BottomReserve = 20;

        public Image? CompanyLogo { get; set; }

        public ReportPrintDocument(List<ReportField> fields, List<ReportDataItem> dataItems, string title)
        {
            _fields = fields ?? new List<ReportField>();
            _dataItems = dataItems ?? new List<ReportDataItem>();
            _title = title ?? "گزارش";
            DefaultPageSettings.Landscape = _fields.Count > 8;
            DefaultPageSettings.Margins = new Margins(40, 40, 30, 40);
            DocumentName = title + (_fields.Count > 8 ? " (افقی)" : " (عمودی)");
        }

        protected override void OnBeginPrint(PrintEventArgs e)
        {
            base.OnBeginPrint(e);
            _currentRow = 0;
            _totals = new decimal[_fields.Count];
            foreach (var item in _dataItems)
            {
                for (int i = 0; i < _fields.Count; i++)
                {
                    if (_fields[i].IsSummable)
                    {
                        var val = _fields[i].GetValue(item);
                        if (val != null && decimal.TryParse(val.ToString(), out decimal d)) _totals[i] += d;
                    }
                }
            }
        }

        protected override void OnPrintPage(PrintPageEventArgs e)
        {
            base.OnPrintPage(e);
            var g = e.Graphics;
            var bounds = e.MarginBounds;
            int y = bounds.Top;
            if (_fields.Count == 0) { e.HasMorePages = false; return; }

            if (CompanyLogo != null)
            {
                float ratio = (float)CompanyLogo.Width / CompanyLogo.Height;
                float maxWidth = 70;
                float maxHeight = 70;
                float drawWidth = maxWidth;
                float drawHeight = drawWidth / ratio;

                if (drawHeight > maxHeight)
                {
                    drawHeight = maxHeight;
                    drawWidth = drawHeight * ratio;
                }

                float logoX = bounds.Right - drawWidth;
                float logoY = bounds.Top - 15;

                g.DrawImage(CompanyLogo, logoX, logoY, drawWidth, drawHeight);
            }

            float titleOffset = CompanyLogo != null ? 85f : 0f;
            float titleWidth = bounds.Width - titleOffset;

            // ✅ FIX: فونت‌های کش‌شده نباید Dispose شوند
            var titleFont = UIHelper.PersianFont(16, FontStyle.Bold);
            var subFont = UIHelper.PersianFont(9);

            using (var centered = new StringFormat { Alignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft })
            {
                g.DrawString(_title, titleFont, Brushes.Black, new RectangleF(bounds.X, y, titleWidth, 30), centered);
                y += 32;
                g.DrawString($"تاریخ: {DateHelper.ToPersianDate(DateTime.Now)}   —   تعداد رکورد: {NumberHelper.FormatThousands(_dataItems.Count, usePersianDigits: true)}", subFont, Brushes.DimGray, new RectangleF(bounds.X, y, titleWidth, 20), centered);
                y += 22;
            }

            int colCount = _fields.Count;
            int colWidth = Math.Max(1, bounds.Width / colCount);

            float cellFontSize = 9f;
            if (colCount > 20) cellFontSize = 6f;
            else if (colCount > 16) cellFontSize = 6.5f;
            else if (colCount > 12) cellFontSize = 7f;
            else if (colCount > 8) cellFontSize = 7.5f;

            float headerFontSize = cellFontSize + 0.5f;
            int rowHeight = Math.Max(18, (int)(cellFontSize * 2.8f));
            int headerRowHeight = Math.Max(24, (int)(headerFontSize * 3f));

            // ✅ FIX: headerFont بدون using
            var headerFont = UIHelper.PersianFont(headerFontSize, FontStyle.Bold);
            using (var headerBg = new SolidBrush(Color.FromArgb(248, 237, 227)))
            using (var headerPen = new Pen(Color.FromArgb(200, 180, 160)))
            using (var headerFmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.FillRectangle(headerBg, bounds.X, y, bounds.Width, headerRowHeight);
                g.DrawRectangle(headerPen, bounds.X, y, bounds.Width, headerRowHeight);
                for (int i = 0; i < colCount; i++)
                {
                    int x = bounds.Right - (i + 1) * colWidth;
                    g.DrawString(_fields[i].DisplayName, headerFont, Brushes.Black, new RectangleF(x + 3, y + 4, colWidth - 6, headerRowHeight - 8), headerFmt);
                }
            }
            y += headerRowHeight;

            bool hasDependentFields = _fields.Any(f => f.IsDependentField);

            using (var borderPen = new Pen(Color.FromArgb(225, 215, 205)))
            using (var altBg = new SolidBrush(Color.FromArgb(255, 254, 252)))
            using (var empBg = new SolidBrush(Color.FromArgb(253, 250, 245)))
            using (var cellFmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft, Trimming = StringTrimming.EllipsisCharacter })
            {
                while (_currentRow < _dataItems.Count)
                {
                    if (y + rowHeight > bounds.Bottom - BottomReserve) { e.HasMorePages = true; return; }
                    var item = _dataItems[_currentRow];

                    g.FillRectangle(Brushes.White, bounds.X, y, bounds.Width, rowHeight);

                    if (hasDependentFields)
                    {
                        if (item.IsEmployeeRow)
                        {
                            g.FillRectangle(empBg, bounds.X, y, bounds.Width, rowHeight);
                        }
                    }
                    else
                    {
                        if (_currentRow % 2 == 1)
                        {
                            g.FillRectangle(altBg, bounds.X, y, bounds.Width, rowHeight);
                        }
                    }

                    for (int i = 0; i < colCount; i++)
                    {
                        int x = bounds.Right - (i + 1) * colWidth;
                        object value;
                        try { value = _fields[i].GetValue(item); } catch { value = null; }

                        string text = "";
                        if (value is decimal dec) text = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                        else if (value is int iVal) text = NumberHelper.FormatThousands(iVal, usePersianDigits: true);
                        else if (value is long lVal) text = NumberHelper.FormatThousands(lVal, usePersianDigits: true);
                        else text = value?.ToString() ?? "";

                        float currentSize = cellFontSize;
                        if (!string.IsNullOrEmpty(text))
                        {
                            // ✅ FIX: measureFont بدون using
                            var measureFont = UIHelper.PersianFont(currentSize, item.IsEmployeeRow ? FontStyle.Bold : FontStyle.Regular);
                            SizeF textSize = g.MeasureString(text, measureFont);
                            if (textSize.Width > colWidth - 6)
                            {
                                float scale = (float)(colWidth - 6) / textSize.Width;
                                currentSize = Math.Max(5f, currentSize * scale);
                            }
                        }

                        // ✅ FIX: cellFont بدون using
                        var cellFont = UIHelper.PersianFont(currentSize, item.IsEmployeeRow ? FontStyle.Bold : FontStyle.Regular);
                        g.DrawString(text, cellFont, Brushes.Black, new RectangleF(x + 3, y, colWidth - 6, rowHeight), cellFmt);
                    }
                    g.DrawLine(borderPen, bounds.X, y + rowHeight, bounds.Right, y + rowHeight);
                    y += rowHeight;
                    _currentRow++;
                }

                if (_fields.Any(f => f.IsSummable) && _dataItems.Count > 0)
                {
                    if (y + rowHeight > bounds.Bottom - BottomReserve) { e.HasMorePages = true; return; }
                    using (var totalBg = new SolidBrush(Color.FromArgb(245, 238, 230)))
                    using (var totalFmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft, Trimming = StringTrimming.EllipsisCharacter })
                    {
                        // ✅ FIX: totalFont بدون using
                        var totalFont = UIHelper.PersianFont(cellFontSize, FontStyle.Bold);
                        g.FillRectangle(totalBg, bounds.X, y, bounds.Width, rowHeight);
                        g.DrawRectangle(borderPen, bounds.X, y, bounds.Width, rowHeight);
                        for (int i = 0; i < colCount; i++)
                        {
                            int x = bounds.Right - (i + 1) * colWidth;
                            string text = i == 0 ? "جمع کل" : (_fields[i].IsSummable ? NumberHelper.FormatThousands(_totals[i], usePersianDigits: true) : "");
                            g.DrawString(text, totalFont, Brushes.Black, new RectangleF(x + 3, y, colWidth - 6, rowHeight), totalFmt);
                        }
                    }
                }
            }
            e.HasMorePages = false;
        }
    }
}