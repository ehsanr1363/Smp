using Microsoft.Data.SqlClient;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMP.Data
{
    public class CheckbookRepository
    {
        private readonly string _connectionString;
        public CheckbookRepository(string connectionString) => _connectionString = connectionString;

        public async Task<List<Checkbook>> GetByAccountIdAsync(int accountId)
        {
            var list = new List<Checkbook>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = @"
                SELECT c.Id, c.AccountId, a.Title AS AccountTitle, c.SerialNumber, 
                       c.StartSayadNumber, c.StartCheckNumber, c.PageCount, c.CreatedAt
                FROM Checkbooks c
                INNER JOIN CompanyBankAccounts a ON c.AccountId = a.Id
                WHERE c.AccountId = @AccountId
                ORDER BY c.Id DESC";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@AccountId", accountId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new Checkbook
                {
                    Id = reader.GetInt32(0),
                    AccountId = reader.GetInt32(1),
                    AccountTitle = reader.GetString(2),
                    SerialNumber = reader.GetString(3),
                    StartSayadNumber = reader.GetString(4),
                    StartCheckNumber = reader.GetString(5),
                    PageCount = reader.GetInt32(6),
                    CreatedAt = reader.GetDateTime(7)
                });
            }
            return list;
        }

        public async Task AddAsync(Checkbook checkbook)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = @"
                INSERT INTO Checkbooks (AccountId, SerialNumber, StartSayadNumber, StartCheckNumber, PageCount)
                VALUES (@AccountId, @Serial, @Sayad, @StartCheck, @Pages);
                SELECT SCOPE_IDENTITY();";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@AccountId", checkbook.AccountId);
            cmd.Parameters.AddWithValue("@Serial", checkbook.SerialNumber);
            cmd.Parameters.AddWithValue("@Sayad", checkbook.StartSayadNumber);
            cmd.Parameters.AddWithValue("@StartCheck", checkbook.StartCheckNumber);
            cmd.Parameters.AddWithValue("@Pages", checkbook.PageCount);
            checkbook.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("DELETE FROM Checkbooks WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        // ✅ پیدا کردن شماره چک بعدی برای یک حساب
        public async Task<string> GetNextCheckNumberAsync(int accountId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // ۱. ببینید آخرین چکِ پرداختیِ این حساب چه شماره‌ای داشته است
            string sqlLastCheck = @"
                SELECT ISNULL(MAX(TRY_CAST(CheckNumber AS INT)), 0) 
                FROM CashChecks 
                WHERE AccountId = @AccountId AND CheckType = N'پرداختی'";
            using var cmdLast = new SqlCommand(sqlLastCheck, conn);
            cmdLast.Parameters.AddWithValue("@AccountId", accountId);
            var lastCheckObj = await cmdLast.ExecuteScalarAsync();
            long lastCheck = Convert.ToInt64(lastCheckObj);

            // ۲. اگر چکی وجود دارد، یک عدد به آن اضافه کن
            if (lastCheck > 0) return (lastCheck + 1).ToString();

            // ۳. اگر اولین چک است، شماره شروع آخرین دسته چک ثبت‌شده را پیدا کن
            string sqlFirstCheck = @"
                SELECT TOP 1 StartCheckNumber 
                FROM Checkbooks 
                WHERE AccountId = @AccountId 
                ORDER BY Id DESC";
            using var cmdFirst = new SqlCommand(sqlFirstCheck, conn);
            cmdFirst.Parameters.AddWithValue("@AccountId", accountId);
            var startCheckObj = await cmdFirst.ExecuteScalarAsync();

            if (startCheckObj != null && startCheckObj != DBNull.Value)
                return startCheckObj.ToString()!;

            return ""; // هیچ دسته چکی تعریف نشده
        }
    }
}