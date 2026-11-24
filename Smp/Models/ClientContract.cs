using System;

namespace Smp.Models;

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

    // قرارداد دو بخشی: ۱) کارکرد (بدون ارزش افزوده)  ۲) حق‌الزحمه (مشمول ارزش افزوده)
    // false = رفتار قدیمی (ارزش افزوده روی کل کارکرد)؛ قراردادهای موجود دست‌نخورده می‌مانند.
    public bool HasFeeSection { get; set; }
    // درصد حق‌الزحمه نسبت به مبلغ کارکرد (پیش‌فرضِ محاسبه؛ در صورت‌وضعیت قابل ویرایش دستی است)
    public decimal FeePercentage { get; set; }

    // اتصال به لیست حقوق: فقط کارگران «روزمزد» با همین «محل خدمت» در محاسبه می‌آیند
    public string WorkLocation { get; set; } = string.Empty;
    // حق‌الزحمه (حالت لیست حقوق) = (جمع حقوق و اضافه‌کار − بیمه ۷٪) × OverheadPercentage ÷ FeeDivisorPercentage
    // هر دو «عدد درصد» هستند: بالاسری ۱۰ (یعنی ۱۰٪)، مقسوم‌علیه ۸۳٫۳۳۳۳ (یعنی ۰٫۸۳۳۳)
    public decimal OverheadPercentage { get; set; }
    public decimal FeeDivisorPercentage { get; set; }

    /// <summary>قرارداد دو بخشی که کارکرد و حق‌الزحمه‌اش از لیست حقوق کارگران روزمزد محاسبه می‌شود</summary>
    public bool IsPayrollBased => HasFeeSection && !string.IsNullOrWhiteSpace(WorkLocation);

    public string DisplayName { get; set; } = string.Empty;
}
