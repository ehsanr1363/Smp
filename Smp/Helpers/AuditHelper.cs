using Smp.Data;
using System;

namespace Smp.Helpers;

/// <summary>
/// ثبت سریع لاگ تغییرات (Audit). کاربر جاری از SessionManager خوانده می‌شود.
/// خطای ثبت لاگ هرگز عملیات اصلی را مختل نمی‌کند.
/// </summary>
public static class AuditHelper
{
    public static void Log(string actionType, string moduleName, string description)
    {
        try
        {
            string user = string.IsNullOrWhiteSpace(SessionManager.Username) ? "سیستم" : SessionManager.Username;
            _ = new AuditLogRepository(AppConfig.ConnectionString).AddLogAsync(user, actionType, moduleName, description);
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "AuditHelper.Log");
        }
    }
}
