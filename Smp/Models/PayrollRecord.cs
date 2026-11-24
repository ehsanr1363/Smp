namespace Smp.Models;

public class PayrollRecord
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }

    public decimal BaseSalary { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }

    public decimal InsuranceEmployee { get; set; }
    public decimal InsuranceEmployer { get; set; }
    public decimal IncomeTax { get; set; }

    public decimal LoanDeduction { get; set; }
    public decimal AdvanceDeduction { get; set; }
    public decimal SupplementaryInsuranceDeduction { get; set; }
    public decimal CarInsuranceDeduction { get; set; }
    public decimal OtherDeductions { get; set; }

    // 👈 فیلد جدید
    public decimal OvertimePay { get; set; }
    public decimal AbsenceDeduction { get; set; }

    public decimal RemainingDebt { get; set; }
}
