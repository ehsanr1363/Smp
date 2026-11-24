using System;

namespace Smp.Models;

public class CashCheck
{
    public int Id { get; set; }
    public string CheckNumber { get; set; } = "";
    public string? BankName { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public string CheckType { get; set; } = "";
    public string Status { get; set; } = "در جریان";

    public int? PartyId { get; set; }
    public string? PartyName { get; set; }

    public int? AccountId { get; set; }
    public string? AccountTitle { get; set; }

    public string? SayadNumber { get; set; }

    public string? RecipientName { get; set; }
    public string? RecipientNationalId { get; set; }
    public bool IsLegalEntity { get; set; }

    // ═══ ارتباط با قرارداد کارفرما ═══
    public int? ContractId { get; set; }
    public string? ContractTitle { get; set; }

    // 👈 NEW: ارتباط با صورت وضعیت (برای قفل شدن در صورت وضعیت)
    public int? ClientStatementId { get; set; }

    // ✅ NEW: ارتباط با قرارداد پیمانکار
    public int? ContractorContractId { get; set; }

    // ✅ NEW: نوع پرداخت
    public string? PaymentScope { get; set; }
    // Administrative | DirectPurchase | InformalContractor | ContractorContract

    // ✅ NEW: ماهیت هزینه
    public string? CostType { get; set; }

    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
