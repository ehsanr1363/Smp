using System;

namespace PersonnelApp.Models
{
    public class ContractorPayment
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentType { get; set; } // نوع پرداخت
        public string Description { get; set; } // توضیحات
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}