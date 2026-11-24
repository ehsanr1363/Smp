using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Smp.Helpers;

public static class NumberHelper
{
    public static string FormatThousands(decimal value, int decimals = 0, string thousandsSeparator = ",", string decimalSeparator = ".", bool usePersianDigits = false)
    {
        var nfi = new NumberFormatInfo { NumberGroupSeparator = thousandsSeparator, NumberDecimalSeparator = decimalSeparator, NumberDecimalDigits = decimals, NegativeSign = "-" };
        string result = value.ToString("N" + decimals, nfi);
        if (usePersianDigits) { result = StringHelper.ConvertToPersianDigits(result); result = result.Replace(decimalSeparator, "٫").Replace(thousandsSeparator, "٬"); }
        return result;
    }

    /// <summary>
    /// متن عددی را برای Parse آماده می‌کند: ارقام فارسی/عربی را لاتین می‌کند،
    /// جداکننده‌های هزارگان (، , ٬ فاصله NBSP نیم‌فاصله) را حذف و اعشار فارسی (٫) را به نقطه تبدیل می‌کند.
    /// </summary>
    public static string NormalizeNumberText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c >= '\u06F0' && c <= '\u06F9') { sb.Append((char)('0' + (c - '\u06F0'))); continue; } // ۰-۹
            if (c >= '\u0660' && c <= '\u0669') { sb.Append((char)('0' + (c - '\u0660'))); continue; } // ٠-٩

            switch (c)
            {
                case ',':
                case '\u060C':   // ، ویرگول فارسی
                case '\u066C':   // ٬ جداکننده هزارگان
                case ' ':
                case '\u00A0':   // فاصله بدون شکست
                case '\u200C':   // نیم‌فاصله
                case '\u200E':
                case '\u200F':
                    break;        // حذف
                case '\u066B':   // ٫ اعشار فارسی
                    sb.Append('.');
                    break;
                case '\u2212':   // − منفی یونیکد
                    sb.Append('-');
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// تبدیل امن: متن خالی یا نامعتبر = false (بی‌صدا صفر نمی‌شود).
    /// برای مسیرهای ذخیره/محاسبه‌ی مالی از این متد استفاده کنید.
    /// </summary>
    public static bool TryParseFormatted(string? formatted, out decimal value)
    {
        value = 0m;
        string clean = NormalizeNumberText(formatted);
        if (clean.Length == 0) return false;
        return decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>ورودی نامعتبر یا خالی = FormatException با پیام فارسی.</summary>
    public static decimal ParseFormattedStrict(string? formatted, string fieldName = "مبلغ")
    {
        if (TryParseFormatted(formatted, out decimal v)) return v;
        throw new FormatException($"مقدار «{fieldName}» عدد معتبر نیست: «{formatted}»");
    }

    /// <summary>
    /// سازگار با ۶۳ فراخوانی قبلی: خالی = ۰. متن غیرخالی ولی نامعتبر هم ۰ می‌شود،
    /// اما حالا در فایل لاگ ثبت می‌شود تا قابل ردیابی باشد.
    /// ⚠️ در مسیرهای ذخیره ترجیحاً TryParseFormatted یا ParseFormattedStrict را به کار ببرید.
    /// </summary>
    public static decimal ParseFormatted(string formatted)
    {
        if (string.IsNullOrWhiteSpace(formatted)) return 0m;
        if (TryParseFormatted(formatted, out decimal v)) return v;

        LogHelper.LogWarning($"متن عددی نامعتبر به ۰ تبدیل شد: «{formatted}»", "NumberHelper.ParseFormatted");
        return 0m;
    }

    public static decimal Round(decimal value, int decimals = 2, MidpointRounding mode = MidpointRounding.AwayFromZero)
        => Math.Round(value, decimals, mode);

    // --- عدد به حروف ---
    private static readonly string[] Yekan = { "", "یک", "دو", "سه", "چهار", "پنج", "شش", "هفت", "هشت", "نه" };
    private static readonly string[] Dahgan = { "", "", "بیست", "سی", "چهل", "پنجاه", "شصت", "هفتاد", "هشتاد", "نود" };
    private static readonly string[] Dahyek = { "ده", "یازده", "دوازده", "سیزده", "چهارده", "پانزده", "شانزده", "هفده", "هجده", "نوزده" };
    private static readonly string[] Sadgan = { "", "صد", "دویست", "سیصد", "چهارصد", "پانصد", "ششصد", "هفتصد", "هشتصد", "نهصد" };
    private static readonly string[] Scale = { "", "هزار", "میلیون", "میلیارد", "تریلیون" };

    private static string ThreeDigitsToWords(int num)
    {
        var parts = new List<string>();
        int sad = num / 100; int rem = num % 100;
        if (sad > 0) parts.Add(Sadgan[sad]);
        if (rem >= 10 && rem < 20) parts.Add(Dahyek[rem - 10]);
        else { int dah = rem / 10; int yek = rem % 10; if (dah > 0) parts.Add(Dahgan[dah]); if (yek > 0) parts.Add(Yekan[yek]); }
        return string.Join(" و ", parts);
    }

    public static string ToPersianWords(decimal value)
    {
        if (value == 0m) return "صفر";
        var sb = new StringBuilder();
        if (value < 0) { sb.Append("منفی "); value = decimal.Negate(value); }
        decimal integerPart = Math.Truncate(value);
        var integerBig = new BigInteger(integerPart);
        var parts = new List<string>();
        int scaleIndex = 0;
        while (integerBig > 0 && scaleIndex < Scale.Length)
        {
            int three = (int)(integerBig % 1000);
            if (three > 0)
            {
                string words = ThreeDigitsToWords(three);
                if (scaleIndex > 0) words += " " + Scale[scaleIndex];
                parts.Insert(0, words);
            }
            integerBig /= 1000; scaleIndex++;
        }
        sb.Append(string.Join(" و ", parts));
        return sb.ToString().Trim();
    }

    public static string ToPersianWordsWithUnit(decimal value, string unit) => string.IsNullOrEmpty(unit) ? ToPersianWords(value) : ToPersianWords(value) + " " + unit;
}
