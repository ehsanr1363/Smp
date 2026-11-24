using System;
using Microsoft.Data.SqlClient; // 👈 تغییر به SqlClient

namespace Smp.Services;

public class SystemMaintenanceService
{
    private readonly string _connectionString;

    public SystemMaintenanceService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void ClearInsuranceData()
    {
        Smp.Helpers.AuthorizationHelper.RequireAdmin("پاک‌سازی داده‌های بیمه");
        using var conn = new SqlConnection(_connectionString); // 👈 تغییر به SqlConnection
        conn.Open();

        using var transaction = conn.BeginTransaction();

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;

            // حذف رکوردهای جداول بیمه
            cmd.CommandText = "DELETE FROM InsuranceRecords;";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "DELETE FROM WorkshopRecords;";
            cmd.ExecuteNonQuery();

            // 👈 ریست کردن شمارنده‌های Identity (جایگزین sqlite_sequence در SQL Server)
            cmd.CommandText = @"
                    DBCC CHECKIDENT ('InsuranceRecords', RESEED, 0);
                    DBCC CHECKIDENT ('WorkshopRecords', RESEED, 0);
                ";
            cmd.ExecuteNonQuery();

            transaction.Commit();
            Smp.Helpers.AuditHelper.Log("حذف", "بیمه", "داده‌های جداول بیمه (InsuranceRecords / WorkshopRecords) پاک‌سازی شد");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            throw new Exception("خطا در پاک‌سازی داده‌های بیمه در دیتابیس.", ex);
        }
    }
}
