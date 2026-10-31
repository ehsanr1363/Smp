using System;
using System.Collections.Generic;

namespace PersonnelApp.Models
{
    public class SalarySettings
    {
        public int Id { get; set; }
        public int Year { get; set; }

        public decimal MinimumWage { get; set; }
        public decimal HousingAllowance { get; set; }
        public decimal FoodAllowance { get; set; }
        public decimal MarriageAllowance { get; set; } // 👈 فیلد جدید

        public decimal ChildAllowanceRate { get; set; }
        public decimal InsuranceEmployeeRate { get; set; }
        public decimal InsuranceEmployerRate { get; set; }

        public decimal TaxExemptionLimit { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public decimal NormalDayHours { get; set; } = 8.50m;
        public decimal HolidayDayHours { get; set; } = 7.00m;
        public decimal MonthlyWorkingHours { get; set; } = 220.00m;

        // ... (بقیه فیلدهای TaxInclude, InsuranceInclude, OvertimeInclude بدون تغییر)
        public bool TaxIncludeBaseSalary { get; set; } = true;
        public bool TaxIncludeYearsBonus { get; set; } = true;
        public bool TaxIncludeAttraction { get; set; } = true;
        public bool TaxIncludeResponsibility { get; set; } = true;
        public bool TaxIncludeManagement { get; set; } = true;
        public bool TaxIncludeTechnical { get; set; } = true;
        public bool TaxIncludeHousing { get; set; } = false;
        public bool TaxIncludeGrocery { get; set; } = false;
        public bool TaxIncludeChild { get; set; } = false;
        public bool TaxIncludeMarriage { get; set; } = false;
        public bool TaxIncludeTransportation { get; set; } = true;
        public bool TaxIncludeOtherAllowances { get; set; } = true;

        public bool InsuranceIncludeBaseSalary { get; set; } = true;
        public bool InsuranceIncludeYearsBonus { get; set; } = true;
        public bool InsuranceIncludeAttraction { get; set; } = true;
        public bool InsuranceIncludeResponsibility { get; set; } = true;
        public bool InsuranceIncludeManagement { get; set; } = true;
        public bool InsuranceIncludeTechnical { get; set; } = true;
        public bool InsuranceIncludeHousing { get; set; } = true;
        public bool InsuranceIncludeGrocery { get; set; } = true;
        public bool InsuranceIncludeChild { get; set; } = true;
        public bool InsuranceIncludeMarriage { get; set; } = false;
        public bool InsuranceIncludeTransportation { get; set; } = false;
        public bool InsuranceIncludeOther { get; set; } = true;

        public bool OvertimeIncludeBaseSalary { get; set; } = true;
        public bool OvertimeIncludeYearsBonus { get; set; } = true;
        public bool OvertimeIncludeAttraction { get; set; } = false;
        public bool OvertimeIncludeResponsibility { get; set; } = false;
        public bool OvertimeIncludeManagement { get; set; } = false;
        public bool OvertimeIncludeTechnical { get; set; } = false;
        public bool OvertimeIncludeHousing { get; set; } = false;
        public bool OvertimeIncludeGrocery { get; set; } = false;
        public bool OvertimeIncludeChild { get; set; } = false;
        public bool OvertimeIncludeMarriage { get; set; } = false;
        public bool OvertimeIncludeTransportation { get; set; } = false;
        public bool OvertimeIncludeOther { get; set; } = false;

        public List<TaxBracketDto> TaxBrackets { get; set; } = new();
    }
}