using System;

namespace SMP.Models
{
    public class CashTransaction
    {
        public int Id { get; set; }
        public string TableName { get; set; } = "CashTransactions"; // 👈 این خط اضافه شود

        public string? PaymentScope { get; set; }
        public int? ContractId { get; set; }
        public int? ContractorContractId { get; set; }
        public string? ContractTitle { get; set; }

        public int? ClientStatementId { get; set; }

        public DateTime TransactionDate { get; set; }

        // مبلغ نهایی (جمع کل)
        public decimal Amount { get; set; }

        public decimal Discount { get; set; }
        public decimal Shipping { get; set; }
        public decimal VAT { get; set; }

        // ✅ جدید: محاسبه مبلغ پایه (برای جلوگیری از تکرار فرمول در کدها)
        public decimal BaseAmount => Amount - Shipping + Discount - VAT;

        public string TransactionType { get; set; } = "نقدی";
        public string Direction { get; set; } = "پرداختی";

        public int? AccountId { get; set; }
        public string? AccountTitle { get; set; }

        public string? Category { get; set; }
        public string? CostType { get; set; }

        public string? RecipientName { get; set; }
        public string? RecipientSheba { get; set; }
        public string? TafsiliCode { get; set; }
        public string? TafsiliTitle { get; set; }

        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }

        public string BillingStatus => ClientStatementId.HasValue ? "🔒 صورت وضعیت شده" : "⏳ در انتظار صورت وضعیت";

        public string PaymentScopeDisplay
        {
            get
            {
                return PaymentScope switch
                {
                    "Administrative" => "اداری",
                    "DirectPurchase" => "خرید مستقیم پروژه",
                    "InformalContractor" => "پیمانکار بدون قرارداد",
                    "ContractorContract" => "پیمانکار دارای قرارداد",
                    _ => "نامشخص"
                };
            }
        }
    }
}