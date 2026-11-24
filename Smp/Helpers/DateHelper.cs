using System;
using System.Text;
using System.Globalization;
using System.Windows.Forms;

namespace Smp.Helpers;

public static class DateHelper
{
    private static readonly PersianCalendar PersianCalendar = new();

    public static string ToPersianDate(DateTime? date)
    {
        if (!date.HasValue || date.Value == DateTime.MinValue) return string.Empty;
        try
        {
            var d = date.Value;
            int year = PersianCalendar.GetYear(d);
            int month = PersianCalendar.GetMonth(d);
            int day = PersianCalendar.GetDayOfMonth(d);
            return $"{year:0000}/{month:00}/{day:00}";
        }
        catch { return string.Empty; }
    }

    public static string ToPersianDateTime(DateTime? date)
    {
        if (!date.HasValue || date.Value == DateTime.MinValue) return string.Empty;
        try
        {
            var d = date.Value;
            int year = PersianCalendar.GetYear(d);
            int month = PersianCalendar.GetMonth(d);
            int day = PersianCalendar.GetDayOfMonth(d);
            return $"{year:0000}/{month:00}/{day:00} - {d:HH:mm}";
        }
        catch { return string.Empty; }
    }

    // ✅ FIX 1: اعتبارسنجی دقیق روز و ماه قبل از تبدیل به میلادی برای جلوگیری از Exception
    public static DateTime? ParsePersianDate(string? persianDateStr)
    {
        if (string.IsNullOrWhiteSpace(persianDateStr)) return null;
        try
        {
            string cleanDate = StringHelper.ConvertToEnglishDigits(persianDateStr.Trim()).Replace("-", "/");
            string[] parts = cleanDate.Split('/');
            if (parts.Length != 3) return null;

            if (int.TryParse(parts[0], out int year) &&
                int.TryParse(parts[1], out int month) &&
                int.TryParse(parts[2], out int day))
            {
                if (year < 100) year += 1400;
                if (month < 1 || month > 12) return null;
                if (day < 1 || day > 31) return null;

                // ✅ چک کردن اینکه روز وارد شده در آن ماه وجود دارد یا خیر (مثلاً اسفند ۳۰ در سال غیرکبیسه)
                if (day > PersianCalendar.GetDaysInMonth(year, month)) return null;

                return PersianCalendar.ToDateTime(year, month, day, 0, 0, 0, 0);
            }
        }
        catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "DateHelper"); }
        return null;
    }

    public static DateTime? ToGregorianDate(string? persianDateStr)
    {
        return ParsePersianDate(persianDateStr);
    }

    public static DateTime? ParseDbDate(object? dbValue)
    {
        if (dbValue is null || dbValue == DBNull.Value) return null;
        if (dbValue is DateTime dt) return dt;
        if (dbValue is string s) return ParsePersianDate(s);
        return null;
    }

    public static bool IsValidPersianDate(string? persianDateStr)
    {
        return ParsePersianDate(persianDateStr).HasValue;
    }

    // ✅ FIX 2: جلوگیری از چسبیدن چندباره رویدادها به تکست‌باکس
    public static void ApplyDateMask(TextBox textBox)
    {
        if (textBox is null) return;
        if (textBox.Tag?.ToString() == "DateMaskApplied") return; // جلوگیری از تکراری شدن
        textBox.Tag = "DateMaskApplied";

        textBox.MaxLength = 10;
        textBox.TextAlign = HorizontalAlignment.Center;

        textBox.KeyPress += (s, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '/')
                e.Handled = true;
        };

        // ✅ اسلش‌ها هنگام تایپ خودکار گذاشته می‌شود: 14050101 ← 1405/01/01
        textBox.TextChanged += LiveDateTextChanged;

        textBox.Leave += (s, e) =>
        {
            string text = StringHelper.ConvertToEnglishDigits(textBox.Text.Trim()).Replace("-", "/");
            if (string.IsNullOrEmpty(text)) return;

            // اگر کاربر بدون اسلش وارد کرد، خودکار اسلش بگذارد
            if (text.Length == 8 && !text.Contains('/'))
                text = $"{text.Substring(0, 4)}/{text.Substring(4, 2)}/{text.Substring(6, 2)}";
            else if (text.Length == 6 && !text.Contains('/'))
                text = $"{text.Substring(0, 4)}/{text.Substring(4, 2)}/01";

            textBox.Text = text;
        };
    }

    [ThreadStatic] private static bool _liveFormatting;

    /// <summary>
    /// قالب‌بندی زنده‌ی تاریخ شمسی هنگام تایپ: بعد از رقم چهارم و ششم خودش «/» می‌گذارد.
    /// برای TextBox معمولی از طریق ApplyDateMask وصل می‌شود؛ برای سلول‌های DataGridView در EditingControlShowing
    /// (با -= قبل از += تا چندبار وصل نشود) به TextChanged ادیتور وصل کنید.
    /// اگر کاربر خودش اسلش‌ها را جای دیگری گذاشته باشد (مثلاً 1405/1/5) دست نمی‌زند.
    /// </summary>
    public static void LiveDateTextChanged(object? sender, EventArgs e)
    {
        if (_liveFormatting || sender is not TextBox tb) return;
        if (!tb.Focused || tb.ReadOnly) return;          // فقط وقتی کاربر دارد تایپ می‌کند (نه مقداردهی برنامه‌ای)

        string raw = tb.Text;
        if (raw.Length == 0) return;

        var sb = new StringBuilder();
        foreach (char c in raw) if (char.IsDigit(c)) sb.Append(c);
        if (sb.Length > 8) sb.Length = 8;
        string d = sb.ToString();

        string auto = d.Length <= 4 ? d
                    : d.Length <= 6 ? d.Substring(0, 4) + "/" + d.Substring(4)
                    : d.Substring(0, 4) + "/" + d.Substring(4, 2) + "/" + d.Substring(6);

        if (raw == auto) return;

        // اگر کاربر خودش یکی از بخش‌های کامل‌شده را کوتاه‌تر نوشته (مثلاً 1405/1/ برای ماه تک‌رقمی) دست نمی‌زنیم
        string[] parts = raw.Split('/');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            int expected = i == 0 ? 4 : 2;
            if (parts[i].Length != expected) return;
        }

        _liveFormatting = true;
        try
        {
            tb.Text = auto;
            tb.SelectionStart = tb.Text.Length;
        }
        finally { _liveFormatting = false; }
    }

    public static string GetCurrentPersianYearMonth()
    {
        try
        {
            int year = PersianCalendar.GetYear(DateTime.Now);
            int month = PersianCalendar.GetMonth(DateTime.Now);
            return $"{year}/{month:00}";
        }
        catch (Exception __lex) { Smp.Helpers.LogHelper.LogException(__lex, "DateHelper.GetCurrentPersianYearMonth"); return "1405/01"; }
    }

    public static (int Year, int Month) GetCurrentPersianYearAndMonth()
    {
        try
        {
            int year = PersianCalendar.GetYear(DateTime.Now);
            int month = PersianCalendar.GetMonth(DateTime.Now);
            return (year, month);
        }
        catch (Exception __lex) { Smp.Helpers.LogHelper.LogException(__lex, "DateHelper.GetCurrentPersianYearAndMonth"); return (1405, 1); }
    }
}
