using System;

namespace Smp.Models;

public class Asset
{
    public int Id { get; set; }
    public string AssetCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Category { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal PurchaseAmount { get; set; }
    public string Status { get; set; } = AssetStatuses.Active;
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public int? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public int DocCount { get; set; }            // تعداد اسناد خرید متصل (فقط برای نمایش در لیست)
}

public static class AssetStatuses
{
    public const string Active = "فعال";
    public const string InRepair = "در تعمیر";
    public const string Scrapped = "اسقاط";
    public const string Sold = "فروخته‌شده";
    public static readonly string[] All = { Active, InRepair, Scrapped, Sold };
}

/// <summary>اتصال مال به سند خرید</summary>
public class AssetPurchaseLink
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string DocType { get; set; } = "";
    public int DocId { get; set; }
    public string DocDisplay { get; set; } = "";
    public decimal? Amount { get; set; }
    public string DocTypeName => AssetDocTypes.Name(DocType);
}

/// <summary>یک سند خرید قابل انتخاب (چک، تراکنش نقدی یا هزینه پروژه)</summary>
public class PurchaseDocItem
{
    public string DocType { get; set; } = "";
    public int DocId { get; set; }
    public DateTime? Date { get; set; }
    public decimal Amount { get; set; }
    public string Party { get; set; } = "";
    public string Description { get; set; } = "";
    public string DocNumber { get; set; } = "";
    public string DocTypeName => AssetDocTypes.Name(DocType);
    public string Display => $"{DocTypeName} {DocNumber} | {Party} | {Amount:N0} ریال".Replace("  ", " ");
}

public static class AssetDocTypes
{
    public const string Check = "Check";
    public const string CashTransaction = "CashTransaction";
    public const string ProjectExpense = "ProjectExpense";

    public static string Name(string? t) => t switch
    {
        Check => "چک",
        CashTransaction => "تراکنش نقدی",
        ProjectExpense => "هزینه پروژه",
        _ => t ?? ""
    };
}

public class AssetTransfer
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public DateTime TransferDate { get; set; }
    public string? FromDepartment { get; set; }
    public string? FromEmployee { get; set; }
    public string? FromLocation { get; set; }
    public string? ToDepartment { get; set; }
    public string? ToEmployee { get; set; }
    public string? ToLocation { get; set; }
    public string? Notes { get; set; }
}

public class IdName
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public override string ToString() => Name;
}
