using PersonnelApp.Helpers;
using System;
using System.Text.RegularExpressions;

namespace PersonnelApp.Models
{
    public class Attendance
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }

        public string EmployeeName { get; set; } = "";
        public string PersonnelCode { get; set; } = "";
        public string PersonnelType { get; set; } = "";

        public int WorkingDays { get; set; } = 30;
        public int HolidayDays { get; set; } = 0;
        public decimal HoursWorked { get; set; } = 0;
        public decimal OvertimeHours { get; set; } = 0;
        public int AbsenceDays { get; set; } = 0;

        public decimal HourlyLeave { get; set; } = 0;
        public int DailyLeave { get; set; } = 0;
        public decimal TotalPresence { get; set; } = 0;
        public decimal GrossOvertime { get; set; } = 0;
        public decimal AbsenceHours { get; set; } = 0;
        public decimal NetOvertime { get; set; } = 0;
        public decimal PayableOvertime { get; set; } = 0;
        public int LunchDays { get; set; } = 0;
        public decimal OtherOvertime { get; set; } = 0;
        public decimal EarlyOvertime { get; set; } = 0;

        // ─── پراپرتی‌های نمایشی و ورودی (فرمت ساعت:دقیقه) ───
        public string TotalDaysStr => (WorkingDays + HolidayDays).ToString();
        public string ExpectedHoursStr { get; set; } = "";
        public string NormalDayRate { get; set; } = "8:30";
        public string HolidayDayRate { get; set; } = "7:00";

        public string TotalPresenceStr
        {
            get => TimeHelper.FormatToTimeString(TotalPresence);
            set => TotalPresence = TimeHelper.ParseTimeString(value);
        }

        public string GrossOvertimeStr
        {
            get => TimeHelper.FormatToTimeString(GrossOvertime);
            set => GrossOvertime = TimeHelper.ParseTimeString(value);
        }

        public string AbsenceHoursStr
        {
            get => TimeHelper.FormatToTimeString(AbsenceHours);
            set => AbsenceHours = TimeHelper.ParseTimeString(value);
        }

        public string EarlyOvertimeStr
        {
            get => TimeHelper.FormatToTimeString(EarlyOvertime);
            set => EarlyOvertime = TimeHelper.ParseTimeString(value);
        }

        public string PayableOvertimeStr
        {
            get => TimeHelper.FormatToTimeString(PayableOvertime);
            set => PayableOvertime = TimeHelper.ParseTimeString(value);
        }

        public string OtherOvertimeStr
        {
            get => TimeHelper.FormatToTimeString(OtherOvertime);
            set => OtherOvertime = TimeHelper.ParseTimeString(value);
        }

        public string NetOvertimeStr => TimeHelper.FormatToTimeString(NetOvertime);
    }

    public static class TimeHelper
    {
        public static decimal ParseTimeString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;
            input = input.Trim().ToLower();

            // پشتیبانی از فرمت "روز و ساعت" (اگر از اکسل کپی شود)
            var matchDays = Regex.Match(input, @"(\d+)\s*days?,\s*(\d+):(\d+)");
            if (matchDays.Success)
            {
                int days = int.Parse(matchDays.Groups[1].Value);
                int hours = int.Parse(matchDays.Groups[2].Value);
                int minutes = int.Parse(matchDays.Groups[3].Value);
                return days * 24 + hours + minutes / 60m;
            }

            // پشتیبانی از فرمت "ساعت:دقیقه" (مثلاً 209:38 یا 8:30)
            var matchTime = Regex.Match(input, @"^(-)?(\d+):(\d+)$");
            if (matchTime.Success)
            {
                bool isNegative = matchTime.Groups[1].Success;
                int hours = int.Parse(matchTime.Groups[2].Value);
                int minutes = int.Parse(matchTime.Groups[3].Value);
                decimal result = hours + minutes / 60m;
                return isNegative ? -result : result;
            }

            // پشتیبانی از عدد اعشاری
            string clean = StringHelper.ConvertToEnglishDigits(input).Replace("٫", ".").Replace(",", "");
            if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal dec))
                return dec;

            return 0;
        }

        public static string FormatToTimeString(decimal totalHours)
        {
            if (totalHours == 0) return "0:00";

            bool isNegative = totalHours < 0;
            totalHours = Math.Abs(totalHours);

            int hours = (int)totalHours;
            int minutes = (int)Math.Round((totalHours - hours) * 60);

            if (minutes == 60) { minutes = 0; hours++; }

            string result = $"{hours}:{minutes:00}";
            return isNegative ? "-" + result : result;
        }
    }
}