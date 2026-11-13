using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SMP.Models;

namespace SMP.Data
{
    public class AccountTransferRepository
    {
        private readonly string _connectionString;

        public AccountTransferRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task AddAsync(AccountTransfer transfer)
        {
            if (transfer == null) throw new ArgumentNullException(nameof(transfer));
            if (transfer.Amount <= 0) throw new InvalidOperationException("مبلغ انتقال باید بزرگتر از صفر باشد.");
            if (transfer.SourceAccountId == transfer.DestinationAccountId)
                throw new InvalidOperationException("حساب مبدا و مقصد نمی‌توانند یکسان باشند.");

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                string sql = @"
                    INSERT INTO AccountTransfers (SourceAccountId, DestinationAccountId, Amount, TransferFee, TransferDate, Description, CreatedAt, TransferType)
                    VALUES (@SrcId, @DestId, @Amount, 0, @Date, @Desc, GETDATE(), @TransferType);
                    SELECT SCOPE_IDENTITY();";

                using var cmd = new SqlCommand(sql, conn, transaction);
                cmd.Parameters.AddWithValue("@SrcId", transfer.SourceAccountId);
                cmd.Parameters.AddWithValue("@DestId", transfer.DestinationAccountId);
                cmd.Parameters.AddWithValue("@Amount", transfer.Amount);
                cmd.Parameters.AddWithValue("@Date", transfer.TransferDate);
                cmd.Parameters.AddWithValue("@Desc", (object?)transfer.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TransferType", (object?)transfer.TransferType ?? DBNull.Value);

                var result = await cmd.ExecuteScalarAsync();
                transfer.Id = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;

                using (var balCmd = new SqlCommand(@"
                    UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance - @Amount WHERE Id = @SrcId;
                    UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance + @Amount WHERE Id = @DestId;", conn, transaction))
                {
                    balCmd.Parameters.AddWithValue("@Amount", transfer.Amount);
                    balCmd.Parameters.AddWithValue("@SrcId", transfer.SourceAccountId);
                    balCmd.Parameters.AddWithValue("@DestId", transfer.DestinationAccountId);
                    await balCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateAsync(AccountTransfer transfer)
        {
            if (transfer == null) throw new ArgumentNullException(nameof(transfer));
            if (transfer.Id <= 0) throw new ArgumentException("شناسه انتقال نامعتبر است.");

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                int oldSrcId, oldDestId;
                decimal oldAmount;

                using (var readCmd = new SqlCommand("SELECT SourceAccountId, DestinationAccountId, Amount FROM AccountTransfers WHERE Id = @Id", conn, transaction))
                {
                    readCmd.Parameters.AddWithValue("@Id", transfer.Id);
                    using var reader = await readCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync()) throw new InvalidOperationException("انتقال مورد نظر یافت نشد.");
                    oldSrcId = reader.GetInt32(0);
                    oldDestId = reader.GetInt32(1);
                    oldAmount = reader.GetDecimal(2);
                }

                using (var revCmd = new SqlCommand(@"
                    UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance + @Amount WHERE Id = @OldSrcId;
                    UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance - @Amount WHERE Id = @OldDestId;", conn, transaction))
                {
                    revCmd.Parameters.AddWithValue("@Amount", oldAmount);
                    revCmd.Parameters.AddWithValue("@OldSrcId", oldSrcId);
                    revCmd.Parameters.AddWithValue("@OldDestId", oldDestId);
                    await revCmd.ExecuteNonQueryAsync();
                }

                string sql = @"
                    UPDATE AccountTransfers SET 
                        SourceAccountId = @SrcId, DestinationAccountId = @DestId, 
                        Amount = @Amount, TransferFee = 0, TransferDate = @Date, Description = @Desc,
                        TransferType = @TransferType
                    WHERE Id = @Id";

                using (var cmd = new SqlCommand(sql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@SrcId", transfer.SourceAccountId);
                    cmd.Parameters.AddWithValue("@DestId", transfer.DestinationAccountId);
                    cmd.Parameters.AddWithValue("@Amount", transfer.Amount);
                    cmd.Parameters.AddWithValue("@Date", transfer.TransferDate);
                    cmd.Parameters.AddWithValue("@Desc", (object?)transfer.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@TransferType", (object?)transfer.TransferType ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Id", transfer.Id);
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var balCmd = new SqlCommand(@"
                    UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance - @Amount WHERE Id = @SrcId;
                    UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance + @Amount WHERE Id = @DestId;", conn, transaction))
                {
                    balCmd.Parameters.AddWithValue("@Amount", transfer.Amount);
                    balCmd.Parameters.AddWithValue("@SrcId", transfer.SourceAccountId);
                    balCmd.Parameters.AddWithValue("@DestId", transfer.DestinationAccountId);
                    await balCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                int srcId, destId;
                decimal amount;

                using (var readCmd = new SqlCommand("SELECT SourceAccountId, DestinationAccountId, Amount FROM AccountTransfers WHERE Id = @Id", conn, transaction))
                {
                    readCmd.Parameters.AddWithValue("@Id", id);
                    using var reader = await readCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync()) throw new InvalidOperationException("انتقال مورد نظر یافت نشد.");
                    srcId = reader.GetInt32(0);
                    destId = reader.GetInt32(1);
                    amount = reader.GetDecimal(2);
                }

                using (var delCmd = new SqlCommand("DELETE FROM AccountTransfers WHERE Id = @Id", conn, transaction))
                {
                    delCmd.Parameters.AddWithValue("@Id", id);
                    await delCmd.ExecuteNonQueryAsync();
                }

                using (var balCmd = new SqlCommand(@"
                    UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance + @Amount WHERE Id = @SrcId;
                    UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance - @Amount WHERE Id = @DestId;", conn, transaction))
                {
                    balCmd.Parameters.AddWithValue("@Amount", amount);
                    balCmd.Parameters.AddWithValue("@SrcId", srcId);
                    balCmd.Parameters.AddWithValue("@DestId", destId);
                    await balCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ✅ متد جدید: دریافت تمام انتقالات مربوط به یک حساب
        public async Task<List<AccountTransfer>> GetByAccountIdAsync(int accountId)
        {
            var list = new List<AccountTransfer>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string query = @"
                SELECT t.*, 
                       ISNULL(s.Title, N'حساب حذف شده') AS SourceAccountTitle, 
                       ISNULL(d.Title, N'حساب حذف شده') AS DestinationAccountTitle
                FROM AccountTransfers t
                LEFT JOIN CompanyBankAccounts s ON t.SourceAccountId = s.Id
                LEFT JOIN CompanyBankAccounts d ON t.DestinationAccountId = d.Id
                WHERE t.SourceAccountId = @Id OR t.DestinationAccountId = @Id
                ORDER BY t.TransferDate DESC, t.Id DESC";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@Id", accountId);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new AccountTransfer
                {
                    Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
                    SourceAccountId = reader["SourceAccountId"] != DBNull.Value ? Convert.ToInt32(reader["SourceAccountId"]) : 0,
                    SourceAccountTitle = reader["SourceAccountTitle"]?.ToString() ?? "",
                    DestinationAccountId = reader["DestinationAccountId"] != DBNull.Value ? Convert.ToInt32(reader["DestinationAccountId"]) : 0,
                    DestinationAccountTitle = reader["DestinationAccountTitle"]?.ToString() ?? "",
                    Amount = reader["Amount"] != DBNull.Value ? Convert.ToDecimal(reader["Amount"]) : 0,
                    TransferFee = reader["TransferFee"] != DBNull.Value ? Convert.ToDecimal(reader["TransferFee"]) : 0,
                    TransferDate = reader["TransferDate"] != DBNull.Value ? Convert.ToDateTime(reader["TransferDate"]) : DateTime.Now,
                    Description = reader["Description"] != DBNull.Value ? reader["Description"].ToString() : null,
                    CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.Now,
                    TransferType = reader["TransferType"] != DBNull.Value ? reader["TransferType"].ToString() : null
                });
            }
            return list;
        }

        public async Task<List<AccountTransfer>> GetAllAsync()
        {
            var list = new List<AccountTransfer>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string query = @"
                SELECT t.*, 
                       ISNULL(s.Title, N'حساب حذف شده') AS SourceAccountTitle, 
                       ISNULL(d.Title, N'حساب حذف شده') AS DestinationAccountTitle
                FROM AccountTransfers t
                LEFT JOIN CompanyBankAccounts s ON t.SourceAccountId = s.Id
                LEFT JOIN CompanyBankAccounts d ON t.DestinationAccountId = d.Id
                ORDER BY t.TransferDate DESC, t.Id DESC";

            using var cmd = new SqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new AccountTransfer
                {
                    Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
                    SourceAccountId = reader["SourceAccountId"] != DBNull.Value ? Convert.ToInt32(reader["SourceAccountId"]) : 0,
                    SourceAccountTitle = reader["SourceAccountTitle"]?.ToString() ?? "",
                    DestinationAccountId = reader["DestinationAccountId"] != DBNull.Value ? Convert.ToInt32(reader["DestinationAccountId"]) : 0,
                    DestinationAccountTitle = reader["DestinationAccountTitle"]?.ToString() ?? "",
                    Amount = reader["Amount"] != DBNull.Value ? Convert.ToDecimal(reader["Amount"]) : 0,
                    TransferFee = reader["TransferFee"] != DBNull.Value ? Convert.ToDecimal(reader["TransferFee"]) : 0,
                    TransferDate = reader["TransferDate"] != DBNull.Value ? Convert.ToDateTime(reader["TransferDate"]) : DateTime.Now,
                    Description = reader["Description"] != DBNull.Value ? reader["Description"].ToString() : null,
                    CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.Now,
                    TransferType = reader["TransferType"] != DBNull.Value ? reader["TransferType"].ToString() : null
                });
            }
            return list;
        }
    }
}