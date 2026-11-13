using System;

namespace SMP.Models
{
    public class ClientContract
    {
        public int Id { get; set; }
        public int PartyId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public string ContractRow { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public decimal Amount { get; set; }

        public DateTime? ContractDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? TerminationDate { get; set; }

        public string Status { get; set; } = "فعال";
        public DateTime? CreatedAt { get; set; }

        // 👈 فیلدهای درصدی برای قرارداد کارفرما
        public decimal VatPercentage { get; set; }
        public decimal RetentionPercentage { get; set; }
        public decimal InsurancePercentage { get; set; }
        public decimal AdvancePaymentPercentage { get; set; }
        public decimal TotalAdvanceAmount { get; set; }

        public string DisplayName { get; set; } = string.Empty;
    }
}