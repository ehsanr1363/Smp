using System;
using System.Collections.Generic;

namespace SMP.Models
{
    public class PayrollConfig
    {
        public decimal MinimumDailyWage { get; set; }
        public decimal InsuranceMaxDailyWage { get; set; }
        public decimal EmployeeInsuranceRate { get; set; }
        public decimal HousingAllowance { get; set; }
        public decimal GroceryAllowance { get; set; }
        public decimal ChildAllowance { get; set; }
        public decimal TaxExemptionLimit { get; set; }
    }

    public class TaxBracketDto
    {
        public decimal FromAmount { get; set; }
        public decimal? ToAmount { get; set; }
        public decimal TaxRate { get; set; }
    }

    public class PayslipResult
    {
        public decimal BaseSalary { get; set; }
        public decimal OvertimePay { get; set; }
        public decimal Benefits { get; set; }
        public decimal GrossSalary => BaseSalary + OvertimePay + Benefits;

        public decimal InsuranceEmployeeShare { get; set; }
        public decimal IncomeTax { get; set; }
        public decimal OtherDeductions { get; set; }

        public decimal NetPay => GrossSalary - (InsuranceEmployeeShare + IncomeTax + OtherDeductions);
    }

    public class PayrollResult
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = "";
        public string PersonnelCode { get; set; } = "";
        public int Year { get; set; }
        public int Month { get; set; }

        public string PersonnelType { get; set; } = "";
        public decimal DailyWage { get; set; }
        public decimal HourlyWage { get; set; }

        // 👇 تفکیک کامل مزایا 👇
        public decimal BaseSalary { get; set; }
        public decimal YearsBonus { get; set; }
        public decimal HousingAllowance { get; set; }
        public decimal GroceryAllowance { get; set; }
        public decimal ChildAllowance { get; set; }
        public decimal AttractionAllowance { get; set; }
        public decimal ResponsibilityAllowance { get; set; }
        public decimal ManagementAllowance { get; set; }
        public decimal TechnicalAllowance { get; set; }
        public decimal MarriageAllowance { get; set; }
        public decimal TransportationAllowance { get; set; }
        public decimal OtherAllowances { get; set; }

        public decimal OvertimePay { get; set; }
        public decimal TotalEarnings { get; set; }

        // کسورات
        public decimal AbsenceDeduction { get; set; }
        public decimal InsuranceEmployee { get; set; }
        public decimal InsuranceEmployer { get; set; }
        public decimal IncomeTax { get; set; }
        public decimal LoanDeduction { get; set; }
        public decimal SupplementaryInsuranceDeduction { get; set; }
        public decimal CarInsuranceDeduction { get; set; }
        public decimal OtherDeductions { get; set; }

        public decimal TotalDeductions { get; set; }
        public decimal NetPay { get; set; }
    }
}