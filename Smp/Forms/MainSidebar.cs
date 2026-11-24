using Smp.Data;
using Smp.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Smp.Forms;

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
            catch (Exception __lex)
            {
                Smp.Helpers.LogHelper.LogException(__lex, "MainSidebar.GetAllowedMenus");   // بدون لاگ، کاربر فقط منوی خالی می‌دید
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
        this.BackColor = ModernSidebar.Back;

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent, Padding = new Padding(5) };
        mainFlow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
        scroll.Controls.Add(mainFlow);
        this.Controls.Add(scroll);
    }

    private FlowLayoutPanel AddCategory(string title, string icon, Color accentColor, bool isExpandedByDefault = false)
    {
        var header = ModernSidebar.CreateAccordionHeader(title, icon, accentColor, isExpandedByDefault, out Label chevron);
        var container = ModernSidebar.CreateAccordionContainer(isExpandedByDefault);

        Action toggle = () =>
        {
            bool willOpen = !container.Visible;
            foreach (Control ctrl in mainFlow.Controls)
            {
                if (ctrl is FlowLayoutPanel flp && flp != container) flp.Visible = false;
                if (ctrl is Panel p && p != header)
                {
                    var l = p.Controls.OfType<Label>().FirstOrDefault(x => x.Text == "▼" || x.Text == "▶");
                    if (l is not null) l.Text = "▶";
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
        var btn = ModernSidebar.CreateAccordionButton(key, icon, text, accentColor);
        btn.Click += (s, e) => MenuClicked?.Invoke(key);
        parent.Controls.Add(btn);
        _allButtons[key] = btn;
    }

    private void BuildMenu()
    {
        var catDashboards = AddCategory("داشبوردهای مدیریتی", "📊", ModernTheme.Purple);
        AddButton(catDashboards, "ManagementDashboard", "📊", "داشبورد مدیریتی (پرسنل)", ModernTheme.Purple);
        AddButton(catDashboards, "ContractsDashboard", "📄", "داشبورد مدیریت قراردادها", ModernTheme.Orange);
        AddButton(catDashboards, "LiquidityDashboard", "💵", "داشبورد تحلیلی نقدینگی", ModernTheme.Warning);
        AddButton(catDashboards, "PayrollSummary", "💹", "داشبورد مالی و بیمه", ModernTheme.Success);
        AddButton(catDashboards, "GraphicalDashboard", "📈", "داشبورد گرافیکی", ModernTheme.Success);
        AddButton(catDashboards, "SalaryForecast", "📉", "پیش‌بینی حقوق", ModernTheme.Success);
        AddButton(catDashboards, "ProfitLoss", "💰", "داشبورد سود و زیان", ModernTheme.Teal);
        AddButton(catDashboards, "CashFlowForecast", "📈", "پیش‌بینی جریان نقدی", ModernTheme.Primary);
        AddButton(catDashboards, "GlobalSearch", "🔍", "جستجوی سراسری مالی", ModernTheme.Danger);
        AddButton(catDashboards, "UnbilledExpenses", "🧾", "گزارش هزینه‌های صورت وضعیت نشده", ModernTheme.Warning);
        AddButton(catDashboards, "GlobalReport", "📊", "داشبورد جامع قراردادها", ModernTheme.Teal);
        AddButton(catDashboards, "GlobalGuarantees", "🏦", "گزارش جامع ضمانت‌نامه‌ها", ModernTheme.Teal);

        var catPersonnel = AddCategory("مدیریت پرسنل", "👥", ModernTheme.Primary);
        AddButton(catPersonnel, "PersonnelList", "📋", "لیست پرسنل", ModernTheme.Primary);

        var catLiquidity = AddCategory("مدیریت نقدینگی", "💵", ModernTheme.Warning);
        AddButton(catLiquidity, "QuickClientReceipt", "📥", "ثبت دریافت از کارفرما", ModernTheme.Success);
        AddButton(catLiquidity, "ClientReceiptList", "📋", "لیست دریافتی‌های کارفرما", ModernTheme.Success);
        AddButton(catLiquidity, "CashFlowManagement", "📝", "مدیریت تراکنش‌ها و چک‌ها", ModernTheme.Orange);
        AddButton(catLiquidity, "CashTransaction", "💸", "ثبت حواله / نقدی", ModernTheme.Warning);
        AddButton(catLiquidity, "BulkCashTransaction", "📑", "ثبت گروهی حواله‌ها", ModernTheme.Orange);
        AddButton(catLiquidity, "CashCheckEdit", "📄", "ثبت چک جدید", ModernTheme.Orange);
        AddButton(catLiquidity, "CashTransactionList", "📋", "لیست تراکنش‌های نقدی", ModernTheme.Orange);
        AddButton(catLiquidity, "CashCheckList", "🧾", "لیست و ویرایش چک‌ها", ModernTheme.Orange);
        AddButton(catLiquidity, "AccountTransfer", "🔄", "انتقال بین حساب‌ها", ModernTheme.Teal);
        AddButton(catLiquidity, "BankFee", "💸", "ثبت کارمزد بانکی", ModernTheme.Danger);
        AddButton(catLiquidity, "BankInterest", "💰", "ثبت سود بانکی", ModernTheme.Success);
        AddButton(catLiquidity, "OtherReceipt", "📥", "ثبت دریافت متفرقه", ModernTheme.Success);
        AddButton(catLiquidity, "BankAccounts", "🏦", "مدیریت حساب‌های بانکی", ModernTheme.Warning);

        // ── بخش مستقل تنخواه ──
        var catTankhah = AddCategory("تنخواه", "👛", ModernTheme.Warning);
        AddButton(catTankhah, "TankhahManagement", "💰", "مدیریت تنخواه‌ها", ModernTheme.Warning);
        AddButton(catTankhah, "TankhahCharge", "💳", "شارژ / انتقال به تنخواه", ModernTheme.Warning);
        AddButton(catTankhah, "TankhahSettle", "↩️", "تسویه تنخواه (برگشت به بانک)", ModernTheme.Warning);
        AddButton(catTankhah, "TankhahPerformanceReport", "📊", "گزارش عملکرد تنخواه", ModernTheme.Success);

        // ── بخش اموال ──
        var catAssets = AddCategory("اموال", "🏷️", ModernTheme.Purple);
        AddButton(catAssets, "AssetManagement", "🏷️", "مدیریت اموال", ModernTheme.Purple);

        var catContracts = AddCategory("قراردادها", "📄", ModernTheme.Orange);
        AddButton(catContracts, "ClientContracts", "🏢", "قراردادهای کارفرما", ModernTheme.Orange);
        AddButton(catContracts, "ContractorContracts", "📝", "قراردادهای پیمانکار", ModernTheme.Orange);
        AddButton(catContracts, "PartyManagement", "👥", "مدیریت طرفین قرارداد", ModernTheme.Orange);
        AddButton(catContracts, "ContractImport", "📥", "ایمپورت قراردادها از اکسل", ModernTheme.Orange);
        AddButton(catContracts, "ContractTemplateEdit", "📝", "ویرایش متن چاپی قرارداد", ModernTheme.Orange);
        AddButton(catContracts, "BatchContractPrint", "🖨️", "چاپ گروهی قراردادهای پرسنل", ModernTheme.Orange);

        var catPayroll = AddCategory("حقوق و دستمزد", "💰", ModernTheme.Success);
        AddButton(catPayroll, "SalarySettings", "💰", "تنظیمات حقوق سالانه", ModernTheme.Success);
        AddButton(catPayroll, "AttendanceForm", "📅", "ثبت کارکرد ماهانه", ModernTheme.Success);
        AddButton(catPayroll, "LeaveManagement", "🏖️", "مدیریت مرخصی‌ها", ModernTheme.Success);
        AddButton(catPayroll, "MissionManagement", "🚗", "ماموریت و حق ماموریت", ModernTheme.Success);
        AddButton(catPayroll, "Settlement", "📄", "تسویه حساب پرسنل", ModernTheme.Success);
        AddButton(catPayroll, "PayrollProcess", "📊", "پردازش و محاسبه حقوق", ModernTheme.Success);
        AddButton(catPayroll, "PayrollHistory", "📑", "تاریخچه و گزارش حقوق", ModernTheme.Success);
        AddButton(catPayroll, "LoanManagement", "🏦", "مدیریت وام و مساعده", ModernTheme.Success);
        AddButton(catPayroll, "SupplementaryInsuranceManagement", "🩺", "مدیریت بیمه تکمیلی", ModernTheme.Success);
        AddButton(catPayroll, "CarInsuranceManagement", "🚗", "مدیریت بیمه خودرو", ModernTheme.Success);
        AddButton(catPayroll, "BankPayment", "🏧", "فایل واریز بانک", ModernTheme.Success);
        AddButton(catPayroll, "YearEndBonus", "🎁", "محاسبه عیدی و پاداش", ModernTheme.Success);
        AddButton(catPayroll, "DecreeSalaryManagement", "✍️", "ورود مبالغ حکم حقوقی", ModernTheme.Primary);
        AddButton(catPayroll, "PersonnelDecree", "📜", "صدور حکم حقوقی", ModernTheme.Success);
        AddButton(catPayroll, "ReversePayroll", "🧮", "مهندسی معکوس حقوق", ModernTheme.Brand);

        var catReports = AddCategory("گزارش‌ها و چاپ", "🖨️", ModernTheme.Teal);
        AddButton(catReports, "PayrollReports", "🧾", "گزارش لیست حقوق و فیش", ModernTheme.Primary);
        AddButton(catReports, "ReportBuilder", "🛠️", "گزارش‌ساز سفارشی", ModernTheme.Teal);
        AddButton(catReports, "BatchPayslip", "🖨️", "چاپ گروهی فیش‌ها", ModernTheme.Teal);
        AddButton(catReports, "ExcelComparison", "📊", "مقایسه دو فایل اکسل", ModernTheme.Success);

        var catOfficial = AddCategory("گزارشات رسمی", "📋", ModernTheme.Dark);
        AddButton(catOfficial, "InsuranceSummaryReport", "📊", "خلاصه بیمه پرداختی", ModernTheme.Dark);
        AddButton(catOfficial, "NonPersonnelInsuranceReport", "🧾", "بیمه افراد غیرفعال", ModernTheme.Dark);
        AddButton(catOfficial, "OfficialInsurance", "📋", "لیست رسمی بیمه", ModernTheme.Dark);
        AddButton(catOfficial, "OfficialTax", "🧮", "لیست رسمی مالیات", ModernTheme.Dark);
        AddButton(catOfficial, "ImportCombinedDbf", "📥", "ایمپورت همزمان هدر و جزئیات بیمه", ModernTheme.Dark);
        AddButton(catOfficial, "ClearInsuranceData", "🗑️", "پاک‌سازی داده‌های بیمه", ModernTheme.Danger);

        var catSettings = AddCategory("تنظیمات", "⚙️", ModernTheme.Neutral);
        AddButton(catSettings, "TafsiliManagement", "🔢", "مدیریت تفصیلی‌ها", ModernTheme.Neutral);
        AddButton(catSettings, "UserManagement", "👤", "مدیریت کاربران", ModernTheme.Neutral);
        AddButton(catSettings, "RolePermissions", "🔐", "مدیریت سطوح دسترسی", ModernTheme.Neutral);
        AddButton(catSettings, "AuditLogs", "📜", "مشاهده لاگ تغییرات", ModernTheme.Neutral);
        AddButton(catSettings, "ManageLookups", "⚙️", "مدیریت Lookups", ModernTheme.Neutral);
        AddButton(catSettings, "ManageDepartments", "🏢", "دپارتمان‌ها", ModernTheme.Neutral);
        AddButton(catSettings, "ManageCompanies", "🏛️", "شرکت‌ها", ModernTheme.Neutral);
        AddButton(catSettings, "DatabaseBackup", "🗄️", "پشتیبان‌گیری دیتابیس", ModernTheme.Neutral);
        AddButton(catSettings, "DatabaseRestore", "♻️", "بازیابی دیتابیس", ModernTheme.Neutral);
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
                if (ctrl is Button btn && btn.Tag is not null && _allowedMenus.Contains(btn.Tag.ToString()!))
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
