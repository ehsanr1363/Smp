using System;
using System.Collections.Generic;

namespace PersonnelApp.Helpers
{
    public static class TranslationHelper
    {
        public static readonly Dictionary<string, string> ColumnTranslations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // --- عمومی و شناسه‌ها ---
            { "Id", "شناسه" }, { "Code", "کد" }, { "CreatedAt", "تاریخ ثبت" }, { "UpdatedAt", "تاریخ بروزرسانی" },
            { "Year", "سال" }, { "Month", "ماه" }, { "Status", "وضعیت" }, { "Description", "توضیحات" },
            { "IsActive", "فعال" }, { "DisplayText", "متن نمایشی" },
            
            // --- اطلاعات پرسنل ---
            { "PersonnelCode", "کد پرسنلی" }, { "FirstName", "نام" }, { "LastName", "نام خانوادگی" },
            { "FullName", "نام و نام خانوادگی" }, { "FatherName", "نام پدر" }, { "NationalCode", "کد ملی" },
            { "NationalId", "شناسه ملی" }, { "BirthCertificateNumber", "شماره شناسنامه" }, { "BirthDate", "تاریخ تولد" },
            { "BirthPlace", "محل تولد" }, { "IssuancePlace", "محل صدور" }, { "MaritalStatus", "وضعیت تأهل" },
            { "MilitaryStatus", "وضعیت نظام وظیفه" }, { "Education", "تحصیلات" }, { "SpecializedField", "رسته تخصصی" },
            { "ChildrenCount", "تعداد فرزند" }, { "InsuranceNumber", "شماره بیمه" }, { "Gender", "جنسیت" },
            { "PhoneNumber", "تلفن تماس" }, { "Address", "آدرس" }, { "DepartmentName", "واحد سازمانی" },
            { "Position", "سمت" }, { "PersonnelType", "نوع پرسنلی" }, { "HireDate", "تاریخ استخدام" },
            { "ContractStartDate", "شروع قرارداد" }, { "ContractEndDate", "پایان قرارداد" }, { "ContractYear", "سال قرارداد" },
            { "ContractNumber", "شماره قرارداد" }, { "CompanyTitle", "شرکت محل کار" }, { "WorkLocation", "محل کار" },
            
            // --- تفصیلی‌ها ---
            { "PersonType", "نوع شخص" }, { "CompanyName", "نام شرکت" }, { "Type", "نقش" }, { "EconomicCode", "کد اقتصادی" },

            // --- تنظیمات حقوق ---
            { "MinimumWage", "حداقل حقوق" }, { "MinimumDailyWage", "حداقل دستمزد روزانه" }, { "InsuranceMaxDailyWage", "سقف دستمزد روزانه بیمه" },
            { "ChildAllowanceRate", "نرخ حق اولاد" }, { "TaxExemptionLimit", "سقف معافیت مالیاتی" },
            { "InsuranceEmployeeRate", "نرخ بیمه کارمند" }, { "InsuranceEmployerRate", "نرخ بیمه کارفرما" },

            // --- پله‌های مالیات ---
            { "FromAmount", "از مبلغ (ریال)" }, { "ToAmount", "تا مبلغ (ریال)" }, { "TaxRate", "نرخ مالیات" },

            // --- مزایا و حقوق ---
            { "BaseSalary", "حقوق پایه" }, { "YearsBonus", "پایه سنوات" }, { "AttractionAllowance", "حق جذب" },
            { "ResponsibilityAllowance", "حق مسئولیت" }, { "HousingAllowance", "حق مسکن" }, { "GroceryAllowance", "حق خواربار" },
            { "ChildAllowance", "حق اولاد" }, { "ManagementAllowance", "حق مدیریت" }, { "TechnicalAllowance", "حق فنی" },
            { "MarriageAllowance", "حق تأهل" }, { "OtherAllowances", "سایر مزایا" }, { "TransportationAllowance", "حق ایاب و ذهاب" },
            { "OvertimeHours", "ساعات اضافه‌کاری" }, { "WorkingDays", "روزهای کارکرد" }, { "HoursWorked", "ساعات کارکرد" },
            { "AbsenceDays", "روزهای غیبت" }, { "OvertimePay", "مبلغ اضافه‌کاری" }, { "DailyWage", "حقوق روزانه" },
            { "HourlyWage", "حقوق ساعتی" }, { "HasInsurance", "بیمه رد شود" }, { "Benefits", "جمع مزایا" },

            // --- کسورات و فیش حقوقی ---
            { "InsuranceEmployee", "بیمه سهم کارمند" }, { "InsuranceEmployeeShare", "بیمه سهم کارمند" }, { "InsuranceEmployer", "بیمه سهم کارفرما" },
            { "IncomeTax", "مالیات بر درآمد" }, { "Tax", "مالیات" }, { "AbsenceDeduction", "کسر غیبت" },
            { "LoanDeduction", "کسر وام" }, { "SupplementaryInsuranceDeduction", "بیمه تکمیلی" }, { "CarInsuranceDeduction", "بیمه خودرو" },
            { "OtherDeductions", "سایر کسورات" }, { "TotalEarnings", "جمع مزایا" }, { "GrossSalary", "جمع حقوق و مزایا" },
            { "TotalDeductions", "جمع کسورات" }, { "NetPay", "خالص پرداختی" }, { "NetPayInWords", "خالص پرداختی (به حروف)" },
            { "CalculatedAt", "تاریخ محاسبه" }, { "TotalGross", "جمع کل حقوق" }, { "TotalInsurance", "جمع بیمه" },
            { "TotalTax", "جمع مالیات" }, { "NetSettlement", "خالص تسویه" },

            // --- کارکرد (Attendance) ---
            { "HolidayDays", "روزهای تعطیل" }, { "EarlyOvertime", "اضافه کار اول وقت" }, { "TotalDays", "جمع کارکرد (روز)" },
            { "ExpectedHours", "موظفی (ساعت)" }, { "NormalDayRate", "روز عادی (ساعت)" }, { "HolidayDayRate", "روز تعطیل (ساعت)" },
            { "TotalPresence", "حضور کل (ساعت)" }, { "NetOvertime", "اضافه کار (کسر کار)" }, { "GrossOvertime", "اضافه کار ناخالص" },
            { "PayableOvertime", "اضافه کار قابل پرداخت" }, { "OtherOvertime", "سایر اضافه کار" }, { "HourlyLeave", "مرخصی ساعتی" },
            { "DailyLeave", "مرخصی روزانه" }, { "AbsenceHours", "کسر کار (ساعت)" }, { "LunchDays", "روزهای نهاری" },
            
            // 👇 فیلدهای رشته‌ای (Str) که در جدول نمایش داده می‌شوند 👇
            { "TotalDaysStr", "جمع کارکرد (روز)" },
            { "ExpectedHoursStr", "موظفی (ساعت)" },
            { "TotalPresenceStr", "حضور کل (ساعت)" },
            { "EarlyOvertimeStr", "اضافه کار اول وقت" },
            { "NetOvertimeStr", "اضافه کار (کسر کار)" },
            { "GrossOvertimeStr", "اضافه کار ناخالص" },
            { "AbsenceHoursStr", "کسر کار (ساعت)" },
            { "PayableOvertimeStr", "اضافه کار قابل پرداخت" },
            { "OtherOvertimeStr", "سایر اضافه کار" },

            // --- مرخصی ---
            { "LeaveType", "نوع مرخصی" }, { "StartDate", "تاریخ شروع" }, { "EndDate", "تاریخ پایان" }, { "DaysCount", "تعداد روز" },

            // --- وام و مساعده ---
            { "TotalAmount", "مبلغ کل وام" }, { "MonthlyInstallment", "مبلغ قسط" }, { "RemainingAmount", "مانده وام" },
            { "LoanDate", "تاریخ ثبت وام" }, { "EmployeeName", "نام کارمند" },

            // --- تسویه حساب و عیدی ---
            { "YearsOfWork", "سال‌های کارکرد" }, { "ServiceBonus", "مبلغ سنوات" }, { "LeaveBonus", "بازخرید مرخصی" },
            { "SeverancePay", "عیدی/پاداش پایان سال" }, { "RemainingLoan", "مانده وام" }, { "MonthsWorked", "ماه‌های کارکرد" },
            { "BonusAmount", "مبلغ عیدی" }, { "NetBonus", "خالص عیدی" },

            // --- کاربران و لاگ ---
            { "Username", "نام کاربری" }, { "Role", "سطح دسترسی" }, { "ActionType", "نوع عملیات" },
            { "ModuleName", "بخش سیستم" }, { "LogDate", "تاریخ و زمان" },

            // --- بیمه و بانک ---
            { "ShebaNumber", "شماره شبا" }, { "BankName", "نام بانک" }, { "PaymentDate", "تاریخ پرداخت" },
            { "PaymentType", "نوع پرداخت" }, { "Amount", "مبلغ" }, { "GuaranteeNumber", "شماره ضمانت‌نامه" },
            { "MaturityDate", "تاریخ سررسید" }, { "IssueDate", "تاریخ صدور" },
            
            // --- قراردادها ---
            { "PartyName", "نام طرف قرارداد" }, { "Relation", "نسبت" }, { "HasSupplementaryInsurance", "بیمه تکمیلی" },
            { "WorkshopCode", "کد کارگاه" }, { "ContractRow", "ردیف پیمان" }, { "Subject", "موضوع" },
            { "ContractDate", "تاریخ قرارداد" }, { "GuaranteePercentage", "درصد تضمین" }, { "RetentionPercentage", "درصد حسن انجام کار" },
            { "InsurancePercentage", "درصد بیمه" }, { "AdvancePaymentPercentage", "درصد پیش‌پرداخت" }, { "TotalAdvanceAmount", "مبلغ کل پیش‌پرداخت" },
            { "VatPercentage", "درصد ارزش افزوده" },
            
            // --- صورت وضعیت ---
            { "StatementNumber", "شماره صورت‌وضعیت" }, { "StatementDate", "تاریخ صورت‌وضعیت" }, { "GrossAmount", "مبلغ کارکرد" },
            { "RetentionDeduction", "کسر حسن انجام کار" }, { "InsuranceDeduction", "کسر بیمه" }, { "AdvanceRecovery", "کسر پیش‌پرداخت" },
            { "NetPayableAmount", "مبلغ خالص" }
        };

        public static readonly string[] MonthNames = { "", "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };

        public static readonly Dictionary<string, string> StatusTranslations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        { { "Pending", "در انتظار" }, { "Approved", "تایید شده" }, { "Rejected", "رد شده" }, { "Active", "فعال" }, { "Inactive", "غیرفعال" }, { "Calculated", "محاسبه شده" }, { "Paid", "پرداخت شده" } };

        public static readonly Dictionary<string, string> RoleTranslations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        { { "Admin", "مدیر سیستم" }, { "HR", "کارشناس منابع انسانی" }, { "Viewer", "بازدیدکننده" } };

        public static string GetColumnName(string englishName)
        {
            if (string.IsNullOrWhiteSpace(englishName)) return englishName;
            return ColumnTranslations.ContainsKey(englishName) ? ColumnTranslations[englishName] : englishName;
        }

        public static string GetMonthName(int month) => (month >= 1 && month <= 12) ? MonthNames[month] : month.ToString();
        public static string GetStatusName(string status) => string.IsNullOrWhiteSpace(status) ? status : (StatusTranslations.ContainsKey(status) ? StatusTranslations[status] : status);
        public static string GetRoleName(string role) => string.IsNullOrWhiteSpace(role) ? role : (RoleTranslations.ContainsKey(role) ? RoleTranslations[role] : role);
    }
}