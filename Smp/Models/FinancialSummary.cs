namespace Smp.Models;

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

    // ✅ هزینه‌های تفکیک‌شده جدید
    public decimal TotalAdminExpenses { get; set; }           // هزینه‌های اداری (حقوق، اجاره، ...)
    public decimal TotalDirectProjectExpenses { get; set; }   // خریدهای مستقیم پروژه (کالا/خدمات)

    // 👇 هزینه‌های فاکتوری (افراد بدون قرارداد - سیستم قدیمی)
    public decimal TotalInvoiceExpenses { get; set; }

    // محاسبات سود و زیان (با احتساب تمام هزینه‌ها)
    // سود ناخالص = درآمدهای صورتحساب شده - (هزینه پیمانکاران + خریدهای مستقیم + فاکتورها)
    public decimal GrossProfit => TotalClientInvoiced - (TotalContractorInvoiced + TotalDirectProjectExpenses + TotalInvoiceExpenses);

    // جریان نقدی = پول واقعی وارد شده - پول واقعی خارج شده
    public decimal NetCashFlow => TotalClientReceived - (TotalContractorPaid + TotalAdminExpenses + TotalDirectProjectExpenses + TotalInvoiceExpenses);
}
