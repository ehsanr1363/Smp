using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient; // 👈 تغییر به SqlClient

namespace SMP.Helpers
{
    public static class LookupHelper
    {
        // لیست استاندارد دسته‌بندی‌ها (کلید انگلیسی و مقدار فارسی)
        public static readonly Dictionary<string, string> StandardCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Education", "تحصیلات" },
            { "MaritalStatus", "وضعیت تاهل" },
            { "Gender", "جنسیت" },
            { "City", "شهر" },
            { "Province", "استان" },
            { "Job", "شغل" },
            { "InsuranceType", "نوع بیمه" },
            { "MilitaryStatus", "وضعیت نظام وظیفه" },
            { "WorkLocation", "محل کار" },
            { "PersonnelType", "نوع پرسنل" } // 👈 فقط این املا استاندارد است
        };

        /// <summary>
        /// پاکسازی و یکدست‌سازی دیتابیس (تغییر نام‌های اشتباه و حذف تکراری‌ها)
        /// </summary>
        public static void NormalizeDatabase(string connectionString)
        {
            try
            {
                using var conn = new SqlConnection(connectionString); // 👈 تغییر به SqlConnection
                conn.Open();

                // ۱. اصلاح نام‌های اشتباه رایج
                using (var cmd = new SqlCommand(@"
                    UPDATE LookupItems SET Category = N'PersonnelType' WHERE Category IN (N'PersonelType', N'Personel Type', N'Personel_Type', N'PersonelType ');
                    UPDATE LookupItems SET Category = N'MaritalStatus' WHERE Category IN (N'Marital Status');
                    UPDATE LookupItems SET Category = N'MilitaryStatus' WHERE Category IN (N'Military Status');
                    UPDATE LookupItems SET Category = N'WorkLocation' WHERE Category IN (N'Work Location');
                    UPDATE LookupItems SET Category = N'Education' WHERE Category IN (N'education', N'EDUCATION');
                ", conn)) // 👈 تغییر به SqlCommand و اضافه کردن N برای رشته‌ها (اختیاری اما توصیه‌شده)
                {
                    cmd.ExecuteNonQuery();
                }

                // ۲. حذف رکوردهای تکراری (نگه داشتن قدیمی‌ترین رکورد)
                using (var cmd = new SqlCommand(@"
                    DELETE FROM LookupItems 
                    WHERE Id NOT IN (
                        SELECT MIN(Id) 
                        FROM LookupItems 
                        GROUP BY Category, Value
                    );
                ", conn)) // 👈 تغییر به SqlCommand
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch { /* برای جلوگیری از کرش کردن برنامه، خطا را نادیده می‌گیریم */ }
        }
    }
}