using Smp.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Smp.Services;

public class YearEndBonusService
{
    /// <summary>فقط پرسنل قراردادی / ماهانه مشمول عیدی و سنوات هستند (نه روزمزد و ساعتی)</summary>
    public static bool IsEligible(Employee emp)
        => PayrollCalculatorService.ResolveKind(emp.PersonnelType) == PayrollKind.Monthly;

    public YearEndBonusRecord CalculateBonus(Employee emp, int targetYear, SalarySettings settings, DateTime? settlementDate = null, int? workedDays = null, IEnumerable<EmployeeContract>? contracts = null)
    {
        // ✅ مبنای عیدی = مجموع اجزای تیک‌خورده در «تنظیمات حقوق ← مشمولین عیدی و سنوات»
        decimal bonusBase = SalaryComponents.ComputeEidiBase(emp, settings);

        var result = new YearEndBonusRecord
        {
            EmployeeId = emp.Id,
            EmployeeName = emp.FullName,
            Year = targetYear,
            BaseSalary = bonusBase
        };

        // عیدی فقط برای پرسنل «قراردادی / ماهانه» محاسبه می‌شود (روزمزد و ساعتی مشمول نیستند)
        if (!IsEligible(emp))
        {
            result.DaysWorked = 0;
            result.MonthsWorked = 0;
            result.BonusAmount = 0;
            result.Tax = 0;
            result.NetBonus = 0;
            return result;
        }

        // ✅ FIX P0: targetYear شمسی است، new DateTime(1403,1,1) اشتباه بود
        var pc = new PersianCalendar();
        DateTime yearStart = pc.ToDateTime(targetYear, 1, 1, 0, 0, 0, 0);
        int lastDay = pc.GetDaysInMonth(targetYear, 12);
        DateTime yearEnd = pc.ToDateTime(targetYear, 12, lastDay, 23, 59, 59, 0);

        // ✅ محاسبه بر اساس «تعداد روزهای واقعی کارکرد» (نه ماه گرد‌شده)
        //    روز شروع و پایان هر دو شمرده می‌شوند (شمارش شامل)؛ حداکثر = تعداد روزهای سال شمسی (۳۶۵ یا ۳۶۶)
        int daysInYear = pc.IsLeapYear(targetYear) ? 366 : 365;   // مخرج = تعداد روزهای همان سال شمسی (کبیسه ۳۶۶)

        // روزهای کارکرد = «جمع کارکرد ثبت‌شده‌ی پرسنل در حضور و غیاب همین سال» (workedDays)
        // اگر مقدار داده نشد (فراخوانی بدون حضور و غیاب)، از روی بازه‌ی قراردادها/استخدام تخمین زده می‌شود.
        int daysWorked;
        if (workedDays.HasValue)
        {
            daysWorked = Math.Max(0, Math.Min(daysInYear, workedDays.Value));
        }
        else
        {
            DateTime windowEnd = ((settlementDate.HasValue && settlementDate.Value < yearEnd) ? settlementDate.Value : yearEnd).Date;
            var periods = WorkDaysHelper.BuildPeriods(emp, contracts, yearStart, windowEnd);
            daysWorked = Math.Min(daysInYear, WorkDaysHelper.CountDays(periods));
        }

        if (daysWorked <= 0)
        {
            result.DaysWorked = 0;
            result.MonthsWorked = 0;
            result.BonusAmount = 0;
            result.NetBonus = 0;
            return result;
        }

        result.DaysWorked = daysWorked;
        // فقط جهت نمایش/سازگاری با ستون دیتابیس (معادل ماهانه‌ی روزهای کارکرد)
        result.MonthsWorked = (int)Math.Round(daysWorked * 12m / daysInYear, MidpointRounding.AwayFromZero);

        // عیدی کامل سال = ۲ برابر مبنا (مجموع اجزای تیک‌خورده)
        decimal fullBonus = bonusBase * 2m;

        // سقف = ۳ برابر حداقل حقوق ماهانه؛ اگر عیدی کامل از سقف بیشتر بود، سقف جایگزین می‌شود
        decimal maxBonus = settings.MinimumWage * 3m;
        decimal cappedFullBonus = (maxBonus > 0m) ? Math.Min(fullBonus, maxBonus) : fullBonus;

        // سپس به نسبت روزهای کارکرد: مثلاً ۳۱ روز → سقف × ۳۱ ÷ ۳۶۵
        result.BonusAmount = Math.Round(cappedFullBonus * daysWorked / daysInYear, 0);

        // مالیات: معافیت = پله‌ی اول جدول مالیات حقوق (سقف پله‌ی اول، برای یک سال کامل)
        // که ابتدا بر ۱۲ و سپس بر روزهای سال تقسیم و در روزهای کارکرد ضرب می‌شود.
        // مازاد بر معافیت با نرخ پله‌ی دوم مالیات می‌شود (اگر پله‌ی دوم نبود، نرخ پله‌ی اول).
        var brackets = (settings.TaxBrackets ?? new List<TaxBracketDto>())
            .OrderBy(x => x.FromAmount).ToList();

        decimal annualExemption;
        decimal taxRate;
        if (brackets.Count > 0 && brackets[0].ToAmount.HasValue)
        {
            annualExemption = brackets[0].ToAmount.Value;
            decimal rate = (brackets.Count > 1 ? brackets[1] : brackets[0]).TaxRate;
            taxRate = rate > 1m ? rate / 100m : rate;   // پشتیبانی از هر دو حالت 0.10 و 10
        }
        else
        {
            // جدول مالیاتی در دسترس نیست → رفتار قبلی
            annualExemption = settings.MinimumWage * 2m;
            taxRate = 0.10m;
        }

        // معافیت = پله‌ی اول ÷ ۱۲ ÷ تعداد روزهای سال (۳۶۵ یا ۳۶۶ کبیسه) × روزهای کارکرد کارمند
        decimal taxExemption = Math.Round(annualExemption / 12m / daysInYear * daysWorked, 0);
        if (result.BonusAmount > taxExemption)
            result.Tax = Math.Round((result.BonusAmount - taxExemption) * taxRate, 0);
        else
            result.Tax = 0;

        result.NetBonus = result.BonusAmount - result.Tax;
        return result;
    }
}
