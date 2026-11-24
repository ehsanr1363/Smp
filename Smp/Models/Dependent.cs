using System;

namespace Smp.Models;

public class Dependent
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? NationalCode { get; set; }
    public string? BirthCertificateNumber { get; set; }
    public string? FatherName { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? BirthPlace { get; set; }
    public string? Relation { get; set; }
    public bool HasSupplementaryInsurance { get; set; }

    // ✅ حذف فاصلهٔ اضافه در صورت خالی بودن یکی از فیلدها
    public string FullName =>
        $"{FirstName} {LastName}".Trim();

    // ✅ سن به‌صورت رشته (برای نمایش در DataGridView)
    public string Age
    {
        get
        {
            if (BirthDate is null) return "---";
            var today = DateTime.Today;
            var age = today.Year - BirthDate.Value.Year;
            if (BirthDate.Value.Date > today.AddYears(-age)) age--;
            return $"{age} سال";
        }
    }

    // ✅ سن به‌صورت عدد (برای مرتب‌سازی درست در DataGridView)
    public int? AgeNumber
    {
        get
        {
            if (BirthDate is null) return null;
            var today = DateTime.Today;
            var age = today.Year - BirthDate.Value.Year;
            if (BirthDate.Value.Date > today.AddYears(-age)) age--;
            return age;
        }
    }
}
