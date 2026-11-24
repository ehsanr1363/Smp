using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Smp.Helpers;

public static class WorkingDaysHelper
{
    private static readonly PersianCalendar _pc = new PersianCalendar();

    private static readonly ConcurrentBag<DateTime> _holidays = new ConcurrentBag<DateTime>();

    public static IReadOnlyCollection<DateTime> Holidays => _holidays.ToArray();

    // ✅ FIX: ConcurrentBag<T> در .NET Framework متد Clear ندارد — با TryTake تخلیه می‌شود
    public static void SetHolidays(IEnumerable<DateTime> holidays)
    {
        while (_holidays.TryTake(out _)) { }
        if (holidays is not null)
        {
            foreach (var d in holidays) _holidays.Add(d.Date);
        }
    }

    public static void AddHoliday(DateTime date) => _holidays.Add(date.Date);

    private static readonly Dictionary<int, HashSet<string>> _holidaysByYear = new()
    {
        [1403] = new HashSet<string>(StringComparer.Ordinal)
        {
            "1403/01/01", "1403/01/02", "1403/01/03", "1403/01/04",
            "1403/01/12", "1403/01/13",
            "1403/03/14",
            "1403/03/15",
            "1403/11/22",
            "1403/12/29"
        }
    };

    public static bool IsHoliday(DateTime date)
    {
        if (_holidays.Contains(date.Date)) return true;

        int year = _pc.GetYear(date);
        string key = DateHelper.ToPersianDate(date);

        if (_holidaysByYear.TryGetValue(year, out var set) && set.Contains(key))
            return true;

        return _pc.GetDayOfWeek(date) == DayOfWeek.Friday;
    }

    public static void RegisterHolidays(int year, IEnumerable<string> holidays)
    {
        if (holidays is null) return;
        _holidaysByYear[year] = new HashSet<string>(holidays, StringComparer.Ordinal);
    }

    public static int CountWorkingDays(DateTime from, DateTime to)
    {
        if (from > to) (from, to) = (to, from);

        int count = 0;
        for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
            if (!IsHoliday(d)) count++;
        return count;
    }

    public static int GetDaysInMonth(int year, int month) => _pc.GetDaysInMonth(year, month);

    public static int CalculateAge(DateTime birthDate)
    {
        DateTime today = DateTime.Today;
        int age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age)) age--;
        return age;
    }

    public static int CalculateServiceYears(DateTime hireDate, DateTime? endDate = null)
    {
        DateTime end = endDate ?? DateTime.Today;
        int years = end.Year - hireDate.Year;
        if (hireDate.Date > end.AddYears(-years)) years--;
        return Math.Max(0, years);
    }

    public static DateTime StartOfPersianMonth(DateTime date)
    {
        int y = _pc.GetYear(date);
        int m = _pc.GetMonth(date);
        return _pc.ToDateTime(y, m, 1, 0, 0, 0, 0);
    }

    public static DateTime EndOfPersianMonth(DateTime date)
    {
        int y = _pc.GetYear(date);
        int m = _pc.GetMonth(date);
        int lastDay = _pc.GetDaysInMonth(y, m);
        return _pc.ToDateTime(y, m, lastDay, 23, 59, 59, 0);
    }

    public static DateTime StartOfPersianYear(DateTime date)
    {
        int y = _pc.GetYear(date);
        return _pc.ToDateTime(y, 1, 1, 0, 0, 0, 0);
    }
}
