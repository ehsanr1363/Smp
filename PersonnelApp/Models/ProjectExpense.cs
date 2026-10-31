using System;

namespace PersonnelApp.Models
{
    public class ProjectExpense
    {
        public int Id { get; set; }
        public int ClientContractId { get; set; }
        public int? PartyId { get; set; }

        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // فیلد نمایشی برای نام طرف حساب
        public string PartyName { get; set; }
    }
}