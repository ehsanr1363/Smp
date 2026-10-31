using System;

namespace PersonnelApp.Models
{
    public class ClientPayment
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentType { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}