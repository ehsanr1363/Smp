using System;

namespace SMP.Models
{
    public class ContractorPayment
    {
        public int Id { get; set; }
        public int ContractId { get; set; }

        // ✅ NEW: شناسه قرارداد کارفرما (برای گزارش‌گیری‌های یکپارچه)
        public int? ClientContractId { get; set; }

        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentType { get; set; } = "";

        // ✅ NEW: ماهیت هزینه (خرید کالا / خرید خدمات)
        public string? CostType { get; set; }

        public string Description { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}