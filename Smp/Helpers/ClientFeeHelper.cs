using System;
using System.Globalization;

namespace Smp.Helpers;

public static class ClientFeeHelper
{
    /// <summary>نرخ بیمه سهم کارگر (۷٪) که از جمع حقوق و اضافه‌کار کسر می‌شود</summary>
    public const decimal EmployeeInsuranceRate = 0.07m;

    /// <summary>مقسوم‌علیه استاندارد حق‌الزحمه (۰٫۸۳۳۳)</summary>
    public const decimal StandardDivisor = 0.8333m;

    /// <summary>بالاسری به‌صورت «عدد درصد» ذخیره می‌شود (۱۰ یعنی ۱۰٪، مثل بقیه‌ی درصدهای قرارداد) و به نسبت تبدیل می‌شود</summary>
    public static decimal OverheadRate(decimal overheadPoints) => overheadPoints / 100m;

    /// <summary>
    /// مقسوم‌علیه را به «عدد درصد» برمی‌گرداند: اگر کسر وارد شده باشد (۰٫۸۳ یا ۰٫۸۳۳۳) می‌شود ۸۳٫۳۳۳۳؛
    /// عددهای بزرگ‌تر از ۱ همان‌طور که هستند می‌مانند. هم در فرم قرارداد و هم در اسکریپت مهاجرت استفاده می‌شود.
    /// </summary>
    public static decimal NormalizeDivisorPoints(decimal v)
    {
        if (v <= 0m || v > 1m) return v;
        return Math.Round(v, 2) == 0.83m ? 83.3333m : v * 100m;
    }

    /// <summary>
    /// مقسوم‌علیه را به نسبت تبدیل می‌کند. ۸۳٫۳۳ یا ۰٫۸۳ یا ۰٫۸۳۳۳ همگی می‌شوند ۰٫۸۳۳۳
    /// (حالت‌های ≤۱ برای دیتابیسی است که هنوز اسکریپت ۱۷ روی آن اجرا نشده).
    /// </summary>
    public static decimal DivisorRate(decimal divisor)
    {
        decimal d = divisor > 1m ? divisor / 100m : divisor;
        return Math.Round(d, 2) == 0.83m ? StandardDivisor : d;
    }

    /// <summary>متن نمایشی بالاسری، مثلاً «10%» (ارقام لاتین؛ برای نمایش فارسی ConvertToPersianDigits بزنید)</summary>
    public static string OverheadText(decimal overhead)
        => (OverheadRate(overhead) * 100m).ToString("0.##", CultureInfo.InvariantCulture) + "%";

    /// <summary>متن نمایشی مقسوم‌علیه، مثلاً «0.8333»</summary>
    public static string DivisorText(decimal divisor)
        => DivisorRate(divisor).ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>
    /// حق‌الزحمه = ((جمع حقوق و اضافه‌کار − ۷٪ آن) × بالاسری) ÷ ۰٫۸۳۳۳
    /// </summary>
    public static decimal PayrollFee(decimal totalEarnings, decimal overheadPercent, decimal divisorPercent)
    {
        decimal divisor = DivisorRate(divisorPercent);
        if (divisor <= 0m) return 0m;
        decimal afterInsurance = totalEarnings - Math.Round(totalEarnings * EmployeeInsuranceRate, 0);
        return Math.Round(afterInsurance * OverheadRate(overheadPercent) / divisor, 0);
    }
}
