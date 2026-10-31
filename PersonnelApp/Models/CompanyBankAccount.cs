using System;

namespace PersonnelApp.Models
{
    public class CompanyBankAccount
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? AccountNumber { get; set; }
        public decimal InitialBalance { get; set; }
        public decimal CurrentBalance { get; set; }
        public bool IsCashDesk { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}