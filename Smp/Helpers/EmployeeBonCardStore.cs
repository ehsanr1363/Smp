using Microsoft.Data.SqlClient;
using Smp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Smp.Helpers;

/// <summary>
/// ذخیره و خواندن شماره بن کارت (۱۶ رقمی) پرسنل. مستقل از EmployeeRepository کار می‌کند
/// و در صورت نبودن ستون BonCardNumber، آن را خودکار به جدول Employees اضافه می‌کند.
/// </summary>
public static class EmployeeBonCardStore
{
    private static bool _columnChecked;

    /// <summary>حذف فاصله/خط تیره و تبدیل ارقام فارسی و عربی به انگلیسی</summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        string s = StringHelper.ConvertToEnglishDigits(input);
        return new string(s.Where(char.IsDigit).ToArray());
    }

    public static bool IsValidFormat(string? digits) =>
        digits is not null && digits.Length == 16 && digits.All(c => c >= '0' && c <= '9');

    /// <summary>اعتبارسنجی Luhn (کارت‌های بانکی ایران معمولاً از آن پیروی می‌کنند)</summary>
    public static bool PassesLuhn(string digits)
    {
        if (!IsValidFormat(digits)) return false;
        int sum = 0;
        for (int i = 0; i < 16; i++)
        {
            int d = digits[15 - i] - '0';
            if (i % 2 == 1) { d *= 2; if (d > 9) d -= 9; }
            sum += d;
        }
        return sum % 10 == 0;
    }

    /// <summary>نمایش خوانا: 6037-9912-3456-7890</summary>
    public static string Format(string? digits)
    {
        string d = Normalize(digits);
        return d.Length == 16 ? $"{d[..4]}-{d.Substring(4, 4)}-{d.Substring(8, 4)}-{d.Substring(12, 4)}" : d;
    }

    private static async Task EnsureColumnAsync(SqlConnection conn)
    {
        if (_columnChecked) return;
        using var cmd = new SqlCommand(
            "IF COL_LENGTH('Employees','BonCardNumber') IS NULL ALTER TABLE Employees ADD BonCardNumber NVARCHAR(16) NULL;", conn);
        await cmd.ExecuteNonQueryAsync();
        _columnChecked = true;
    }

    public static async Task<string> GetAsync(int employeeId)
    {
        if (employeeId <= 0) return "";
        using var conn = new SqlConnection(AppConfig.ConnectionString);
        await conn.OpenAsync();
        await EnsureColumnAsync(conn);
        using var cmd = new SqlCommand("SELECT BonCardNumber FROM Employees WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@id", employeeId);
        var res = await cmd.ExecuteScalarAsync();
        return res is null || res == DBNull.Value ? "" : Convert.ToString(res) ?? "";
    }

    /// <summary>شماره بن کارت همه‌ی پرسنلی که ثبت شده است (کلید = شناسه‌ی کارمند) — برای گزارش‌ها</summary>
    public static async Task<Dictionary<int, string>> GetAllAsync()
    {
        var result = new Dictionary<int, string>();
        using var conn = new SqlConnection(AppConfig.ConnectionString);
        await conn.OpenAsync();
        await EnsureColumnAsync(conn);
        using var cmd = new SqlCommand("SELECT Id, BonCardNumber FROM Employees WHERE BonCardNumber IS NOT NULL AND BonCardNumber <> ''", conn);
        using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            result[rd.GetInt32(0)] = rd.GetString(1);
        return result;
    }

    /// <summary>ذخیره؛ رشته‌ی خالی یعنی پاک کردن شماره‌ی ثبت‌شده</summary>
    public static async Task SaveAsync(int employeeId, string? digits)
    {
        if (employeeId <= 0) return;
        string d = Normalize(digits);
        if (d.Length != 0 && !IsValidFormat(d))
            throw new ArgumentException("شماره بن کارت باید دقیقاً ۱۶ رقم باشد.");

        using var conn = new SqlConnection(AppConfig.ConnectionString);
        await conn.OpenAsync();
        await EnsureColumnAsync(conn);
        using var cmd = new SqlCommand("UPDATE Employees SET BonCardNumber = @card WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@card", d.Length == 0 ? DBNull.Value : d);
        cmd.Parameters.AddWithValue("@id", employeeId);
        await cmd.ExecuteNonQueryAsync();
    }
}
