using SMP.Data;
using SMP.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SMP.Forms
{
    public class MainSidebar : UserControl
    {
        private FlowLayoutPanel mainFlow = null!;
        private readonly Dictionary<string, Button> _allButtons = new Dictionary<string, Button>();
        private readonly Dictionary<Control, List<Control>> _categoryItems = new Dictionary<Control, List<Control>>();

        public event Action<string>? MenuClicked;
        private readonly string _role;
        private readonly HashSet<string> _allowedMenus = new HashSet<string>();

        public MainSidebar(string? role, string connectionString)
        {
            _role = string.IsNullOrEmpty(role) ? "Viewer" : role.Trim();

            if (!string.Equals(_role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var permRepo = new PermissionRepository(connectionString);
                    _allowedMenus = permRepo.GetAllowedMenus(_role);
                }
                catch
                {
                    _allowedMenus = new HashSet<string>();
                }
            }

            InitializeUI();
            BuildMenu();
            ApplyDynamicPermissions();
        }

        private void InitializeUI()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = UIHelper.SidebarBackColor;

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent, Padding = new Padding(5) };
            mainFlow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
            scroll.Controls.Add(mainFlow);
            this.Controls.Add(scroll);
        }

        private FlowLayoutPanel AddCategory(string title, string icon, Color accentColor, bool isExpandedByDefault = false)
        {
            var header = UIHelper.CreateAccordionHeader(title, icon, accentColor, isExpandedByDefault, out Label chevron);
            var container = UIHelper.CreateAccordionContainer(isExpandedByDefault);

            Action toggle = () =>
            {
                bool willOpen = !container.Visible;
                foreach (Control ctrl in mainFlow.Controls)
                {
                    if (ctrl is FlowLayoutPanel flp && flp != container) flp.Visible = false;
                    if (ctrl is Panel p && p != header)
                    {
                        var l = p.Controls.OfType<Label>().FirstOrDefault(x => x.Text == "▼" || x.Text == "▶");
                        if (l != null) l.Text = "▶";
                    }
                }
                container.Visible = willOpen;
                chevron.Text = willOpen ? "▼" : "▶";
            };

            foreach (Control ctrl in header.Controls) ctrl.Click += (s, e) => toggle();
            header.Click += (s, e) => toggle();

            mainFlow.Controls.Add(header);
            mainFlow.Controls.Add(container);

            _categoryItems[header] = new List<Control> { container };
            return container;
        }

        private void AddButton(FlowLayoutPanel parent, string key, string icon, string text, Color accentColor)
        {
            var btn = UIHelper.CreateAccordionButton(key, icon, text, accentColor);
            btn.Click += (s, e) => MenuClicked?.Invoke(key);
            parent.Controls.Add(btn);
            _allButtons[key] = btn;
        }

        private void BuildMenu()
        {
            var catDashboards = AddCategory("داشبوردهای مدیریتی", "📊", UIHelper.SectionPurple, true);
            AddButton(catDashboards, "ManagementDashboard", "📊", "داشبورد مدیریتی (پرسنل)", UIHelper.SectionPurple);
            AddButton(catDashboards, "ContractsDashboard", "📄", "داشبورد مدیریت قراردادها", UIHelper.SectionOrange);
            AddButton(catDashboards, "LiquidityDashboard", "💵", "داشبورد تحلیلی نقدینگی", UIHelper.ActionAmber);
            AddButton(catDashboards, "PayrollSummary", "💹", "داشبورد مالی و بیمه", UIHelper.ActionGreen);
            AddButton(catDashboards, "GraphicalDashboard", "📈", "داشبورد گرافیکی", UIHelper.ActionGreen);
            AddButton(catDashboards, "SalaryForecast", "📉", "پیش‌بینی حقوق", UIHelper.ActionGreen);
            AddButton(catDashboards, "ProfitLoss", "💰", "داشبورد سود و زیان", UIHelper.SectionTeal);
            AddButton(catDashboards, "CashFlowForecast", "📈", "پیش‌بینی جریان نقدی", UIHelper.SectionBlue);
            AddButton(catDashboards, "GlobalSearch", "🔍", "جستجوی سراسری مالی", UIHelper.ActionRed);
            AddButton(catDashboards, "UnbilledExpenses", "🧾", "گزارش هزینه‌های صورت وضعیت نشده", UIHelper.ActionAmber);
            AddButton(catDashboards, "GlobalReport", "📊", "داشبورد جامع قراردادها", UIHelper.SectionTeal);
            AddButton(catDashboards, "GlobalGuarantees", "🏦", "گزارش جامع ضمانت‌نامه‌ها", UIHelper.SectionTeal);

            var catPersonnel = AddCategory("مدیریت پرسنل", "👥", UIHelper.SectionBlue);
            AddButton(catPersonnel, "PersonnelList", "📋", "لیست پرسنل", UIHelper.SectionBlue);

            var catLiquidity = AddCategory("مدیریت نقدینگی", "💵", UIHelper.ActionAmber);
            AddButton(catLiquidity, "QuickClientReceipt", "📥", "ثبت دریافت از کارفرما", UIHelper.ActionGreen);
            AddButton(catLiquidity, "ClientReceiptList", "📋", "لیست دریافتی‌های کارفرما", UIHelper.ActionGreen);
            AddButton(catLiquidity, "CashFlowManagement", "📝", "مدیریت تراکنش‌ها و چک‌ها", UIHelper.SectionOrange);
            AddButton(catLiquidity, "CashTransaction", "💸", "ثبت حواله / نقدی", UIHelper.ActionAmber);
            AddButton(catLiquidity, "BulkCashTransaction", "📑", "ثبت گروهی حواله‌ها", UIHelper.SectionOrange);
            AddButton(catLiquidity, "CashCheckEdit", "📄", "ثبت چک جدید", UIHelper.SectionOrange);
            AddButton(catLiquidity, "CashTransactionList", "📋", "لیست تراکنش‌های نقدی", UIHelper.SectionOrange);
            AddButton(catLiquidity, "CashCheckList", "🧾", "لیست و ویرایش چک‌ها", UIHelper.SectionOrange);
            AddButton(catLiquidity, "AccountTransfer", "🔄", "انتقال بین حساب‌ها", UIHelper.SectionTeal);
            AddButton(catLiquidity, "BankFee", "💸", "ثبت کارمزد بانکی", UIHelper.WarmDanger);
            AddButton(catLiquidity, "BankInterest", "💰", "ثبت سود بانکی", UIHelper.ActionGreen);
            AddButton(catLiquidity, "BankAccounts", "🏦", "مدیریت حساب‌های بانکی", UIHelper.ActionAmber);
            AddButton(catLiquidity, "TankhahManagement", "💰", "مدیریت تنخواه‌ها", UIHelper.ActionAmber);
            AddButton(catLiquidity, "TankhahPerformanceReport", "📊", "گزارش عملکرد تنخواه", UIHelper.ActionGreen);

            var catContracts = AddCategory("قراردادها", "📄", UIHelper.SectionOrange);
            AddButton(catContracts, "ClientContracts", "🏢", "قراردادهای کارفرما", UIHelper.SectionOrange);
            AddButton(catContracts, "ContractorContracts", "📝", "قراردادهای پیمانکار", UIHelper.SectionOrange);
            AddButton(catContracts, "PartyManagement", "👥", "مدیریت طرفین قرارداد", UIHelper.SectionOrange);
            AddButton(catContracts, "ContractImport", "📥", "ایمپورت قراردادها از اکسل", UIHelper.SectionOrange);
            AddButton(catContracts, "ContractTemplateEdit", "📝", "ویرایش متن چاپی قرارداد", UIHelper.SectionOrange);

            var catPayroll = AddCategory("حقوق و دستمزد", "💰", UIHelper.ActionGreen);
            AddButton(catPayroll, "SalarySettings", "💰", "تنظیمات حقوق سالانه", UIHelper.ActionGreen);
            AddButton(catPayroll, "AttendanceForm", "📅", "ثبت کارکرد ماهانه", UIHelper.ActionGreen);
            AddButton(catPayroll, "LeaveManagement", "🏖️", "مدیریت مرخصی‌ها", UIHelper.ActionGreen);
            AddButton(catPayroll, "Settlement", "📄", "تسویه حساب پرسنل", UIHelper.ActionGreen);
            AddButton(catPayroll, "PayrollProcess", "📊", "پردازش و محاسبه حقوق", UIHelper.ActionGreen);
            AddButton(catPayroll, "PayrollHistory", "📑", "تاریخچه و گزارش حقوق", UIHelper.ActionGreen);
            AddButton(catPayroll, "LoanManagement", "🏦", "مدیریت وام و مساعده", UIHelper.ActionGreen);
            AddButton(catPayroll, "SupplementaryInsuranceManagement", "🩺", "مدیریت بیمه تکمیلی", UIHelper.ActionGreen);
            AddButton(catPayroll, "CarInsuranceManagement", "🚗", "مدیریت بیمه خودرو", UIHelper.ActionGreen);
            AddButton(catPayroll, "BankPayment", "🏧", "فایل واریز بانک", UIHelper.ActionGreen);
            AddButton(catPayroll, "YearEndBonus", "🎁", "محاسبه عیدی و پاداش", UIHelper.ActionGreen);
            AddButton(catPayroll, "DecreeSalaryManagement", "✍️", "ورود مبالغ حکم حقوقی", UIHelper.SectionBlue);
            AddButton(catPayroll, "PersonnelDecree", "📜", "صدور حکم حقوقی", UIHelper.ActionGreen);

            // 👈 دکمه جدید مهندسی معکوس
            AddButton(catPayroll, "ReversePayroll", "🧮", "مهندسی معکوس حقوق", UIHelper.WarmPrimary);

            var catReports = AddCategory("گزارش‌ها و چاپ", "🖨️", UIHelper.SectionTeal);
            AddButton(catReports, "PayrollReports", "🧾", "گزارش لیست حقوق و فیش", UIHelper.ActionBlue);
            AddButton(catReports, "ReportBuilder", "🛠️", "گزارش‌ساز سفارشی", UIHelper.SectionTeal);
            AddButton(catReports, "BatchPayslip", "🖨️", "چاپ گروهی فیش‌ها", UIHelper.SectionTeal);
            AddButton(catReports, "ExcelComparison", "📊", "مقایسه دو فایل اکسل", UIHelper.ActionGreen);

            var catOfficial = AddCategory("گزارشات رسمی", "📋", UIHelper.SectionDark);
            AddButton(catOfficial, "InsuranceSummaryReport", "📊", "خلاصه بیمه پرداختی", UIHelper.SectionDark);
            AddButton(catOfficial, "NonPersonnelInsuranceReport", "🧾", "بیمه افراد غیرفعال", UIHelper.SectionDark);
            AddButton(catOfficial, "OfficialInsurance", "📋", "لیست رسمی بیمه", UIHelper.SectionDark);
            AddButton(catOfficial, "OfficialTax", "🧮", "لیست رسمی مالیات", UIHelper.SectionDark);
            AddButton(catOfficial, "ImportCombinedDbf", "📥", "ایمپورت همزمان هدر و جزئیات بیمه", UIHelper.SectionDark);
            AddButton(catOfficial, "ClearInsuranceData", "🗑️", "پاک‌سازی داده‌های بیمه", UIHelper.WarmDanger);

            var catSettings = AddCategory("تنظیمات", "⚙️", UIHelper.SectionGray);
            AddButton(catSettings, "TafsiliManagement", "🔢", "مدیریت تفصیلی‌ها", UIHelper.SectionGray);
            AddButton(catSettings, "UserManagement", "👤", "مدیریت کاربران", UIHelper.SectionGray);
            AddButton(catSettings, "RolePermissions", "🔐", "مدیریت سطوح دسترسی", UIHelper.SectionGray);
            AddButton(catSettings, "AuditLogs", "📜", "مشاهده لاگ تغییرات", UIHelper.SectionGray);
            AddButton(catSettings, "ManageLookups", "⚙️", "مدیریت Lookups", UIHelper.SectionGray);
            AddButton(catSettings, "ManageDepartments", "🏢", "دپارتمان‌ها", UIHelper.SectionGray);
            AddButton(catSettings, "ManageCompanies", "🏛️", "شرکت‌ها", UIHelper.SectionGray);
            AddButton(catSettings, "DatabaseBackup", "🗄️", "پشتیبان‌گیری دیتابیس", UIHelper.SectionGray);
            AddButton(catSettings, "DatabaseRestore", "♻️", "بازیابی دیتابیس", UIHelper.SectionGray);
        }

        private void ApplyDynamicPermissions()
        {
            if (string.Equals(_role, "Admin", StringComparison.OrdinalIgnoreCase))
                return;

            foreach (var kvp in _allButtons)
            {
                if (!_allowedMenus.Contains(kvp.Key))
                {
                    kvp.Value.Visible = false;
                }
            }

            foreach (var kvp in _categoryItems)
            {
                Panel header = (Panel)kvp.Key;
                FlowLayoutPanel container = (FlowLayoutPanel)kvp.Value[0];

                bool hasAllowedButton = false;
                foreach (Control ctrl in container.Controls)
                {
                    if (ctrl is Button btn && btn.Tag != null && _allowedMenus.Contains(btn.Tag.ToString()!))
                    {
                        hasAllowedButton = true;
                        break;
                    }
                }

                if (!hasAllowedButton)
                {
                    header.Visible = false;
                    container.Visible = false;
                }
            }
        }
    }
}