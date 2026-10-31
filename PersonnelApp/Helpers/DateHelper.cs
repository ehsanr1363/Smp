using System;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace PersonnelApp.Helpers
{
    public static class DateHelper
    {
        // ==========================================================
        // ۱. تبدیل مستقیم از دیتابیس (پشتیبانی از DateTime و رشته شمسی)
        // ==========================================================
        public static DateTime? ParseDbDate(object? val)
        {
            if (val == null || val == DBNull.Value) return null;

            if (val is DateTime dt) return dt;
            if (val is DateTimeOffset dto) return dto.DateTime;

            string s = val.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(s)) return null;

            s = StringHelper.ConvertToEnglishDigits(s).Trim().Replace("-", "/").Replace(".", "/");

            // اگر شمسی بود
            var parts = s.Split('/');
            if (parts.Length == 3 && int.TryParse(parts[0], out int y) && y >= 1200 && y <= 1600)
            {
                try
                {
                    PersianCalendar pc = new PersianCalendar();
                    return pc.ToDateTime(y, int.Parse(parts[1]), int.Parse(parts[2]), 0, 0, 0, 0);
                }
                catch { }
            }

            // اگر میلادی بود
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime gDate))
                return gDate;

            return null;
        }

        // ==========================================================
        // ۲. تبدیل رشته شمسی به DateTime میلادی (فقط برای ورودی UI)
        // ==========================================================
        public static DateTime? ParsePersianDate(string? persianDate)
        {
            if (string.IsNullOrWhiteSpace(persianDate)) return null;

            persianDate = StringHelper.ConvertToEnglishDigits(persianDate)
                .Trim()
                .Replace("-", "/")
                .Replace(".", "/")
                .Replace(" ", "");

            try
            {
                int year, month, day;
                if (persianDate.Contains("/"))
                {
                    var parts = persianDate.Split('/');
                    if (parts.Length != 3) return null;
                    if (!int.TryParse(parts[0], out year) ||
                        !int.TryParse(parts[1], out month) ||
                        !int.TryParse(parts[2], out day))
                        return null;
                }
                else if (persianDate.Length == 8)
                {
                    if (!int.TryParse(persianDate.Substring(0, 4), out year) ||
                        !int.TryParse(persianDate.Substring(4, 2), out month) ||
                        !int.TryParse(persianDate.Substring(6, 2), out day))
                        return null;
                }
                else return null;

                if (year < 1200 || year > 1600 || month < 1 || month > 12 || day < 1 || day > 31)
                    return null;

                PersianCalendar pc = new PersianCalendar();
                return pc.ToDateTime(year, month, day, 0, 0, 0, 0);
            }
            catch
            {
                return null;
            }
        }

        // ==========================================================
        // ۳. تبدیل DateTime به رشته شمسی (برای نمایش در UI)
        // ==========================================================
        public static string ToPersianDate(DateTime? date)
        {
            if (!date.HasValue) return "";
            if (date.Value <= DateTime.MinValue || date.Value.Year < 622) return "";

            try
            {
                PersianCalendar pc = new PersianCalendar();
                return $"{pc.GetYear(date.Value):0000}/{pc.GetMonth(date.Value):00}/{pc.GetDayOfMonth(date.Value):00}";
            }
            catch { return ""; }
        }

        public static string ToPersianDateTime(DateTime? date)
        {
            if (!date.HasValue) return "";
            string d = ToPersianDate(date);
            return string.IsNullOrEmpty(d) ? "" : d + $" {date.Value:HH:mm}";
        }

        public static string ToPersianDateWithWeekday(DateTime? date)
        {
            if (!date.HasValue) return "";
            string d = ToPersianDate(date);
            if (string.IsNullOrEmpty(d)) return "";

            try
            {
                PersianCalendar pc = new PersianCalendar();
                string[] weekdays = { "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه", "شنبه" };
                var dow = (int)pc.GetDayOfWeek(date.Value);
                return $"{weekdays[dow]} {d}";
            }
            catch { return d; }
        }

        // ==========================================================
        // ۴. ماسک ورودی تاریخ شمسی روی TextBox
        // ==========================================================
        public static void ApplyDateMask(TextBox txt)
        {
            if (txt == null) return;

            if (txt.Tag != null && txt.Tag.ToString() == "DateMaskApplied") return;
            txt.Tag = "DateMaskApplied";

            bool isFormatting = false;
            txt.TextChanged += (s, e) =>
            {
                if (isFormatting) return;
                isFormatting = true;
                try
                {
                    string currentText = txt.Text ?? "";
                    int cursorPos = Math.Min(txt.SelectionStart, currentText.Length);

                    int digitsBeforeCursor = 0;
                    for (int i = 0; i < cursorPos; i++)
                    {
                        string converted = StringHelper.ConvertToEnglishDigits(currentText[i].ToString());
                        if (converted.Length > 0 && converted[0] >= '0' && converted[0] <= '9')
                            digitsBeforeCursor++;
                    }

                    string raw = StringHelper.ConvertToEnglishDigits(currentText);
                    raw = new string(raw.Where(c => c >= '0' && c <= '9').ToArray());
                    if (raw.Length > 8) raw = raw.Substring(0, 8);

                    string formatted = "";
                    if (raw.Length > 0)
                    {
                        formatted = raw.Substring(0, Math.Min(4, raw.Length));
                        if (raw.Length > 4) formatted += "/" + raw.Substring(4, Math.Min(2, raw.Length - 4));
                        if (raw.Length > 6) formatted += "/" + raw.Substring(6, Math.Min(2, raw.Length - 6));
                    }

                    if (currentText != formatted)
                    {
                        txt.Text = formatted;

                        int newPos = 0, dCount = 0;
                        while (newPos < formatted.Length && dCount < digitsBeforeCursor)
                        {
                            if (formatted[newPos] >= '0' && formatted[newPos] <= '9') dCount++;
                            newPos++;
                        }
                        txt.SelectionStart = Math.Min(newPos, txt.Text.Length);
                    }
                }
                finally
                {
                    isFormatting = false;
                }
            };
        }

        // ==========================================================
        // ۵. اعتبارسنجی
        // ==========================================================
        public static bool IsValidPersianDate(string persianDate)
        {
            return ParsePersianDate(persianDate).HasValue;
        }

        public static string ToPersianDateWithPersianDigits(DateTime? date)
        {
            string d = ToPersianDate(date);
            return string.IsNullOrEmpty(d) ? "" : StringHelper.ConvertToPersianDigits(d);
        }
    }
}