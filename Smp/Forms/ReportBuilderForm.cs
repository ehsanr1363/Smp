using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using SMP.Data;
using SMP.Models;
using SMP.Services;
using SMP.Helpers;

namespace SMP.Forms
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

        private readonly SemaphoreSlim _operationLock = new(1, 1);

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

            FormClosed += (s, e) =>
            {
                try { _operationLock?.Dispose(); } catch { }
                try { _treeToolTip?.Dispose(); } catch { }
            };

            if (employees == null && employeeService != null)
            {
                Shown += async (s, e) =>
                {
                    try
                    {
                        await LoadEmployeesSafeAsync();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogException(ex, "ReportBuilderForm.Shown");
                        if (!IsDisposed)
                            ModernMessageBox.ShowError("خطا در بارگذاری لیست پرسنل:\n" + ex.Message);
                    }
                };
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
            treeFields.AfterCheck += TreeFields_AfterCheck;
            treeFields.MouseMove += TreeFields_MouseMove;

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
            foreach (TreeNode cat in treeFields.Nodes)
                foreach (TreeNode child in cat.Nodes)
                    if (child.Checked && child.Tag is ReportField f) _selectedFields.Add(f);
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

            var filteredList = filteredEmployees.ToList();
            if (filteredList.Count == 0) return new List<ReportDataItem>();

            var items = new List<ReportDataItem>();
            bool hasDependentFields = fields.Any(f => f.IsDependentField);
            bool needsBankData = fields.Any(f => f.DisplayName.Contains("شبا") || f.DisplayName.Contains("بانک"));

            Dictionary<int, List<EmployeeBankAccount>> accountsByEmp = new();
            Dictionary<int, List<Dependent>> depsByEmp = new();

            var employeeIds = filteredList.Select(e => e.Id).ToList();

            if (needsBankData)
            {
                var allAccounts = await _bankAccountRepo.GetByEmployeeIdsAsync(employeeIds) ?? new List<EmployeeBankAccount>();
                accountsByEmp = allAccounts.GroupBy(a => a.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
            }

            if (hasDependentFields)
            {
                var allDependents = await new DependentRepository(AppConfig.ConnectionString).GetByEmployeeIdsAsync(employeeIds) ?? new List<Dependent>();
                depsByEmp = allDependents.GroupBy(d => d.EmployeeId).ToDictionary(g => g.Key, g => g.ToList());
            }

            foreach (var emp in filteredList)
            {
                string activeSheba = "";
                string activeBankName = "";

                if (needsBankData && accountsByEmp.TryGetValue(emp.Id, out var accounts))
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
            if (IsDisposed) return;
            if (!await _operationLock.WaitAsync(0)) return;

            try
            {
                RebuildSelectedFields();
                if (_selectedFields.Count == 0) { ModernMessageBox.ShowWarning("حداقل یک فیلد انتخاب کنید."); return; }
                if (_employees.Count == 0) { ModernMessageBox.ShowWarning("هیچ کارمندی برای گزارش وجود ندارد."); return; }

                await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var items = await GetReportDataAsync(_selectedFields);
                    var dt = BuildDataTable(_selectedFields, items);

                    if (IsDisposed) return;
                    dgvPreview.DataSource = dt;
                    if (dgvPreview.Columns["_IsEmployeeRow"] != null) dgvPreview.Columns["_IsEmployeeRow"].Visible = false;
                    UpdateStatus();
                }, "در حال ساخت پیش‌نمایش...");
            }
            finally
            {
                try { _operationLock.Release(); } catch { }
            }
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
            if (_employees.Count == 0) { ModernMessageBox.ShowWarning("هیچ کارمندی برای گزارش وجود ندارد."); return false; }

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
                await GeneratePreviewAsync();
                return dgvPreview.DataSource != null;
            }
            return true;
        }

        private async Task ExportExcelAsync()
        {
            if (IsDisposed) return;
            if (!await _operationLock.WaitAsync(0)) return;

            try
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

                var fieldsSnapshot = _selectedFields.ToList();
                string titleSnapshot = string.IsNullOrWhiteSpace(txtReportTitle.Text) ? "گزارش" : txtReportTitle.Text.Trim();

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    var dataItems = await GetReportDataAsync(fieldsSnapshot);

                    await Task.Run(() =>
                    {
                        using var wb = new XLWorkbook();
                        var ws = wb.Worksheets.Add("گزارش");
                        int colCount = fieldsSnapshot.Count;

                        ws.Cell(1, 1).Value = titleSnapshot;
                        var titleRange = ws.Range(1, 1, 1, Math.Max(1, colCount));
                        titleRange.Merge();
                        titleRange.Style.Font.Bold = true;
                        titleRange.Style.Font.FontSize = 14;
                        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        ws.Cell(2, 1).Value = $"تاریخ: {DateHelper.ToPersianDate(DateTime.Now)}  —  تعداد رکورد: {NumberHelper.FormatThousands(dataItems.Count, usePersianDigits: true)}";
                        var subRange = ws.Range(2, 1, 2, Math.Max(1, colCount));
                        subRange.Merge();
                        subRange.Style.Font.FontSize = 10;
                        subRange.Style.Font.FontColor = XLColor.FromArgb(100, 116, 139);
                        subRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        for (int i = 0; i < colCount; i++)
                        {
                            var cell = ws.Cell(4, i + 1);
                            cell.Value = fieldsSnapshot[i].DisplayName;
                            cell.Style.Font.Bold = true;
                            cell.Style.Font.FontColor = XLColor.FromArgb(90, 60, 40);
                            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(248, 237, 227);
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            cell.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                        }
                        ws.Row(4).Height = 24;

                        int row = 5;
                        decimal[] totals = new decimal[colCount];

                        foreach (var item in dataItems)
                        {
                            for (int i = 0; i < colCount; i++)
                            {
                                object value;
                                try { value = fieldsSnapshot[i].GetValue(item); } catch { value = null; }

                                var cell = ws.Cell(row, i + 1);

                                if (value is decimal d)
                                {
                                    cell.Value = d;
                                    cell.Style.NumberFormat.Format = "#,##0";
                                    if (fieldsSnapshot[i].IsSummable) totals[i] += d;
                                }
                                else if (value is int iv)
                                {
                                    cell.Value = iv;
                                    cell.Style.NumberFormat.Format = "#,##0";
                                    if (fieldsSnapshot[i].IsSummable) totals[i] += iv;
                                }
                                else if (value is long lv)
                                {
                                    cell.Value = lv;
                                    cell.Style.NumberFormat.Format = "#,##0";
                                    if (fieldsSnapshot[i].IsSummable) totals[i] += lv;
                                }
                                else
                                {
                                    cell.Value = value?.ToString() ?? "";
                                }

                                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                cell.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                                if (item.IsEmployeeRow)
                                {
                                    cell.Style.Font.Bold = true;
                                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(253, 250, 245);
                                }
                                else if (row % 2 == 0)
                                {
                                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(255, 254, 252);
                                }
                            }
                            row++;
                        }

                        if (fieldsSnapshot.Any(f => f.IsSummable) && dataItems.Count > 0)
                        {
                            var totalRowRange = ws.Range(row, 1, row, colCount);
                            totalRowRange.Style.Font.Bold = true;
                            totalRowRange.Style.Fill.BackgroundColor = XLColor.FromArgb(245, 238, 230);
                            totalRowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            totalRowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                            ws.Cell(row, 1).Value = "جمع کل";
                            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                            for (int i = 0; i < colCount; i++)
                            {
                                var cell = ws.Cell(row, i + 1);
                                if (fieldsSnapshot[i].IsSummable)
                                {
                                    cell.Value = totals[i];
                                    cell.Style.NumberFormat.Format = "#,##0";
                                }
                                else
                                {
                                    cell.Value = "";
                                }
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

                if (success && !IsDisposed)
                {
                    ToastHelper.Show("فایل اکسل ذخیره شد.", isSuccess: true);
                    if (ModernMessageBox.AskConfirm("فایل ذخیره شد. باز شود؟", "موفق"))
                    {
                        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true }); } catch { }
                    }
                }
            }
            finally
            {
                try { _operationLock.Release(); } catch { }
            }
        }

        private async Task<(Image? Logo, string CompanyName)> GetCompanyInfoAsync()
        {
            try
            {
                using var conn = new SqlConnection(AppConfig.ConnectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("SELECT TOP 1 Name, Logo FROM Companies WHERE Logo IS NOT NULL", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    string name = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    if (!reader.IsDBNull(1))
                    {
                        byte[] bytes = (byte[])reader[1];
                        return (ImageHelper.ByteArrayToImage(bytes), name);
                    }
                    return (null, name);
                }
                using var cmd2 = new SqlCommand("SELECT TOP 1 Name FROM Companies", conn);
                var nameObj = await cmd2.ExecuteScalarAsync();
                return (null, nameObj?.ToString() ?? "شرکت");
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ReportBuilderForm.GetCompanyInfo");
            }
            return (null, "شرکت");
        }

        private async Task PrintReportAsync()
        {
            if (IsDisposed) return;
            if (!await _operationLock.WaitAsync(0)) return;

            try
            {
                if (!await EnsurePreviewUpToDateAsync()) return;

                var fieldsSnapshot = _selectedFields.ToList();
                List<ReportDataItem>? dataItems = null;
                (Image? Logo, string CompanyName) companyInfo = (null, "");

                bool success = await AsyncHelper.RunWithLoading(this, async () =>
                {
                    dataItems = await GetReportDataAsync(fieldsSnapshot);
                    companyInfo = await GetCompanyInfoAsync();
                }, "در حال آماده‌سازی چاپ...");

                if (!success || dataItems == null || IsDisposed) return;

                try
                {
                    string reportTitle = string.IsNullOrWhiteSpace(txtReportTitle.Text) ? "گزارش" : txtReportTitle.Text.Trim();
                    using var pd = new ReportPrintDocument(fieldsSnapshot, dataItems, reportTitle);
                    pd.CompanyLogo = companyInfo.Logo;
                    pd.CompanyName = companyInfo.CompanyName;

                    using var previewForm = new CustomPrintPreviewForm(pd, "پیش‌نمایش چاپ گزارش");
                    previewForm.ShowDialog(this);
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "ReportBuilderForm.PrintReport");
                    ModernMessageBox.ShowError("خطا در چاپ:\n" + ex.Message);
                }
            }
            finally
            {
                try { _operationLock.Release(); } catch { }
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

    public class CustomPrintPreviewForm : Form
    {
        private readonly PrintDocument _document;
        private TextBox txtFrom = null!, txtTo = null!;

        public CustomPrintPreviewForm(PrintDocument document, string title)
        {
            _document = document;
            Text = title;
            Width = 1050;
            Height = 750;
            StartPosition = FormStartPosition.CenterParent;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            ShowInTaskbar = false;

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 55, BackColor = Color.FromArgb(240, 240, 240), Padding = new Padding(10) };

            var btnPrint = new Button
            {
                Text = "🖨️ چاپ",
                Width = 120,
                Height = 35,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Green,
                ForeColor = Color.White,
                Font = UIHelper.PersianFont(11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnPrint.FlatAppearance.BorderSize = 0;
            btnPrint.Click += (s, e) =>
            {
                using var pd = new PrintDialog
                {
                    Document = _document,
                    AllowSomePages = true,
                    UseEXDialog = true
                };

                if (int.TryParse(txtFrom.Text, out int fromPage) && int.TryParse(txtTo.Text, out int toPage) && fromPage > 0 && toPage >= fromPage)
                {
                    pd.PrinterSettings.PrintRange = PrintRange.SomePages;
                    pd.PrinterSettings.FromPage = fromPage;
                    pd.PrinterSettings.ToPage = toPage;
                }
                else
                {
                    pd.PrinterSettings.PrintRange = PrintRange.AllPages;
                }

                if (pd.ShowDialog(this) == DialogResult.OK)
                {
                    _document.Print();
                }
            };

            var btnClose = new Button
            {
                Text = "بستن",
                Width = 100,
                Height = 35,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Gray,
                ForeColor = Color.White,
                Font = UIHelper.PersianFont(11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => Close();

            var pnlRange = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 350, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
            var lblFrom = new Label { Text = "از صفحه:", AutoSize = true, Font = UIHelper.PersianFont(10f), Margin = new Padding(0, 8, 5, 0) };
            txtFrom = new TextBox { Width = 50, Text = "1", Font = UIHelper.PersianFont(10f), Margin = new Padding(0, 5, 15, 0) };
            var lblTo = new Label { Text = "تا صفحه (خالی = همه):", AutoSize = true, Font = UIHelper.PersianFont(10f), Margin = new Padding(0, 8, 5, 0) };
            txtTo = new TextBox { Width = 50, Text = "", Font = UIHelper.PersianFont(10f), Margin = new Padding(0, 5, 0, 0) };

            pnlRange.Controls.Add(lblFrom);
            pnlRange.Controls.Add(txtFrom);
            pnlRange.Controls.Add(lblTo);
            pnlRange.Controls.Add(txtTo);

            topPanel.Controls.Add(pnlRange);
            topPanel.Controls.Add(btnPrint);
            topPanel.Controls.Add(btnClose);

            var previewControl = new PrintPreviewControl
            {
                Dock = DockStyle.Fill,
                Document = _document,
                Zoom = 0.85,
                UseAntiAlias = true,
                BackColor = Color.DarkGray
            };

            Controls.Add(previewControl);
            Controls.Add(topPanel);
        }
    }

    public class ReportPrintDocument : PrintDocument
    {
        private readonly List<ReportField> _fields;
        private readonly List<ReportDataItem> _dataItems;
        private readonly string _title;
        private int _currentRow;
        private int _pageNumber;
        private decimal[] _totals = Array.Empty<decimal>();
        private const int BottomReserve = 50;

        public Image? CompanyLogo { get; set; }
        public string CompanyName { get; set; } = "شرکت";

        public ReportPrintDocument(List<ReportField> fields, List<ReportDataItem> dataItems, string title)
        {
            _fields = fields ?? new List<ReportField>();
            _dataItems = dataItems ?? new List<ReportDataItem>();
            _title = title ?? "گزارش";

            DefaultPageSettings.Landscape = _fields.Count > 8;
            DefaultPageSettings.Margins = new Margins(40, 40, 50, 50);
            DocumentName = title + (_fields.Count > 8 ? " (افقی)" : " (عمودی)");
        }

        protected override void OnBeginPrint(PrintEventArgs e)
        {
            base.OnBeginPrint(e);
            _currentRow = 0;
            _pageNumber = 1;
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
            if (g == null) return;
            var bounds = e.MarginBounds;
            int y = bounds.Top;
            if (_fields.Count == 0) { e.HasMorePages = false; return; }

            bool shouldDraw = true;
            if (e.PageSettings.PrinterSettings.PrintRange == PrintRange.SomePages)
            {
                if (_pageNumber > e.PageSettings.PrinterSettings.ToPage)
                {
                    e.HasMorePages = false;
                    return;
                }
                if (_pageNumber < e.PageSettings.PrinterSettings.FromPage)
                {
                    shouldDraw = false;
                }
            }

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            // 👈 فونت اختصاصی و درشت برای نام شرکت
            var companyFont = UIHelper.PersianFont(18, FontStyle.Bold);

            var titleFont = UIHelper.PersianFont(16, FontStyle.Bold);
            var headerFont = UIHelper.PersianFont(9.5f, FontStyle.Bold);
            var bodyFont = UIHelper.PersianFont(9f, FontStyle.Regular);
            var smallFont = UIHelper.PersianFont(8f, FontStyle.Regular);
            var totalFont = UIHelper.PersianFont(9.5f, FontStyle.Bold);

            var rtlFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
            var rtlRight = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap };
            var titleFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft };

            // 👈 ارتفاع هدر را بیشتر کردیم تا متن‌های درشت‌تر جا شوند
            int headerHeight = 110;

            if (shouldDraw && CompanyLogo != null)
            {
                try
                {
                    float ratio = (float)CompanyLogo.Width / CompanyLogo.Height;
                    float maxWidth = 90; // کمی بزرگتر شد
                    float maxHeight = 90;
                    float drawWidth = maxWidth;
                    float drawHeight = drawWidth / ratio;
                    if (drawHeight > maxHeight) { drawHeight = maxHeight; drawWidth = drawHeight * ratio; }

                    g.DrawImage(CompanyLogo, bounds.Right - drawWidth, bounds.Top, drawWidth, drawHeight);
                }
                catch { }
            }

            if (shouldDraw)
            {
                // 👈 نام شرکت حالا با companyFont و رنگ مشکی چاپ می‌شود
                g.DrawString(CompanyName, companyFont, Brushes.Black, new RectangleF(bounds.Left, bounds.Top + 5, bounds.Width, 30), rtlFormat);
                g.DrawString(_title, titleFont, Brushes.Black, new RectangleF(bounds.Left, bounds.Top + 45, bounds.Width, 30), titleFormat);
                g.DrawString($"تاریخ گزارش: {DateHelper.ToPersianDate(DateTime.Now)}", smallFont, Brushes.Gray, new RectangleF(bounds.Left, bounds.Top + 80, bounds.Width, 20), rtlFormat);
            }

            y = bounds.Top + headerHeight + 10;

            int colCount = _fields.Count;
            float tableWidth = bounds.Width;
            float colWidth = tableWidth / (float)colCount;
            float rowHeight = 28f;
            float headerRowHeight = 32f;

            float tableTop = y;

            var headerBgBrush = new SolidBrush(Color.FromArgb(250, 245, 235));

            if (shouldDraw)
            {
                g.FillRectangle(headerBgBrush, bounds.Left, y, tableWidth, headerRowHeight);
                float x = bounds.Right;
                for (int i = 0; i < colCount; i++)
                {
                    x -= colWidth;
                    g.DrawString(_fields[i].DisplayName, headerFont, Brushes.Black, new RectangleF(x + 2, y, colWidth - 4, headerRowHeight), rtlFormat);
                }
            }
            y += (int)headerRowHeight;

            var empBgBrush = new SolidBrush(Color.FromArgb(248, 252, 255));
            var altBgBrush = new SolidBrush(Color.FromArgb(252, 252, 252));

            bool hasDependentFields = _fields.Any(f => f.IsDependentField);
            bool hasSummable = _fields.Any(f => f.IsSummable) && _dataItems.Count > 0;

            while (_currentRow < _dataItems.Count)
            {
                if (y + rowHeight > bounds.Bottom - BottomReserve)
                {
                    if (shouldDraw) DrawGridAndFooter(g, bounds, y, tableTop, tableWidth, colWidth, colCount, headerRowHeight, rowHeight, smallFont, rtlFormat, rtlRight);
                    _pageNumber++;

                    if (e.PageSettings.PrinterSettings.PrintRange == PrintRange.SomePages && _pageNumber > e.PageSettings.PrinterSettings.ToPage)
                    {
                        e.HasMorePages = false;
                        return;
                    }

                    e.HasMorePages = true;
                    return;
                }

                var item = _dataItems[_currentRow];

                if (shouldDraw)
                {
                    if (hasDependentFields && item.IsEmployeeRow)
                        g.FillRectangle(empBgBrush, bounds.Left, y, tableWidth, rowHeight);
                    else if (_currentRow % 2 == 1)
                        g.FillRectangle(altBgBrush, bounds.Left, y, tableWidth, rowHeight);

                    float x = bounds.Right;
                    for (int i = 0; i < colCount; i++)
                    {
                        x -= colWidth;
                        object value;
                        try { value = _fields[i].GetValue(item); } catch { value = null; }

                        string text = "";
                        if (value is decimal dec) text = NumberHelper.FormatThousands(dec, usePersianDigits: true);
                        else if (value is int iVal) text = NumberHelper.FormatThousands(iVal, usePersianDigits: true);
                        else if (value is long lVal) text = NumberHelper.FormatThousands(lVal, usePersianDigits: true);
                        else text = value?.ToString() ?? "";

                        float currentSize = bodyFont.Size;
                        if (!string.IsNullOrEmpty(text))
                        {
                            var measureFont = UIHelper.PersianFont(currentSize, item.IsEmployeeRow ? FontStyle.Bold : FontStyle.Regular);
                            SizeF textSize = g.MeasureString(text, measureFont);
                            if (textSize.Width > colWidth - 6)
                            {
                                float scale = (float)(colWidth - 6) / textSize.Width;
                                currentSize = Math.Max(6.5f, currentSize * scale);
                            }
                        }

                        var cellFont = UIHelper.PersianFont(currentSize, item.IsEmployeeRow ? FontStyle.Bold : FontStyle.Regular);
                        g.DrawString(text, cellFont, Brushes.Black, new RectangleF(x + 3, y, colWidth - 6, rowHeight), rtlFormat);
                    }
                }

                y += (int)rowHeight;
                _currentRow++;
            }

            if (hasSummable)
            {
                if (y + rowHeight > bounds.Bottom - BottomReserve)
                {
                    if (shouldDraw) DrawGridAndFooter(g, bounds, y, tableTop, tableWidth, colWidth, colCount, headerRowHeight, rowHeight, smallFont, rtlFormat, rtlRight);
                    _pageNumber++;

                    if (e.PageSettings.PrinterSettings.PrintRange == PrintRange.SomePages && _pageNumber > e.PageSettings.PrinterSettings.ToPage)
                    {
                        e.HasMorePages = false;
                        return;
                    }

                    e.HasMorePages = true;
                    return;
                }

                if (shouldDraw)
                {
                    var totalBgBrush = new SolidBrush(Color.FromArgb(250, 245, 235));
                    g.FillRectangle(totalBgBrush, bounds.Left, y, tableWidth, rowHeight);

                    float x = bounds.Right;
                    for (int i = 0; i < colCount; i++)
                    {
                        x -= colWidth;
                        string text = i == 0 ? "جمع کل" : (_fields[i].IsSummable ? NumberHelper.FormatThousands(_totals[i], usePersianDigits: true) : "");
                        g.DrawString(text, totalFont, Brushes.Black, new RectangleF(x + 2, y, colWidth - 4, rowHeight), rtlFormat);
                    }
                }
                y += (int)rowHeight;
            }

            if (shouldDraw)
            {
                DrawGridAndFooter(g, bounds, y, tableTop, tableWidth, colWidth, colCount, headerRowHeight, rowHeight, smallFont, rtlFormat, rtlRight);
            }

            _pageNumber++;

            if (e.PageSettings.PrinterSettings.PrintRange == PrintRange.SomePages && _pageNumber > e.PageSettings.PrinterSettings.ToPage)
            {
                e.HasMorePages = false;
                return;
            }

            e.HasMorePages = _currentRow < _dataItems.Count;
        }

        private void DrawGridAndFooter(Graphics g, Rectangle bounds, int currentY, float tableTop, float tableWidth, float colWidth, int colCount, float headerRowHeight, float rowHeight, Font smallFont, StringFormat rtlFormat, StringFormat rtlRight)
        {
            var borderPen = new Pen(Color.FromArgb(200, 200, 200), 1f);
            var thickBorderPen = new Pen(Color.FromArgb(150, 150, 150), 1.5f);

            float currentH_Y = tableTop + headerRowHeight;
            while (currentH_Y < currentY - 1)
            {
                g.DrawLine(borderPen, bounds.Left, currentH_Y, bounds.Right, currentH_Y);
                currentH_Y += rowHeight;
            }

            float x = bounds.Right;
            for (int i = 0; i < colCount; i++)
            {
                x -= colWidth;
                if (i < colCount - 1)
                {
                    g.DrawLine(borderPen, x, tableTop, x, currentY);
                }
            }

            g.DrawRectangle(thickBorderPen, bounds.Left, tableTop, tableWidth, currentY - tableTop);

            int y = bounds.Bottom - 20;
            g.DrawLine(borderPen, bounds.Left, y, bounds.Right, y);
            g.DrawString($"تعداد کل رکوردها: {NumberHelper.FormatThousands(_dataItems.Count, usePersianDigits: true)}", smallFont, Brushes.Black, new RectangleF(bounds.Left, y + 5, bounds.Width / 2, 20), rtlRight);
            g.DrawString($"صفحه {_pageNumber}", smallFont, Brushes.Black, new RectangleF(bounds.Right - (bounds.Width / 2), y + 5, bounds.Width / 2, 20), rtlFormat);
        }
    }
}