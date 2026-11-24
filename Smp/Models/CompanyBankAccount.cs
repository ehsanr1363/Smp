using System;

namespace Smp.Models;

public class CompanyBankAccount
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? AccountNumber { get; set; }
    public string? BankCode { get; set; }
    public string? BankName { get; set; }
    public string? AccountType { get; set; }
    public string? Branch { get; set; }

    public decimal InitialBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    // مانده واقعی بانک همان CurrentBalance است و چک‌های پاس‌نشده روی آن اثری ندارند.
    // این دو فقط برای اطلاع نمایش داده می‌شوند:
    public decimal PendingChecksAmount { get; set; }           // چک‌های پرداختی در جریان
    public decimal PendingIncomingChecksAmount { get; set; }   // چک‌های دریافتی در انتظار وصول

    public bool IsCashDesk { get; set; }
    public bool IsActive { get; set; }
    public bool HasCheckbook { get; set; }

    public string? CheckbookSerial { get; set; }
    public string? CheckbookSayad { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// عنوان خودکار حساب بانکی (بدون ایموجی) که در ستون Title ذخیره می‌شود تا گزارش‌ها و دفتر کل نام معنی‌دار نشان دهند.
    /// ورودی دستی «عنوان اضافه» حذف شده است؛ برای صندوق/تنخواه همان Title خودش حفظ می‌شود.
    /// </summary>
    public string AutoTitle
    {
        get
        {
            if (IsCashDesk) return Title ?? "";
            string bankName = string.IsNullOrWhiteSpace(BankName) ? "نامشخص" : BankName.Trim();
            string accType = string.IsNullOrWhiteSpace(AccountType) ? "حساب" : AccountType.Trim();
            string branch = string.IsNullOrWhiteSpace(Branch) ? "" : $" - شعبه {Branch.Trim()}";
            return $"{accType} - {bankName}{branch}";
        }
    }

    // ✅ نمایش دقیقاً به شکل: [نوع حساب] - [نام بانک] - [شعبه]
    public string DisplayTitle
    {
        get
        {
            if (IsCashDesk) return $"💰 {Title} (صندوق)";

            string bankName = string.IsNullOrWhiteSpace(BankName) ? "نامشخص" : BankName.Trim();
            string accType = string.IsNullOrWhiteSpace(AccountType) ? "حساب" : AccountType.Trim();
            string branch = string.IsNullOrWhiteSpace(Branch) ? "" : $" - شعبه {Branch.Trim()}";

            // ❌ عنوان اضافه (Title) در اینجا اصلاً استفاده نمی‌شود تا پرانتز ایجاد نشود
            return $"🏦 {accType} - {bankName}{branch}";
        }
    }

    // ── ستون‌های نمایشی گرید «مدیریت حساب‌های بانکی» ──
    /// <summary>تیتر ردیف: نام بانک (برای صندوق همان Title)</summary>
    public string BankTitle
    {
        get
        {
            if (IsCashDesk) return Title ?? "";
            if (!string.IsNullOrWhiteSpace(BankName)) return BankName.Trim();
            return string.IsNullOrWhiteSpace(Title) ? "نامشخص" : Title.Trim();
        }
    }

    /// <summary>متن کوچک زیر تیتر: «نوع حساب · شعبه»</summary>
    public string AccountSubtitle
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(AccountType)) parts.Add(AccountType.Trim());
            if (!string.IsNullOrWhiteSpace(Branch)) parts.Add("شعبه " + Branch.Trim());
            return string.Join(" · ", parts);
        }
    }

    public string StatusText => IsActive ? "فعال" : "غیرفعال";
    public string CheckbookText => HasCheckbook ? "دارد" : "ندارد";
}
