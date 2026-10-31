using System;
using PersonnelApp.Helpers;

namespace PersonnelApp.Models
{
    public class EmployeeCarInsurance
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = "";

        public decimal TotalAmount { get; set; }
        public decimal MonthlyInstallment { get; set; }
        public decimal RemainingAmount { get; set; }

        public int StartYear { get; set; }
        public int StartMonth { get; set; }
        public DateTime StartDate { get; set; }             // تاریخ ثبت
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public int TotalInstallments => MonthlyInstallment > 0
            ? (int)Math.Ceiling(TotalAmount / MonthlyInstallment)
            : 0;

        public int PaidInstallments => MonthlyInstallment > 0
            ? (int)Math.Floor((TotalAmount - RemainingAmount) / MonthlyInstallment)
            : 0;

        public int RemainingInstallments => TotalInstallments - PaidInstallments;

        public string StartPeriod =>
            StartYear > 0 && StartMonth > 0
                ? $"{StringHelper.ConvertToPersianDigits(StartYear.ToString())}/{StringHelper.ConvertToPersianDigits(StartMonth.ToString("00"))}"
                : "---";
    }
}