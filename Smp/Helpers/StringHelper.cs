using System;

namespace Smp.Helpers;

/// <summary>
/// نمای سازگار با کدهای قدیمی؛ منطق اصلی در <see cref="PersianText"/> است.
/// </summary>
public static class StringHelper
{
    public static string ConvertToEnglishDigits(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return PersianText.ToLatinDigits(input);
    }

    public static string ConvertToPersianDigits(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return PersianText.ToPersianDigits(input);
    }

    public static string NormalizePersianText(string? input) => PersianText.NormalizeText(input);

    public static string GenderToPersian(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "نامشخص";
        return value.Trim().ToLower() switch
        {
            "male" or "m" or "مرد" or "1" => "مرد",
            "female" or "f" or "زن" or "2" => "زن",
            _ => "نامشخص"
        };
    }
}
