using System;

namespace Smp.Models;

public class TankhahList
{
    public int Id { get; set; }
    public int TankhahId { get; set; }
    public string Title { get; set; } = ""; // مثل: لیست 1
    public DateTime? ChargeDate { get; set; }
    public decimal ChargeAmount { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>یک شارژ ثبت‌شده‌ی تنخواه (انتقال از حساب بانکی به یک لیست تنخواه)</summary>
public class TankhahCharge
{
    public int Id { get; set; }
    public int ListId { get; set; }
    public string ListTitle { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string BankTitle { get; set; } = "";
    public string TankhahAccountTitle { get; set; } = "";
    public string MatchText { get; set; } = "";
    public string Description { get; set; } = "";
}
