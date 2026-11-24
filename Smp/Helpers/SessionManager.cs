using Smp.Models;
using System;

namespace Smp.Helpers;

public static class SessionManager
{
    public static int UserId { get; set; }
    public static string Username { get; set; } = "";
    public static string FullName { get; set; } = "";
    public static string Role { get; set; } = ""; // Admin, HR, Viewer

    /// <summary>
    /// آیا کاربر جاری دسترسی ادمین دارد؟
    /// </summary>
    public static bool IsAdmin => string.Equals(Role?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase);

    public static void Start(User user)
    {
        UserId = user.Id;
        Username = user.Username;
        FullName = user.EmployeeFullName ?? "";
        Role = user.Role ?? "";
    }

    public static void Logout()
    {
        UserId = 0;
        Username = "";
        FullName = "";
        Role = "";
    }
}
