using System;
using System.Collections.Generic;

namespace SMP.Helpers
{
    public static class TranslationHelper
    {
        public static readonly Dictionary<string, string> ColumnTranslations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // --- عمومی و شناسه‌ها ---
            { "Id", "شناسه" },
            { "Identifier", "شناسه" },
            { "Code", "کد" },
            { "Name", "نام / شرکت" },
            { "FullName", "نام و نام خانوادگی" },
            { "CreatedAt", "تاریخ ثبت" },
            { "UpdatedAt", "تاریخ بروزرسانی" },
            { "Year", "سال" },
            { "Month", "ماه" },
            { "Status", "وضعیت" },
            { "Description", "توضیحات" },
            { "IsActive", "فعال" },
            { "DisplayName", "نام طرف قرارداد" },
            { "Value", "عنوان / مقدار" },
            { "Age", "سن" },
            { "AgeNumber", "سن (عدد)" },
            { "Date", "تاریخ" },
            { "DateStr", "تاریخ" },
            { "TableName", "نام جدول" },
            { "Phone", "تلفن تماس" },
            { "Title", "عنوان" },
            { "InitialBalance", "موجودی اولیه" },
            { "CurrentBalance", "موجودی فعلی" },
            { "TotalSpent", "مجموع پرداختی" },
            { "Scope", "نوع پرداخت" },
            { "Contract", "قرارداد / پروژه" },
            { "Tafsili", "طرف حساب" },
            { "TankhahNumber", "شماره تنخواه" },
            { "ReceiptDate", "تاریخ دریافت" },
            { "ReceiptType", "روش دریافت" },
            { "MenuTitle", "عنوان منو / بخش" },
            { "IsAllowed", "دسترسی دارد؟" },
            { "ClientAmount", "مبلغ قرارداد" },
            { "TotalCosts", "جمع هزینه‌ها" },
            { "RecordCount", "تعداد" },

            // 👈 کلمات جدید مربوط به تنخواه که اضافه شدند
            { "TotalCharged", "کل پول گرفته شده (شارژ)" },
            { "TotalExpenses", "جمع هزینه‌ها" },
            { "TotalIncomes", "دریافتی‌های دیگر" },
            { "TotalSettled", "تسویه شده" },
            { "TrueBalance", "موجودی واقعی" },
            { "SourceBank", "بانک مبدا" },
            { "TankhahName", "تنخواه" },
            { "Type", "نوع" },
            { "Amount", "مبلغ" },

            // --- اطلاعات پرسنل ---
            { "PersonnelCode", "کد پرسنلی" },
            { "FirstName", "نام" },
            { "LastName", "نام خانوادگی" },
            { "FatherName", "نام پدر" },
            { "NationalCode", "کد ملی" },
            { "NationalId", "شناسه ملی" },
            { "BirthCertificateNumber", "شماره شناسنامه" },
            { "BirthDate", "تاریخ تولد" },
            { "BirthPlace", "محل تولد" },
            { "IssuancePlace", "محل صدور" },
            { "MaritalStatus", "وضعیت تأهل" },
            { "MilitaryStatus", "وضعیت نظام وظیفه" },
            { "Education", "تحصیلات" },
            { "SpecializedField", "رسته تخصصی" },
            { "ChildrenCount", "تعداد فرزند" },
            { "InsuranceNumber", "شماره بیمه" },
            { "Gender", "جنسیت" },
            { "PhoneNumber", "تلفن تماس" },
            { "Address", "آدرس" },
            { "DepartmentName", "واحد سازمانی" },
            { "Position", "سمت" },
            { "PersonnelType", "نوع پرسنلی" },
            { "HireDate", "تاریخ استخدام" },
            { "WorkLocation", "محل خدمت" },
            { "DailyWageCategory", "دسته‌بندی روزمزد" },
            { "DepartmentAddress", "آدرس واحد سازمانی" },
            { "CompanyAddress", "آدرس شرکت" },
            { "CompanyName", "نام شرکت" },
            { "CompanyTitle", "نام شرکت" },
            { "AccountType", "نوع حساب" },

            // --- قرارداد ---
            { "ContractStartDate", "شروع قرارداد" },
            { "ContractEndDate", "پایان قرارداد" },
            { "ContractYear", "سال قرارداد" },
            { "ContractNumber", "شماره قرارداد" },
            { "ContractRow", "ردیف پیمان" },
            { "ContractDate", "تاریخ قرارداد" },
            { "ContractType", "نوع قرارداد" },
            { "ContractStatus", "وضعیت قرارداد" },
            { "ClientContractNumber", "شماره قرارداد کارفرما" },
            { "ContractId", "شناسه قرارداد" },
            { "ContractTitle", "عنوان قرارداد / پروژه" },

            // --- تفصیلی‌ها و طرفین ---
            { "PersonType", "نوع شخص" },
            { "EconomicCode", "کد اقتصادی" },
            { "IsClient", "کارفرما" },
            { "IsContractor", "پیمانکار" },
            { "PartyType", "نوع طرف" },
            { "TafsiliCode", "کد تفصیلی" },
            { "TafsiliName", "نام طرف حساب" },
            { "TafsiliTitle", "عنوان تفصیلی" },
            { "IsMainAccount", "حساب اصلی" },

            // --- تنظیمات حقوق ---
            { "MinimumWage", "حداقل حقوق" },
            { "MinimumDailyWage", "حداقل دستمزد روزانه" },
            { "InsuranceMaxDailyWage", "سقف دستمزد روزانه بیمه" },
            { "ChildAllowanceRate", "نرخ حق اولاد" },
            { "TaxExemptionLimit", "سقف معافیت مالیاتی" },
            { "InsuranceEmployeeRate", "نرخ بیمه کارمند" },
            { "InsuranceEmployerRate", "نرخ بیمه کارفرما" },

            // --- پله‌های مالیات ---
            { "FromAmount", "از مبلغ (ریال)" },
            { "ToAmount", "تا مبلغ (ریال)" },
            { "TaxRate", "نرخ مالیات" },

            // --- مزایا و حقوق ---
            { "Key", "کلید" },
            { "BaseSalary", "حقوق پایه" },
            { "YearsBonus", "پایه سنوات" },
            { "AttractionAllowance", "حق جذب" },
            { "ResponsibilityAllowance", "حق مسئولیت" },
            { "HousingAllowance", "حق مسکن" },
            { "GroceryAllowance", "حق خواربار" },
            { "ChildAllowance", "حق اولاد" },
            { "ManagementAllowance", "حق مدیریت" },
            { "TechnicalAllowance", "حق فنی" },
            { "MarriageAllowance", "حق تأهل" },
            { "OtherAllowances", "سایر مزایا" },
            { "TransportationAllowance", "حق ایاب و ذهاب" },
            { "OvertimeHours", "ساعات اضافه‌کاری" },
            { "WorkingDays", "روزهای کارکرد" },
            { "HoursWorked", "ساعات کارکرد" },
            { "AbsenceDays", "روزهای غیبت" },
            { "OvertimePay", "مبلغ اضافه‌کاری" },
            { "DailyWage", "حقوق روزانه" },
            { "HourlyWage", "حقوق ساعتی" },
            { "HasInsurance", "بیمه رد شود" },
            { "IsTaxable", "مشمول مالیات" },
            { "IsInsurable", "مشمول بیمه" },
            { "Benefits", "جمع مزایا" },

            // --- کسورات و فیش حقوقی ---
            { "InsuranceEmployee", "بیمه سهم کارمند" },
            { "InsuranceEmployeeShare", "بیمه سهم کارمند" },
            { "InsuranceEmployer", "بیمه سهم کارفرما" },
            { "IncomeTax", "مالیات بر درآمد" },
            { "Tax", "مالیات" },
            { "AbsenceDeduction", "کسر غیبت" },
            { "LoanDeduction", "کسر وام" },
            { "SupplementaryInsuranceDeduction", "بیمه تکمیلی" },
            { "CarInsuranceDeduction", "بیمه خودرو" },
            { "OtherDeductions", "سایر کسورات" },
            { "TotalEarnings", "جمع مزایا" },
            { "GrossSalary", "جمع حقوق و مزایا" },
            { "TotalDeductions", "جمع کسورات" },
            { "NetPay", "خالص پرداختی" },
            { "NetPayInWords", "خالص پرداختی (به حروف)" },
            { "CalculatedAt", "تاریخ محاسبه" },
            { "TotalGross", "جمع کل حقوق" },
            { "TotalInsurance", "جمع بیمه" },
            { "TotalTax", "جمع مالیات" },
            { "NetSettlement", "خالص تسویه" },

            // --- کارکرد (Attendance) ---
            { "HolidayDays", "روزهای تعطیل" },
            { "EarlyOvertime", "اضافه کار اول وقت" },
            { "EarlyOvertimeStr", "اضافه کار اول وقت" },
            { "TotalDays", "جمع کارکرد (روز)" },
            { "ExpectedHours", "موظفی (ساعت)" },
            { "ExpectedHoursStr", "موظفی (ساعت)" },
            { "NormalDayRate", "روز عادی (ساعت)" },
            { "HolidayDayRate", "روز تعطیل (ساعت)" },
            { "TotalPresence", "حضور کل (ساعت)" },
            { "TotalPresenceStr", "حضور کل (ساعت)" },
            { "NetOvertime", "اضافه کار (کسر کار)" },
            { "NetOvertimeStr", "اضافه کار (کسر کار)" },
            { "GrossOvertime", "اضافه کار ناخالص" },
            { "GrossOvertimeStr", "اضافه کار ناخالص" },
            { "PayableOvertime", "اضافه کار قابل پرداخت" },
            { "PayableOvertimeStr", "اضافه کار قابل پرداخت" },
            { "OtherOvertime", "سایر اضافه کار" },
            { "OtherOvertimeStr", "سایر اضافه کار" },
            { "HourlyLeave", "مرخصی ساعتی" },
            { "DailyLeave", "مرخصی روزانه" },
            { "AbsenceHours", "کسر کار (ساعت)" },
            { "AbsenceHoursStr", "کسر کار (ساعت)" },
            { "LunchDays", "روزهای نهاری" },

            // --- مرخصی ---
            { "LeaveType", "نوع مرخصی" },
            { "StartDate", "تاریخ شروع" },
            { "EndDate", "تاریخ پایان" },
            { "DaysCount", "تعداد روز" },

            // --- وام و مساعده ---
            { "TotalAmount", "مبلغ کل" },
            { "MonthlyInstallment", "مبلغ قسط" },
            { "RemainingAmount", "مانده" },
            { "LoanDate", "تاریخ ثبت وام" },
            { "EmployeeName", "نام کارمند" },
            { "LoanType", "نوع" },
            { "StartPeriod", "شروع کسر" },

            // --- تسویه حساب و عیدی ---
            { "YearsOfWork", "سال‌های کارکرد" },
            { "ServiceBonus", "مبلغ سنوات" },
            { "LeaveBonus", "بازخرید مرخصی" },
            { "SeverancePay", "عیدی/پاداش پایان سال" },
            { "RemainingLoan", "مانده وام" },
            { "MonthsWorked", "ماه‌های کارکرد" },
            { "BonusAmount", "مبلغ عیدی" },
            { "NetBonus", "خالص عیدی" },

            // --- کاربران و لاگ ---
            { "Username", "نام کاربری" },
            { "Role", "سطح دسترسی" },
            { "ActionType", "نوع عملیات" },
            { "ModuleName", "بخش سیستم" },
            { "LogDate", "تاریخ و زمان" },

            // --- بیمه و بانک ---
            { "ShebaNumber", "شماره شبا" },
            { "BankName", "نام بانک" },
            { "PaymentDate", "تاریخ پرداخت" },
            { "PaymentType", "نوع پرداخت" },
            { "GuaranteeNumber", "شماره ضمانت‌نامه" },
            { "MaturityDate", "تاریخ سررسید" },
            { "IssueDate", "تاریخ صدور" },
            { "AccountNumber", "شماره حساب" },
            { "AccountTitle", "عنوان حساب" },
            { "IsCashDesk", "صندوق" },
            { "HasCheckbook", "دسته چک" },
            { "BankCode", "کد بانک" },
            { "RecipientSheba", "شماره شبا گیرنده" },
            { "RecipientNationalId", "کد/شناسه ملی گیرنده" },

            // --- تراکنش‌های نقدی و حواله‌ها ---
            { "Direction", "جهت" },
            { "TransactionType", "روش پرداخت" },
            { "Category", "دسته‌بندی" },
            { "CostType", "نوع هزینه" },
            { "AccountId", "شناسه حساب" },

            // --- چک‌ها و دسته چک ---
            { "CheckNumber", "شماره چک" },
            { "CheckType", "نوع چک" },
            { "SayadNumber", "شناسه صیاد" },
            { "RecipientName", "نام گیرنده" },
            { "IsLegalEntity", "شخص حقوقی" },
            { "DueDate", "تاریخ سررسید" },
            { "SerialNumber", "سریال دسته چک" },
            { "StartSayadNumber", "شناسه صیاد چک اول" },
            { "StartCheckNumber", "شماره چک اول" },
            { "PageCount", "تعداد برگ" },
            { "EndCheckNumber", "شماره چک آخر" },
            { "EndSayadNumber", "شناسه صیاد آخر" },
            { "RangeDisplay", "محدوده شماره چک‌ها" },
            { "SayadRangeDisplay", "محدوده صیاد" },
            { "PaymentScope", "نوع پرداخت" },
            { "Attachment", "پیوست" },

            // --- قراردادها ---
            { "PartyName", "نام طرف قرارداد" },
            { "Relation", "نسبت" },
            { "HasSupplementaryInsurance", "بیمه تکمیلی" },
            { "WorkshopCode", "کد کارگاه" },
            { "Subject", "موضوع" },
            { "GuaranteePercentage", "درصد تضمین" },
            { "RetentionPercentage", "درصد حسن انجام کار" },
            { "InsurancePercentage", "درصد بیمه" },
            { "AdvancePaymentPercentage", "درصد پیش‌پرداخت" },
            { "TotalAdvanceAmount", "مبلغ کل پیش‌پرداخت" },
            { "VatPercentage", "درصد ارزش افزوده" },

            // --- صورت وضعیت ---
            { "StatementNumber", "شماره صورت‌وضعیت" },
            { "StatementDate", "تاریخ صورت‌وضعیت" },
            { "GrossAmount", "مبلغ کارکرد" },
            { "RetentionDeduction", "کسر حسن انجام کار" },
            { "InsuranceDeduction", "کسر بیمه" },
            { "AdvanceRecovery", "کسر پیش‌پرداخت" },
            { "NetPayableAmount", "مبلغ خالص" }
        };

        public static readonly string[] MonthNames = { "", "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };

        public static readonly Dictionary<string, string> StatusTranslations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Pending", "در انتظار" }, { "Approved", "تایید شده" }, { "Rejected", "رد شده" },
            { "Active", "فعال" }, { "Inactive", "غیرفعال" }, { "Calculated", "محاسبه شده" },
            { "Paid", "پرداخت شده" }, { "InProgress", "در جریان" }
        };

        public static readonly Dictionary<string, string> RoleTranslations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Admin", "مدیر سیستم" }, { "HR", "کارشناس منابع انسانی" }, { "Viewer", "بازدیدکننده" }
        };

        public static readonly Dictionary<string, string> PaymentScopeTranslations = new(StringComparer.OrdinalIgnoreCase)
        {
            { PaymentScopes.Administrative,       "🏢 اداری" },
            { PaymentScopes.DirectPurchase,       "🏗️ خرید پروژه" },
            { PaymentScopes.InformalContractor,   "👷 پیمانکار بدون قرارداد" },
            { PaymentScopes.ContractorContract,   "📋 پیمانکار دارای قرارداد" }
        };

        public static readonly Dictionary<string, string> CheckStatusTranslations = new(StringComparer.OrdinalIgnoreCase)
        {
            { "در جریان",   "در جریان" },
            { "پاس شده",    "پاس شده" },
            { "برگشتی",     "برگشتی" },
            { "باطل شده",   "باطل شده" }
        };

        public static readonly string[] CheckStatusesList = { "در جریان", "پاس شده", "برگشتی", "باطل شده" };
        public static readonly string[] CheckTypesList = { DirectionTypes.Incoming, DirectionTypes.Outgoing };

        public static string GetColumnName(string englishName)
        {
            if (string.IsNullOrWhiteSpace(englishName)) return englishName;
            return ColumnTranslations.ContainsKey(englishName) ? ColumnTranslations[englishName] : englishName;
        }

        public static string GetMonthName(int month) => (month >= 1 && month <= 12) ? MonthNames[month] : month.ToString();

        public static string GetStatusName(string status) => string.IsNullOrWhiteSpace(status) ? status : (StatusTranslations.ContainsKey(status) ? StatusTranslations[status] : status);

        public static string GetRoleName(string role) => string.IsNullOrWhiteSpace(role) ? role : (RoleTranslations.ContainsKey(role) ? RoleTranslations[role] : role);

        public static string GetPaymentScopeName(string? scope)
        {
            if (string.IsNullOrWhiteSpace(scope)) return "---";
            return PaymentScopeTranslations.TryGetValue(scope.Trim(), out var name) ? name : "---";
        }

        public static string GetCheckStatusName(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return "---";
            return CheckStatusTranslations.TryGetValue(status.Trim(), out var name) ? name : status;
        }
    }
}