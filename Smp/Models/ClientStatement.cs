using System;

namespace Smp.Models;

public class ClientStatement
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public string StatementNumber { get; set; } = "";
    public DateTime StatementDate { get; set; }

    // مبالغ اصلی
    public decimal GrossAmount { get; set; }   // بخش ۱: کارکرد (بدون ارزش افزوده)
    public decimal FeeAmount { get; set; }     // بخش ۲: حق‌الزحمه (مشمول ارزش افزوده)
    public decimal VAT { get; set; }           // ارزش افزوده (فقط روی حق‌الزحمه، یا در قراردادهای قدیمی روی کارکرد)

    // کسورات تفکیک شده
    public decimal RetentionDeduction { get; set; } // کسر حسن انجام کار
    public decimal InsuranceDeduction { get; set; } // کسر بیمه
    public decimal AdvanceRecovery { get; set; }    // 👈 مستهلک شده پیش‌دریافت (فیلد جدید)
    public decimal OtherDeductions { get; set; }    // سایر کسورات

    // جمع کل کسورات و مبلغ خالص
    public decimal Deductions { get; set; }
    public decimal NetReceivableAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // فیلد محاسباتی برای نمایش جمع کل (مبلغ کارکرد + ارزش افزوده)
    public decimal TotalAmount => GrossAmount + FeeAmount + VAT;

    /// <summary>
    /// ✅ تنها فرمول خالص: (کارکرد + حق‌الزحمه + ارزش افزوده) − جمع کسورات تفکیکی.
    /// ریپازیتوری قبل از ذخیره این را صدا می‌زند تا مبلغ خالص هرگز با اجزایش ناسازگار نشود.
    /// </summary>
    public void RecalculateNet()
    {
        Deductions = RetentionDeduction + InsuranceDeduction + AdvanceRecovery + OtherDeductions;
        NetReceivableAmount = GrossAmount + FeeAmount + VAT - Deductions;
    }
}
