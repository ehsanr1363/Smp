using System;

namespace SMP.Models
{
    public class ProjectExpense
    {
        public int Id { get; set; }
        public int ClientContractId { get; set; }
        public int? PartyId { get; set; }

        public string InvoiceNumber { get; set; } = "";
        public DateTime InvoiceDate { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? CostType { get; set; }

        // ✅ NEW: آیدی صورت وضعیتی که این فاکتور در آن لحاظ شده است
        public int? ClientStatementId { get; set; }

        // ✅ NEW: وضعیت نمایشی برای گریدها
        public string BillingStatus => ClientStatementId.HasValue ? "🔒 صورت وضعیت شده" : "⏳ در انتظار صورت وضعیت";

        // فیلد نمایشی برای نام طرف حساب
        public string PartyName { get; set; } = "";
    }
}