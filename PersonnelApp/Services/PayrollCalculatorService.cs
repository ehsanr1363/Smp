using PersonnelApp.Data;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;

namespace PersonnelApp.Services
{
    public class PayrollCalculatorService
    {
        // 👈 گرد کردن مالیات به نزدیک‌ترین ۱۰ ریال (رقم آخر صفر می‌شود)
        private const decimal TaxRoundingUnit = 10m;

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

            string type = StringHelper.NormalizePersianText(result.PersonnelType).Replace(" ", "");
            decimal dayRatio = Math.Min(1m, Math.Max(0m, att.WorkingDays / 30m));

            // ─── ۲. حقوق پایه ───
            decimal monthlyBaseEquivalent;
            switch (type)
            {
                case "روزمزد":
                    result.BaseSalary = Math.Round((att.WorkingDays * emp.DailyWage) + (att.HolidayDays * (emp.DailyWage * 1.4m)), 0);
                    monthlyBaseEquivalent = emp.DailyWage * 30m;
                    break;
                case "ساعتی":
                    result.BaseSalary = Math.Round(emp.HourlyWage * att.HoursWorked, 0);
                    monthlyBaseEquivalent = emp.HourlyWage * settings.MonthlyWorkingHours;
                    break;
                default:
                    result.BaseSalary = emp.BaseSalary;
                    monthlyBaseEquivalent = emp.BaseSalary;
                    break;
            }

            // ─── ۳. تفکیک کامل اقلام مزایا (از تنظیمات و از کارمند) ───
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

            // ─── ۴. محاسبه اضافه‌کاری (مجموع تیک‌خورده‌ها / ساعت ماهانه * خالص اضافه کار) ───
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

            decimal monthlyWorkingHours = settings.MonthlyWorkingHours > 0 ? settings.MonthlyWorkingHours : 220m;

            if (type == "روزمزد")
            {
                decimal hourlyRate = (emp.DailyWage * 30m) / monthlyWorkingHours;
                if (att.NetOvertime > 0) result.OvertimePay = Math.Round(hourlyRate * 1.4m * att.NetOvertime, 0);
                else if (att.NetOvertime < 0) result.AbsenceDeduction += Math.Round(hourlyRate * Math.Abs(att.NetOvertime), 0);
            }
            else if (type == "ساعتی")
            {
                if (att.OvertimeHours > 0) result.OvertimePay = Math.Round(emp.HourlyWage * 1.4m * att.OvertimeHours, 0);
            }
            else // قراردادی / ماهانه
            {
                if (att.AbsenceHours > 0)
                {
                    decimal hourlyRate = emp.BaseSalary / monthlyWorkingHours;
                    result.AbsenceDeduction = Math.Round(att.AbsenceHours * hourlyRate, 0);
                }
                else if (att.WorkingDays < 30)
                {
                    decimal dailyRate = emp.BaseSalary / 30m;
                    result.AbsenceDeduction = Math.Round((30 - att.WorkingDays) * dailyRate, 0);
                }

                // 👈 فقط از NetOvertime استفاده می‌شود تا دوبار حساب نشود
                if (att.NetOvertime > 0)
                {
                    decimal hourlyRate = overtimeBase / monthlyWorkingHours;
                    result.OvertimePay = Math.Round(hourlyRate * 1.4m * att.NetOvertime, 0);
                }
            }

            // ─── ۵. جمع مزایا ───
            result.TotalEarnings = result.BaseSalary + result.YearsBonus + result.HousingAllowance + result.GroceryAllowance +
                                   result.ChildAllowance + result.AttractionAllowance + result.ResponsibilityAllowance +
                                   result.ManagementAllowance + result.TechnicalAllowance + result.MarriageAllowance +
                                   result.TransportationAllowance + result.OtherAllowances + result.OvertimePay;

            // ─── ۶. بیمه (بر اساس چک‌باکس‌ها) ───
            if (emp.HasInsurance)
            {
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

                decimal empRate = AsRate(settings.InsuranceEmployeeRate);
                decimal emprRate = AsRate(settings.InsuranceEmployerRate);
                result.InsuranceEmployee = Math.Round(insuranceBase * empRate, 0);
                result.InsuranceEmployer = Math.Round(insuranceBase * emprRate, 0);
            }

            // ─── ۷. کسورات دیگر ───
            result.SupplementaryInsuranceDeduction = Math.Max(0, supplementaryInsuranceInstallment);
            result.CarInsuranceDeduction = Math.Max(0, carInsuranceInstallment);
            result.OtherDeductions = Math.Max(0, otherDeductions);

            // ─── ۸. مالیات (بر اساس چک‌باکس‌ها + تشخیص هوشمند معافیت) ───
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

            // 👈 تشخیص هوشمند: اگر پله اول 0% بود، معافیت دوباره کسر نشود
            bool exemptionAlreadyInBrackets = false;
            if (taxBrackets != null && taxBrackets.Count > 0)
            {
                var firstBracket = taxBrackets.OrderBy(b => b.FromAmount).FirstOrDefault();
                if (firstBracket != null && firstBracket.FromAmount == 0 && AsRate(firstBracket.TaxRate) == 0)
                {
                    exemptionAlreadyInBrackets = true;
                }
            }

            if (!exemptionAlreadyInBrackets && settings.TaxExemptionLimit > 0)
            {
                decimal monthlyExemption = settings.TaxExemptionLimit / 12m;
                taxBase -= monthlyExemption;
                if (taxBase < 0) taxBase = 0;
            }

            if (taxBase > 0)
            {
                decimal annualTaxable = taxBase * 12m;
                List<TaxBracketDto> brackets = (taxBrackets == null || taxBrackets.Count == 0) ? GetDefaultTaxBrackets() : taxBrackets;
                decimal annualTax = ApplyProgressiveTax(annualTaxable, brackets);
                decimal monthlyTax = annualTax / 12m;

                // 👈 گرد کردن به ۱۰ ریال
                if (TaxRoundingUnit > 0)
                    result.IncomeTax = Math.Round(monthlyTax / TaxRoundingUnit, MidpointRounding.AwayFromZero) * TaxRoundingUnit;
                else
                    result.IncomeTax = monthlyTax;
            }
            else result.IncomeTax = 0;

            // ─── ۹. کسر وام ───
            result.LoanDeduction = loanInstallment;

            // ─── ۱۰. جمع کسورات و خالص ───
            result.TotalDeductions = result.AbsenceDeduction + result.InsuranceEmployee + result.SupplementaryInsuranceDeduction + result.CarInsuranceDeduction + result.OtherDeductions + result.IncomeTax + result.LoanDeduction;

            result.NetPay = result.TotalEarnings - result.TotalDeductions;
            if (result.NetPay < 0) result.NetPay = 0;

            return result;
        }

        private static decimal AsRate(decimal rate) => rate > 1m ? rate / 100m : rate;

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
}