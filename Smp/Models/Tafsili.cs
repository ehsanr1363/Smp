using System;

namespace SMP.Models
{
    public class Tafsili
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";                    // کد تفصیلی
        public string PersonType { get; set; } = "شخص حقیقی";     // نوع شخص: شخص حقیقی یا شخص حقوقی
        public string FirstName { get; set; } = "";               // نام (حقیقی)
        public string LastName { get; set; } = "";                // نام خانوادگی (حقیقی)
        public string CompanyName { get; set; } = "";             // نام شرکت (حقوقی)
        public string Type { get; set; } = "پرسنل";               // نقش: پرسنل، پیمانکار، کارفرما، تأمین‌کننده
        public string? NationalCode { get; set; }                 // کد ملی (حقیقی)
        public string? NationalId { get; set; }                   // شناسه ملی (حقوقی)
        public string? EconomicCode { get; set; }                 // کد اقتصادی
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ✅ فیلد جدید شماره شبا که باعث خطا شده بود
        public string? ShebaNumber { get; set; }                  // شماره شبا

        // نام کامل (اگر کلمه حقوقی در نوع شخص باشد، نام شرکت را برمی‌گرداند)
        public string Name => PersonType.Contains("حقوقی")
            ? (CompanyName ?? "").Trim()
            : $"{FirstName} {LastName}".Trim();

        public string DisplayText => string.IsNullOrWhiteSpace(Name) ? Code : $"{Code} - {Name}";
    }
}