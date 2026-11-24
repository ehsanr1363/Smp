using System;

namespace Smp.Models;

/// <summary>یک برگه‌ی ماموریت (محاسبه‌ی حق ماموریت داخل کشور) برای یک کارمند.</summary>
public class MissionRecord
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";   // فقط برای نمایش

    public string Province { get; set; } = "";       // استان ماموریت
    public string Location { get; set; } = "";       // محل ماموریت
    public string Subject { get; set; } = "";        // موضوع ماموریت

    public DateTime DepartDate { get; set; }
    public string DepartTime { get; set; } = "";     // HH:mm
    public DateTime ReturnDate { get; set; }
    public string ReturnTime { get; set; } = "";     // HH:mm

    // ستون «روز»
    public int DaysCount { get; set; }
    public decimal SalaryBase { get; set; }          // جمع حقوق و مزایای مشمول (ماهانه)
    public decimal MissionRate { get; set; }         // درصد ماموریت (مثلاً 3)
    public decimal DailyAmount { get; set; }         // = SalaryBase × MissionRate% (مبلغ یک روز)
    public decimal DaysTotal { get; set; }           // = DailyAmount × DaysCount

    // ستون «شب»
    public int NightsCount { get; set; }
    public decimal NightUnit { get; set; }
    public decimal NightsTotal { get; set; }

    // ستون «اقامت»
    public int StayCount { get; set; }
    public decimal StayUnit { get; set; }
    public decimal StayTotal { get; set; }

    // ستون «هزینه ایاب و ذهاب» (مبلغ یکجا)
    public decimal TravelCost { get; set; }

    // ستون «حق نهار»
    public int LunchCount { get; set; }
    public decimal LunchUnit { get; set; }
    public decimal LunchTotal { get; set; }

    public decimal GrandTotal { get; set; }
    public string? Notes { get; set; }
    public DateTime? CreatedAt { get; set; }

    /// <summary>محاسبه‌ی همه‌ی مبالغ بر اساس ورودی‌ها (تنها منبع فرمول، هم برای فرم و هم برای چاپ).</summary>
    public void Recalculate()
    {
        DailyAmount = Math.Round(SalaryBase * MissionRate / 100m, 0);
        DaysTotal = DailyAmount * DaysCount;
        NightsTotal = NightUnit * NightsCount;
        StayTotal = StayUnit * StayCount;
        LunchTotal = LunchUnit * LunchCount;
        GrandTotal = DaysTotal + NightsTotal + StayTotal + TravelCost + LunchTotal;
    }
}
