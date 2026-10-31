using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using PersonnelApp.Data;

namespace PersonnelApp.Helpers
{
    public class CalendarEvent
    {
        public DateTime Date { get; set; }
        public string Title { get; set; }
        public string Type { get; set; } // "Birthday", "Holiday", "Contract", "Meeting"
        public string Description { get; set; }
    }

    public static class EventCalendarHelper
    {
        /// <summary>
        /// رویدادهای یک ماه را از منابع مختلف جمع‌آوری می‌کند.
        /// </summary>
        public static List<CalendarEvent> GetEventsForMonth(int year, int month, string connectionString)
        {
            var events = new List<CalendarEvent>();
            var pc = new System.Globalization.PersianCalendar();
            int daysInMonth = pc.GetDaysInMonth(year, month);

            // ۱. تولد پرسنل
            try
            {
                string query = @"
                    SELECT FirstName + ' ' + LastName AS Name, BirthDate 
                    FROM Employees 
                    WHERE BirthDate IS NOT NULL AND IsActive = 1";

                var dt = DbHelper.ExecuteQuery(connectionString, query);
                foreach (DataRow row in dt.Rows)
                {
                    if (row["BirthDate"] == DBNull.Value) continue;

                    // ✅ FIX P1: استفاده از ParseDbDate برای پشتیبانی همزمان از DateTime و رشته
                    DateTime? birth = DateHelper.ParseDbDate(row["BirthDate"]);
                    if (!birth.HasValue) continue;

                    int birthMonth = pc.GetMonth(birth.Value);
                    int birthDay = pc.GetDayOfMonth(birth.Value);

                    if (birthMonth == month)
                    {
                        DateTime eventDate = pc.ToDateTime(year, month, birthDay, 0, 0, 0, 0);
                        events.Add(new CalendarEvent
                        {
                            Date = eventDate,
                            Title = $"تولد {row["Name"]}",
                            Type = "Birthday"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogWarning("خطا در بارگذاری تولدها برای تقویم: " + ex.Message, "EventCalendarHelper");
            }

            // ۲. تعطیلات رسمی
            foreach (var holiday in WorkingDaysHelper.Holidays)
            {
                if (pc.GetYear(holiday) == year && pc.GetMonth(holiday) == month)
                {
                    events.Add(new CalendarEvent
                    {
                        Date = holiday,
                        Title = "تعطیل رسمی",
                        Type = "Holiday"
                    });
                }
            }

            // ۳. قراردادهای در حال اتمام در این ماه
            try
            {
                string query = @"
                    SELECT 
                        CASE WHEN p.PartyType = N'حقیقی' THEN p.FirstName + N' ' + p.LastName ELSE p.CompanyName END AS Name, 
                        c.EndDate 
                    FROM ContractorContracts c
                    INNER JOIN Parties p ON c.PartyId = p.Id
                    WHERE c.EndDate IS NOT NULL AND c.Status = N'فعال'";

                var dt = DbHelper.ExecuteQuery(connectionString, query);
                foreach (DataRow row in dt.Rows)
                {
                    // ✅ FIX P1: EndDate در DB از نوع DATETIME است، نه رشته شمسی
                    DateTime? end = DateHelper.ParseDbDate(row["EndDate"]);

                    if (end.HasValue && pc.GetYear(end.Value) == year && pc.GetMonth(end.Value) == month)
                    {
                        events.Add(new CalendarEvent
                        {
                            Date = end.Value,
                            Title = $"پایان قرارداد {row["Name"]}",
                            Type = "Contract"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogWarning("خطا در بارگذاری قراردادها برای تقویم: " + ex.Message, "EventCalendarHelper");
            }

            return events.OrderBy(e => e.Date).ToList();
        }
    }
}