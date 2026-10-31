using System;

namespace PersonnelApp.Models
{
    public class SettlementRecord
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public DateTime SettlementDate { get; set; }

        public int YearsOfWork { get; set; }

        public decimal ServiceBonus { get; set; }    // سنوات
        public decimal LeaveBonus { get; set; }      // بازخرید مرخصی
        public decimal SeverancePay { get; set; }    // عیدی/پاداش

        public decimal RemainingLoan { get; set; }   // مانده وام

        public decimal NetSettlement => (ServiceBonus + LeaveBonus + SeverancePay) - RemainingLoan;
    }
}