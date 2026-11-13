using SMP.Models;
using System;
using System.Globalization;

namespace SMP.Services
{
    public class SettlementCalculatorService
    {
        public SettlementRecord CalculateSettlement(Employee emp, int approvedLeaveDays, decimal remainingLoan, DateTime settlementDate)
        {
            var result = new SettlementRecord
            {
                EmployeeId = emp.Id,
                EmployeeName = emp.FullName,
                SettlementDate = settlementDate,
                RemainingLoan = remainingLoan
            };

            var pc = new PersianCalendar();

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
            if (emp.HireDate.HasValue)
            {
                int hireYear = pc.GetYear(emp.HireDate.Value);
                int hireMonth = pc.GetMonth(emp.HireDate.Value);
                int hireDay = pc.GetDayOfMonth(emp.HireDate.Value);

                int settleYear = pc.GetYear(settlementDate);
                int settleMonth = pc.GetMonth(settlementDate);
                int settleDay = pc.GetDayOfMonth(settlementDate);

                int years = settleYear - hireYear;

                // اگر هنوز به سالگرد استخدام نرسیده‌ایم، یک سال کم کن
                if (settleMonth < hireMonth || (settleMonth == hireMonth && settleDay < hireDay))
                    years--;

                result.YearsOfWork = Math.Max(0, years);
            }
            else
            {
                result.YearsOfWork = 0;
            }

            // =========================================================
            // ۲. محاسبه سنوات (یک ماه حقوق به ازای هر سال) — بدون تغییر
            // =========================================================
            result.ServiceBonus = Math.Round(result.YearsOfWork * emp.BaseSalary, 0);

            // =========================================================
            // ۳. محاسبه عیدی (بر اساس ماه‌های کارکرد در سال جاری شمسی)
            //    بدون تغییر نسبت به نسخه قبلی
            // =========================================================
            decimal maxSeverance = emp.BaseSalary * 2;

            int currentYear = pc.GetYear(settlementDate);
            DateTime startOfPersianYear = pc.ToDateTime(currentYear, 1, 1, 0, 0, 0, 0);

            DateTime start = emp.HireDate.HasValue && emp.HireDate.Value > startOfPersianYear
                ? emp.HireDate.Value
                : startOfPersianYear;

            int monthsWorkedThisYear = (int)Math.Ceiling((settlementDate - start).TotalDays / 30.4375);

            if (monthsWorkedThisYear > 12) monthsWorkedThisYear = 12;
            if (monthsWorkedThisYear < 0) monthsWorkedThisYear = 0;

            decimal calculatedSeverance = (emp.BaseSalary / 12m) * monthsWorkedThisYear * 2m;
            result.SeverancePay = Math.Min(calculatedSeverance, maxSeverance);

            // =========================================================
            // ۴. محاسبه بازخرید مرخصی — بدون تغییر
            // =========================================================
            double totalMonths = emp.HireDate.HasValue ? (settlementDate - emp.HireDate.Value).TotalDays / 30.4375 : 0;
            double totalAccruedLeave = (totalMonths / 12.0) * 26.0;

            double unusedLeaveDays = totalAccruedLeave - approvedLeaveDays;
            if (unusedLeaveDays < 0) unusedLeaveDays = 0;

            decimal dailyRate = emp.BaseSalary / 30m;
            result.LeaveBonus = Math.Round((decimal)unusedLeaveDays * dailyRate, 0);

            return result;
        }
    }
}