using System;
namespace PersonnelApp.Models
{
    public class CashCheck
    {
        public int Id { get; set; }
        public string CheckNumber { get; set; } = "";
        public string? BankName { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Amount { get; set; }
        public string CheckType { get; set; } = "";
        public string Status { get; set; } = "در جریان";
        public int? PartyId { get; set; }
        public string? PartyName { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}