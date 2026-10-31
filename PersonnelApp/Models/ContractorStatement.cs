using System;

namespace PersonnelApp.Models
{
    public class ContractorStatement
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public string StatementNumber { get; set; } // شماره صورت وضعیت
        public DateTime StatementDate { get; set; } // تاریخ صورت وضعیت

        public decimal GrossAmount { get; set; } // مبلغ کارکرد (ناخالص)
        public decimal VAT { get; set; } // ارزش افزوده
        public decimal RetentionDeduction { get; set; } // حسن انجام کار
        public decimal InsuranceDeduction { get; set; } // بیمه
        public decimal OtherDeductions { get; set; } // سایر کسورات
        public decimal AdvanceRecovery { get; set; } // کسر پیش‌پرداخت

        public decimal NetPayableAmount { get; set; } // مبلغ خالص
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // فیلدهای محاسباتی (برای نمایش در گرید گزارش)
        public decimal TotalAmount => GrossAmount + VAT; // جمع کل
        public decimal TotalDeductions => RetentionDeduction + InsuranceDeduction + OtherDeductions + AdvanceRecovery; // جمع کسورات
    }
}