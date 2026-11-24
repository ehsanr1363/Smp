using System;
using System.Text;

namespace Smp.Helpers;

/// <summary>
/// تنها منبع نرمال‌سازی متن فارسی در برنامه (ارقام، «ي/ك» عربی، نیم‌فاصله، اعراب).
/// همه‌ی نویسه‌ها با کد یونیکد نوشته شده‌اند تا شکل‌های مشابهِ عربی و فارسی
/// (مثل ي و ی یا ك و ک) در ویرایشگر با هم اشتباه گرفته نشوند.
/// </summary>
public static class PersianText
{
    private const char Zwnj = '\u200C';        // نیم‌فاصله
    private const char ArabicYeh = '\u064A';   // ي
    private const char AlefMaksura = '\u0649'; // ى
    private const char ArabicKaf = '\u0643';   // ك
    private const char TehMarbuta = '\u0629';  // ة
    private const char HehYeh = '\u06C0';      // ۀ
    private const char PersianYeh = '\u06CC';  // ی
    private const char PersianKeheh = '\u06A9';// ک
    private const char Heh = '\u0647';         // ه

    // ───────────────────────── ارقام ─────────────────────────

    /// <summary>رقم فارسی (۰-۹) یا عربی (٠-٩) را به رقم لاتین تبدیل می‌کند؛ بقیه‌ی نویسه‌ها دست‌نخورده می‌مانند.</summary>
    public static char ToLatinDigit(char c) => c switch
    {
        >= '\u06F0' and <= '\u06F9' => (char)('0' + (c - '\u06F0')),
        >= '\u0660' and <= '\u0669' => (char)('0' + (c - '\u0660')),
        _ => c
    };

    public static string ToLatinDigits(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++) chars[i] = ToLatinDigit(chars[i]);
        return new string(chars);
    }

    public static string ToPersianDigits(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (chars[i] >= '0' && chars[i] <= '9') chars[i] = (char)('\u06F0' + (chars[i] - '0'));
        return new string(chars);
    }

    // ───────────────────────── حروف ─────────────────────────

    /// <summary>ي→ی و ك→ک (دو جایگزینی رایج در داده‌های قدیمی و ورودی DBF).</summary>
    public static char UnifyLetter(char c) => c switch
    {
        ArabicYeh => PersianYeh,
        ArabicKaf => PersianKeheh,
        _ => c
    };

    /// <summary>
    /// نرمال‌سازی عمومی متن نمایشی/ذخیره‌شدنی: ي ك ى ة → فارسی، نیم‌فاصله → فاصله،
    /// حذف علامت‌های جهت (LRM/RLM) و اعراب، و Trim.
    /// </summary>
    public static string NormalizeText(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var sb = new StringBuilder(input.Length);
        foreach (char ch in input)
        {
            switch (ch)
            {
                case ArabicYeh:
                case AlefMaksura: sb.Append(PersianYeh); break;
                case ArabicKaf: sb.Append(PersianKeheh); break;
                case TehMarbuta: sb.Append(Heh); break;
                case Zwnj: sb.Append(' '); break;
                case '\u200E':
                case '\u200F': break;
                default:
                    if (ch >= '\u064B' && ch <= '\u065F') break; // اعراب
                    sb.Append(ch);
                    break;
            }
        }
        return sb.ToString().Trim();
    }

    // ───────────────────────── کلیدهای مقایسه و جستجو ─────────────────────────

    /// <summary>کلید جستجو: ارقام لاتین، ي/ك فارسی، حذف فاصله و نیم‌فاصله، حروف کوچک.</summary>
    public static string NormalizeForSearch(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (char ch in s)
        {
            if (ch == ' ' || ch == Zwnj) continue;
            sb.Append(char.ToLowerInvariant(UnifyLetter(ToLatinDigit(ch))));
        }
        return sb.ToString();
    }

    /// <summary>کلید شناسه‌ها (کد ملی، شناسه ملی، شماره قرارداد): ارقام لاتین و حذف فاصله و خط‌تیره.</summary>
    public static string NormalizeIdentifier(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (char ch in s)
        {
            char c = ToLatinDigit(ch);
            if (!char.IsWhiteSpace(c) && c != '-') sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>کلید مقایسه‌ی نام: ي/ك/ة/ۀ فارسی‌سازی و حذف فاصله، نیم‌فاصله، خط‌تیره و پرانتز.</summary>
    public static string NameKey(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (char ch in s)
        {
            char c = ch switch
            {
                ArabicYeh => PersianYeh,
                ArabicKaf => PersianKeheh,
                TehMarbuta => Heh,
                HehYeh => Heh,
                _ => ch
            };
            if (char.IsWhiteSpace(c) || c == Zwnj || c == '-' || c == '\u2013' || c == '(' || c == ')') continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>ي/ك فارسی‌سازی، نیم‌فاصله → فاصله و فشرده‌کردن فاصله‌های پشت‌سرهم.</summary>
    public static string CollapseWords(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        s = s.Replace(ArabicYeh, PersianYeh).Replace(ArabicKaf, PersianKeheh).Replace(Zwnj, ' ');
        return string.Join(" ", s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}
