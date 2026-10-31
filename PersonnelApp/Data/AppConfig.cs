using System;
using System.Configuration;
using Microsoft.Data.SqlClient;

namespace PersonnelApp.Data
{
    public static class AppConfig
    {
        // متغیری برای کش کردن رشته اتصال (تا هر بار دیتابیس را تست نکند)
        private static string _connectionString;

        public static string ConnectionString
        {
            get
            {
                // اگر قبلاً پیدا شده بود، همان را برگردان
                if (!string.IsNullOrEmpty(_connectionString)) return _connectionString;

                // =========================================================
                // ۱. تلاش اول: اتصال به SQL Server Express (محیط کار)
                // نام سرور بر اساس تصویر SSMS شما: localhost\SQLEXPRESS
                // =========================================================
                string sqlExpressConn = @"Server=localhost\SQLEXPRESS;Database=PersonnelDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
                if (IsServerAvailable(sqlExpressConn))
                {
                    _connectionString = sqlExpressConn;
                    return _connectionString;
                }

                // =========================================================
                // ۲. تلاش دوم: اتصال به LocalDB (محیط خانه)
                // =========================================================
                string localDbConn = @"Server=(localdb)\MSSQLLocalDB;Database=PersonnelDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
                if (IsServerAvailable(localDbConn))
                {
                    _connectionString = localDbConn;
                    return _connectionString;
                }

                // =========================================================
                // ۳. اگر هیچ کدام وصل نشدند، خطا بده
                // =========================================================
                throw new Exception("اتصال به هیچ کدام از دیتابیس‌ها (SQL Express یا LocalDB) برقرار نشد.\n\n" +
                                    "لطفاً موارد زیر را بررسی کنید:\n" +
                                    "۱. آیا سرویس SQL Server در ویندوز (Services.msc) در حال اجرا (Running) است؟\n" +
                                    "۲. آیا در SSMS دیتابیسی به نام 'PersonnelDB' ساخته‌اید؟\n" +
                                    "۳. آیا نام دیتابیس در کد AppConfig با نام دیتابیس در SSMS یکسان است؟");
            }
        }

        // متدی برای تست سریع اتصال (با تایم‌اوت ۲ ثانیه‌ای تا برنامه فریز نشود)
        private static bool IsServerAvailable(string connectionString)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                builder.ConnectTimeout = 2; // حداکثر ۲ ثانیه صبر می‌کند

                using var conn = new SqlConnection(builder.ConnectionString);
                conn.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}