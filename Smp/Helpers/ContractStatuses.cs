using System;
using System.Windows.Forms;

namespace Smp.Helpers;

/// <summary>
/// وضعیت‌های قرارداد (کارفرما/پیمانکار). «فعال» همان مقداری است که داشبورد و گزارش‌ها فیلتر می‌کنند.
/// </summary>
public static class ContractStatuses
{
    public const string Active = "فعال";
    public const string Completed = "تکمیل شده";
    public const string Terminated = "فسخ شده";
    public const string Suspended = "تعلیق";

    public static readonly string[] All = { Active, Completed, Terminated, Suspended };

    /// <summary>اگر وضعیت ذخیره‌شده در لیست نبود (مثلاً از دیتابیس قدیمی)، به لیست اضافه و انتخاب می‌شود.</summary>
    public static void EnsureItem(ComboBox combo, string? status)
    {
        string s = string.IsNullOrWhiteSpace(status) ? Active : status.Trim();
        if (!combo.Items.Contains(s)) combo.Items.Add(s);
        combo.SelectedItem = s;
    }

    /// <summary>
    /// فعال ⇒ بدون تاریخ فسخ. سایر وضعیت‌ها ⇒ تاریخ واردشده، و اگر خالی بود امروز.
    /// </summary>
    public static DateTime? ResolveTerminationDate(string? status, DateTime? entered)
    {
        if (string.IsNullOrWhiteSpace(status) || status.Trim() == Active) return null;
        return (entered ?? DateTime.Today).Date;
    }
}
