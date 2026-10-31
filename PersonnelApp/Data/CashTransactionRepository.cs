using Microsoft.Data.SqlClient;
using PersonnelApp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PersonnelApp.Data
{
    public class CashTransactionRepository
    {
        private readonly string _connectionString;
        public CashTransactionRepository(string connectionString) => _connectionString = connectionString;

        public async Task<List<CashTransaction>> GetByDateRangeAsync(DateTime from, DateTime to)
        {
            var list = new List<CashTransaction>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = @"SELECT t.Id, t.AccountId, a.Title, t.TransactionDate, t.Amount, t.TransactionType, t.Category, t.Description, t.ReferenceNumber, t.CreatedAt
                           FROM CashTransactions t INNER JOIN CompanyBankAccounts a ON t.AccountId = a.Id
                           WHERE t.TransactionDate >= @From AND t.TransactionDate <= @To ORDER BY t.TransactionDate DESC, t.Id DESC";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@From", from.Date);
            cmd.Parameters.AddWithValue("@To", to.Date.AddDays(1).AddSeconds(-1));
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new CashTransaction
                {
                    Id = reader.GetInt32(0),
                    AccountId = reader.GetInt32(1),
                    AccountTitle = reader.GetString(2),
                    TransactionDate = reader.GetDateTime(3),
                    Amount = reader.GetDecimal(4),
                    TransactionType = reader.GetString(5),
                    Category = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Description = reader.IsDBNull(7) ? null : reader.GetString(7),
                    ReferenceNumber = reader.IsDBNull(8) ? null : reader.GetString(8),
                    CreatedAt = reader.GetDateTime(9)
                });
            }
            return list;
        }

        public async Task AddAsync(CashTransaction transaction)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                string sql = @"INSERT INTO CashTransactions (AccountId, TransactionDate, Amount, TransactionType, Category, Description, ReferenceNumber)
                               VALUES (@AccId, @Date, @Amount, @Type, @Cat, @Desc, @Ref); SELECT SCOPE_IDENTITY();";
                int newId;
                using (var cmd = new SqlCommand(sql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@AccId", transaction.AccountId);
                    cmd.Parameters.AddWithValue("@Date", transaction.TransactionDate);
                    cmd.Parameters.AddWithValue("@Amount", transaction.Amount);
                    cmd.Parameters.AddWithValue("@Type", transaction.TransactionType);
                    cmd.Parameters.AddWithValue("@Cat", (object?)transaction.Category ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Desc", (object?)transaction.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Ref", (object?)transaction.ReferenceNumber ?? DBNull.Value);
                    newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
                decimal amountChange = transaction.TransactionType == "دریافت" ? transaction.Amount : -transaction.Amount;
                using (var cmdUpdate = new SqlCommand("UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance + @Change WHERE Id = @AccId", conn, tx))
                {
                    cmdUpdate.Parameters.AddWithValue("@Change", amountChange);
                    cmdUpdate.Parameters.AddWithValue("@AccId", transaction.AccountId);
                    await cmdUpdate.ExecuteNonQueryAsync();
                }
                tx.Commit(); transaction.Id = newId;
            }
            catch { tx.Rollback(); throw; }
        }

        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                decimal amount = 0; int accountId = 0; string type = "";
                using (var cmdRead = new SqlCommand("SELECT AccountId, Amount, TransactionType FROM CashTransactions WHERE Id = @Id", conn, tx))
                {
                    cmdRead.Parameters.AddWithValue("@Id", id);
                    using var reader = await cmdRead.ExecuteReaderAsync();
                    if (await reader.ReadAsync()) { accountId = reader.GetInt32(0); amount = reader.GetDecimal(1); type = reader.GetString(2); }
                    else return;
                }
                using (var cmdDel = new SqlCommand("DELETE FROM CashTransactions WHERE Id = @Id", conn, tx))
                {
                    cmdDel.Parameters.AddWithValue("@Id", id); await cmdDel.ExecuteNonQueryAsync();
                }
                decimal amountReverse = type == "دریافت" ? -amount : amount;
                using (var cmdUpdate = new SqlCommand("UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance + @Change WHERE Id = @AccId", conn, tx))
                {
                    cmdUpdate.Parameters.AddWithValue("@Change", amountReverse);
                    cmdUpdate.Parameters.AddWithValue("@AccId", accountId);
                    await cmdUpdate.ExecuteNonQueryAsync();
                }
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }
    }
}