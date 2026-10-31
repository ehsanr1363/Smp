using PersonnelApp.Models;
using System;

namespace PersonnelApp.Services
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

            // =========================================================
            // ۱. محاسبه سال و ماه‌های کارکرد
            // =========================================================
            if (emp.HireDate.HasValue)
            {
                var diff = settlementDate - emp.HireDate.Value;
                result.YearsOfWork = (int)(diff.TotalDays / 365.25);

                // 👈 محاسبه دقیق کل ماه‌های کارکرد (با اعشار) برای محاسبه مرخصی
                double totalMonthsWorked = diff.TotalDays / 30.4375; // میانگین روزهای ماه
            }
            else
            {
                result.YearsOfWork = 0;
            }

            // =========================================================
            // ۲. محاسبه سنوات (یک ماه حقوق به ازای هر سال)
            // =========================================================
            result.ServiceBonus = Math.Round(result.YearsOfWork * emp.BaseSalary, 0);

            // =========================================================
            // ۳. محاسبه عیدی (بر اساس ماه‌های کارکرد در سال جاری شمسی)
            // =========================================================
            decimal maxSeverance = emp.BaseSalary * 2; // حداکثر ۲ ماه حقوق

            // شروع سال شمسی را محاسبه می‌کنیم
            var pc = new System.Globalization.PersianCalendar();
            int currentYear = pc.GetYear(settlementDate);
            DateTime startOfPersianYear = pc.ToDateTime(currentYear, 1, 1, 0, 0, 0, 0);

            DateTime start = emp.HireDate.HasValue && emp.HireDate.Value > startOfPersianYear
                ? emp.HireDate.Value
                : startOfPersianYear;

            int monthsWorkedThisYear = (int)Math.Ceiling((settlementDate - start).TotalDays / 30.4375);

            if (monthsWorkedThisYear > 12) monthsWorkedThisYear = 12;
            if (monthsWorkedThisYear < 0) monthsWorkedThisYear = 0;

            // فرمول عیدی: (حقوق پایه / ۱۲) × ماه‌های کارکرد × ۲
            decimal calculatedSeverance = (emp.BaseSalary / 12m) * monthsWorkedThisYear * 2m;
            result.SeverancePay = Math.Min(calculatedSeverance, maxSeverance);

            // =========================================================
            // ۴. محاسبه بازخرید مرخصی
            // =========================================================
            // 👈 اصلاح شد: محاسبه دقیق مرخصی بر اساس کل ماه‌های کارکرد (نه فقط سال‌های کامل)
            double totalMonths = emp.HireDate.HasValue ? (settlementDate - emp.HireDate.Value).TotalDays / 30.4375 : 0;

            // به ازای هر ۱۲ ماه، ۲۶ روز مرخصی (با محاسبه ماه‌های ناقص)
            double totalAccruedLeave = (totalMonths / 12.0) * 26.0;

            double unusedLeaveDays = totalAccruedLeave - approvedLeaveDays;
            if (unusedLeaveDays < 0) unusedLeaveDays = 0;

            decimal dailyRate = emp.BaseSalary / 30m;
            result.LeaveBonus = Math.Round((decimal)unusedLeaveDays * dailyRate, 0);

            return result;
        }
    }
}