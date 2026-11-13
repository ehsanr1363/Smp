using Microsoft.Data.SqlClient;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMP.Data
{
    public class AuditLogRepository
    {
        private readonly string _connectionString;

        public AuditLogRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        // ✅ FIX 1: اگر خطایی رخ دهد، برنامه کرش نمی‌کند اما خطا در فایل Logs ذخیره می‌شود
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
                cmd.Parameters.AddWithValue("@Action", actionType ?? string.Empty);
                cmd.Parameters.AddWithValue("@Module", moduleName ?? string.Empty);
                cmd.Parameters.AddWithValue("@Desc", description ?? string.Empty);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                // جلوگیری از کرش کردن برنامه در صورت خطای لاگ، اما ثبت خطا در فایل متنی
                LogHelper.LogException(ex, "AuditLogRepository.AddLogAsync");
            }
        }

        // ✅ FIX 2: افزودن محدودیت (TOP) برای جلوگیری از فریز شدن برنامه
        // ✅ FIX 3: خواندن ستون‌ها با نام (به جای ایندکس عددی)
        public async Task<List<AuditLog>> GetAllLogsAsync(int limit = 3000)
        {
            var list = new List<AuditLog>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // خواندن آخرین ۳۰۰۰ لاگ برای جلوگیری از پر شدن حافظه
            string sql = $@"
                SELECT TOP ({limit}) 
                    Id, Username, ActionType, ModuleName, Description, LogDate 
                FROM AuditLogs 
                ORDER BY LogDate DESC";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new AuditLog
                {
                    Id = reader.IsDBNull(reader.GetOrdinal("Id")) ? 0 : reader.GetInt32(reader.GetOrdinal("Id")),
                    Username = reader.IsDBNull(reader.GetOrdinal("Username")) ? string.Empty : reader.GetString(reader.GetOrdinal("Username")),
                    ActionType = reader.IsDBNull(reader.GetOrdinal("ActionType")) ? string.Empty : reader.GetString(reader.GetOrdinal("ActionType")),
                    ModuleName = reader.IsDBNull(reader.GetOrdinal("ModuleName")) ? string.Empty : reader.GetString(reader.GetOrdinal("ModuleName")),
                    Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? string.Empty : reader.GetString(reader.GetOrdinal("Description")),
                    LogDate = reader.IsDBNull(reader.GetOrdinal("LogDate")) ? DateTime.Now : reader.GetDateTime(reader.GetOrdinal("LogDate"))
                });
            }
            return list;
        }
    }
}