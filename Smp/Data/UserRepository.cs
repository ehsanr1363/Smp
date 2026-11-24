using Microsoft.Data.SqlClient;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Smp.Data;

public class UserRepository
{
    private readonly string _connectionString;

    public UserRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    // ── ستون MustChangePassword (اگر نبود، یک‌بار به‌صورت خودکار ساخته می‌شود) ──
    private static bool? _hasMustChangeColumn;
    private static readonly object _schemaLock = new object();

    private async Task<bool> HasMustChangeColumnAsync(SqlConnection conn)
    {
        if (_hasMustChangeColumn.HasValue) return _hasMustChangeColumn.Value;

        bool exists = false;
        try
        {
            async Task<bool> CheckAsync()
            {
                using var chk = new SqlCommand("SELECT CASE WHEN COL_LENGTH('Users','MustChangePassword') IS NULL THEN 0 ELSE 1 END", conn);
                return Convert.ToInt32(await chk.ExecuteScalarAsync()) == 1;
            }

            exists = await CheckAsync();
            if (!exists)
            {
                using (var alter = new SqlCommand("ALTER TABLE Users ADD MustChangePassword BIT NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT 0", conn))
                    await alter.ExecuteNonQueryAsync();
                exists = await CheckAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "UserRepository.HasMustChangeColumn");
        }

        lock (_schemaLock) _hasMustChangeColumn = exists;
        return exists;
    }

    /// <summary>تغییر رمز توسط خود کاربر (نیاز به ادمین ندارد) و برداشتن پرچم تغییر اجباری.</summary>
    public async Task ChangeOwnPasswordAsync(int userId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword)) throw new ArgumentException("رمز عبور خالی است.");
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        bool hasCol = await HasMustChangeColumnAsync(conn);
        using var cmd = new SqlCommand("UPDATE Users SET PasswordHash = @Pass" + (hasCol ? ", MustChangePassword = 0" : "") + " WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Pass", SecurityHelper.HashPassword(newPassword));
        cmd.Parameters.AddWithValue("@Id", userId);
        await cmd.ExecuteNonQueryAsync();
        AuditHelper.Log("تغییر رمز", "کاربران", "کاربر رمز عبور خود را تغییر داد.");
    }

    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        bool hasMustCol = await HasMustChangeColumnAsync(conn);
        string sql = @"SELECT u.Id, u.EmployeeId, u.Username, u.Role, u.IsActive, u.PasswordHash, e.FirstName, e.LastName" + (hasMustCol ? ", u.MustChangePassword" : "") + @" FROM Users u LEFT JOIN Employees e ON u.EmployeeId = e.Id WHERE u.Username = @User";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@User", username.Trim());
        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        bool isActive = reader.IsDBNull(4) || Convert.ToInt32(reader[4]) == 1;
        if (!isActive) return null;

        string storedHash = reader.IsDBNull(5) ? "" : reader.GetString(5);
        if (!SecurityHelper.VerifyPassword(password, storedHash)) return null;
        bool needsRehash = SecurityHelper.NeedsRehash(storedHash);
        // رمزهای پیش‌فرض شناخته‌شده همیشه تغییر اجباری دارند، حتی اگر پرچم دیتابیس صفر باشد
        bool mustChange = (hasMustCol && !reader.IsDBNull(8) && Convert.ToInt32(reader[8]) == 1)
                          || password == "admin" || password == "123456";

        string firstName = reader.IsDBNull(6) ? "" : reader.GetString(6);
        string lastName = reader.IsDBNull(7) ? "" : reader.GetString(7);
        string fullName = $"{firstName} {lastName}".Trim();
        if (string.IsNullOrWhiteSpace(fullName)) fullName = "مدیر سیستم";

        var user = new User
        {
            Id = reader.GetInt32(0),
            EmployeeId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
            Username = reader.IsDBNull(2) ? "" : reader.GetString(2),
            Role = reader.IsDBNull(3) ? "Viewer" : reader.GetString(3),
            EmployeeFullName = fullName,
            MustChangePassword = mustChange
        };

        await reader.CloseAsync();
        // مهاجرت خودکار هش قدیمی (SHA256 بدون salt) به PBKDF2 در اولین ورود موفق
        using var updateCmd = new SqlCommand(needsRehash
            ? "UPDATE Users SET LastLogin = GETDATE(), PasswordHash = @NewHash WHERE Id = @Id"
            : "UPDATE Users SET LastLogin = GETDATE() WHERE Id = @Id", conn);
        if (needsRehash) updateCmd.Parameters.AddWithValue("@NewHash", SecurityHelper.HashPassword(password));
        updateCmd.Parameters.AddWithValue("@Id", user.Id);
        await updateCmd.ExecuteNonQueryAsync();
        return user;
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        var list = new List<User>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        string sql = @"SELECT u.Id, u.EmployeeId, u.Username, u.Role, u.IsActive, u.LastLogin, u.CreatedAt, e.FirstName, e.LastName FROM Users u LEFT JOIN Employees e ON u.EmployeeId = e.Id ORDER BY u.Username";
        using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string fName = reader.IsDBNull(7) ? "" : reader.GetString(7);
            string lName = reader.IsDBNull(8) ? "" : reader.GetString(8);
            list.Add(new User
            {
                Id = reader.GetInt32(0),
                EmployeeId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                Username = reader.GetString(2),
                Role = reader.IsDBNull(3) ? "Viewer" : reader.GetString(3),
                IsActive = !reader.IsDBNull(4) && Convert.ToInt32(reader[4]) == 1,
                LastLogin = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                CreatedAt = reader.IsDBNull(6) ? DateTime.Now : reader.GetDateTime(6),
                EmployeeFullName = string.IsNullOrWhiteSpace(fName) && string.IsNullOrWhiteSpace(lName) ? "--- بدون اتصال ---" : $"{fName} {lName}".Trim()
            });
        }
        return list;
    }

    public async Task<bool> IsEmployeeAssignedToAnotherUserAsync(int employeeId, int? excludeUserId = null)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        string sql = "SELECT COUNT(1) FROM Users WHERE EmployeeId = @EmpId AND IsActive = 1";
        if (excludeUserId.HasValue) sql += " AND Id != @ExcludeId";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@EmpId", employeeId);
        if (excludeUserId.HasValue) cmd.Parameters.AddWithValue("@ExcludeId", excludeUserId.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<bool> IsUsernameTakenAsync(string username, int? excludeUserId = null)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        string sql = "SELECT COUNT(1) FROM Users WHERE Username = @User";
        if (excludeUserId.HasValue) sql += " AND Id != @ExcludeId";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@User", username.Trim());
        if (excludeUserId.HasValue) cmd.Parameters.AddWithValue("@ExcludeId", excludeUserId.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<User?> GetByEmployeeIdAsync(int employeeId)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        string sql = @"SELECT u.Id, u.EmployeeId, u.Username, u.Role, u.IsActive, e.FirstName, e.LastName FROM Users u LEFT JOIN Employees e ON u.EmployeeId = e.Id WHERE u.EmployeeId = @EmpId";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@EmpId", employeeId);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            string firstName = reader.IsDBNull(5) ? "" : reader.GetString(5);
            string lastName = reader.IsDBNull(6) ? "" : reader.GetString(6);
            return new User
            {
                Id = reader.GetInt32(0),
                EmployeeId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                Username = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Role = reader.IsDBNull(3) ? "Viewer" : reader.GetString(3),
                IsActive = !reader.IsDBNull(4) && Convert.ToInt32(reader[4]) == 1,
                EmployeeFullName = $"{firstName} {lastName}".Trim()
            };
        }
        return null;
    }

    public async Task AddAsync(User user)
    {
        AuthorizationHelper.RequireAdmin("افزودن کاربر");
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        bool hasMustCol = await HasMustChangeColumnAsync(conn);
        string sql = hasMustCol
            ? @"INSERT INTO Users (Username, PasswordHash, Role, EmployeeId, IsActive, CreatedAt, MustChangePassword) VALUES (@User, @Pass, @Role, @EmpId, @IsActive, GETDATE(), 1); SELECT SCOPE_IDENTITY();"
            : @"INSERT INTO Users (Username, PasswordHash, Role, EmployeeId, IsActive, CreatedAt) VALUES (@User, @Pass, @Role, @EmpId, @IsActive, GETDATE()); SELECT SCOPE_IDENTITY();";
        if (string.IsNullOrWhiteSpace(user.PasswordHash))
            throw new InvalidOperationException("رمز عبور برای کاربر جدید الزامی است.");
        string finalHash = user.PasswordHash;
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@User", user.Username); cmd.Parameters.AddWithValue("@Pass", finalHash); cmd.Parameters.AddWithValue("@Role", user.Role ?? "Viewer");
        cmd.Parameters.AddWithValue("@EmpId", (object?)user.EmployeeId ?? DBNull.Value); cmd.Parameters.AddWithValue("@IsActive", user.IsActive ? 1 : 0);
        user.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        AuditHelper.Log("ثبت", "کاربران", $"کاربر جدید «{user.Username}» با نقش {user.Role}");
    }

    public async Task UpdateAsync(User user)
    {
        AuthorizationHelper.RequireAdmin("ویرایش کاربر");
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        bool resetPass = !string.IsNullOrWhiteSpace(user.PasswordHash);
        // اگر ادمین رمز کاربر دیگری را تعیین کند، کاربر در ورود بعدی باید رمز را عوض کند
        bool forceChange = resetPass && user.Id != SessionManager.UserId && await HasMustChangeColumnAsync(conn);
        string sql = @"UPDATE Users SET Username = @User, Role = @Role, EmployeeId = @EmpId, IsActive = @IsActive " + (resetPass ? ", PasswordHash = @Pass " : "") + (forceChange ? ", MustChangePassword = 1 " : "") + @"WHERE Id = @Id";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@User", user.Username); cmd.Parameters.AddWithValue("@Role", user.Role ?? "Viewer");
        cmd.Parameters.AddWithValue("@EmpId", (object?)user.EmployeeId ?? DBNull.Value); cmd.Parameters.AddWithValue("@IsActive", user.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@Id", user.Id);
        if (!string.IsNullOrWhiteSpace(user.PasswordHash)) cmd.Parameters.AddWithValue("@Pass", user.PasswordHash);
        await cmd.ExecuteNonQueryAsync();
        AuditHelper.Log("ویرایش", "کاربران", $"کاربر «{user.Username}» (شناسه {user.Id})" + (resetPass ? " - رمز عبور بازنشانی شد" : ""));
    }

    public void Delete(int id)
    {
        AuthorizationHelper.RequireAdmin("حذف کاربر");
        if (id == SessionManager.UserId) throw new InvalidOperationException("حذف حساب کاربری خودتان ممکن نیست.");
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("DELETE FROM Users WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.ExecuteNonQuery();
        AuditHelper.Log("حذف", "کاربران", $"کاربر با شناسه {id} حذف شد");
    }
}
