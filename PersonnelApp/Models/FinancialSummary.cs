namespace PersonnelApp.Models
{
    public class FinancialSummary
    {
        // درآمدها (قراردادهای کارفرما)
        public decimal TotalClientContractAmount { get; set; }
        public decimal TotalClientInvoiced { get; set; }
        public decimal TotalClientReceived { get; set; }

        // هزینه‌ها (قراردادهای پیمانکار)
        public decimal TotalContractorContractAmount { get; set; }
        public decimal TotalContractorInvoiced { get; set; }
        public decimal TotalContractorPaid { get; set; }

        // 👇 هزینه‌های فاکتوری (افراد بدون قرارداد) 👇
        public decimal TotalInvoiceExpenses { get; set; }

        // محاسبات سود و زیان (با احتساب فاکتورها)
        public decimal GrossProfit => TotalClientInvoiced - (TotalContractorInvoiced + TotalInvoiceExpenses);
        public decimal NetCashFlow => TotalClientReceived - (TotalContractorPaid + TotalInvoiceExpenses);
    }
}