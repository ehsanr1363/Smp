using Smp.Data;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;

namespace Smp.Services;

public enum PayrollKind { Monthly, Daily, Hourly }

public class PayrollCalculatorService
{
    // کش پله‌های مالیاتی (هر سال یک‌بار از دیتابیس) تا اورلودهای ساده برای هر کارمند کوئری نزنند
    private readonly Dictionary<int, List<TaxBracketDto>> _bracketCache = new();

    public PayrollCalculatorService() { }

    /// <summary>
    /// تشخیص نوع پرسنل؛ هم محاسبه‌گر و هم فرم‌ها باید فقط از همین استفاده کنند.
    /// </summary>
    public static PayrollKind ResolveKind(string? personnelType)
    {
        string type = StringHelper.NormalizePersianText(personnelType ?? "ماهانه").Replace(" ", "");
        return type switch
        {
            "روزمزد" => PayrollKind.Daily,
            "ساعتی" => PayrollKind.Hourly,
            _ => PayrollKind.Monthly
        };
    }

    public PayrollResult CalculatePayroll(Employee emp, Attendance att, SalarySettings settings, decimal loanInstallment)
        => CalculatePayroll(emp, att, settings, loanInstallment, 0, 0, 0, ResolveBrackets(settings, att.Year));

    public PayrollResult CalculatePayroll(Employee emp, Attendance att, SalarySettings settings, decimal loanInstallment, List<TaxBracketDto> taxBrackets)
        => CalculatePayroll(emp, att, settings, loanInstallment, 0, 0, 0, taxBrackets);

    public PayrollResult CalculatePayroll(Employee emp, Attendance att, SalarySettings settings, decimal loanInstallment, decimal supplementaryInsuranceInstallment, List<TaxBracketDto> taxBrackets)
        => CalculatePayroll(emp, att, settings, loanInstallment, supplementaryInsuranceInstallment, 0, 0, taxBrackets);

    public PayrollResult CalculatePayroll(
        Employee emp, Attendance att, SalarySettings settings,
        decimal loanInstallment, decimal supplementaryInsuranceInstallment,
        decimal carInsuranceInstallment, decimal otherDeductions,
        List<TaxBracketDto> taxBrackets, decimal advanceInstallment = 0m)
    {
        if (emp is null) throw new ArgumentNullException(nameof(emp));
        if (att is null) throw new ArgumentNullException(nameof(att));
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        BasePayrollCalculator calculator = ResolveKind(emp.PersonnelType) switch
        {
            PayrollKind.Daily => new DailyWagePayrollCalculator(GetDailyOvertimeMultiplier(att.Year)),
            PayrollKind.Hourly => new HourlyPayrollCalculator(),
            _ => new ContractualPayrollCalculator()
        };

        // روزمزد: طبقه‌ی کارگری از مشخصات پرسنل خوانده می‌شود و نرخ آن از «تنظیمات حقوق سالانه ← حقوق روزمزدها»
        // (همان سالِ حقوق) گرفته می‌شود؛ مقدار کپی‌شده در Employees.DailyWage فقط پشتیبان است.
        // مقدار فقط حین محاسبه جایگزین می‌شود و بعد به حالت قبل برمی‌گردد.
        decimal originalDailyWage = emp.DailyWage;
        if (ResolveKind(emp.PersonnelType) == PayrollKind.Daily)
            emp.DailyWage = ResolveDailyWage(emp, att.Year);

        try
        {
            return calculator.Calculate(emp, att, settings,
                loanInstallment, supplementaryInsuranceInstallment,
                carInsuranceInstallment, otherDeductions, taxBrackets, advanceInstallment);
        }
        finally
        {
            emp.DailyWage = originalDailyWage;
        }
    }

    // ═══════════════ مبنای سنوات و عیدی (طبق تنظیمات سال) ═══════════════

    /// ریز حقوق و مزایای یک ماه کامل (بدون تناسب روز و بدون غیبت).
    /// روزمزد = دستمزد روزانه × ۳۰، ساعتی = دستمزد ساعتی × ساعت کار ماهانه؛ این دو مزایای ماهانه ندارند.
    public PayrollResult GetFullMonthComponents(Employee emp, SalarySettings settings)
    {
        if (emp is null) throw new ArgumentNullException(nameof(emp));
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        var r = new PayrollResult { EmployeeId = emp.Id };
        switch (ResolveKind(emp.PersonnelType))
        {
            case PayrollKind.Daily:
                r.BaseSalary = ResolveDailyWage(emp, settings.Year) * 30m;
                break;
            case PayrollKind.Hourly:
                r.BaseSalary = emp.HourlyWage * (settings.MonthlyWorkingHours > 0 ? settings.MonthlyWorkingHours : 220m);
                break;
            default:
                r.BaseSalary = emp.BaseSalary;
                BasePayrollCalculator.FillAllowances(emp, settings, 1m, r);
                break;
        }
        return r;
    }

    /// جمع عوامل انتخاب‌شده در «مبنای سنوات» (یک ماه حقوق)
    public decimal GetSeveranceMonthlyBase(Employee emp, SalarySettings settings)
        => SumComponents(GetFullMonthComponents(emp, settings), settings.SanavatIncludeComponents);

    /// جمع عوامل انتخاب‌شده در «مبنای عیدی» (یک ماه حقوق)
    public decimal GetBonusMonthlyBase(Employee emp, SalarySettings settings)
        => SumComponents(GetFullMonthComponents(emp, settings), settings.EidiIncludeComponents);

    /// <summary>
    /// 🔴 متد جدید: جمع کردن مقادیر بر اساس رشته کاما-جدا شده (مثلاً "Base,Housing")
    /// </summary>
    private static decimal SumComponents(PayrollResult c, string includeComponents)
    {
        if (string.IsNullOrWhiteSpace(includeComponents))
            return 0m;

        var components = includeComponents.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                          .Select(x => x.Trim().ToLowerInvariant())
                                          .ToList();

        decimal sum = 0m;
        foreach (var comp in components)
        {
            // توجه: کلیدهای اینجا باید دقیقاً با کلیدهایی که در UI ذخیره می‌کنید یکسان باشد
            switch (comp)
            {
                case "base": sum += c.BaseSalary; break;
                case "yearsbonus": sum += c.YearsBonus; break;
                case "attraction": sum += c.AttractionAllowance; break;
                case "responsibility": sum += c.ResponsibilityAllowance; break;
                case "management": sum += c.ManagementAllowance; break;
                case "technical": sum += c.TechnicalAllowance; break;
                case "housing": sum += c.HousingAllowance; break;
                case "grocery": sum += c.GroceryAllowance; break;
                case "child": sum += c.ChildAllowance; break;
                case "marriage": sum += c.MarriageAllowance; break;
                case "transportation": sum += c.TransportationAllowance; break;
                case "other": sum += c.OtherAllowances; break;
            }
        }
        return sum;
    }

    /// <summary>
    /// دستمزد روزانه‌ی پرسنل روزمزد: طبقه‌ی کارگری (از مشخصات پرسنل) ← نرخ همان طبقه در جدول سالانه‌ی سالِ حقوق.
    /// عمداً کش نمی‌شود تا تغییر نرخ در تنظیمات بلافاصله در «محاسبه مجدد» اعمال شود.
    /// اگر طبقه یا نرخی پیدا نشود، همان emp.DailyWage برمی‌گردد.
    /// </summary>
    public decimal ResolveDailyWage(Employee emp, int year)
    {
        if (emp is null) throw new ArgumentNullException(nameof(emp));
        if (string.IsNullOrWhiteSpace(emp.DailyWageCategory)) return emp.DailyWage;

        try
        {
            string key = StringHelper.NormalizePersianText(emp.DailyWageCategory).Trim();
            var rates = new DailyWageRateRepository(AppConfig.ConnectionString).GetRatesWithFallback(year).Rates;
            foreach (var r in rates)
                if (StringHelper.NormalizePersianText(r.Category).Trim() == key && r.Amount > 0)
                    return r.Amount;
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "PayrollCalculatorService.ResolveDailyWage");
        }
        return emp.DailyWage;
    }

    // کش ضریب اضافه‌کار روزمزد (هر سال یک‌بار از جدول DailyWageYearSettings)
    private readonly Dictionary<int, decimal> _dailyMultiplierCache = new();

    /// <summary>
    /// ضریب اضافه‌کار روزمزد از جدول سالانه‌ی «تنظیمات حقوق ← روزمزد».
    /// اگر برای آن سال ثبت نشده باشد، ضریب پیش‌فرض (1.4) استفاده می‌شود.
    /// </summary>
    public decimal GetDailyOvertimeMultiplier(int year)
    {
        if (_dailyMultiplierCache.TryGetValue(year, out var cached)) return cached;

        decimal mult = BasePayrollCalculator.DefaultDailyOvertimeMultiplier;
        try
        {
            var v = new DailyWageRateRepository(AppConfig.ConnectionString).GetOvertimeMultiplier(year);
            if (v.HasValue && v.Value > 0) mult = v.Value;
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "PayrollCalculatorService.GetDailyOvertimeMultiplier");
        }

        _dailyMultiplierCache[year] = mult;
        return mult;
    }

    private List<TaxBracketDto> ResolveBrackets(SalarySettings settings, int year)
    {
        if (settings?.TaxBrackets is not null && settings.TaxBrackets.Count > 0)
            return settings.TaxBrackets;
        return GetTaxBracketsSafe(year);
    }

    /// <summary>
    /// ✅ FIX: نام قدیمی حفظ شده (کدهای دیگر صدایش می‌زنند) ولی دیگر خطا را نمی‌بلعد.
    /// اگر پله‌ای نباشد یا کوئری خطا بدهد، Exception واضح می‌دهد؛ دیگر مالیات پیش‌فرض بی‌صدا اعمال نمی‌شود.
    /// </summary>
    public List<TaxBracketDto> GetTaxBracketsSafe(int year)
    {
        if (_bracketCache.TryGetValue(year, out var cached)) return cached;

        List<TaxBracketDto> result;
        try
        {
            const string sql = "SELECT FromAmount, ToAmount, TaxRate FROM TaxBrackets WHERE Year = @Year ORDER BY FromAmount ASC";
            var dt = DbHelper.ExecuteQuery(AppConfig.ConnectionString, sql, new SqlParameter("@Year", year));
            result = new List<TaxBracketDto>(dt.Rows.Count);
            foreach (DataRow row in dt.Rows)
            {
                result.Add(new TaxBracketDto
                {
                    FromAmount = Convert.ToDecimal(row["FromAmount"]),
                    ToAmount = row["ToAmount"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(row["ToAmount"]),
                    TaxRate = Convert.ToDecimal(row["TaxRate"])
                });
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "PayrollCalculatorService.GetTaxBracketsSafe");
            throw new InvalidOperationException($"خطا در خواندن پله‌های مالیاتی سال {year}: {ex.Message}", ex);
        }

        if (result.Count == 0)
            throw new InvalidOperationException(
                $"پله‌های مالیاتی سال {year} تعریف نشده است. ابتدا از «تنظیمات حقوق» پله‌ها را وارد کنید.");

        _bracketCache[year] = result;
        return result;
    }
}

internal abstract class BasePayrollCalculator
{
    /// <summary>واحد گردکردن مالیات ماهانه؛ صفر = مالیات گرد نمی‌شود (مقدار خام محاسبه‌شده). قبلاً ۱۰ ریال بود.</summary>
    protected const decimal TaxRoundingUnit = 0m;

    // ───────── سیاست‌های قابل تنظیم (برای برگرداندن رفتار قبلی مقدارشان را عوض کنید) ─────────

    /// ضریب اضافه‌کاری قراردادی
    protected static readonly decimal OvertimeMultiplier = 1.4m;

    /// ضریب پیش‌فرض اضافه‌کاری روزمزد؛ فقط وقتی برای آن سال در جدول سالانه ضریبی ثبت نشده باشد.
    internal const decimal DefaultDailyOvertimeMultiplier = 1.4m;

    /// ساعت کار قانونی روزانه برای محاسبه نرخ ساعتی روزمزد (قبلاً هاردکد ۸ بود)
    protected static readonly decimal LegalDailyHours = 8m;

    /// آیا اضافه‌کاری در مبنای بیمه بیاید؟ (طبق مقررات تأمین اجتماعی با حسابدار چک شود؛ false = خارج از مبنا)
    protected static readonly bool InsuranceIncludesOvertime = true;

    /// اگر غیبت ساعتی و غیبت روزانه هر دو ثبت شده باشند جمع شوند؟
    /// ⚠️ اگر AbsenceHours در ورود اطلاعات حضور و غیاب، روزهای غیبت را هم شامل می‌شود، false بگذارید (رفتار قبلی).
    protected static readonly bool CombineDayAndHourAbsence = true;

    public PayrollResult Calculate(
        Employee emp, Attendance att, SalarySettings settings,
        decimal loanInstallment, decimal supplementaryInsuranceInstallment,
        decimal carInsuranceInstallment, decimal otherDeductions,
        List<TaxBracketDto> taxBrackets, decimal advanceInstallment = 0m)
    {
        if (emp is null) throw new ArgumentNullException(nameof(emp));
        if (att is null) throw new ArgumentNullException(nameof(att));
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        var result = new PayrollResult
        {
            EmployeeId = emp.Id,
            EmployeeName = emp.FullName,
            PersonnelCode = emp.PersonnelCode ?? "",
            Year = att.Year,
            Month = att.Month,
            PersonnelType = (emp.PersonnelType ?? "ماهانه").Trim(),
            DailyWage = emp.DailyWage,
            HourlyWage = emp.HourlyWage
        };

        decimal monthlyWorkingHours = settings.MonthlyWorkingHours > 0 ? settings.MonthlyWorkingHours : 220m;

        // کارکرد (برای فیش و گزارش‌ها)
        result.WorkingDays = ResolveWorkingDays(att);
        result.OvertimeHours = ResolveOvertimeHours(att);
        result.AbsenceHours = Math.Max(0, att.AbsenceHours);

        CalculateBaseSalary(emp, att, settings, result);
        CalculateAllowances(emp, att, settings, result);
        decimal overtimeBase = CalculateOvertimeBase(settings, result);
        CalculateOvertimeAndAbsence(emp, att, settings, result, overtimeBase, monthlyWorkingHours);
        CalculateTotalEarnings(result);
        CalculateInsurance(emp, settings, result);

        result.SupplementaryInsuranceDeduction = Math.Max(0, supplementaryInsuranceInstallment);
        result.CarInsuranceDeduction = Math.Max(0, carInsuranceInstallment);
        result.OtherDeductions = Math.Max(0, otherDeductions);

        CalculateTax(settings, taxBrackets, result);
        result.LoanDeduction = Math.Max(0, loanInstallment);
        result.AdvanceDeduction = Math.Max(0, advanceInstallment);

        result.TotalDeductions = result.AbsenceDeduction + result.InsuranceEmployee
                               + result.SupplementaryInsuranceDeduction + result.CarInsuranceDeduction
                               + result.OtherDeductions + result.IncomeTax + result.LoanDeduction
                               + result.AdvanceDeduction;
        result.NetPay = result.TotalEarnings - result.TotalDeductions;
        if (result.NetPay < 0) result.NetPay = 0;

        return result;
    }

    /// روزهای کارکرد برای نمایش در فیش؛ روزمزد و ساعتی همان WorkingDays ثبت‌شده را نشان می‌دهند.
    protected virtual decimal ResolveWorkingDays(Attendance att) => GetPayDays(att);

    /// ساعات اضافه‌کار قابل پرداخت (برای فیش و محاسبه). پیش‌فرض (روزمزد/ساعتی) = «اضافه کار (کسر کار)»؛
    /// برای قراردادی/ماهانه = ستون «اضافه کار قابل پرداخت» جدول کارکرد.
    protected virtual decimal ResolveOvertimeHours(Attendance att) => Math.Max(0, att.NetOvertime);

    protected abstract void CalculateBaseSalary(Employee emp, Attendance att, SalarySettings settings, PayrollResult result);
    protected abstract void CalculateOvertimeAndAbsence(Employee emp, Attendance att, SalarySettings settings, PayrollResult result, decimal overtimeBase, decimal monthlyWorkingHours);

    /// <summary>تعداد روزهای واقعی ماه شمسی: فروردین تا شهریور ۳۱، مهر تا بهمن ۳۰، اسفند ۲۹ (کبیسه ۳۰)</summary>
    protected static decimal GetMonthDays(Attendance att)
        => (att.Year > 0 && att.Month >= 1 && att.Month <= 12)
            ? WorkingDaysHelper.GetDaysInMonth(att.Year, att.Month)
            : 30;

    /// <summary>
    /// روزهای کارکرد = همان «روزهای کارکرد» ثبت‌شده در جدول حضور و غیاب همین ماه
    /// (حداکثر تعداد روزهای واقعی همان ماه). حقوق ماهانه به نسبت این روزها به روزهای واقعی ماه محاسبه می‌شود:
    /// ماه کامل = حقوق کامل، هر روز غیبت = ۱/۳۱ (یا ۱/۳۰ و ۱/۲۹ بسته به ماه) از حقوق.
    /// </summary>
    protected static decimal GetPayDays(Attendance att)
        => Math.Min(GetMonthDays(att), Math.Max(0m, att.WorkingDays));

    protected virtual void CalculateAllowances(Employee emp, Attendance att, SalarySettings settings, PayrollResult result)
    {
        decimal dayRatio = Math.Min(1m, Math.Max(0m, GetPayDays(att) / GetMonthDays(att)));
        FillAllowances(emp, settings, dayRatio, result);
    }

    /// محاسبه ریز مزایا با ضریب تناسب (۱ = ماه کامل). برای حقوق ماهانه و مبنای سنوات/عیدی مشترک است.
    internal static void FillAllowances(Employee emp, SalarySettings settings, decimal dayRatio, PayrollResult result)
    {

        result.YearsBonus = Math.Round(emp.YearsBonus * dayRatio, 0);
        result.HousingAllowance = Math.Round(settings.HousingAllowance * dayRatio, 0);
        result.GroceryAllowance = Math.Round(settings.FoodAllowance * dayRatio, 0);

        string marital = StringHelper.NormalizePersianText(emp.MaritalStatus ?? "");
        result.MarriageAllowance = (marital.Contains("متاهل") || marital.Contains("متأهل"))
            ? Math.Round(settings.MarriageAllowance * dayRatio, 0) : 0;

        decimal childRate = AsRate(settings.ChildAllowanceRate);
        result.ChildAllowance = Math.Round(settings.MinimumWage * childRate * emp.ChildrenCount * dayRatio, 0);

        result.AttractionAllowance = Math.Round(emp.AttractionAllowance * dayRatio, 0);
        result.ResponsibilityAllowance = Math.Round(emp.ResponsibilityAllowance * dayRatio, 0);
        result.ManagementAllowance = Math.Round(emp.ManagementAllowance * dayRatio, 0);
        result.TechnicalAllowance = Math.Round(emp.TechnicalAllowance * dayRatio, 0);
        result.TransportationAllowance = Math.Round(emp.TransportationAllowance * dayRatio, 0);
        result.OtherAllowances = Math.Round(emp.OtherAllowances * dayRatio, 0);
    }

    protected virtual void CalculateInsurance(Employee emp, SalarySettings settings, PayrollResult result)
    {
        if (!emp.HasInsurance) return;

        decimal insuranceBase = 0;
        if (settings.InsuranceIncludeBaseSalary) insuranceBase += result.BaseSalary;
        if (settings.InsuranceIncludeYearsBonus) insuranceBase += result.YearsBonus;
        if (settings.InsuranceIncludeAttraction) insuranceBase += result.AttractionAllowance;
        if (settings.InsuranceIncludeResponsibility) insuranceBase += result.ResponsibilityAllowance;
        if (settings.InsuranceIncludeManagement) insuranceBase += result.ManagementAllowance;
        if (settings.InsuranceIncludeTechnical) insuranceBase += result.TechnicalAllowance;
        if (settings.InsuranceIncludeHousing) insuranceBase += result.HousingAllowance;
        if (settings.InsuranceIncludeGrocery) insuranceBase += result.GroceryAllowance;
        if (settings.InsuranceIncludeChild) insuranceBase += result.ChildAllowance;
        if (settings.InsuranceIncludeMarriage) insuranceBase += result.MarriageAllowance;
        if (settings.InsuranceIncludeTransportation) insuranceBase += result.TransportationAllowance;
        if (settings.InsuranceIncludeOther) insuranceBase += result.OtherAllowances;

        // ✅ FIX: کسر غیبت از پایه حقوق، مبنای بیمه را هم کم می‌کند
        if (settings.InsuranceIncludeBaseSalary) insuranceBase -= result.AbsenceDeduction;

        FinalizeInsurance(insuranceBase, settings, result);
    }

    /// <summary>
    /// ✅ FIX: سقف مبنای بیمه (۷ برابر حداقل حقوق) حالا برای هر سه نوع پرسنل اعمال می‌شود.
    /// </summary>
    protected static void FinalizeInsurance(decimal insuranceBase, SalarySettings settings, PayrollResult result)
    {
        if (InsuranceIncludesOvertime) insuranceBase += result.OvertimePay;
        if (insuranceBase < 0) insuranceBase = 0;

        decimal cap = settings.MinimumWage * 7m;
        if (cap > 0 && insuranceBase > cap) insuranceBase = cap;

        result.InsuranceBase = Math.Round(insuranceBase, 0);
        result.InsuranceEmployee = Math.Round(insuranceBase * AsRate(settings.InsuranceEmployeeRate), 0);
        result.InsuranceEmployer = Math.Round(insuranceBase * AsRate(settings.InsuranceEmployerRate), 0);
    }

    protected virtual void CalculateTax(SalarySettings settings, List<TaxBracketDto> taxBrackets, PayrollResult result)
    {
        decimal taxBase = 0;
        if (settings.TaxIncludeBaseSalary) taxBase += result.BaseSalary;
        if (settings.TaxIncludeYearsBonus) taxBase += result.YearsBonus;
        if (settings.TaxIncludeAttraction) taxBase += result.AttractionAllowance;
        if (settings.TaxIncludeResponsibility) taxBase += result.ResponsibilityAllowance;
        if (settings.TaxIncludeManagement) taxBase += result.ManagementAllowance;
        if (settings.TaxIncludeTechnical) taxBase += result.TechnicalAllowance;
        if (settings.TaxIncludeHousing) taxBase += result.HousingAllowance;
        if (settings.TaxIncludeGrocery) taxBase += result.GroceryAllowance;
        if (settings.TaxIncludeChild) taxBase += result.ChildAllowance;
        if (settings.TaxIncludeMarriage) taxBase += result.MarriageAllowance;
        if (settings.TaxIncludeTransportation) taxBase += result.TransportationAllowance;
        if (settings.TaxIncludeOtherAllowances) taxBase += result.OtherAllowances;

        taxBase += result.OvertimePay;

        // ✅ FIX: کسر غیبت از پایه حقوق، مبنای مالیات را هم کم می‌کند
        if (settings.TaxIncludeBaseSalary) taxBase -= result.AbsenceDeduction;

        taxBase -= result.InsuranceEmployee;
        taxBase -= result.SupplementaryInsuranceDeduction;

        result.TaxableBase = Math.Round(Math.Max(0, taxBase), 0);

        if (taxBase <= 0)
        {
            result.IncomeTax = 0;
            return;
        }

        // ✅ FIX: دیگر پله‌های پیش‌فرض هاردکد جایگزین نمی‌شود؛ نبودن پله‌ها خطای واضح است.
        if (taxBrackets is null || taxBrackets.Count == 0)
            throw new InvalidOperationException(
                $"پله‌های مالیاتی سال {result.Year} در دسترس نیست؛ محاسبه مالیات ممکن نیست.");

        // اگر پله اول ۰٪ باشد، معافیت خودکار اعمال می‌شود.
        decimal annualTaxable = taxBase * 12m;
        decimal annualTax = ApplyProgressiveTax(annualTaxable, taxBrackets);
        decimal monthlyTax = annualTax / 12m;

        result.IncomeTax = TaxRoundingUnit > 0
            ? Math.Round(monthlyTax / TaxRoundingUnit, MidpointRounding.AwayFromZero) * TaxRoundingUnit
            : monthlyTax;
    }

    protected static decimal CalculateOvertimeBase(SalarySettings settings, PayrollResult result)
    {
        decimal overtimeBase = 0;
        if (settings.OvertimeIncludeBaseSalary) overtimeBase += result.BaseSalary;
        if (settings.OvertimeIncludeYearsBonus) overtimeBase += result.YearsBonus;
        if (settings.OvertimeIncludeAttraction) overtimeBase += result.AttractionAllowance;
        if (settings.OvertimeIncludeResponsibility) overtimeBase += result.ResponsibilityAllowance;
        if (settings.OvertimeIncludeManagement) overtimeBase += result.ManagementAllowance;
        if (settings.OvertimeIncludeTechnical) overtimeBase += result.TechnicalAllowance;
        if (settings.OvertimeIncludeHousing) overtimeBase += result.HousingAllowance;
        if (settings.OvertimeIncludeGrocery) overtimeBase += result.GroceryAllowance;
        if (settings.OvertimeIncludeChild) overtimeBase += result.ChildAllowance;
        if (settings.OvertimeIncludeMarriage) overtimeBase += result.MarriageAllowance;
        if (settings.OvertimeIncludeTransportation) overtimeBase += result.TransportationAllowance;
        if (settings.OvertimeIncludeOther) overtimeBase += result.OtherAllowances;
        return overtimeBase;
    }

    private static void CalculateTotalEarnings(PayrollResult result)
    {
        result.TotalEarnings = result.BaseSalary + result.YearsBonus + result.HousingAllowance + result.GroceryAllowance +
                               result.ChildAllowance + result.AttractionAllowance + result.ResponsibilityAllowance +
                               result.ManagementAllowance + result.TechnicalAllowance + result.MarriageAllowance +
                               result.TransportationAllowance + result.OtherAllowances + result.OvertimePay;
    }

    /// ✅ FIX: نرخ ۱ (و بالاتر) درصد حساب می‌شود (قبلاً دقیقاً «1» به‌عنوان ۱۰۰٪ برداشت می‌شد).
    protected static decimal AsRate(decimal rate) => rate >= 1m ? rate / 100m : rate;

    private static decimal ApplyProgressiveTax(decimal annualTaxable, List<TaxBracketDto> brackets)
    {
        if (annualTaxable <= 0 || brackets is null || brackets.Count == 0) return 0;
        var sorted = brackets.OrderBy(b => b.FromAmount).ToList();
        decimal annualTax = 0m; decimal remaining = annualTaxable;

        for (int i = 0; i < sorted.Count && remaining > 0; i++)
        {
            var b = sorted[i]; decimal rate = AsRate(b.TaxRate);
            decimal bracketSize = b.ToAmount.HasValue ? b.ToAmount.Value - b.FromAmount : decimal.MaxValue;
            if (bracketSize <= 0) continue;
            decimal amountInBracket = Math.Min(remaining, bracketSize);
            if (amountInBracket <= 0) continue;
            annualTax += amountInBracket * rate;
            remaining -= amountInBracket;
        }
        if (remaining > 0) annualTax += remaining * AsRate(sorted[sorted.Count - 1].TaxRate);
        return annualTax;
    }
}

internal sealed class ContractualPayrollCalculator : BasePayrollCalculator
{
    protected override decimal ResolveOvertimeHours(Attendance att) => Math.Max(0m, att.PayableOvertime);

    protected override void CalculateBaseSalary(Employee emp, Attendance att, SalarySettings settings, PayrollResult result)
    {
        result.BaseSalary = emp.BaseSalary;
    }

    protected override void CalculateOvertimeAndAbsence(Employee emp, Attendance att, SalarySettings settings, PayrollResult result, decimal overtimeBase, decimal monthlyWorkingHours)
    {
        decimal multiplier = settings.AbsenceMultiplier > 0 ? settings.AbsenceMultiplier : 1m;

        // غیبت ساعتی
        decimal hoursPart = 0m;
        if (att.AbsenceHours > 0)
        {
            decimal hourlyRate = emp.BaseSalary / monthlyWorkingHours;
            hoursPart = att.AbsenceHours * hourlyRate * multiplier;
        }

        // ✅ FIX: غیبت روزانه دیگر وقتی AbsenceHours>0 است نادیده گرفته نمی‌شود (با CombineDayAndHourAbsence قابل تنظیم)
        decimal daysPart = 0m;
        decimal monthDays = GetMonthDays(att);            // ۳۱ / ۳۰ / ۲۹ (یا ۳۰ در اسفند کبیسه)
        decimal missingDays = monthDays - GetPayDays(att);  // غیبت = روزهای ماه − کارکرد جدول حضور و غیاب
        if (missingDays > 0)
        {
            decimal dailyRate = emp.BaseSalary / monthDays;
            daysPart = missingDays * dailyRate * multiplier;
        }
        if (!CombineDayAndHourAbsence && hoursPart > 0) daysPart = 0m;

        // کسر غیبت از پایه حقوق بیشتر نمی‌شود
        result.AbsenceDeduction = Math.Round(Math.Min(hoursPart + daysPart, emp.BaseSalary), 0);

        // ✅ اضافه‌کار قراردادی/ماهانه = «اضافه کار قابل پرداخت» ثبت‌شده در جدول کارکرد (نه «اضافه کار (کسر کار)»)
        decimal payableOvertime = ResolveOvertimeHours(att);
        if (payableOvertime > 0)
        {
            decimal hourlyRate = overtimeBase / monthlyWorkingHours;
            result.OvertimePay = Math.Round(hourlyRate * OvertimeMultiplier * payableOvertime, 0);
        }
    }
}

internal sealed class DailyWagePayrollCalculator : BasePayrollCalculator
{
    private readonly decimal _overtimeMultiplier;

    public DailyWagePayrollCalculator(decimal overtimeMultiplier = DefaultDailyOvertimeMultiplier)
    {
        _overtimeMultiplier = overtimeMultiplier > 0 ? overtimeMultiplier : DefaultDailyOvertimeMultiplier;
    }

    /// ✅ روزمزد: روزهای قابل پرداخت = روزهای کارکرد + روزهای کارکرد در تعطیلی (همان «جمع روزها» در فرم حضور و غیاب)
    private static decimal PaidDays(Attendance att) => Math.Max(0, att.WorkingDays) + Math.Max(0, att.HolidayDays);

    protected override decimal ResolveWorkingDays(Attendance att) => PaidDays(att);

    protected override void CalculateBaseSalary(Employee emp, Attendance att, SalarySettings settings, PayrollResult result)
    {
        // ✅ FIX: قبلاً فقط WorkingDays ضرب در دستمزد روزانه می‌شد و روزهای تعطیلی کارکرده پرداخت نمی‌شد.
        // اگر سیاست شرکت «حداکثر ۳۰ روز» است، اینجا Math.Min(PaidDays(att), 30) بگذارید.
        result.BaseSalary = Math.Round(PaidDays(att) * emp.DailyWage, 0);
    }

    protected override void CalculateOvertimeAndAbsence(Employee emp, Attendance att, SalarySettings settings, PayrollResult result, decimal overtimeBase, decimal monthlyWorkingHours)
    {
        // اضافه‌کار روزمزد = (دستمزد روزانه ÷ 8) × ضریب جدول سالانه × ستون «اضافه کار (کسر کار)»
        decimal hourlyRate = emp.DailyWage / LegalDailyHours;
        decimal multiplier = settings.AbsenceMultiplier > 0 ? settings.AbsenceMultiplier : 1m;

        if (att.NetOvertime > 0)
            result.OvertimePay = Math.Round(hourlyRate * _overtimeMultiplier * att.NetOvertime, 0);
        else if (att.NetOvertime < 0)
            result.AbsenceDeduction = Math.Round(hourlyRate * Math.Abs(att.NetOvertime) * multiplier, 0);
    }

    protected override void CalculateAllowances(Employee emp, Attendance att, SalarySettings settings, PayrollResult result)
    {
        // روزمزد: بدون مزایای ماهانه
    }

    protected override void CalculateInsurance(Employee emp, SalarySettings settings, PayrollResult result)
    {
        if (!emp.HasInsurance) return;
        FinalizeInsurance(result.BaseSalary - result.AbsenceDeduction, settings, result);
    }
}

internal sealed class HourlyPayrollCalculator : BasePayrollCalculator
{
    protected override decimal ResolveWorkingDays(Attendance att) => att.WorkingDays;

    protected override void CalculateBaseSalary(Employee emp, Attendance att, SalarySettings settings, PayrollResult result)
    {
        result.BaseSalary = Math.Round(emp.HourlyWage * att.HoursWorked, 0);
    }

    protected override void CalculateOvertimeAndAbsence(Employee emp, Attendance att, SalarySettings settings, PayrollResult result, decimal overtimeBase, decimal monthlyWorkingHours)
    {
        // ساعتی: بدون اضافه‌کاری
    }

    protected override void CalculateAllowances(Employee emp, Attendance att, SalarySettings settings, PayrollResult result)
    {
        // ساعتی: بدون مزایای ماهانه
    }

    protected override void CalculateInsurance(Employee emp, SalarySettings settings, PayrollResult result)
    {
        if (!emp.HasInsurance) return;
        FinalizeInsurance(result.BaseSalary, settings, result);
    }
}
