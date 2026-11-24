namespace Smp.Models;

public class EmployeeBankAccount
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string AccountNumber { get; set; } = "";
    public string ShebaNumber { get; set; } = "";
    public string BankCode { get; set; } = "";
    public string BankName { get; set; } = "";
    public bool IsMainAccount { get; set; } = false;
    public string AccountType { get; set; } = "حقوق";

    // ✅ فیلد جدید: وضعیت فعال یا غیرفعال بودن حساب
    public bool IsActive { get; set; } = true;
}
