using Smp.Helpers;
using System;

namespace Smp.Models;

public class EmployeeLoan
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public string LoanType { get; set; } = "وام";       // "وام" یا "مساعده"
    public decimal TotalAmount { get; set; }
    public decimal MonthlyInstallment { get; set; }
    public decimal RemainingAmount { get; set; }
    public int StartYear { get; set; }
    public int StartMonth { get; set; }
    public DateTime LoanDate { get; set; }              // تاریخ ثبت در سیستم (نگه‌داری برای نمایش)
    public string Description { get; set; } = "";
    public bool IsActive { get; set; }

    public bool IsAdvance => LoanType == "مساعده";

    /// <summary>مساعده فقط در ماه شروعِ خودش کسر می‌شود (اگر ماه شروع ثبت نشده باشد، در هر ماه قابل کسر است)</summary>
    public bool IsDueInPeriod(int year, int month)
        => StartYear <= 0 || StartMonth <= 0 || (StartYear == year && StartMonth == month);

    /// <summary>مساعده‌ی کسرنشده‌ای که ماه شروعش گذشته است</summary>
    public bool IsOverdueBefore(int year, int month)
        => StartYear > 0 && StartMonth > 0 && (StartYear < year || (StartYear == year && StartMonth < month));

    public string StartPeriod =>
        StartYear > 0 && StartMonth > 0
            ? $"{StringHelper.ConvertToPersianDigits(StartYear.ToString())}/{StringHelper.ConvertToPersianDigits(StartMonth.ToString("00"))}"
            : "---";
}
