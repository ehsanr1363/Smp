using SMP.Data;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;

namespace SMP.Services
{
    public class PayrollCalculatorService
    {
        public PayrollCalculatorService() { }

        public PayrollResult CalculatePayroll(Employee emp, Attendance att, SalarySettings settings, decimal loanInstallment)
            => CalculatePayroll(emp, att, settings, loanInstallment, 0, 0, 0, GetTaxBracketsSafe(att.Year));

        public PayrollResult CalculatePayroll(Employee emp, Attendance att, SalarySettings settings, decimal loanInstallment, List<TaxBracketDto> taxBrackets)
            => CalculatePayroll(emp, att, settings, loanInstallment, 0, 0, 0, taxBrackets);

        public PayrollResult CalculatePayroll(Employee emp, Attendance att, SalarySettings settings, decimal loanInstallment, decimal supplementaryInsuranceInstallment, List<TaxBracketDto> taxBrackets)
            => CalculatePayroll(emp, att, settings, loanInstallment, supplementaryInsuranceInstallment, 0, 0, taxBrackets);

        public PayrollResult CalculatePayroll(
            Employee emp, Attendance att, SalarySettings settings,
            decimal loanInstallment, decimal supplementaryInsuranceInstallment,
            decimal carInsuranceInstallment, decimal otherDeductions,
            List<TaxBracketDto> taxBrackets)
        {
            if (emp == null) throw new ArgumentNullException(nameof(emp));
            if (att == null) throw new ArgumentNullException(nameof(att));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            string type = StringHelper.NormalizePersianText(emp.PersonnelType ?? "ماهانه").Replace(" ", "");

            BasePayrollCalculator calculator;
            switch (type)
            {
                case "روزمزد":
                    calculator = new DailyWagePayrollCalculator();
                    break;
                case "ساعتی":
                    calculator = new HourlyPayrollCalculator();
                    break;
                default:
                    calculator = new ContractualPayrollCalculator();
                    break;
            }

            return calculator.Calculate(emp, att, settings,
                loanInstallment, supplementaryInsuranceInstallment,
                carInsuranceInstallment, otherDeductions, taxBrackets);
        }

        public List<TaxBracketDto> GetTaxBracketsSafe(int year)
        {
            try
            {
                const string sql = "SELECT FromAmount, ToAmount, TaxRate FROM TaxBrackets WHERE Year = @Year ORDER BY FromAmount ASC";
                var dt = DbHelper.ExecuteQuery(AppConfig.ConnectionString, sql, new SqlParameter("@Year", year));
                var result = new List<TaxBracketDto>(dt.Rows.Count);
                foreach (DataRow row in dt.Rows)
                {
                    result.Add(new TaxBracketDto
                    {
                        FromAmount = Convert.ToDecimal(row["FromAmount"]),
                        ToAmount = row["ToAmount"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(row["ToAmount"]),
                        TaxRate = Convert.ToDecimal(row["TaxRate"])
                    });
                }
                return result;
            }
            catch { return new List<TaxBracketDto>(); }
        }
    }

    internal abstract class BasePayrollCalculator
    {
        protected const decimal TaxRoundingUnit = 10m;

        public PayrollResult Calculate(
            Employee emp, Attendance att, SalarySettings settings,
            decimal loanInstallment, decimal supplementaryInsuranceInstallment,
            decimal carInsuranceInstallment, decimal otherDeductions,
            List<TaxBracketDto> taxBrackets)
        {
            if (emp == null) throw new ArgumentNullException(nameof(emp));
            if (att == null) throw new ArgumentNullException(nameof(att));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var result = new PayrollResult
            {
                EmployeeId = emp.Id,
                EmployeeName = emp.FullName,
                PersonnelCode = emp.PersonnelCode ?? "",
                Year = att.Year,
                Month = att.Month,
                PersonnelType = (emp.PersonnelType ?? "ماهانه").Trim(),
                DailyWage = emp.DailyWage,
                HourlyWage = emp.HourlyWage
            };

            decimal monthlyWorkingHours = settings.MonthlyWorkingHours > 0 ? settings.MonthlyWorkingHours : 220m;

            CalculateBaseSalary(emp, att, settings, result);
            CalculateAllowances(emp, att, settings, result);
            decimal overtimeBase = CalculateOvertimeBase(settings, result);
            CalculateOvertimeAndAbsence(emp, att, settings, result, overtimeBase, monthlyWorkingHours);
            CalculateTotalEarnings(result);
            CalculateInsurance(emp, settings, result);

            result.SupplementaryInsuranceDeduction = Math.Max(0, supplementaryInsuranceInstallment);
            result.CarInsuranceDeduction = Math.Max(0, carInsuranceInstallment);
            result.OtherDeductions = Math.Max(0, otherDeductions);

            CalculateTax(settings, taxBrackets, result);
            result.LoanDeduction = loanInstallment;

            result.TotalDeductions = result.AbsenceDeduction + result.InsuranceEmployee
                                   + result.SupplementaryInsuranceDeduction + result.CarInsuranceDeduction
                                   + result.OtherDeductions + result.IncomeTax + result.LoanDeduction;
            result.NetPay = result.TotalEarnings - result.TotalDeductions;
            if (result.NetPay < 0) result.NetPay = 0;

            return result;
        }

        protected abstract void CalculateBaseSalary(Employee emp, Attendance att, SalarySettings settings, PayrollResult result);
        protected abstract void CalculateOvertimeAndAbsence(Employee emp, Attendance att, SalarySettings settings, PayrollResult result, decimal overtimeBase, decimal monthlyWorkingHours);

        protected virtual void CalculateAllowances(Employee emp, Attendance att, SalarySettings settings, PayrollResult result)
        {
            decimal dayRatio = Math.Min(1m, Math.Max(0m, att.WorkingDays / 30m));

            result.YearsBonus = Math.Round(emp.YearsBonus * dayRatio, 0);
            result.HousingAllowance = Math.Round(settings.HousingAllowance * dayRatio, 0);
            result.GroceryAllowance = Math.Round(settings.FoodAllowance * dayRatio, 0);

            string marital = StringHelper.NormalizePersianText(emp.MaritalStatus ?? "");
            result.MarriageAllowance = (marital.Contains("متاهل") || marital.Contains("متأهل"))
                ? Math.Round(settings.MarriageAllowance * dayRatio, 0) : 0;

            decimal childRate = settings.ChildAllowanceRate > 1 ? settings.ChildAllowanceRate / 100m : settings.ChildAllowanceRate;
            result.ChildAllowance = Math.Round(settings.MinimumWage * childRate * emp.ChildrenCount * dayRatio, 0);

            result.AttractionAllowance = Math.Round(emp.AttractionAllowance * dayRatio, 0);
            result.ResponsibilityAllowance = Math.Round(emp.ResponsibilityAllowance * dayRatio, 0);
            result.ManagementAllowance = Math.Round(emp.ManagementAllowance * dayRatio, 0);
            result.TechnicalAllowance = Math.Round(emp.TechnicalAllowance * dayRatio, 0);
            result.TransportationAllowance = Math.Round(emp.TransportationAllowance * dayRatio, 0);
            result.OtherAllowances = Math.Round(emp.OtherAllowances * dayRatio, 0);
        }

        protected virtual void CalculateInsurance(Employee emp, SalarySettings settings, PayrollResult result)
        {
            if (!emp.HasInsurance) return;

            decimal insuranceBase = 0;
            if (settings.InsuranceIncludeBaseSalary) insuranceBase += result.BaseSalary;
            if (settings.InsuranceIncludeYearsBonus) insuranceBase += result.YearsBonus;
            if (settings.InsuranceIncludeAttraction) insuranceBase += result.AttractionAllowance;
            if (settings.InsuranceIncludeResponsibility) insuranceBase += result.ResponsibilityAllowance;
            if (settings.InsuranceIncludeManagement) insuranceBase += result.ManagementAllowance;
            if (settings.InsuranceIncludeTechnical) insuranceBase += result.TechnicalAllowance;
            if (settings.InsuranceIncludeHousing) insuranceBase += result.HousingAllowance;
            if (settings.InsuranceIncludeGrocery) insuranceBase += result.GroceryAllowance;
            if (settings.InsuranceIncludeChild) insuranceBase += result.ChildAllowance;
            if (settings.InsuranceIncludeMarriage) insuranceBase += result.MarriageAllowance;
            if (settings.InsuranceIncludeTransportation) insuranceBase += result.TransportationAllowance;
            if (settings.InsuranceIncludeOther) insuranceBase += result.OtherAllowances;

            insuranceBase += result.OvertimePay;

            decimal cap = settings.MinimumWage * 7m;
            if (insuranceBase > cap) insuranceBase = cap;

            result.InsuranceEmployee = Math.Round(insuranceBase * AsRate(settings.InsuranceEmployeeRate), 0);
            result.InsuranceEmployer = Math.Round(insuranceBase * AsRate(settings.InsuranceEmployerRate), 0);
        }

        protected virtual void CalculateTax(SalarySettings settings, List<TaxBracketDto> taxBrackets, PayrollResult result)
        {
            decimal taxBase = 0;
            if (settings.TaxIncludeBaseSalary) taxBase += result.BaseSalary;
            if (settings.TaxIncludeYearsBonus) taxBase += result.YearsBonus;
            if (settings.TaxIncludeAttraction) taxBase += result.AttractionAllowance;
            if (settings.TaxIncludeResponsibility) taxBase += result.ResponsibilityAllowance;
            if (settings.TaxIncludeManagement) taxBase += result.ManagementAllowance;
            if (settings.TaxIncludeTechnical) taxBase += result.TechnicalAllowance;
            if (settings.TaxIncludeHousing) taxBase += result.HousingAllowance;
            if (settings.TaxIncludeGrocery) taxBase += result.GroceryAllowance;
            if (settings.TaxIncludeChild) taxBase += result.ChildAllowance;
            if (settings.TaxIncludeMarriage) taxBase += result.MarriageAllowance;
            if (settings.TaxIncludeTransportation) taxBase += result.TransportationAllowance;
            if (settings.TaxIncludeOtherAllowances) taxBase += result.OtherAllowances;

            taxBase += result.OvertimePay;
            taxBase -= result.InsuranceEmployee;
            taxBase -= result.SupplementaryInsuranceDeduction;

            if (taxBase < 0) taxBase = 0;

            // 🔴 ساده‌سازی: حالا فقط پله‌ها اعمال می‌شوند. اگر پله اول ۰٪ باشد، معافیت خودکار اعمال می‌شود.
            if (taxBase > 0)
            {
                decimal annualTaxable = taxBase * 12m;
                List<TaxBracketDto> brackets = (taxBrackets == null || taxBrackets.Count == 0) ? GetDefaultTaxBrackets() : taxBrackets;
                decimal annualTax = ApplyProgressiveTax(annualTaxable, brackets);
                decimal monthlyTax = annualTax / 12m;

                result.IncomeTax = TaxRoundingUnit > 0
                    ? Math.Round(monthlyTax / TaxRoundingUnit, MidpointRounding.AwayFromZero) * TaxRoundingUnit
                    : monthlyTax;
            }
            else result.IncomeTax = 0;
        }

        protected static decimal CalculateOvertimeBase(SalarySettings settings, PayrollResult result)
        {
            decimal overtimeBase = 0;
            if (settings.OvertimeIncludeBaseSalary) overtimeBase += result.BaseSalary;
            if (settings.OvertimeIncludeYearsBonus) overtimeBase += result.YearsBonus;
            if (settings.OvertimeIncludeAttraction) overtimeBase += result.AttractionAllowance;
            if (settings.OvertimeIncludeResponsibility) overtimeBase += result.ResponsibilityAllowance;
            if (settings.OvertimeIncludeManagement) overtimeBase += result.ManagementAllowance;
            if (settings.OvertimeIncludeTechnical) overtimeBase += result.TechnicalAllowance;
            if (settings.OvertimeIncludeHousing) overtimeBase += result.HousingAllowance;
            if (settings.OvertimeIncludeGrocery) overtimeBase += result.GroceryAllowance;
            if (settings.OvertimeIncludeChild) overtimeBase += result.ChildAllowance;
            if (settings.OvertimeIncludeMarriage) overtimeBase += result.MarriageAllowance;
            if (settings.OvertimeIncludeTransportation) overtimeBase += result.TransportationAllowance;
            if (settings.OvertimeIncludeOther) overtimeBase += result.OtherAllowances;
            return overtimeBase;
        }

        private static void CalculateTotalEarnings(PayrollResult result)
        {
            result.TotalEarnings = result.BaseSalary + result.YearsBonus + result.HousingAllowance + result.GroceryAllowance +
                                   result.ChildAllowance + result.AttractionAllowance + result.ResponsibilityAllowance +
                                   result.ManagementAllowance + result.TechnicalAllowance + result.MarriageAllowance +
                                   result.TransportationAllowance + result.OtherAllowances + result.OvertimePay;
        }

        protected static decimal AsRate(decimal rate) => rate > 1m ? rate / 100m : rate;

        private static decimal ApplyProgressiveTax(decimal annualTaxable, List<TaxBracketDto> brackets)
        {
            if (annualTaxable <= 0 || brackets == null || brackets.Count == 0) return 0;
            var sorted = brackets.OrderBy(b => b.FromAmount).ToList();
            decimal annualTax = 0m; decimal remaining = annualTaxable;

            for (int i = 0; i < sorted.Count && remaining > 0; i++)
            {
                var b = sorted[i]; decimal rate = AsRate(b.TaxRate);
                decimal bracketSize = b.ToAmount.HasValue ? b.ToAmount.Value - b.FromAmount : decimal.MaxValue;
                if (bracketSize <= 0) continue;
                decimal amountInBracket = Math.Min(remaining, bracketSize);
                if (amountInBracket <= 0) continue;
                annualTax += amountInBracket * rate;
                remaining -= amountInBracket;
            }
            if (remaining > 0) annualTax += remaining * AsRate(sorted[sorted.Count - 1].TaxRate);
            return annualTax;
        }

        private static List<TaxBracketDto> GetDefaultTaxBrackets() => new()
        {
            new() { FromAmount = 0, ToAmount = 1_000_000_000, TaxRate = 0.10m },
            new() { FromAmount = 1_000_000_000, ToAmount = 1_500_000_000, TaxRate = 0.15m },
            new() { FromAmount = 1_500_000_000, ToAmount = 2_300_000_000, TaxRate = 0.20m },
            new() { FromAmount = 2_300_000_000, ToAmount = null, TaxRate = 0.30m }
        };
    }

    internal sealed class ContractualPayrollCalculator : BasePayrollCalculator
    {
        protected override void CalculateBaseSalary(Employee emp, Attendance att, SalarySettings settings, PayrollResult result)
        {
            result.BaseSalary = emp.BaseSalary;
        }

        protected override void CalculateOvertimeAndAbsence(Employee emp, Attendance att, SalarySettings settings, PayrollResult result, decimal overtimeBase, decimal monthlyWorkingHours)
        {
            decimal multiplier = settings.AbsenceMultiplier > 0 ? settings.AbsenceMultiplier : 1m;

            if (att.AbsenceHours > 0)
            {
                decimal hourlyRate = emp.BaseSalary / monthlyWorkingHours;
                result.AbsenceDeduction = Math.Round(att.AbsenceHours * hourlyRate * multiplier, 0);
            }
            else if (att.WorkingDays < 30)
            {
                decimal dailyRate = emp.BaseSalary / 30m;
                result.AbsenceDeduction = Math.Round((30 - att.WorkingDays) * dailyRate * multiplier, 0);
            }

            if (att.NetOvertime > 0)
            {
                decimal hourlyRate = overtimeBase / monthlyWorkingHours;
                result.OvertimePay = Math.Round(hourlyRate * 1.4m * att.NetOvertime, 0);
            }
        }
    }

    internal sealed class DailyWagePayrollCalculator : BasePayrollCalculator
    {
        protected override void CalculateBaseSalary(Employee emp, Attendance att, SalarySettings settings, PayrollResult result)
        {
            result.BaseSalary = Math.Round(att.WorkingDays * emp.DailyWage, 0);
        }

        protected override void CalculateOvertimeAndAbsence(Employee emp, Attendance att, SalarySettings settings, PayrollResult result, decimal overtimeBase, decimal monthlyWorkingHours)
        {
            decimal hourlyRate = emp.DailyWage / 8m;
            decimal multiplier = settings.AbsenceMultiplier > 0 ? settings.AbsenceMultiplier : 1m;

            if (att.NetOvertime > 0)
                result.OvertimePay = Math.Round(hourlyRate * att.NetOvertime, 0);
            else if (att.NetOvertime < 0)
                result.AbsenceDeduction = Math.Round(hourlyRate * Math.Abs(att.NetOvertime) * multiplier, 0);
        }

        protected override void CalculateAllowances(Employee emp, Attendance att, SalarySettings settings, PayrollResult result)
        {
            // روزمزد: بدون مزایای ماهانه
        }

        protected override void CalculateInsurance(Employee emp, SalarySettings settings, PayrollResult result)
        {
            if (!emp.HasInsurance) return;

            decimal insuranceBase = result.BaseSalary + result.OvertimePay - result.AbsenceDeduction;
            if (insuranceBase < 0) insuranceBase = 0;

            result.InsuranceEmployee = Math.Round(insuranceBase * AsRate(settings.InsuranceEmployeeRate), 0);
            result.InsuranceEmployer = Math.Round(insuranceBase * AsRate(settings.InsuranceEmployerRate), 0);
        }
    }

    internal sealed class HourlyPayrollCalculator : BasePayrollCalculator
    {
        protected override void CalculateBaseSalary(Employee emp, Attendance att, SalarySettings settings, PayrollResult result)
        {
            result.BaseSalary = Math.Round(emp.HourlyWage * att.HoursWorked, 0);
        }

        protected override void CalculateOvertimeAndAbsence(Employee emp, Attendance att, SalarySettings settings, PayrollResult result, decimal overtimeBase, decimal monthlyWorkingHours)
        {
            // ساعتی: بدون اضافه‌کاری
        }

        protected override void CalculateAllowances(Employee emp, Attendance att, SalarySettings settings, PayrollResult result)
        {
            // ساعتی: بدون مزایای ماهانه
        }

        protected override void CalculateInsurance(Employee emp, SalarySettings settings, PayrollResult result)
        {
            if (!emp.HasInsurance) return;

            decimal insuranceBase = result.BaseSalary;
            if (insuranceBase < 0) insuranceBase = 0;

            result.InsuranceEmployee = Math.Round(insuranceBase * AsRate(settings.InsuranceEmployeeRate), 0);
            result.InsuranceEmployer = Math.Round(insuranceBase * AsRate(settings.InsuranceEmployerRate), 0);
        }
    }
}