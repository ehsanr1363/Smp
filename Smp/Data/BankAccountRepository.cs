using Microsoft.Data.SqlClient;
using SMP.Helpers;
using SMP.Models;
using SMP.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMP.Data
{
    public class BankAccountRepository
    {
        private readonly string _connectionString;
        public BankAccountRepository(string connectionString) => _connectionString = connectionString;

        private static CompanyBankAccount Map(SqlDataReader reader)
        {
            string? bankCode = reader.IsDBNull(reader.GetOrdinal("BankCode")) ? null : reader.GetString(reader.GetOrdinal("BankCode"));
            return new CompanyBankAccount
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Title = reader.IsDBNull(reader.GetOrdinal("Title")) ? string.Empty : reader.GetString(reader.GetOrdinal("Title")),
                AccountNumber = reader.IsDBNull(reader.GetOrdinal("AccountNumber")) ? null : reader.GetString(reader.GetOrdinal("AccountNumber")),
                InitialBalance = reader.IsDBNull(reader.GetOrdinal("InitialBalance")) ? 0m : reader.GetDecimal(reader.GetOrdinal("InitialBalance")),
                CurrentBalance = reader.IsDBNull(reader.GetOrdinal("CurrentBalance")) ? 0m : reader.GetDecimal(reader.GetOrdinal("CurrentBalance")),
                IsCashDesk = !reader.IsDBNull(reader.GetOrdinal("IsCashDesk")) && reader.GetBoolean(reader.GetOrdinal("IsCashDesk")),
                IsActive = !reader.IsDBNull(reader.GetOrdinal("IsActive")) && reader.GetBoolean(reader.GetOrdinal("IsActive")),
                CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTime.Now : reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                BankCode = bankCode,
                BankName = string.IsNullOrEmpty(bankCode) ? null : BankHelper.GetBankNameByCode(bankCode),
                HasCheckbook = !reader.IsDBNull(reader.GetOrdinal("HasCheckbook")) && Convert.ToBoolean(reader[reader.GetOrdinal("HasCheckbook")])
            };
        }

        public async Task<List<CompanyBankAccount>> GetAllAsync()
        {
            var list = new List<CompanyBankAccount>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT Id, Title, AccountNumber, InitialBalance, CurrentBalance, IsCashDesk, IsActive, CreatedAt, BankCode, HasCheckbook FROM CompanyBankAccounts ORDER BY IsCashDesk DESC, Title", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(Map(reader));
            return list;
        }

        public async Task<List<CompanyBankAccount>> GetWithCheckbookAsync()
        {
            var list = new List<CompanyBankAccount>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT Id, Title, AccountNumber, InitialBalance, CurrentBalance, IsCashDesk, IsActive, CreatedAt, BankCode, HasCheckbook
                FROM CompanyBankAccounts
                WHERE IsActive = 1 AND HasCheckbook = 1 AND IsCashDesk = 0
                ORDER BY Title", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(Map(reader));
            return list;
        }

        public async Task<List<CompanyBankAccount>> GetActiveAsync()
        {
            var list = new List<CompanyBankAccount>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT Id, Title, AccountNumber, InitialBalance, CurrentBalance, IsCashDesk, IsActive, CreatedAt, BankCode, HasCheckbook FROM CompanyBankAccounts WHERE IsActive = 1 ORDER BY IsCashDesk DESC, Title", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(Map(reader));
            return list;
        }

        public async Task AddAsync(CompanyBankAccount account)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = "INSERT INTO CompanyBankAccounts (Title, AccountNumber, InitialBalance, CurrentBalance, IsCashDesk, IsActive, BankCode, HasCheckbook) VALUES (@Title, @AccNum, @Initial, @Initial, @IsCash, @IsActive, @BankCode, @HasCheckbook); SELECT SCOPE_IDENTITY();";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Title", account.Title ?? string.Empty);
            cmd.Parameters.AddWithValue("@AccNum", (object?)account.AccountNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Initial", account.InitialBalance);
            cmd.Parameters.AddWithValue("@IsCash", account.IsCashDesk);
            cmd.Parameters.AddWithValue("@IsActive", account.IsActive);
            cmd.Parameters.AddWithValue("@BankCode", (object?)account.BankCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@HasCheckbook", account.HasCheckbook ? 1 : 0);

            var result = await cmd.ExecuteScalarAsync();
            account.Id = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
        }

        public async Task UpdateAsync(CompanyBankAccount account)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                decimal currentInitialBalance = 0;
                decimal currentBalance = 0;

                using (var cmdRead = new SqlCommand("SELECT InitialBalance, CurrentBalance FROM CompanyBankAccounts WHERE Id = @Id", conn, transaction))
                {
                    cmdRead.Parameters.AddWithValue("@Id", account.Id);
                    using var reader = await cmdRead.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        throw new InvalidOperationException("حساب بانکی مورد نظر یافت نشد.");

                    currentInitialBalance = reader.IsDBNull(0) ? 0m : reader.GetDecimal(0);
                    currentBalance = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                }

                decimal diff = account.InitialBalance - currentInitialBalance;
                decimal newBalance = currentBalance + diff;

                if (newBalance < 0)
                {
                    throw new InvalidOperationException(
                        $"با این تغییر، موجودی حساب منفی می‌شود.\n\n" +
                        $"موجودی فعلی: {currentBalance:N0} ریال\n" +
                        $"تغییر مورد نظر: {diff:N0} ریال\n" +
                        $"موجودی جدید: {newBalance:N0} ریال\n\n" +
                        "توجه: موجودی اولیه نمی‌تواند آن‌قدر کاهش یابد که موجودی منفی شود.");
                }

                string sql = @"
                    UPDATE CompanyBankAccounts 
                    SET 
                        Title = @Title,
                        AccountNumber = @AccNum,
                        IsCashDesk = @IsCash,
                        IsActive = @IsActive,
                        BankCode = @BankCode,
                        HasCheckbook = @HasCheckbook,
                        CurrentBalance = CurrentBalance + (@NewInitialBalance - InitialBalance),
                        InitialBalance = @NewInitialBalance
                    WHERE Id = @Id;";

                using var cmd = new SqlCommand(sql, conn, transaction);
                cmd.Parameters.AddWithValue("@Title", account.Title ?? string.Empty);
                cmd.Parameters.AddWithValue("@AccNum", (object?)account.AccountNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NewInitialBalance", account.InitialBalance);
                cmd.Parameters.AddWithValue("@IsCash", account.IsCashDesk);
                cmd.Parameters.AddWithValue("@IsActive", account.IsActive);
                cmd.Parameters.AddWithValue("@BankCode", (object?)account.BankCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@HasCheckbook", account.HasCheckbook ? 1 : 0);
                cmd.Parameters.AddWithValue("@Id", account.Id);

                await cmd.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateCheckbookStatusAsync(int id, bool hasCheckbook)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("UPDATE CompanyBankAccounts SET HasCheckbook = @HasCheckbook WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@HasCheckbook", hasCheckbook ? 1 : 0);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                using (var checkCmd = new SqlCommand("SELECT COUNT(1) FROM Checkbooks WHERE AccountId = @Id", conn, transaction))
                {
                    checkCmd.Parameters.AddWithValue("@Id", id);
                    var res = await checkCmd.ExecuteScalarAsync();
                    if (res != null && res != DBNull.Value && Convert.ToInt32(res) > 0)
                        throw new InvalidOperationException("این حساب دارای دسته چک ثبت‌شده است و قابل حذف نیست.");
                }

                using (var checkCmd = new SqlCommand("SELECT COUNT(1) FROM CashChecks WHERE AccountId = @Id", conn, transaction))
                {
                    checkCmd.Parameters.AddWithValue("@Id", id);
                    var res = await checkCmd.ExecuteScalarAsync();
                    if (res != null && res != DBNull.Value && Convert.ToInt32(res) > 0)
                        throw new InvalidOperationException("این حساب دارای چک ثبت‌شده است و قابل حذف نیست.");
                }

                using (var transCmd = new SqlCommand("SELECT COUNT(1) FROM CashTransactions WHERE AccountId = @Id", conn, transaction))
                {
                    transCmd.Parameters.AddWithValue("@Id", id);
                    var res = await transCmd.ExecuteScalarAsync();
                    if (res != null && res != DBNull.Value && Convert.ToInt32(res) > 0)
                        throw new InvalidOperationException("این حساب دارای تراکنش ثبت‌شده است و قابل حذف نیست.");
                }

                using (var transCmd = new SqlCommand("SELECT COUNT(1) FROM AccountTransfers WHERE SourceAccountId = @Id OR DestinationAccountId = @Id", conn, transaction))
                {
                    transCmd.Parameters.AddWithValue("@Id", id);
                    var res = await transCmd.ExecuteScalarAsync();
                    if (res != null && res != DBNull.Value && Convert.ToInt32(res) > 0)
                        throw new InvalidOperationException("این حساب در انتقالات بین حساب‌ها استفاده شده و قابل حذف نیست.");
                }

                using var cmd = new SqlCommand("DELETE FROM CompanyBankAccounts WHERE Id = @Id", conn, transaction);
                cmd.Parameters.AddWithValue("@Id", id);
                await cmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // ✅ متد جدید: محاسبه موجودی واقعی بر اساس گردش
        //    - موجودی اولیه
        //    - + تراکنش نقدی/حواله دریافتی + چک دریافتی + انتقال ورودی
        //    - − تراکنش نقدی/حواله پرداختی − چک پرداختی − انتقال خروجی
        //    - (تراکنش‌های دستی «انتقال بین حساب‌ها» نادیده گرفته می‌شوند
        //       چون همان گردش در AccountTransfers ثبت شده)
        // ═══════════════════════════════════════════════════════════════
        public async Task<decimal> GetTrueBalanceAsync(int accountId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            const string sql = @"
                DECLARE @InitialBalance DECIMAL(18,0) = 0;
                SELECT @InitialBalance = ISNULL(InitialBalance, 0)
                FROM CompanyBankAccounts WHERE Id = @Id;

                DECLARE @Inflow DECIMAL(18,0) = 0;
                DECLARE @Outflow DECIMAL(18,0) = 0;

                -- ۱. تراکنش‌های نقدی/حواله (بدون انتقال بین حساب‌ها)
                SELECT @Inflow = @Inflow + ISNULL(SUM(Amount), 0)
                FROM CashTransactions
                WHERE AccountId = @Id
                  AND Direction = N'دریافتی'
                  AND LTRIM(RTRIM(ISNULL(Category, ''))) <> N'انتقال بین حساب‌ها';

                SELECT @Outflow = @Outflow + ISNULL(SUM(Amount), 0)
                FROM CashTransactions
                WHERE AccountId = @Id
                  AND Direction = N'پرداختی'
                  AND LTRIM(RTRIM(ISNULL(Category, ''))) <> N'انتقال بین حساب‌ها';

                -- ۲. چک‌ها (بدون باطل/برگشتی)
                SELECT @Inflow = @Inflow + ISNULL(SUM(Amount), 0)
                FROM CashChecks
                WHERE AccountId = @Id
                  AND CheckType = N'دریافتی'
                  AND Status NOT IN (N'باطل شده', N'برگشتی');

                SELECT @Outflow = @Outflow + ISNULL(SUM(Amount), 0)
                FROM CashChecks
                WHERE AccountId = @Id
                  AND CheckType = N'پرداختی'
                  AND Status NOT IN (N'باطل شده', N'برگشتی');

                -- ۳. انتقالات بین حساب‌ها
                SELECT @Inflow = @Inflow + ISNULL(SUM(Amount), 0)
                FROM AccountTransfers
                WHERE DestinationAccountId = @Id;

                SELECT @Outflow = @Outflow + ISNULL(SUM(Amount), 0)
                FROM AccountTransfers
                WHERE SourceAccountId = @Id;

                SELECT @InitialBalance + @Inflow - @Outflow;
            ";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", accountId);
            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value ? Convert.ToDecimal(result) : 0m;
        }
    }
}