using System.Collections.Generic;

namespace SMP.Helpers
{
    public static class MenuHelper
    {
        public static readonly Dictionary<string, string> AllMenus = new()
        {
            // ... (همان منوهای قبلی تا بخش گزارش‌ها)
            { "ManagementDashboard", "داشبورد مدیریتی (پرسنل)" },
            { "ContractsDashboard", "داشبورد مدیریت قراردادها" },
            { "LiquidityDashboard", "داشبورد تحلیلی نقدینگی" },
            { "PayrollSummary", "داشبورد مالی و بیمه" },
            { "GraphicalDashboard", "داشبورد گرافیکی" },
            { "SalaryForecast", "پیش‌بینی حقوق" },
            { "ProfitLoss", "داشبورد سود و زیان" },
            { "CashFlowForecast", "پیش‌بینی جریان نقدی (Forecast)" },
            { "GlobalSearch", "جستجوی سراسری مالی" },
            { "UnbilledExpenses", "گزارش هزینه‌های صورت وضعیت نشده" },
            { "GlobalReport", "داشبورد جامع قراردادها" },
            { "GlobalGuarantees", "گزارش جامع ضمانت‌نامه‌ها" },
            { "PersonnelList", "لیست پرسنل" },
            { "QuickClientReceipt", "ثبت دریافت از کارفرما" },
            { "ClientReceiptList", "لیست دریافتی‌های کارفرما" },
            { "CashFlowManagement", "مدیریت تراکنش‌ها و چک‌ها" },
            { "CashTransaction", "ثبت حواله / نقدی" },
            { "BulkCashTransaction", "ثبت گروهی حواله‌ها" },
            { "CashCheckEdit", "ثبت چک جدید" },
            { "CashTransactionList", "لیست تراکنش‌های نقدی" },
            { "CashCheckList", "لیست و ویرایش چک‌ها" },
            { "AccountTransfer", "انتقال بین حساب‌ها" },
            { "BankFee", "ثبت کارمزد بانکی" },
            { "BankInterest", "ثبت سود بانکی" },
            { "BankAccounts", "مدیریت حساب‌های بانکی" },
            { "BanksList", "لیست مرجع بانک‌ها" },
            { "TankhahManagement", "مدیریت تنخواه‌ها" },
            { "TankhahPerformanceReport", "گزارش عملکرد تنخواه‌ها" },
            { "ClientContracts", "قراردادهای کارفرما" },
            { "ContractorContracts", "قراردادهای پیمانکار" },
            { "PartyManagement", "مدیریت طرفین قرارداد" },
            { "ContractImport", "ایمپورت قراردادها از اکسل" },
            
            // 👈 خط جدید برای متن قرارداد
            { "ContractTemplateEdit", "ویرایش متن چاپی قرارداد" },

            { "SalarySettings", "تنظیمات حقوق سالانه" },
            { "AttendanceForm", "ثبت کارکرد ماهانه" },
            { "LeaveManagement", "مدیریت مرخصی‌ها" },
            { "Settlement", "تسویه حساب پرسنل" },
            { "PayrollProcess", "پردازش و محاسبه حقوق" },
            { "PayrollHistory", "تاریخچه و گزارش حقوق" },
            { "LoanManagement", "مدیریت وام و مساعده" },
            { "SupplementaryInsuranceManagement", "مدیریت بیمه تکمیلی" },
            { "CarInsuranceManagement", "مدیریت بیمه خودرو" },
            { "BankPayment", "فایل واریز بانک" },
            { "YearEndBonus", "محاسبه عیدی و پاداش" },
            { "DecreeSalaryManagement", "ورود مبالغ حکم حقوقی" },
            { "PersonnelDecree", "صدور حکم حقوقی" },
            { "PayrollReports", "گزارش لیست حقوق و فیش" },
            { "ReversePayroll", "مهندسی معکوس حقوق" },
            { "ReportBuilder", "گزارش‌ساز سفارشی" },
            { "BatchPayslip", "چاپ گروهی فیش‌ها" },
            { "ExcelComparison", "مقایسه دو فایل اکسل" },
            { "InsuranceSummaryReport", "خلاصه بیمه پرداختی" },
            { "NonPersonnelInsuranceReport", "بیمه افراد غیرفعال" },
            { "OfficialInsurance", "لیست رسمی بیمه" },
            { "OfficialTax", "لیست رسمی مالیات" },
            { "ImportCombinedDbf", "ایمپورت همزمان هدر و جزئیات بیمه" },
            { "ClearInsuranceData", "پاک‌سازی داده‌های بیمه" },
            { "TafsiliManagement", "مدیریت تفصیلی‌ها" },
            { "UserManagement", "مدیریت کاربران" },
            { "RolePermissions", "مدیریت سطوح دسترسی" },
            { "AuditLogs", "مشاهده لاگ تغییرات" },
            { "ManageLookups", "مدیریت Lookups" },
            { "ManageDepartments", "دپارتمان‌ها" },
            { "ManageCompanies", "شرکت‌ها" },
            { "DatabaseBackup", "پشتیبان‌گیری دیتابیس" },
            { "DatabaseRestore", "بازیابی دیتابیس" }
        };
    }
}