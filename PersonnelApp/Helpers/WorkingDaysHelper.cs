using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PersonnelApp.Helpers
{
    public static class WorkingDaysHelper
    {
        private static readonly PersianCalendar _pc = new PersianCalendar();

        // ✅ FIX P1: استفاده از ConcurrentBag برای Thread-Safety
        private static readonly ConcurrentBag<DateTime> _holidays = new ConcurrentBag<DateTime>();

        /// <summary>
        /// دسترسی فقط-خواندنی به لیست تعطیلات (بدون امکان تغییر مستقیم)
        /// </summary>
        public static IReadOnlyCollection<DateTime> Holidays => _holidays.ToArray();

        /// <summary>
        /// جایگزینی کل لیست تعطیلات
        /// </summary>
        public static void SetHolidays(IEnumerable<DateTime> holidays)
        {
            _holidays.Clear();
            if (holidays != null)
            {
                foreach (var d in holidays) _holidays.Add(d.Date);
            }
        }

        /// <summary>
        /// افزودن یک تعطیلات جدید
        /// </summary>
        public static void AddHoliday(DateTime date) => _holidays.Add(date.Date);

        // تعطیلات هر سال بر اساس تقویم رسمی ایران.
        // برای سال‌های بعدی، از جدول دیتابیس یا فایل JSON/XML پر شود.
        private static readonly Dictionary<int, HashSet<string>> _holidaysByYear = new()
        {
            [1403] = new HashSet<string>(StringComparer.Ordinal)
            {
                // نوروز
                "1403/01/01", "1403/01/02", "1403/01/03", "1403/01/04",
                // ۱۲ و ۱۳ فروردین
                "1403/01/12", "1403/01/13",
                // رحلت امام خمینی
                "1403/03/14",
                // قیام ۱۵ خرداد
                "1403/03/15",
                // پیروزی انقلاب اسلامی
                "1403/11/22",
                // ۲۹ اسفند — ملی شدن صنعت نفت
                "1403/12/29"
                // توجه: تعطیلات مذهبی متغیر بر اساس تقویم قمری اینجا نیست؛
                // برای دقت بیشتر از جدول دیتابیس استفاده کنید.
            }
        };

        /// <summary>
        /// آیا این تاریخ تعطیل رسمی است؟ (جمعه‌ها هم تعطیل هستند)
        /// </summary>
        public static bool IsHoliday(DateTime date)
        {
            if (_holidays.Contains(date.Date)) return true;

            int year = _pc.GetYear(date);
            string key = DateHelper.ToPersianDate(date);

            if (_holidaysByYear.TryGetValue(year, out var set) && set.Contains(key))
                return true;

            return _pc.GetDayOfWeek(date) == DayOfWeek.Friday;
        }

        /// <summary>ثبت تعطیلات برای یک سال (اگر از دیتابیس بارگذاری می‌کنید).</summary>
        public static void RegisterHolidays(int year, IEnumerable<string> holidays)
        {
            if (holidays == null) return;
            _holidaysByYear[year] = new HashSet<string>(holidays, StringComparer.Ordinal);
        }

        /// <summary>تعداد روزهای کاری بین دو تاریخ (بدون جمعه و تعطیلات).</summary>
        public static int CountWorkingDays(DateTime from, DateTime to)
        {
            if (from > to) (from, to) = (to, from);

            int count = 0;
            for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
                if (!IsHoliday(d)) count++;
            return count;
        }

        /// <summary>تعداد روزهای یک ماه شمسی.</summary>
        public static int GetDaysInMonth(int year, int month) => _pc.GetDaysInMonth(year, month);

        /// <summary>محاسبه سن به سال (بر اساس تاریخ تولد).</summary>
        public static int CalculateAge(DateTime birthDate)
        {
            DateTime today = DateTime.Today;
            int age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age)) age--;
            return age;
        }

        /// <summary>سال‌های کارکرد (برای محاسبه سنوات).</summary>
        public static int CalculateServiceYears(DateTime hireDate, DateTime? endDate = null)
        {
            DateTime end = endDate ?? DateTime.Today;
            int years = end.Year - hireDate.Year;
            if (hireDate.Date > end.AddYears(-years)) years--;
            return Math.Max(0, years);
        }

        /// <summary>شروع ماه شمسی جاری.</summary>
        public static DateTime StartOfPersianMonth(DateTime date)
        {
            int y = _pc.GetYear(date);
            int m = _pc.GetMonth(date);
            return _pc.ToDateTime(y, m, 1, 0, 0, 0, 0);
        }

        /// <summary>پایان ماه شمسی جاری.</summary>
        public static DateTime EndOfPersianMonth(DateTime date)
        {
            int y = _pc.GetYear(date);
            int m = _pc.GetMonth(date);
            int lastDay = _pc.GetDaysInMonth(y, m);
            return _pc.ToDateTime(y, m, lastDay, 23, 59, 59, 0);
        }

        /// <summary>شروع سال شمسی (اول فروردین).</summary>
        public static DateTime StartOfPersianYear(DateTime date)
        {
            int y = _pc.GetYear(date);
            return _pc.ToDateTime(y, 1, 1, 0, 0, 0, 0);
        }
    }
}