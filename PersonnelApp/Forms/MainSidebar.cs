using PersonnelApp.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PersonnelApp.Forms
{
    public class MainSidebar : UserControl
    {
        private FlowLayoutPanel mainFlow = null!;
        private readonly Dictionary<string, Button> _allButtons = new Dictionary<string, Button>();

        public event Action<string>? MenuClicked;
        private readonly string _role;

        public MainSidebar(string? role)
        {
            _role = string.IsNullOrEmpty(role) ? "Viewer" : role;
            InitializeUI();
            BuildMenu();
            ApplyRolePermissions();
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
            var catDashboard = AddCategory("داشبورد", "📊", UIHelper.SectionPurple);
            AddButton(catDashboard, "ManagementDashboard", "📊", "داشبورد مدیریتی", UIHelper.SectionPurple);

            var catPersonnel = AddCategory("مدیریت پرسنل", "👥", UIHelper.SectionBlue, true);
            AddButton(catPersonnel, "PersonnelList", "📋", "لیست پرسنل", UIHelper.SectionBlue);

            var catLiquidity = AddCategory("مدیریت نقدینگی", "💵", UIHelper.ActionAmber);
            AddButton(catLiquidity, "LiquidityDashboard", "📈", "داشبورد تحلیلی نقدینگی", UIHelper.ActionAmber);
            AddButton(catLiquidity, "CashFlowManagement", "📝", "مدیریت تراکنش‌ها و چک‌ها", UIHelper.SectionOrange);

            var catContracts = AddCategory("قراردادها", "📄", UIHelper.SectionOrange);
            AddButton(catContracts, "ClientContracts", "🏢", "قراردادهای کارفرما", UIHelper.SectionOrange);
            AddButton(catContracts, "ContractorContracts", "📝", "قراردادهای پیمانکار", UIHelper.SectionOrange);
            AddButton(catContracts, "PartyManagement", "👥", "مدیریت طرفین قرارداد", UIHelper.SectionOrange);
            AddButton(catContracts, "ContractImport", "📥", "ایمپورت قراردادها از اکسل", UIHelper.SectionOrange);

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
            AddButton(catPayroll, "PayrollSummary", "💹", "داشبورد مالی و بیمه", UIHelper.ActionGreen);
            AddButton(catPayroll, "BankPayment", "🏧", "فایل واریز بانک", UIHelper.ActionGreen);
            AddButton(catPayroll, "YearEndBonus", "🎁", "محاسبه عیدی و پاداش", UIHelper.ActionGreen);
            AddButton(catPayroll, "GraphicalDashboard", "📈", "داشبورد گرافیکی", UIHelper.ActionGreen);
            AddButton(catPayroll, "SalaryForecast", "📉", "پیش‌بینی حقوق", UIHelper.ActionGreen);

            var catReports = AddCategory("گزارش‌ها و چاپ", "🖨️", UIHelper.SectionTeal);
            AddButton(catReports, "ProfitLoss", "💰", "داشبورد سود و زیان", UIHelper.SectionTeal);
            AddButton(catReports, "GlobalReport", "📊", "داشبورد جامع قراردادها", UIHelper.SectionTeal);
            AddButton(catReports, "GlobalGuarantees", "🏦", "گزارش جامع ضمانت‌نامه‌ها", UIHelper.SectionTeal);
            AddButton(catReports, "ReportBuilder", "🛠️", "گزارش‌ساز سفارشی", UIHelper.SectionTeal);
            AddButton(catReports, "BatchPayslip", "🖨️", "چاپ گروهی فیش‌ها", UIHelper.SectionTeal);

            var catOfficial = AddCategory("گزارشات رسمی", "📋", UIHelper.SectionDark);
            AddButton(catOfficial, "InsuranceSummaryReport", "📊", "خلاصه بیمه پرداختی", UIHelper.SectionDark);
            AddButton(catOfficial, "NonPersonnelInsuranceReport", "🧾", "بیمه افراد غیرفعال", UIHelper.SectionDark);
            AddButton(catOfficial, "OfficialInsurance", "📋", "لیست رسمی بیمه", UIHelper.SectionDark);
            AddButton(catOfficial, "OfficialTax", "🧮", "لیست رسمی مالیات", UIHelper.SectionDark);

            var catSettings = AddCategory("تنظیمات", "⚙️", UIHelper.SectionGray);
            AddButton(catSettings, "TafsiliManagement", "🔢", "مدیریت تفصیلی‌ها", UIHelper.SectionGray);
            AddButton(catSettings, "UserManagement", "👤", "مدیریت کاربران", UIHelper.SectionGray);
            AddButton(catSettings, "AuditLogs", "📜", "مشاهده لاگ تغییرات", UIHelper.SectionGray);
            AddButton(catSettings, "ManageLookups", "⚙️", "مدیریت Lookups", UIHelper.SectionGray);
            AddButton(catSettings, "ManageDepartments", "🏢", "دپارتمان‌ها", UIHelper.SectionGray);
            AddButton(catSettings, "ManageCompanies", "🏛️", "شرکت‌ها", UIHelper.SectionGray);
            AddButton(catSettings, "DatabaseBackup", "🗄️", "پشتیبان‌گیری دیتابیس", UIHelper.SectionGray);
            AddButton(catSettings, "DatabaseRestore", "♻️", "بازیابی دیتابیس", UIHelper.SectionGray);
        }

        private void ApplyRolePermissions()
        {
            if (_role == "HR")
            {
                HideButton("UserManagement"); HideButton("AuditLogs"); HideButton("SalarySettings");
                HideButton("DatabaseBackup"); HideButton("DatabaseRestore"); HideButton("TafsiliManagement");
            }
            else if (_role == "Viewer")
            {
                HideButton("SalarySettings"); HideButton("AttendanceForm"); HideButton("LeaveManagement");
                HideButton("Settlement"); HideButton("PayrollProcess"); HideButton("LoanManagement");
                HideButton("SupplementaryInsuranceManagement"); HideButton("CarInsuranceManagement");
                HideButton("YearEndBonus"); HideButton("UserManagement"); HideButton("AuditLogs");
                HideButton("DatabaseBackup"); HideButton("DatabaseRestore"); HideButton("TafsiliManagement");
                HideButton("CashFlowManagement");
            }
        }

        private void HideButton(string key)
        {
            if (_allButtons.TryGetValue(key, out var btn)) btn.Visible = false;
        }
    }
}