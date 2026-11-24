using System;

namespace Smp.Models;

public class ContractArchive
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateTime ArchiveDate { get; set; } // تاریخ ثبت در آرشیو (همان لحظه ذخیره)

    // اطلاعات قرارداد در لحظه آرشیو
    public string? ContractNumber { get; set; }
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal YearsBonus { get; set; }
    public decimal AttractionAllowance { get; set; }
    public decimal ResponsibilityAllowance { get; set; }
    public decimal GroceryAllowance { get; set; }
    public decimal HousingAllowance { get; set; }
    public decimal ChildAllowance { get; set; }
    public decimal MarriageAllowance { get; set; }
    public decimal ManagementAllowance { get; set; }
    public decimal TechnicalAllowance { get; set; }

    // 🌟 فیلد جدید: سایر مزایا
    public decimal OtherAllowances { get; set; }

    // اطلاعات شغلی که ممکن است تغییر کنند
    public string? Position { get; set; }
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public int? CompanyId { get; set; }
    public string? CompanyTitle { get; set; }
}
