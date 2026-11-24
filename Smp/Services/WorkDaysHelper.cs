using Smp.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Smp.Services;

/// <summary>
/// محاسبه‌ی «روزهای واقعی کارکرد» از روی احکام/قراردادهای پرسنل (جدول EmployeeContracts).
/// بازه‌ی هر قرارداد = [شروع، پایان] (هر دو روز شمرده می‌شوند)؛ قرارداد بدون پایان تا انتهای بازه‌ی محاسبه ادامه دارد.
/// بازه‌های هم‌پوشان/متوالی ادغام می‌شوند تا هیچ روزی دو بار شمرده نشود و فاصله‌ی بین قراردادها شمرده نشود.
/// اگر پرسنل هیچ قراردادی نداشت، از تاریخ استخدام استفاده می‌شود.
/// </summary>
public static class WorkDaysHelper
{
    public static List<(DateTime From, DateTime To)> BuildPeriods(
        Employee emp, IEnumerable<EmployeeContract>? contracts, DateTime windowStart, DateTime windowEnd)
    {
        windowStart = windowStart.Date;
        windowEnd = windowEnd.Date;
        var raw = new List<(DateTime From, DateTime To)>();

        var list = contracts?.ToList();
        if (list is not null && list.Count > 0)
        {
            foreach (var c in list)
            {
                DateTime from = c.StartDate.Date > windowStart ? c.StartDate.Date : windowStart;
                DateTime endRaw = (c.EndDate ?? windowEnd).Date;
                DateTime to = endRaw < windowEnd ? endRaw : windowEnd;
                if (to >= from) raw.Add((from, to));
            }
        }
        else if (emp.HireDate.HasValue)
        {
            DateTime from = emp.HireDate.Value.Date > windowStart ? emp.HireDate.Value.Date : windowStart;
            if (windowEnd >= from) raw.Add((from, windowEnd));
        }

        var merged = new List<(DateTime From, DateTime To)>();
        foreach (var p in raw.OrderBy(x => x.From))
        {
            if (merged.Count > 0 && p.From <= merged[^1].To.AddDays(1))
            {
                var last = merged[^1];
                if (p.To > last.To) merged[^1] = (last.From, p.To);
            }
            else merged.Add(p);
        }
        return merged;
    }

    /// <summary>مجموع روزهای بازه‌ها</summary>
    public static int CountDays(IEnumerable<(DateTime From, DateTime To)> periods)
        => periods.Sum(p => (p.To - p.From).Days + 1);

    /// <summary>روزهای بازه‌ها که داخل [from, to] می‌افتند</summary>
    public static int CountDays(IEnumerable<(DateTime From, DateTime To)> periods, DateTime from, DateTime to)
    {
        from = from.Date; to = to.Date;
        int total = 0;
        foreach (var p in periods)
        {
            DateTime a = p.From > from ? p.From : from;
            DateTime b = p.To < to ? p.To : to;
            if (b >= a) total += (b - a).Days + 1;
        }
        return total;
    }
}
