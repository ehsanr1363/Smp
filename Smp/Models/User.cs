using System;

namespace Smp.Models;

public class User
{
    public int Id { get; set; }
    public int? EmployeeId { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "User"; // Admin یا User
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public DateTime? LastLogin { get; set; }
    public DateTime CreatedAt { get; set; }

    // برای نمایش در جدول
    public string? EmployeeFullName { get; set; }
}
