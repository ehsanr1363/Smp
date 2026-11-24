using System;

namespace Smp.Models;

public class AccountTransfer
{
    public int Id { get; set; }
    public int SourceAccountId { get; set; }
    public string? SourceAccountTitle { get; set; }
    public int DestinationAccountId { get; set; }
    public string? DestinationAccountTitle { get; set; }
    public decimal Amount { get; set; }
    public decimal TransferFee { get; set; }
    public DateTime TransferDate { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }

    // ✅ ستون جدید: نوع انتقال
    // مقادیر: BankToBank | BankToTankhah | TankhahToBank | TankhahToTankhah
    public string? TransferType { get; set; }
}
