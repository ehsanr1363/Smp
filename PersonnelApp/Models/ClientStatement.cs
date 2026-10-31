using System;

namespace PersonnelApp.Models
{
    public class ClientStatement
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public string StatementNumber { get; set; }
        public DateTime StatementDate { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal VAT { get; set; }
        public decimal Deductions { get; set; }
        public decimal NetReceivableAmount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public decimal TotalAmount => GrossAmount + VAT;
    }
}