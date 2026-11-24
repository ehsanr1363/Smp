using Smp.Models;
using System;

namespace Smp.Services;

/// <summary>
/// محاسبه‌ی موظفی و اضافه‌کار/کسر کار پرسنل روزمزد (همان منطق نمونه‌ی اکسل)، با ساعت‌های
/// «تنظیمات حقوق سالانه» (SalarySettings.NormalDayHours / HolidayDayHours):
///   موظفی = روزهای عادی × ساعت روز عادی + روزهای تعطیل × ساعت روز تعطیل
///   اضافه‌کار (کسر کار) = حضور کل − اضافه‌کار اول وقت − موظفی   (منفی = کسر کار)
/// تمام ساعت‌ها اعشاری‌اند (۸.۵ = ۸ ساعت و ۳۰ دقیقه).
/// </summary>
public static class DailyWageDutyHelper
{
    public static decimal DutyHours(decimal normalDays, decimal holidayDays, SalarySettings s)
        => normalDays * s.NormalDayHours + holidayDays * s.HolidayDayHours;

    public static decimal NetOvertime(decimal totalPresenceHours, decimal earlyOvertimeHours,
                                      decimal normalDays, decimal holidayDays, SalarySettings s)
        => (totalPresenceHours - earlyOvertimeHours) - DutyHours(normalDays, holidayDays, s);
}
