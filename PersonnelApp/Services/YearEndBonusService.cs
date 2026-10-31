using PersonnelApp.Models;
using System;
using System.Globalization;

namespace PersonnelApp.Services
{
    public class YearEndBonusService
    {
        public YearEndBonusRecord CalculateBonus(Employee emp, int targetYear, SalarySettings settings, DateTime? settlementDate = null)
        {
            var result = new YearEndBonusRecord
            {
                EmployeeId = emp.Id,
                EmployeeName = emp.FullName,
                Year = targetYear,
                BaseSalary = emp.BaseSalary
            };

            // ✅ FIX P0: targetYear شمسی است، new DateTime(1403,1,1) اشتباه بود
            var pc = new PersianCalendar();
            DateTime yearStart = pc.ToDateTime(targetYear, 1, 1, 0, 0, 0, 0);
            int lastDay = pc.GetDaysInMonth(targetYear, 12);
            DateTime yearEnd = pc.ToDateTime(targetYear, 12, lastDay, 23, 59, 59, 0);

            DateTime start = emp.HireDate.HasValue && emp.HireDate.Value > yearStart
                ? emp.HireDate.Value
                : yearStart;
            DateTime end = settlementDate ?? yearEnd;

            if (start > end)
            {
                result.MonthsWorked = 0;
                result.BonusAmount = 0;
                result.NetBonus = 0;
                return result;
            }

            // محاسبه ماه‌های کارکرد (تقریبی)
            int daysWorked = (end - start).Days;
            result.MonthsWorked = Math.Min(12, (int)Math.Ceiling(daysWorked / 30.4));
            if (result.MonthsWorked < 0) result.MonthsWorked = 0;

            // ۲ ماه حقوق پایه
            decimal calculatedBonus = (emp.BaseSalary / 12m) * result.MonthsWorked * 2m;

            // سقف قانونی = ۳ برابر حداقل حقوق ماهانه
            decimal maxBonus = settings.MinimumWage * 3m;
            result.BonusAmount = Math.Min(calculatedBonus, maxBonus);

            // مالیات (ساده)
            decimal taxExemption = settings.MinimumWage * 2m;
            if (result.BonusAmount > taxExemption)
                result.Tax = Math.Round((result.BonusAmount - taxExemption) * 0.10m, 0);
            else
                result.Tax = 0;

            result.NetBonus = result.BonusAmount - result.Tax;
            return result;
        }
    }
}