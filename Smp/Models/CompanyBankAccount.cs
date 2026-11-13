using System;

namespace SMP.Models
{
    public class CompanyBankAccount
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? AccountNumber { get; set; }
        public string? BankCode { get; set; }
        public string? BankName { get; set; }

        public decimal InitialBalance { get; set; }
        public decimal CurrentBalance { get; set; }
        public bool IsCashDesk { get; set; }
        public bool IsActive { get; set; }
        public bool HasCheckbook { get; set; }

        // ✅ فیلدهای جدید برای مشخصات دسته چک
        public string? CheckbookSerial { get; set; }
        public string? CheckbookSayad { get; set; }

        public DateTime CreatedAt { get; set; }

        public string DisplayTitle => IsCashDesk
            ? $"💰 {Title} (صندوق)"
            : $"🏦 {Title}" +
              (string.IsNullOrEmpty(BankName) ? "" : $" — {BankName}") +
              (HasCheckbook ? " 📑" : "");
    }
}