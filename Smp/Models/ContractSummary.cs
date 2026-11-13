using System;

namespace SMP.Models
{
    public class ContractSummary
    {
        public int ContractId { get; set; }
        public string ContractNumber { get; set; }
        public string ContractorName { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalWorkDone { get; set; }
        public decimal TotalNetPayable { get; set; }
        public decimal TotalPaid { get; set; }

        // فیلدهای محاسباتی
        public decimal RemainingContractAmount => TotalAmount - TotalWorkDone;
        public decimal RemainingDebt => TotalNetPayable - TotalPaid;
        public double ProgressPercent => TotalAmount > 0 ? (double)(TotalWorkDone / TotalAmount) * 100 : 0;
    }
}