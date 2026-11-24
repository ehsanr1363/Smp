using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Smp.Services;

/// <summary>
/// اجزای حقوق که می‌توانند در مبنای محاسبه‌ی «عیدی» و «سنوات» لحاظ شوند.
/// انتخاب‌ها در «تنظیمات حقوق سالانه ← مشمولین عیدی و سنوات» ذخیره می‌شود.
/// </summary>
public static class SalaryComponents
{
    public const string DefaultKeys = "Base"; // رفتار قبلی: فقط حقوق پایه

    public static readonly (string Key, string Title)[] All =
    {
        ("Base", "حقوق پایه"),
        ("Years", "پایه سنوات"),
        ("Attraction", "حق جذب"),
        ("Responsibility", "حق مسئولیت"),
        ("Management", "حق مدیریت"),
        ("Technical", "حق فنی"),
        ("Housing", "حق مسکن"),
        ("Grocery", "حق خواربار"),
        ("Child", "حق اولاد"),
        ("Marriage", "حق تاهل"),
        ("Transportation", "حق ایاب و ذهاب"),
        ("Other", "سایر مزایا"),
    };

    /// <summary>null = تنظیم نشده (پیش‌فرض: فقط حقوق پایه)؛ رشته‌ی خالی = هیچ‌کدام تیک نخورده.</summary>
    public static HashSet<string> Parse(string? csv)
    {
        if (csv is null) csv = DefaultKeys;
        return new HashSet<string>(
            csv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
               .Select(x => x.Trim()),
            StringComparer.OrdinalIgnoreCase);
    }

    public static string Join(IEnumerable<string> keys) => string.Join(",", keys);

    private static decimal AsRate(decimal rate) => rate >= 1m ? rate / 100m : rate;

    /// <summary>
    /// مبنای ماهانه‌ی کامل (بدون تناسب روز) از مجموع اجزای تیک‌خورده.
    /// </summary>
    public static decimal ComputeBase(Employee emp, SalarySettings settings, ISet<string> keys)
    {
        decimal sum = 0m;
        if (keys.Contains("Base")) sum += emp.BaseSalary;
        if (keys.Contains("Years")) sum += emp.YearsBonus;
        if (keys.Contains("Attraction")) sum += emp.AttractionAllowance;
        if (keys.Contains("Responsibility")) sum += emp.ResponsibilityAllowance;
        if (keys.Contains("Management")) sum += emp.ManagementAllowance;
        if (keys.Contains("Technical")) sum += emp.TechnicalAllowance;
        if (keys.Contains("Transportation")) sum += emp.TransportationAllowance;
        if (keys.Contains("Other")) sum += emp.OtherAllowances;

        if (keys.Contains("Housing")) sum += settings.HousingAllowance;
        if (keys.Contains("Grocery")) sum += settings.FoodAllowance;

        if (keys.Contains("Marriage"))
        {
            string marital = StringHelper.NormalizePersianText(emp.MaritalStatus ?? "");
            if (marital.Contains("متاهل") || marital.Contains("متأهل")) sum += settings.MarriageAllowance;
        }

        if (keys.Contains("Child"))
            sum += settings.MinimumWage * AsRate(settings.ChildAllowanceRate) * emp.ChildrenCount;

        return Math.Round(sum, 0);
    }

    public static decimal ComputeEidiBase(Employee emp, SalarySettings? settings)
        => settings is null ? emp.BaseSalary : ComputeBase(emp, settings, Parse(settings.EidiIncludeComponents));

    public static decimal ComputeSanavatBase(Employee emp, SalarySettings? settings)
        => settings is null ? emp.BaseSalary : ComputeBase(emp, settings, Parse(settings.SanavatIncludeComponents));
}
