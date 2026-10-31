using PersonnelApp.Data;
using PersonnelApp.Forms;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using PersonnelApp.Services;
using System;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersonnelApp
{
    public partial class MainForm : Form
    {
        private readonly EmployeeService _employeeService;
        private readonly DepartmentService _departmentService;
        private readonly LookupService _lookupService;
        private readonly AuditLogRepository _auditLogRepository;
        private readonly User? _loggedInUser;

        private TableLayoutPanel middleLayout = null!;
        private bool _isSidebarCollapsed = false;
        private MainSidebar _sidebar = null!;

        public MainForm(User? loggedInUser = null)
        {
            _loggedInUser = loggedInUser;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            _employeeService = new EmployeeService(new EmployeeRepository(AppConfig.ConnectionString));
            _departmentService = new DepartmentService(new DepartmentRepository(AppConfig.ConnectionString));
            _lookupService = new LookupService(new LookupRepository(AppConfig.ConnectionString));
            _auditLogRepository = new AuditLogRepository(AppConfig.ConnectionString);

            BuildModernUI();
            this.Shown += async (s, e) => await MainForm_ShownAsync();
        }

        private async Task MainForm_ShownAsync()
        {
            if (_loggedInUser != null) this.Text = $"سامانه مدیریت پرسنل - خوش آمدید {_loggedInUser.EmployeeFullName}";
            await Task.CompletedTask;
        }

        private void Sidebar_MenuClicked(string menuKey)
        {
            if (!this.Enabled) return;

            try
            {
                switch (menuKey)
                {
                    case "PersonnelList":
                        using (var form = new frmEmployees(_employeeService, _departmentService, _lookupService, _auditLogRepository, _loggedInUser))
                            form.ShowDialog(this);
                        break;

                    case "ManagementDashboard": OpenForm(() => new ManagementDashboardForm(AppConfig.ConnectionString), "داشبورد مدیریتی"); break;

                    case "LiquidityDashboard": OpenForm(() => new LiquidityDashboardForm(AppConfig.ConnectionString), "داشبورد تحلیلی نقدینگی"); break;
                    case "CashFlowManagement": OpenForm(() => new CashFlowManagementForm(AppConfig.ConnectionString), "مدیریت تراکنش‌ها و چک‌ها"); break;

                    case "ClientContracts": OpenForm(() => new ClientContractManagementForm(AppConfig.ConnectionString), "مدیریت قراردادهای کارفرما"); break;
                    case "ContractorContracts": OpenForm(() => new ContractorContractManagementForm(AppConfig.ConnectionString), "مدیریت قراردادهای پیمانکار"); break;
                    case "PartyManagement": OpenForm(() => new PartyManagementForm(AppConfig.ConnectionString), "مدیریت طرفین قرارداد"); break;
                    case "ContractImport": OpenForm(() => new ContractImportForm(AppConfig.ConnectionString), "ایمپورت قراردادها"); break;

                    case "SalarySettings": OpenForm(() => new SalarySettingsForm(AppConfig.ConnectionString), "تنظیمات حقوق"); break;
                    case "AttendanceForm": OpenForm(() => new AttendanceForm(AppConfig.ConnectionString), "ثبت کارکرد"); break;
                    case "LeaveManagement": OpenForm(() => new LeaveManagementForm(AppConfig.ConnectionString), "مدیریت مرخصی‌ها"); break;
                    case "Settlement": OpenForm(() => new SettlementForm(AppConfig.ConnectionString), "تسویه حساب"); break;
                    case "PayrollProcess": OpenForm(() => new PayrollProcessForm(AppConfig.ConnectionString), "پردازش حقوق"); break;
                    case "PayrollHistory": OpenForm(() => new PayrollHistoryForm(AppConfig.ConnectionString), "تاریخچه حقوق"); break;
                    case "LoanManagement": OpenForm(() => new LoanManagementForm(AppConfig.ConnectionString), "مدیریت وام"); break;
                    case "SupplementaryInsuranceManagement": OpenForm(() => new SupplementaryInsuranceManagementForm(AppConfig.ConnectionString), "مدیریت بیمه تکمیلی"); break;
                    case "CarInsuranceManagement": OpenForm(() => new CarInsuranceManagementForm(AppConfig.ConnectionString), "مدیریت بیمه خودرو"); break;
                    case "PayrollSummary": OpenForm(() => new PayrollSummaryForm(AppConfig.ConnectionString), "داشبورد مالی"); break;
                    case "BankPayment": OpenForm(() => new BankPaymentForm(AppConfig.ConnectionString), "واریز بانکی"); break;
                    case "YearEndBonus": OpenForm(() => new YearEndBonusForm(AppConfig.ConnectionString), "محاسبه عیدی"); break;
                    case "GraphicalDashboard": OpenForm(() => new GraphicalDashboardForm(AppConfig.ConnectionString), "داشبورد گرافیکی"); break;
                    case "SalaryForecast": OpenForm(() => new SalaryForecastForm(AppConfig.ConnectionString), "پیش‌بینی حقوق"); break;

                    case "ProfitLoss": OpenForm(() => new ProfitLossDashboardForm(AppConfig.ConnectionString), "داشبورد سود و زیان"); break;
                    case "GlobalReport": OpenForm(() => new GlobalContractsReportForm(AppConfig.ConnectionString), "داشبورد جامع قراردادها"); break;
                    case "GlobalGuarantees": OpenForm(() => new GlobalGuaranteesReportForm(AppConfig.ConnectionString), "گزارش جامع ضمانت‌نامه‌ها"); break;
                    case "ReportBuilder": OpenForm(() => new ReportBuilderForm(_employeeService, new DependentService(new DependentRepository(AppConfig.ConnectionString)), null), "گزارش‌ساز سفارشی"); break;
                    case "BatchPayslip": OpenForm(() => new BatchPayslipPrintForm(AppConfig.ConnectionString), "چاپ گروهی فیش"); break;

                    case "ImportCombinedDbf": OpenForm(() => new DbfImportForm(), "ایمپورت همزمان هدر و جزئیات بیمه"); break;
                    case "ClearInsuranceData":
                        if (ModernMessageBox.AskConfirm("⚠️ هشدار جدی!\n\nآیا از پاک‌سازی تمام داده‌های بیمه مطمئن هستید؟", "تایید"))
                        {
                            try { new SystemMaintenanceService(AppConfig.ConnectionString).ClearInsuranceData(); ToastHelper.Show("پاک شد.", true); }
                            catch (Exception ex) { ModernMessageBox.ShowError("خطا:\n" + ex.Message); }
                        }
                        break;

                    case "InsuranceSummaryReport": OpenForm(() => new InsuranceSummaryReportForm(AppConfig.ConnectionString), "گزارش بیمه"); break;
                    case "NonPersonnelInsuranceReport": OpenForm(() => new NonPersonnelInsuranceReportForm(AppConfig.ConnectionString), "گزارش بیمه غیرفعال"); break;
                    case "OfficialInsurance": OpenForm(() => new OfficialInsuranceReportForm(AppConfig.ConnectionString), "گزارش رسمی بیمه"); break;
                    case "OfficialTax": OpenForm(() => new OfficialTaxReportForm(AppConfig.ConnectionString), "گزارش مالیات"); break;

                    case "TafsiliManagement": OpenForm(() => new TafsiliManagementForm(AppConfig.ConnectionString), "مدیریت تفصیلی‌ها"); break;
                    case "UserManagement": OpenForm(() => new UserManagementForm(AppConfig.ConnectionString), "مدیریت کاربران"); break;
                    case "AuditLogs": OpenForm(() => new AuditLogForm(AppConfig.ConnectionString), "لاگ تغییرات"); break;
                    case "ManageLookups": OpenForm(() => new LookupManagementForm(AppConfig.ConnectionString), "مدیریت Lookups"); break;
                    case "ManageDepartments": OpenForm(() => new DepartmentManagementForm(AppConfig.ConnectionString), "مدیریت دپارتمان‌ها"); break;
                    case "ManageCompanies": OpenForm(() => new CompanyManagementForm(AppConfig.ConnectionString), "مدیریت شرکت‌ها"); break;
                    case "DatabaseBackup": OpenForm(() => new DatabaseBackupForm(AppConfig.ConnectionString), "پشتیبان‌گیری"); break;
                    case "DatabaseRestore": OpenForm(() => new DatabaseRestoreForm(AppConfig.ConnectionString), "بازیابی دیتابیس"); break;

                    default:
                        LogHelper.LogWarning($"دکمه‌ای با کلید «{menuKey}» هندل نشده است.", "MainForm");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "MainForm - Sidebar_MenuClicked");
                ModernMessageBox.ShowError("خطا در اجرای دستور منو:\n" + ex.Message);
            }
        }

        private void OpenForm<T>(Func<T> formFactory, string formName) where T : Form
        {
            if (!this.Enabled || this.IsDisposed || this.Disposing) return;
            try
            {
                using var form = formFactory();
                form.ShowDialog(this);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("modal dialog")) { }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, $"MainForm - {formName}");
                ModernMessageBox.ShowError($"خطا در باز کردن {formName}:\n" + ex.Message);
            }
        }

        private void BuildModernUI()
        {
            SuspendLayout();
            Text = "سامانه مدیریت پرسنل";
            Size = new Size(1600, 1000);
            MinimumSize = new Size(1300, 800);
            StartPosition = FormStartPosition.CenterScreen;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UIHelper.WarmBackground;
            Font = UIHelper.PersianFont(10f);
            MaximizeBox = false;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = UIHelper.WarmBackground };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));

            root.Controls.Add(BuildHeader(), 0, 0);

            middleLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = UIHelper.WarmBackground, Padding = new Padding(12) };
            middleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270F));
            middleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            _sidebar = new MainSidebar(_loggedInUser?.Role);
            _sidebar.MenuClicked += Sidebar_MenuClicked;
            middleLayout.Controls.Add(_sidebar, 0, 0);

            var welcomePanel = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmBackground };
            var lblWelcome = new Label
            {
                Text = "به سامانه مدیریت پرسنل خوش آمدید\n\nلطفاً از منوی سمت راست، بخش مورد نظر خود را انتخاب کنید.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UIHelper.PersianFont(18f, FontStyle.Bold),
                ForeColor = UIHelper.WarmText
            };
            welcomePanel.Controls.Add(lblWelcome);
            middleLayout.Controls.Add(welcomePanel, 1, 0);

            root.Controls.Add(middleLayout, 0, 1);
            root.Controls.Add(BuildStatusBar(), 0, 2);

            Controls.Add(root);
            ResumeLayout();
        }

        private Panel BuildHeader()
        {
            var header = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground };
            header.Paint += (s, e) => { using var pen = new Pen(UIHelper.WarmBorder, 1); e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1); };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent, Padding = new Padding(10, 0, 24, 0) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400F));

            var btnToggleSidebar = new Button { Text = "☰", Font = new Font("Segoe UI", 16f, FontStyle.Bold), FlatStyle = FlatStyle.Flat, ForeColor = UIHelper.WarmText, BackColor = Color.Transparent, Dock = DockStyle.Fill, Cursor = Cursors.Hand };
            btnToggleSidebar.FlatAppearance.BorderSize = 0;
            btnToggleSidebar.Click += (s, e) => { _isSidebarCollapsed = !_isSidebarCollapsed; middleLayout.ColumnStyles[0].Width = _isSidebarCollapsed ? 0F : 270F; };

            layout.Controls.Add(btnToggleSidebar, 0, 0);
            layout.Controls.Add(BuildTitlePanel(), 1, 0);
            header.Controls.Add(layout);
            return header;
        }

        private Panel BuildTitlePanel()
        {
            var panel = new Panel { BackColor = Color.Transparent, Dock = DockStyle.Fill };
            var lblTitle = new Label { Text = "سامانه مدیریت پرسنل", Font = UIHelper.PersianFont(15f, FontStyle.Bold), ForeColor = UIHelper.WarmText, AutoSize = true };
            var lblSub = new Label { Text = "مدیریت هوشمند اطلاعات، بیمه، قراردادها و نقدینگی", Font = UIHelper.PersianFont(9f), ForeColor = UIHelper.WarmText, AutoSize = true };
            panel.Controls.Add(lblTitle); panel.Controls.Add(lblSub);
            panel.Layout += (s, e) => {
                int titleY = (panel.Height - lblTitle.Height - lblSub.Height - 4) / 2;
                lblTitle.Location = new Point(panel.Width - lblTitle.Width - 10, titleY);
                lblSub.Location = new Point(panel.Width - lblSub.Width - 10, titleY + lblTitle.Height + 4);
            };
            return panel;
        }

        private Panel BuildStatusBar()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = UIHelper.WarmPanelBackground, Padding = new Padding(16, 0, 16, 0) };
            var lblStatus = new Label { Text = "✓  آماده", Font = UIHelper.PersianFont(10f), ForeColor = UIHelper.WarmText, AutoSize = true, Location = new Point(0, 12) };
            var lblVer = new Label { Text = "نسخه 1.0", Font = UIHelper.PersianFont(10f), ForeColor = UIHelper.WarmText, AutoSize = true };
            panel.Controls.Add(lblStatus); panel.Controls.Add(lblVer);
            panel.Resize += (s, e) => lblVer.Location = new Point(panel.Width - lblVer.Width - 16, 12);
            return panel;
        }
    }
}