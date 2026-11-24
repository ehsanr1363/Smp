using Microsoft.Data.SqlClient;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Smp.Data;

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
            PendingChecksAmount = reader.IsDBNull(reader.GetOrdinal("PendingChecksAmount")) ? 0m : reader.GetDecimal(reader.GetOrdinal("PendingChecksAmount")),
            PendingIncomingChecksAmount = reader.IsDBNull(reader.GetOrdinal("PendingIncomingChecksAmount")) ? 0m : reader.GetDecimal(reader.GetOrdinal("PendingIncomingChecksAmount")),
            AccountType = reader.IsDBNull(reader.GetOrdinal("AccountType")) ? null : reader.GetString(reader.GetOrdinal("AccountType")),
            Branch = reader.IsDBNull(reader.GetOrdinal("Branch")) ? null : reader.GetString(reader.GetOrdinal("Branch")),
            IsCashDesk = !reader.IsDBNull(reader.GetOrdinal("IsCashDesk")) && reader.GetBoolean(reader.GetOrdinal("IsCashDesk")),
            IsActive = !reader.IsDBNull(reader.GetOrdinal("IsActive")) && reader.GetBoolean(reader.GetOrdinal("IsActive")),
            CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTime.Now : reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            BankCode = bankCode,
            BankName = string.IsNullOrEmpty(bankCode) ? null : BankHelper.GetBankNameByCode(bankCode),
            HasCheckbook = !reader.IsDBNull(reader.GetOrdinal("HasCheckbook")) && Convert.ToBoolean(reader[reader.GetOrdinal("HasCheckbook")])
        };
    }

    // نکته: View شامل همه حساب‌هاست (بانک + صندوق)؛ فیلتر بانک‌ها صریحاً در کوئری اعمال می‌شود
    private const string BaseSelect = @"
            SELECT a.Id, a.Title, a.AccountNumber, a.InitialBalance, a.CurrentBalance, 
                   a.IsCashDesk, a.IsActive, a.CreatedAt, a.BankCode,
                   CASE WHEN ISNULL(a.HasCheckbook, 0) = 1
                          OR EXISTS (SELECT 1 FROM Checkbooks cb WHERE cb.AccountId = a.Id)
                        THEN 1 ELSE 0 END AS HasCheckbook,
                   a.AccountType, a.Branch,
                   ISNULL((SELECT SUM(Amount) FROM CashChecks 
                           WHERE AccountId = a.Id AND CheckType = N'پرداختی' AND Status = N'در جریان'), 0) AS PendingChecksAmount,
                   ISNULL((SELECT SUM(Amount) FROM CashChecks 
                           WHERE AccountId = a.Id AND CheckType = N'دریافتی' AND Status = N'در جریان'), 0) AS PendingIncomingChecksAmount
            FROM CompanyBankAccounts a";

    public async Task<List<CompanyBankAccount>> GetBankAccountsOnlyAsync()
    {
        var list = new List<CompanyBankAccount>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand($"{BaseSelect} WHERE a.IsCashDesk = 0 ORDER BY a.Title", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(Map(reader));
        return list;
    }

    // ✅ این متد حالا از جدول واقعی (RealCompanyBankAccounts) می‌خواند تا تنخواه‌ها را پیدا کند
    public async Task<List<CompanyBankAccount>> GetTankhahsOnlyAsync()
    {
        var list = new List<CompanyBankAccount>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
                SELECT a.Id, a.Title, a.AccountNumber, a.InitialBalance, a.CurrentBalance, 
                       a.IsCashDesk, a.IsActive, a.CreatedAt, a.BankCode, a.HasCheckbook, a.AccountType, a.Branch,
                       ISNULL((SELECT SUM(Amount) FROM CashChecks 
                               WHERE AccountId = a.Id AND CheckType = N'پرداختی' AND Status = N'در جریان'), 0) AS PendingChecksAmount,
                       ISNULL((SELECT SUM(Amount) FROM CashChecks 
                               WHERE AccountId = a.Id AND CheckType = N'دریافتی' AND Status = N'در جریان'), 0) AS PendingIncomingChecksAmount
                FROM RealCompanyBankAccounts a
                WHERE a.IsCashDesk = 1 ORDER BY a.Id";

        using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(Map(reader));
        return list;
    }

    public async Task<List<CompanyBankAccount>> GetAllAsync()
    {
        var list = new List<CompanyBankAccount>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand($"{BaseSelect} ORDER BY a.Title", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(Map(reader));
        return list;
    }

    public async Task<List<CompanyBankAccount>> GetActiveAsync()
    {
        return await GetAllAsync();
    }

    public async Task<List<CompanyBankAccount>> GetWithCheckbookAsync()
    {
        var list = new List<CompanyBankAccount>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand($"{BaseSelect} WHERE a.IsActive = 1 AND a.HasCheckbook = 1 ORDER BY a.Title", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(Map(reader));
        return list;
    }

    // عنوان دستی حذف شده؛ برای حساب بانکی، Title از نوع/بانک/شعبه ساخته می‌شود (صندوق‌ها دست‌نخورده می‌مانند)
    private static void ApplyAutoTitle(CompanyBankAccount account)
    {
        if (account.IsCashDesk) return;
        if (string.IsNullOrEmpty(account.BankName) && !string.IsNullOrEmpty(account.BankCode))
            account.BankName = BankHelper.GetBankNameByCode(account.BankCode);
        account.Title = account.AutoTitle;
    }

    /// <summary>یک‌بار برای همیشه: عنوان‌های دستی قدیمی حساب‌های بانکی را با عنوان خودکار جایگزین می‌کند (idempotent).</summary>
    public async Task NormalizeBankTitlesAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var rows = new List<(int Id, string Title, string? Code, string? Type, string? Branch)>();
        using (var cmd = new SqlCommand("SELECT Id, Title, BankCode, AccountType, Branch FROM RealCompanyBankAccounts WHERE IsCashDesk = 0", conn))
        using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
                rows.Add((r.GetInt32(0), r.IsDBNull(1) ? "" : r.GetString(1),
                          r.IsDBNull(2) ? null : r.GetString(2),
                          r.IsDBNull(3) ? null : r.GetString(3),
                          r.IsDBNull(4) ? null : r.GetString(4)));
        }

        foreach (var row in rows)
        {
            var acc = new CompanyBankAccount
            {
                BankCode = row.Code,
                BankName = string.IsNullOrEmpty(row.Code) ? null : BankHelper.GetBankNameByCode(row.Code),
                AccountType = row.Type,
                Branch = row.Branch
            };
            string auto = acc.AutoTitle;
            if (auto == row.Title) continue;

            using var up = new SqlCommand("UPDATE RealCompanyBankAccounts SET Title = @T WHERE Id = @Id", conn);
            up.Parameters.AddWithValue("@T", auto);
            up.Parameters.AddWithValue("@Id", row.Id);
            await up.ExecuteNonQueryAsync();
        }
    }

    public async Task AddAsync(CompanyBankAccount account)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        ApplyAutoTitle(account);

        // ✅ درج در جدول اصلی (RealCompanyBankAccounts)
        string sql = @"
                INSERT INTO RealCompanyBankAccounts (Title, AccountNumber, InitialBalance, CurrentBalance, IsCashDesk, IsActive, BankCode, HasCheckbook, AccountType, Branch, CreatedAt) 
                VALUES (@Title, @AccNum, @Initial, @Initial, @IsCash, @IsActive, @BankCode, @HasCheckbook, @AccountType, @Branch, @CreatedAt); 
                SELECT SCOPE_IDENTITY();";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Title", account.Title ?? string.Empty);
        cmd.Parameters.AddWithValue("@AccNum", (object?)account.AccountNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Initial", account.InitialBalance);
        cmd.Parameters.AddWithValue("@IsCash", account.IsCashDesk);
        cmd.Parameters.AddWithValue("@IsActive", account.IsActive);
        cmd.Parameters.AddWithValue("@BankCode", (object?)account.BankCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@HasCheckbook", account.HasCheckbook ? 1 : 0);
        cmd.Parameters.AddWithValue("@AccountType", (object?)account.AccountType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Branch", (object?)account.Branch ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", account.CreatedAt == default ? DateTime.Now : account.CreatedAt);

        var result = await cmd.ExecuteScalarAsync();
        account.Id = result is not null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
        Smp.Helpers.AuditHelper.Log("ثبت", "حساب بانکی", $"{account.AutoTitle}");
    }

    public async Task UpdateAsync(CompanyBankAccount account)
    {
        ApplyAutoTitle(account);

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        try
        {
            decimal currentInitialBalance = 0;
            decimal currentBalance = 0;

            using (var cmdRead = new SqlCommand("SELECT InitialBalance, CurrentBalance FROM RealCompanyBankAccounts WHERE Id = @Id", conn, transaction))
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

            if (!account.IsCashDesk && currentBalance >= 0 && newBalance < 0)
            {
                throw new InvalidOperationException(
                    $"با این تغییر، موجودی حساب بانکی منفی می‌شود.\n\n" +
                    $"موجودی فعلی: {currentBalance:N0} ریال\n" +
                    $"تغییر مورد نظر: {diff:N0} ریال\n" +
                    $"موجودی جدید: {newBalance:N0} ریال");
            }

            string sql = @"
                    UPDATE RealCompanyBankAccounts 
                    SET Title = @Title, AccountNumber = @AccNum, IsCashDesk = @IsCash, IsActive = @IsActive, 
                        BankCode = @BankCode, HasCheckbook = @HasCheckbook, AccountType = @AccountType, Branch = @Branch,
                        CreatedAt = @CreatedAt,
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
            cmd.Parameters.AddWithValue("@AccountType", (object?)account.AccountType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Branch", (object?)account.Branch ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedAt", account.CreatedAt);
            cmd.Parameters.AddWithValue("@Id", account.Id);

            await cmd.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        Smp.Helpers.AuditHelper.Log("ویرایش", "حساب بانکی", $"شناسه {account.Id}");
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        try
        {
            // بررسی وابستگی‌ها (چک، تراکنش، انتقال) در جدول واقعی
            using (var checkCmd = new SqlCommand("SELECT COUNT(1) FROM Checkbooks WHERE AccountId = @Id", conn, transaction))
            {
                checkCmd.Parameters.AddWithValue("@Id", id);
                var res = await checkCmd.ExecuteScalarAsync();
                if (res is not null && res != DBNull.Value && Convert.ToInt32(res) > 0)
                    throw new InvalidOperationException("این حساب دارای دسته چک ثبت‌شده است و قابل حذف نیست.");
            }

            using (var checkCmd = new SqlCommand("SELECT COUNT(1) FROM CashChecks WHERE AccountId = @Id", conn, transaction))
            {
                checkCmd.Parameters.AddWithValue("@Id", id);
                var res = await checkCmd.ExecuteScalarAsync();
                if (res is not null && res != DBNull.Value && Convert.ToInt32(res) > 0)
                    throw new InvalidOperationException("این حساب دارای چک ثبت‌شده است و قابل حذف نیست.");
            }

            using (var transCmd = new SqlCommand("SELECT COUNT(1) FROM CashTransactions WHERE AccountId = @Id", conn, transaction))
            {
                transCmd.Parameters.AddWithValue("@Id", id);
                var res = await transCmd.ExecuteScalarAsync();
                if (res is not null && res != DBNull.Value && Convert.ToInt32(res) > 0)
                    throw new InvalidOperationException("این حساب دارای تراکنش ثبت‌شده است و قابل حذف نیست.");
            }

            using (var transCmd = new SqlCommand("SELECT COUNT(1) FROM AccountTransfers WHERE SourceAccountId = @Id OR DestinationAccountId = @Id", conn, transaction))
            {
                transCmd.Parameters.AddWithValue("@Id", id);
                var res = await transCmd.ExecuteScalarAsync();
                if (res is not null && res != DBNull.Value && Convert.ToInt32(res) > 0)
                    throw new InvalidOperationException("این حساب در انتقالات بین حساب‌ها استفاده شده و قابل حذف نیست.");
            }

            using var cmd = new SqlCommand("DELETE FROM RealCompanyBankAccounts WHERE Id = @Id", conn, transaction);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        Smp.Helpers.AuditHelper.Log("حذف", "حساب بانکی", $"شناسه {id}");
    }

    public async Task UpdateCheckbookStatusAsync(int id, bool hasCheckbook)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand("UPDATE RealCompanyBankAccounts SET HasCheckbook = @HasCheckbook WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@HasCheckbook", hasCheckbook ? 1 : 0);
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
    }
}
