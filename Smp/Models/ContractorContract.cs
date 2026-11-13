using System;
using SMP.Helpers;

namespace SMP.Models
{
    public class ContractorContract
    {
        public int Id { get; set; }
        public int PartyId { get; set; }

        public int? ClientContractId { get; set; }
        public string ClientContractNumber { get; set; }

        public string ContractNumber { get; set; }
        public string WorkshopCode { get; set; }
        public string ContractRow { get; set; }
        public string Subject { get; set; }
        public decimal Amount { get; set; }
        public DateTime? ContractDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string Status { get; set; } = "فعال";
        public DateTime? TerminationDate { get; set; }

        public decimal GuaranteePercentage { get; set; }
        public decimal RetentionPercentage { get; set; }
        public decimal InsurancePercentage { get; set; }
        public decimal AdvancePaymentPercentage { get; set; }
        public decimal TotalAdvanceAmount { get; set; }
        public decimal VatPercentage { get; set; }

        // فیلدهای نمایشی
        public string PartyType { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string NationalCode { get; set; }
        public string CompanyName { get; set; }
        public string NationalId { get; set; }

        public string DisplayName
        {
            get
            {
                // ✅ FIX P1: استفاده از PartyTypeHelper
                if (PartyTypeHelper.IsLegal(PartyType)) return CompanyName ?? "";
                return $"{FirstName ?? ""} {LastName ?? ""}".Trim();
            }
        }

        public string Identifier
        {
            get
            {
                // ✅ FIX P1: استفاده از PartyTypeHelper
                return PartyTypeHelper.IsNatural(PartyType) ? NationalCode : NationalId;
            }
        }
    }
}