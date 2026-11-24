using System;

namespace Smp.Helpers;

/// <summary>
/// محاسبه‌ی مبالغ صورت‌وضعیت کارفرما (ارزش افزوده، بیمه، حسن انجام کار، پیش‌دریافت و خالص).
/// کلاس خالص (بدون UI و دیتابیس) تا قابل تست باشد. همه‌ی نرخ‌ها «نسبت» هستند (۰٫۱ یعنی ۱۰٪).
/// </summary>
public static class ClientStatementCalculator
{
    /// <summary>مقادیر ذخیره‌شده‌ی یک صورت‌وضعیت که در ویرایش بدون تغییر کارکرد/حق‌الزحمه حفظ می‌شوند</summary>
    public sealed record StoredAmounts(decimal Vat, decimal Insurance, decimal Retention);

    public sealed record Input(
        decimal Gross,
        decimal Fee,
        bool FeeMode,                 // قرارداد دو بخشی (کارکرد + حق‌الزحمه)
        decimal VatRate,
        decimal InsuranceRate,
        decimal RetentionRate,
        decimal AdvanceRate,
        decimal ContractAmount,
        decimal PreviousRetention,    // حسن انجام کار کسرشده در صورت‌وضعیت‌های قبلی
        decimal RemainingAdvance,     // مانده‌ی پیش‌دریافت قابل کسر
        decimal? ManualAdvance,       // اگر کاربر دستی وارد کرده، همین مقدار؛ وگرنه null (محاسبه خودکار)
        decimal Other,
        StoredAmounts? Stored);       // اگر null نباشد، بیمه/ارزش‌افزوده/حسن‌انجام‌کار از همین مقادیر برداشته می‌شود

    public sealed record Result(
        decimal DeductionBase,        // مبنای کسورات = کارکرد + حق‌الزحمه
        decimal Vat,
        decimal Insurance,
        decimal Retention,
        bool RetentionCapped,         // حسن انجام کار به سقف قرارداد رسیده و کمتر از محاسبه‌ی عادی است
        decimal Advance,
        decimal Net);

    public static Result Calculate(Input i)
    {
        // مبنای کسورات (حسن انجام کار، بیمه، پیش‌دریافت): کارکرد + حق‌الزحمه (قبل از ارزش افزوده)
        decimal dedBase = i.Gross + i.Fee;

        // ارزش افزوده: قرارداد دو بخشی فقط روی حق‌الزحمه؛ کارکرد مشمول ارزش افزوده نیست
        decimal vatBase = i.FeeMode ? i.Fee : i.Gross;
        decimal vat = i.Stored?.Vat ?? Math.Round(vatBase * i.VatRate, 0);
        decimal insurance = i.Stored?.Insurance ?? Math.Round(dedBase * i.InsuranceRate, 0);

        // حسن انجام کار: تا سقف (مبلغ قرارداد × درصد) منهای کسر شده در صورت‌وضعیت‌های قبلی
        decimal calcRetention = Math.Round(dedBase * i.RetentionRate, 0);
        decimal remainingRetention = Math.Max(0m, i.ContractAmount * i.RetentionRate - i.PreviousRetention);
        decimal retention = i.Stored?.Retention ?? Math.Min(calcRetention, remainingRetention);
        bool capped = i.Stored is null && calcRetention > remainingRetention;

        // پیش‌دریافت: خودکار تا سقف مانده‌ی پیش‌دریافت، مگر اینکه کاربر دستی وارد کرده باشد
        decimal advance = i.ManualAdvance
                          ?? Math.Min(Math.Round(dedBase * i.AdvanceRate, 0), i.RemainingAdvance);

        decimal net = (i.Gross + i.Fee + vat) - (insurance + retention + advance + i.Other);

        return new Result(dedBase, vat, insurance, retention, capped, advance, net);
    }
}
