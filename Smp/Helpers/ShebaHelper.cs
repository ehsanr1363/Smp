using System;
using System.Linq;
using System.Numerics;

namespace Smp.Helpers;

public static class ShebaHelper
{
    /// <summary>
    /// بررسی صحت شماره شبا (IBAN ایران). ارقام فارسی/عربی و نویسه‌های نامرئی را نرمال می‌کند.
    /// </summary>
    public static bool IsValid(string? sheba)
    {
        if (string.IsNullOrWhiteSpace(sheba)) return false;

        // ۱) تبدیل ارقام فارسی/عربی و حذف نویسه‌های نامرئی
        string clean = StringHelper.ConvertToEnglishDigits(sheba)
            .Replace(" ", "").Replace("-", "").Trim()
            .ToUpperInvariant();

        // ۲) حذف نویسه‌های نامرئی رایج در کپی از وب/PDF
        clean = new string(clean.Where(c => c >= '!' && c <= '~').ToArray());

        if (!clean.StartsWith("IR"))
            clean = "IR" + clean;

        if (clean.Length != 26) return false;

        string digitsPart = clean.Substring(2);
        if (!digitsPart.All(c => c >= '0' && c <= '9')) return false;

        // ۳) الگوریتم MOD 97
        string rearranged = clean.Substring(4) + clean.Substring(0, 4);
        rearranged = rearranged.Replace("I", "18").Replace("R", "27");

        try
        {
            BigInteger ibanNumber = BigInteger.Parse(rearranged);
            return ibanNumber % 97 == 1;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>استخراج کد ۳ رقمی بانک از شماره شبا</summary>
    public static string? GetBankCode(string sheba)
    {
        if (!IsValid(sheba)) return null;

        string clean = StringHelper.ConvertToEnglishDigits(sheba)
            .Replace(" ", "").Replace("-", "").Trim().ToUpperInvariant();
        clean = new string(clean.Where(c => c >= '!' && c <= '~').ToArray());
        if (clean.StartsWith("IR")) clean = clean.Substring(2);

        return clean.Substring(0, 3);
    }
}
