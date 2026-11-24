using System;

namespace Smp.Models;

public class ContractorGuarantee
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public string GuaranteeNumber { get; set; }
    public string BankName { get; set; }
    public decimal Amount { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime MaturityDate { get; set; }
    public string GuaranteeType { get; set; }
    public string Status { get; set; } = "فعال";
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // 👇 این دو فیلد برای نمایش در گزارش جامع اضافه شدند 👇
    public string ContractNumber { get; set; }
    public string ContractorName { get; set; }
}
