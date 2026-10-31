using Microsoft.Data.SqlClient;
using PersonnelApp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PersonnelApp.Data
{
    public class BankAccountRepository
    {
        private readonly string _connectionString;
        public BankAccountRepository(string connectionString) => _connectionString = connectionString;

        public async Task<List<CompanyBankAccount>> GetAllAsync()
        {
            var list = new List<CompanyBankAccount>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT * FROM CompanyBankAccounts ORDER BY IsCashDesk DESC, Title", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new CompanyBankAccount
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    AccountNumber = reader.IsDBNull(2) ? null : reader.GetString(2),
                    InitialBalance = reader.GetDecimal(3),
                    CurrentBalance = reader.GetDecimal(4),
                    IsCashDesk = reader.GetBoolean(5),
                    IsActive = reader.GetBoolean(6),
                    CreatedAt = reader.GetDateTime(7)
                });
            }
            return list;
        }

        public async Task AddAsync(CompanyBankAccount account)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = @"INSERT INTO CompanyBankAccounts (Title, AccountNumber, InitialBalance, CurrentBalance, IsCashDesk, IsActive)
                           VALUES (@Title, @AccNum, @Initial, @Initial, @IsCash, @IsActive); SELECT SCOPE_IDENTITY();";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Title", account.Title);
            cmd.Parameters.AddWithValue("@AccNum", (object?)account.AccountNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Initial", account.InitialBalance);
            cmd.Parameters.AddWithValue("@IsCash", account.IsCashDesk);
            cmd.Parameters.AddWithValue("@IsActive", account.IsActive);
            account.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task UpdateAsync(CompanyBankAccount account)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = @"UPDATE CompanyBankAccounts SET Title = @Title, AccountNumber = @AccNum, IsCashDesk = @IsCash, IsActive = @IsActive WHERE Id = @Id";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Title", account.Title);
            cmd.Parameters.AddWithValue("@AccNum", (object?)account.AccountNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IsCash", account.IsCashDesk);
            cmd.Parameters.AddWithValue("@IsActive", account.IsActive);
            cmd.Parameters.AddWithValue("@Id", account.Id);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using (var checkCmd = new SqlCommand("SELECT COUNT(1) FROM CashTransactions WHERE AccountId = @Id", conn))
            {
                checkCmd.Parameters.AddWithValue("@Id", id);
                if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0)
                    throw new InvalidOperationException("این حساب دارای تراکنش است و قابل حذف نیست.");
            }
            using var cmd = new SqlCommand("DELETE FROM CompanyBankAccounts WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}