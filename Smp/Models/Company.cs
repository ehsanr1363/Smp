using System;

namespace SMP.Models
{
    public class Company
    {
        public int Id { get; set; } // کد شرکت
        public string Name { get; set; } = ""; // نام شرکت
        public string? NationalId { get; set; } // شناسه ملی شرکت
        public string? EconomicCode { get; set; } // کد اقتصادی شرکت
        public string? Address { get; set; } // آدرس شرکت
        public string? PostalCode { get; set; } // کد پستی شرکت
        public string? Phone { get; set; } // تلفن شرکت
        public string? WorkshopCode { get; set; } // کد کارگاه شرکت
        public byte[]? Logo { get; set; } // آرم شرکت
    }
}