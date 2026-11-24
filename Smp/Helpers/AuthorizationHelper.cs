using Smp.Data;
using System;
using System.Collections.Generic;

namespace Smp.Helpers;

/// <summary>
/// کنترل دسترسی در زمان اجرا (فراتر از پنهان‌کردن دکمه‌های منو).
/// </summary>
public static class AuthorizationHelper
{
    private static HashSet<string> _allowedMenus = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>بعد از ورود موفق و تنظیم SessionManager صدا زده شود.</summary>
    public static void Initialize(string connectionString)
    {
        try
        {
            _allowedMenus = new PermissionRepository(connectionString).GetAllowedMenus(SessionManager.Role);
        }
        catch (Exception ex)
        {
            // در صورت خطا، دسترسی غیر ادمین بسته می‌ماند (Fail-Closed)
            LogHelper.LogException(ex, "AuthorizationHelper.Initialize");
            _allowedMenus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static bool IsMenuAllowed(string menuKey)
        => SessionManager.IsAdmin || _allowedMenus.Contains(menuKey);

    /// <summary>عملیات حساس: فقط ادمین. در غیر این صورت خطا می‌دهد و لاگ می‌شود.</summary>
    public static void RequireAdmin(string actionDescription)
    {
        if (SessionManager.IsAdmin) return;
        AuditHelper.Log("دسترسی غیرمجاز", "امنیت", $"تلاش برای «{actionDescription}» بدون دسترسی ادمین");
        throw new UnauthorizedAccessException($"برای «{actionDescription}» دسترسی مدیر سیستم لازم است.");
    }
}
