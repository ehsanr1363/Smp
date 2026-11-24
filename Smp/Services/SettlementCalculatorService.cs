using Smp.Models;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace Smp.Services;

public class SettlementCalculatorService
{
    public SettlementRecord CalculateSettlement(Employee emp, int approvedLeaveDays, decimal remainingLoan, DateTime settlementDate, SalarySettings? settings = null, IEnumerable<EmployeeContract>? contracts = null, IDictionary<int, int>? workedDaysByYear = null)
    {
        var result = new SettlementRecord
        {
            EmployeeId = emp.Id,
            EmployeeName = emp.FullName,
            SettlementDate = settlementDate,
            RemainingLoan = remainingLoan
        };

        var pc = new PersianCalendar();

        // ✅ مبنای سنوات و عیدی از «تنظیمات حقوق ← مشمولین عیدی و سنوات» (بدون تنظیمات: فقط حقوق پایه مثل قبل)
        decimal sanavatBase = SalaryComponents.ComputeSanavatBase(emp, settings);
        decimal eidiBase = SalaryComponents.ComputeEidiBase(emp, settings);

        // =========================================================
        // ۱. محاسبه سال‌های کارکرد — با تقویم شمسی (نه میلادی)
        // =========================================================
        // ✅ FIX بحرانی: قبلاً از (settlementDate - hireDate).TotalDays / 365.25 استفاده می‌شد
        //    که یک تقریب میلادی است و در مرزهای سال شمسی، گاهی یک سال کمتر محاسبه می‌کند.
        //    مثال: 1400/01/01 تا 1403/01/01 = 1095 روز → 1095/365.25 = 2.99 → (int) = 2 سال ❌
        //    در حالی که کارمند دقیقاً 3 سال شمسی کار کرده است.
        //    این باگ باعث کم‌پرداختی سنوات به اندازه یک ماه حقوق می‌شد.
        //
        //    محاسبه جدید: تفاضل سال/ماه/روز شمسی — دقیقاً مطابق قانون کار ایران.
        //
        //    ⚠️ توجه: فرمول سنوات (YearsOfWork × BaseSalary) در پایین‌تر دست‌نخورده است.
        //    فقط مقدار YearsOfWork حالا درست است.
        // سال‌های کامل کارکرد (فقط جهت نمایش)
        if (emp.HireDate.HasValue)
        {
            int hireYear = pc.GetYear(emp.HireDate.Value);
            int hireMonth = pc.GetMonth(emp.HireDate.Value);
            int hireDay = pc.GetDayOfMonth(emp.HireDate.Value);

            int settleYear = pc.GetYear(settlementDate);
            int settleMonth = pc.GetMonth(settlementDate);
            int settleDay = pc.GetDayOfMonth(settlementDate);

            int years = settleYear - hireYear;
            if (settleMonth < hireMonth || (settleMonth == hireMonth && settleDay < hireDay))
                years--;

            result.YearsOfWork = Math.Max(0, years);
        }
        else
        {
            result.YearsOfWork = 0;
        }

        // =========================================================
        // ۲. سنوات = یک ماه مبنا به ازای هر سال — بر اساس «روزهای واقعی سابقه»
        //    هر سال شمسی جداگانه حساب می‌شود: روزهای کارکرد آن سال ÷ تعداد روزهای همان سال (۳۶۵ یا ۳۶۶)
        //    مجموع این نسبت‌ها = سابقه به «سال» (با کسر)؛ سنوات = مبنا × سابقه
        // =========================================================
        // روزهای کارکرد هر سال شمسی:
        //   ۱) اگر برای آن سال «جمع کارکرد حضور و غیاب» ثبت شده باشد همان؛
        //   ۲) وگرنه (سال‌های قبل از ثبت حضور و غیاب) از روی بازه‌ی قراردادها/تاریخ استخدام.
        var periods = WorkDaysHelper.BuildPeriods(emp, contracts, new DateTime(1900, 1, 1), settlementDate);

        int DaysWorkedInYear(int y)
        {
            int yDays = pc.IsLeapYear(y) ? 366 : 365;
            if (workedDaysByYear is not null && workedDaysByYear.TryGetValue(y, out int w))
                return Math.Max(0, Math.Min(yDays, w));
            DateTime yStart = pc.ToDateTime(y, 1, 1, 0, 0, 0, 0).Date;
            return WorkDaysHelper.CountDays(periods, yStart, yStart.AddDays(yDays - 1));
        }

        int firstYear = periods.Count > 0 ? pc.GetYear(periods[0].From) : pc.GetYear(settlementDate);
        if (workedDaysByYear is not null && workedDaysByYear.Count > 0)
            firstYear = Math.Min(firstYear, workedDaysByYear.Keys.Min());

        int totalServiceDays = 0;
        decimal serviceYearsFraction = 0m;
        int settleYear2 = pc.GetYear(settlementDate);
        for (int y = firstYear; y <= settleYear2; y++)
        {
            int worked = DaysWorkedInYear(y);
            int yDays = pc.IsLeapYear(y) ? 366 : 365;
            totalServiceDays += worked;
            serviceYearsFraction += (decimal)worked / yDays;
        }
        result.ServiceDays = totalServiceDays;
        // سنوات و عیدی فقط برای پرسنل «قراردادی / ماهانه» (روزمزد و ساعتی مشمول نیستند)
        bool eligible = YearEndBonusService.IsEligible(emp);
        result.ServiceBonus = eligible ? Math.Round(sanavatBase * serviceYearsFraction, 0) : 0m;

        // =========================================================
        // ۳. عیدی = ۲ ماه مبنا × (جمع کارکرد سال شمسی جاری ÷ روزهای همان سال شمسی)
        // =========================================================
        decimal maxSeverance = eidiBase * 2;

        int currentYear = pc.GetYear(settlementDate);
        int daysInYear = pc.IsLeapYear(currentYear) ? 366 : 365;

        int daysWorkedThisYear = DaysWorkedInYear(currentYear);
        result.DaysWorkedThisYear = daysWorkedThisYear;

        decimal calculatedSeverance = Math.Round(eidiBase * 2m * daysWorkedThisYear / daysInYear, 0);
        result.SeverancePay = eligible ? Math.Min(calculatedSeverance, maxSeverance) : 0m;

        // =========================================================
        // ۴. محاسبه بازخرید مرخصی — بدون تغییر
        // =========================================================
        decimal totalMonths = emp.HireDate.HasValue ? (decimal)(settlementDate - emp.HireDate.Value).TotalDays / 30.4375m : 0m;
        decimal totalAccruedLeave = (totalMonths / 12m) * 26m;

        decimal unusedLeaveDays = totalAccruedLeave - approvedLeaveDays;
        if (unusedLeaveDays < 0) unusedLeaveDays = 0;

        decimal dailyRate = emp.BaseSalary / 30m;
        result.LeaveBonus = Math.Round(unusedLeaveDays * dailyRate, 0);

        return result;
    }
}
