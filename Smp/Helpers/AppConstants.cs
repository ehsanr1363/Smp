namespace Smp.Helpers;

public static class PaymentScopes
{
    public const string Administrative = "Administrative";
    public const string DirectPurchase = "DirectPurchase";
    public const string InformalContractor = "InformalContractor";
    public const string ContractorContract = "ContractorContract";
    public const string ProjectShipping = "ProjectShipping";

    // ✅ دسته‌بندی جدید برای هزینه‌های پروژه
    public const string ProjectExpense = "ProjectExpense";
}

public static class DirectionTypes
{
    public const string Incoming = "دریافتی";
    public const string Outgoing = "پرداختی";
}
