using System;
namespace PersonnelApp.Models
{
    public class CashTransaction
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public string AccountTitle { get; set; } = "";
        public DateTime TransactionDate { get; set; }
        public decimal Amount { get; set; }
        public string TransactionType { get; set; } = "";
        public string? Category { get; set; }
        public string? Description { get; set; }
        public string? ReferenceNumber { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}