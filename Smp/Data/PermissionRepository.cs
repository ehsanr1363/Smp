using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Smp.Data;

public class PermissionRepository
{
    private readonly string _connectionString;
    public PermissionRepository(string connectionString) => _connectionString = connectionString;

    public HashSet<string> GetAllowedMenus(string roleName)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(roleName)) return allowed;

        if (string.Equals(roleName.Trim(), "Admin", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var key in Smp.Helpers.MenuHelper.AllMenus.Keys) allowed.Add(key);
            return allowed;
        }

        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        // 👈 FIX: استفاده از LTRIM و RTRIM برای جلوگیری از خطای فاصله (Space) در دیتابیس
        using var cmd = new SqlCommand("SELECT MenuKey FROM RolePermissions WHERE LTRIM(RTRIM(RoleName)) = @Role AND IsAllowed = 1", conn);
        cmd.Parameters.AddWithValue("@Role", roleName.Trim());
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            allowed.Add(reader.GetString(0).Trim());
        }
        return allowed;
    }

    public async Task SavePermissionsAsync(string roleName, Dictionary<string, bool> permissions)
    {
        Smp.Helpers.AuthorizationHelper.RequireAdmin("تغییر دسترسی نقش‌ها");
        if (string.Equals(roleName.Trim(), "Admin", StringComparison.OrdinalIgnoreCase)) return;

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            foreach (var kvp in permissions)
            {
                string sql = @"
                        IF EXISTS (SELECT 1 FROM RolePermissions WHERE LTRIM(RTRIM(RoleName)) = @Role AND MenuKey = @Menu)
                            UPDATE RolePermissions SET IsAllowed = @Allowed WHERE LTRIM(RTRIM(RoleName)) = @Role AND MenuKey = @Menu
                        ELSE
                            INSERT INTO RolePermissions (RoleName, MenuKey, IsAllowed) VALUES (@Role, @Menu, @Allowed)";

                using var cmd = new SqlCommand(sql, conn, tx);
                cmd.Parameters.AddWithValue("@Role", roleName.Trim());
                cmd.Parameters.AddWithValue("@Menu", kvp.Key);
                cmd.Parameters.AddWithValue("@Allowed", kvp.Value ? 1 : 0);
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
            Smp.Helpers.AuditHelper.Log("ویرایش", "دسترسی نقش‌ها", $"دسترسی‌های نقش «{roleName.Trim()}» تغییر کرد");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
