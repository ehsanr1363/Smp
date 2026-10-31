using Microsoft.Data.SqlClient;
using PersonnelApp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PersonnelApp.Data
{
    public class AuditLogRepository
    {
        private readonly string _connectionString;

        public AuditLogRepository(string connectionString)
        {
            _connectionString = connectionString;
            // 👈 EnsureTableExists حذف شد
        }

        public async Task AddLogAsync(string username, string actionType, string moduleName, string description)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string sql = @"
                    INSERT INTO AuditLogs (Username, ActionType, ModuleName, Description)
                    VALUES (@User, @Action, @Module, @Desc)";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@User", username ?? "سیستم");
                cmd.Parameters.AddWithValue("@Action", actionType);
                cmd.Parameters.AddWithValue("@Module", moduleName);
                cmd.Parameters.AddWithValue("@Desc", description ?? "");

                await cmd.ExecuteNonQueryAsync();
            }
            catch { /* جلوگیری از کرش کردن برنامه در صورت خطای لاگ */ }
        }

        public async Task<List<AuditLog>> GetAllLogsAsync()
        {
            var list = new List<AuditLog>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = "SELECT * FROM AuditLogs ORDER BY LogDate DESC";
            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new AuditLog
                {
                    Id = reader.GetInt32(0),
                    Username = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    ActionType = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ModuleName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Description = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    LogDate = reader.GetDateTime(5)
                });
            }
            return list;
        }
    }
}