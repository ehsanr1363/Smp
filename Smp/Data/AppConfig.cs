using System;
using System.Configuration;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Smp.Data;

public static class AppConfig
{
    private static string? _connectionString;

    // ✅ FIX 1: استفاده از قفل برای جلوگیری از تداخل و باز شدن چندباره اتصال همزمان
    private static readonly object _lock = new object();

    public static string ConnectionString
    {
        get
        {
            // اگر قبلاً لود شده، فوراً برگردان (بدون قفل)
            if (!string.IsNullOrEmpty(_connectionString)) return _connectionString;

            lock (_lock)
            {
                // چک مجدد داخل قفل (Double-Check Locking)
                if (!string.IsNullOrEmpty(_connectionString)) return _connectionString;

                try
                {
                    string? encodedConn = ConfigurationManager.AppSettings["DbConnection"];

                    if (string.IsNullOrEmpty(encodedConn))
                        throw new Exception("تنظیمات اتصال در فایل App.config یافت نشد.");

                    // رمزگشایی از Base64
                    byte[] data = Convert.FromBase64String(encodedConn);
                    string networkConn = Encoding.UTF8.GetString(data);

                    // ✅ FIX 2: اعمال تنظیمات Timeout و دریافت رشته نهایی از builder
                    var builder = new SqlConnectionStringBuilder(networkConn)
                    {
                        ConnectTimeout = 5
                    };

                    using var conn = new SqlConnection(builder.ConnectionString);
                    conn.Open();

                    // ✅ FIX 3: کش کردن رشته‌ای که شامل Timeout است
                    _connectionString = builder.ConnectionString;
                    return _connectionString;
                }
                catch (FormatException)
                {
                    throw new Exception("تنظیمات اتصال در فایل App.config فرمت معتبری (Base64) ندارد. لطفاً فایل را بررسی کنید.");
                }
                catch (Exception ex)
                {
                    throw new Exception("خطا در اتصال به سرور دیتابیس (یا رمزگشایی تنظیمات):\n" + ex.Message);
                }
            }
        }
    }

    // ✅ FIX 4: متد Async برای استفاده در فرم‌ها (به عنوان جایگزین بهتر)
    public static async System.Threading.Tasks.Task<string> GetConnectionStringAsync()
    {
        if (!string.IsNullOrEmpty(_connectionString)) return _connectionString;

        return await System.Threading.Tasks.Task.Run(() => ConnectionString);
    }
}
