using System;

namespace Smp.Helpers;

/// <summary>
/// قرارداد واحد برای درصدهای قراردادها: در دیتابیس همیشه «عدد درصد» ذخیره می‌شود (۱۰ یعنی ۱۰٪).
/// هیچ حدس «بزرگتر از ۱ یعنی درصد» وجود ندارد.
/// </summary>
public static class PercentHelper
{
    public static decimal ToRate(decimal percentPoints) => percentPoints / 100m;
}
