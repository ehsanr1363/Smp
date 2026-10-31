using System;

namespace PersonnelApp.Models
{
    public class Employee
    {
        public int Id { get; set; }
        public string? PersonnelCode { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string? FatherName { get; set; }
        public string? NationalCode { get; set; }
        public string? BirthCertificateNumber { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? BirthPlace { get; set; }
        public string? IssuancePlace { get; set; }
        public string? Gender { get; set; }
        public string? MaritalStatus { get; set; }
        public string? MilitaryStatus { get; set; }
        public string? Education { get; set; }
        public string? SpecializedField { get; set; }
        public int ChildrenCount { get; set; }
        public string? InsuranceNumber { get; set; }
        public byte[]? Photo { get; set; }
        public string? Address { get; set; }

        public DateTime? HireDate { get; set; }
        public DateTime? ContractStartDate { get; set; }
        public DateTime? ContractEndDate { get; set; }
        public int? ContractYear { get; set; }
        public string? ContractNumber { get; set; }

        public int? CompanyId { get; set; }
        public string? CompanyTitle { get; set; }
        public string? CompanyAddress { get; set; }

        public int? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public string? DepartmentAddress { get; set; }
        public string? Position { get; set; }
        public string? PersonnelType { get; set; }
        public string? DailyWageCategory { get; set; }
        public string? WorkLocation { get; set; }
        public string? Status { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; } = true;

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

        // 👇 این فیلد برگشت داده شد تا خطای ucEmployeeSalary برطرف شود
        public decimal TransportationAllowance { get; set; }

        public decimal OtherAllowances { get; set; }

        public decimal DailyWage { get; set; }
        public decimal HourlyWage { get; set; }
        public bool HasInsurance { get; set; } = true;

        public string? Username { get; set; }

        public string FullName => $"{FirstName} {LastName}".Trim();

        public string Age
        {
            get
            {
                if (BirthDate == null) return "---";
                var today = DateTime.Today;
                var age = today.Year - BirthDate.Value.Year;
                if (BirthDate.Value.Date > today.AddYears(-age)) age--;
                return $"{age} سال";
            }
        }

        public int? AgeNumber
        {
            get
            {
                if (BirthDate == null) return null;
                var today = DateTime.Today;
                var age = today.Year - BirthDate.Value.Year;
                if (BirthDate.Value.Date > today.AddYears(-age)) age--;
                return age;
            }
        }
    }
}