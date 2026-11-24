using System;

namespace Smp.Models;

public class EmployeeContract
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;

    // ✅ FIX P1: فیلد ContractRow حذف شد (در دیتابیس وجود نداشت و بلااستفاده بود)

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Position { get; set; } = string.Empty;
    public string PersonnelType { get; set; } = string.Empty;

    // فیلد جدید برای نگهداری دسته‌بندی کارگر (مثلاً کارگر ساده)
    public string? DailyWageCategory { get; set; }

    public decimal BaseSalary { get; set; }
    public decimal YearsBonus { get; set; }
    public decimal AttractionAllowance { get; set; }
    public decimal ResponsibilityAllowance { get; set; }
    public decimal ManagementAllowance { get; set; }
    public decimal TechnicalAllowance { get; set; }
    public decimal HousingAllowance { get; set; }
    public decimal GroceryAllowance { get; set; }
    public decimal ChildAllowance { get; set; }
    public decimal MarriageAllowance { get; set; }
    public decimal OtherAllowances { get; set; }
    public decimal TransportationAllowance { get; set; }

    public decimal DailyWage { get; set; }
    public decimal HourlyWage { get; set; }
    public bool HasInsurance { get; set; }
    public string Status { get; set; } = "فعال";
    public DateTime CreatedAt { get; set; }

    public string EmployeeName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    // این پراپرتی برای نمایش حقوق در جدول استفاده می‌شود
    public decimal DisplaySalary { get; set; }
}
