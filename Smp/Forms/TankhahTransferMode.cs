namespace Smp.Forms;

/// <summary>
/// حالت‌های انتقال تنخواه
/// </summary>
public enum TankhahTransferMode
{
    /// <summary>
    /// شارژ تنخواه از بانک (پول از حساب بانکی به تنخواه منتقل می‌شود)
    /// </summary>
    BankToTankhah,

    /// <summary>
    /// تسویه تنخواه به بانک (پول از تنخواه به حساب بانکی برگردانده می‌شود)
    /// </summary>
    TankhahToBank
}
