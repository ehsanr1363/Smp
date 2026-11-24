using Microsoft.Data.SqlClient;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Smp.Data;

public class CashTransactionRepository
{
    private readonly string _connectionString;

    public CashTransactionRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<CashTransaction>> GetByContractorContractIdAsync(int contractorContractId)
    {
        var list = new List<CashTransaction>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        string sql = "SELECT * FROM CashTransactions WHERE ContractorContractId = @Id ORDER BY TransactionDate DESC, Id DESC";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", contractorContractId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(MapReader(reader));
        return list;
    }

    public async Task<List<CashTransaction>> GetByContractIdAsync(int contractId)
    {
        var list = new List<CashTransaction>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        string sql = "SELECT * FROM CashTransactions WHERE ContractId = @ContractId ORDER BY TransactionDate DESC, Id DESC";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ContractId", contractId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(MapReader(reader));
        return list;
    }

    public async Task<CashTransaction?> GetByIdAsync(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        string sql = "SELECT * FROM CashTransactions WHERE Id = @Id";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync()) return MapReader(reader);
        return null;
    }

    public async Task<List<CashTransaction>> GetByAccountIdAsync(int accountId)
    {
        var list = new List<CashTransaction>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        string sql = "SELECT * FROM CashTransactions WHERE AccountId = @AccountId ORDER BY TransactionDate DESC, Id DESC";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@AccountId", accountId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(MapReader(reader));
        return list;
    }

    public async Task<int> AddPayrollTransactionAsync(int accountId, string accountTitle, decimal totalAmount, int year, int month, string description)
    {
        var t = new CashTransaction
        {
            PaymentScope = PaymentScopes.Administrative,
            TransactionDate = DateTime.Now,
            Amount = totalAmount,
            TransactionType = "حواله",
            Direction = DirectionTypes.Outgoing,
            AccountId = accountId,
            AccountTitle = accountTitle,
            Category = "پرسنلی",
            CostType = "سایر",
            Description = description
        };

        await AddAsync(t);
        return t.Id;
    }

    public async Task AddAsync(CashTransaction t)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            string sql = @"
                    INSERT INTO CashTransactions 
                    (PaymentScope, ContractId, ContractorContractId, ContractTitle, ClientStatementId,
                     TransactionDate, Amount, Discount, Shipping, VAT, TransactionType, Direction, AccountId, AccountTitle, 
                     Category, CostType, RecipientName, RecipientSheba, TafsiliCode, TafsiliTitle, Description, CreatedAt)
                    VALUES 
                    (@PaymentScope, @ContractId, @ContractorContractId, @ContractTitle, @ClientStatementId,
                     @TransactionDate, @Amount, @Discount, @Shipping, @VAT, @TransactionType, @Direction, @AccountId, @AccountTitle, 
                     @Category, @CostType, @RecipientName, @RecipientSheba, @TafsiliCode, @TafsiliTitle, @Description, GETDATE());
                    SELECT SCOPE_IDENTITY();";

            using var cmd = new SqlCommand(sql, conn, tx);
            AddTransactionParameters(cmd, t);
            var scalarRes = await cmd.ExecuteScalarAsync();
            t.Id = scalarRes is not null && scalarRes != DBNull.Value ? Convert.ToInt32(scalarRes) : 0;

            if (t.AccountId.HasValue)
            {
                string balanceSql = t.Direction == DirectionTypes.Outgoing
                    ? "UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance - @Amount WHERE Id = @AccId"
                    : "UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance + @Amount WHERE Id = @AccId";

                using var cmdBal = new SqlCommand(balanceSql, conn, tx);
                cmdBal.Parameters.AddWithValue("@Amount", t.Amount);
                cmdBal.Parameters.AddWithValue("@AccId", t.AccountId.Value);
                await cmdBal.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
        Smp.Helpers.AuditHelper.Log("ثبت", "تراکنش نقدی", $"شناسه {t.Id}، مبلغ {t.Amount:N0}");
    }

    public async Task UpdateAsync(CashTransaction t)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            decimal oldAmount = 0;
            string oldDirection = "";
            int? oldAccountId = null;
            int? oldStatementId = null;

            using (var cmdOld = new SqlCommand("SELECT Amount, Direction, AccountId, ClientStatementId FROM CashTransactions WITH (UPDLOCK, ROWLOCK) WHERE Id = @Id", conn, tx))
            {
                cmdOld.Parameters.AddWithValue("@Id", t.Id);
                using var reader = await cmdOld.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    oldAmount = reader.IsDBNull(0) ? 0m : reader.GetDecimal(0);
                    oldDirection = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    oldAccountId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                    oldStatementId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
                }
                else
                {
                    throw new InvalidOperationException("تراکنش مورد نظر یافت نشد (ممکن است قبلاً حذف شده باشد).");
                }
            }

            // ✅ ردیفی که در صورت‌حساب پیمانکار/کارفرما ثبت شده نباید بی‌صدا تغییر کند
            if (oldStatementId.HasValue && (oldAmount != t.Amount || oldAccountId != t.AccountId || oldDirection != t.Direction))
                throw new InvalidOperationException("این تراکنش در یک صورت‌حساب ثبت شده است و تغییر مبلغ، حساب یا جهت آن مجاز نیست. ابتدا آن را از صورت‌حساب خارج کنید.");

            if (oldAccountId.HasValue)
            {
                string revertSql = oldDirection == DirectionTypes.Outgoing
                    ? "UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance + @Amount WHERE Id = @AccId"
                    : "UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance - @Amount WHERE Id = @AccId";

                using var cmdRevert = new SqlCommand(revertSql, conn, tx);
                cmdRevert.Parameters.AddWithValue("@Amount", oldAmount);
                cmdRevert.Parameters.AddWithValue("@AccId", oldAccountId.Value);
                await cmdRevert.ExecuteNonQueryAsync();
            }

            string sql = @"
                    UPDATE CashTransactions SET 
                        PaymentScope = @PaymentScope, ContractId = @ContractId, ContractorContractId = @ContractorContractId, 
                        ContractTitle = @ContractTitle, ClientStatementId = @ClientStatementId, TransactionDate = @TransactionDate, Amount = @Amount, 
                        Discount = @Discount, Shipping = @Shipping, VAT = @VAT,
                        TransactionType = @TransactionType, Direction = @Direction, AccountId = @AccountId, 
                        AccountTitle = @AccountTitle, Category = @Category, CostType = @CostType, 
                        RecipientName = @RecipientName, RecipientSheba = @RecipientSheba, TafsiliCode = @TafsiliCode, 
                        TafsiliTitle = @TafsiliTitle, Description = @Description
                    WHERE Id = @Id";

            using (var cmd = new SqlCommand(sql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", t.Id);
                AddTransactionParameters(cmd, t);
                await cmd.ExecuteNonQueryAsync();
            }

            if (t.AccountId.HasValue)
            {
                string applySql = t.Direction == DirectionTypes.Outgoing
                    ? "UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance - @Amount WHERE Id = @AccId"
                    : "UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance + @Amount WHERE Id = @AccId";

                using var cmdApply = new SqlCommand(applySql, conn, tx);
                cmdApply.Parameters.AddWithValue("@Amount", t.Amount);
                cmdApply.Parameters.AddWithValue("@AccId", t.AccountId.Value);
                await cmdApply.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
        Smp.Helpers.AuditHelper.Log("ویرایش", "تراکنش نقدی", $"شناسه {t.Id}، مبلغ {t.Amount:N0}");
    }

    public async Task<List<CashTransaction>> GetAllAsync()
    {
        var list = new List<CashTransaction>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        string sql = "SELECT * FROM CashTransactions ORDER BY TransactionDate DESC, Id DESC";
        using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(MapReader(reader));
        return list;
    }

    public async Task<(List<CashTransaction> list, int totalCount)> GetPagedAsync(
        int page, int pageSize, string searchTerm = "",
        string sortColumn = "تاریخ", string sortDirection = "DESC", int? accountId = null)
    {
        var list = new List<CashTransaction>();
        int totalCount = 0;

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string whereClause = " WHERE 1=1 ";
        if (accountId.HasValue)
            whereClause += " AND AccountId = @AccountId ";

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            // ✅ جستجو کاملاً پارامتری (جلوگیری از SQL Injection)
            whereClause += @" AND (Description LIKE @Search ESCAPE '\'
                                 OR TafsiliTitle LIKE @Search ESCAPE '\'
                                 OR RecipientName LIKE @Search ESCAPE '\'
                                 OR ContractTitle LIKE @Search ESCAPE '\'
                                 OR CostType LIKE @Search ESCAPE '\'
                                 OR AccountTitle LIKE @Search ESCAPE '\'
                                 OR Direction LIKE @Search ESCAPE '\'
                                 OR Category LIKE @Search ESCAPE '\')";
        }

        string baseQuery = @"
                WITH UnifiedTransactions AS (
                    SELECT 
                        t.Id, CASE WHEN (t.TankhahListId IS NOT NULL OR ISNULL(t.Description, N'') LIKE N'شارژ تنخواه%') AND t.AccountId IS NOT NULL AND ISNULL(t.Category, N'') = N'انتقال بین حساب‌ها' THEN 'TankhahCharges' ELSE 'CashTransactions' END AS TableName, t.PaymentScope, t.ContractId, t.ContractorContractId, 
                        t.ContractTitle, t.ClientStatementId, t.TransactionDate, t.Amount, t.Discount, t.Shipping, t.VAT, 
                        t.TransactionType, t.Direction, t.AccountId, t.AccountTitle, t.Category, t.CostType, 
                        t.RecipientName, t.RecipientSheba, t.TafsiliCode, t.TafsiliTitle, t.Description, t.CreatedAt
                    FROM CashTransactions t
                    WHERE (ISNULL(t.Category, N'') <> N'انتقال بین حساب‌ها' OR (t.TankhahListId IS NOT NULL AND t.AccountId IS NOT NULL) OR (t.TankhahListId IS NULL AND t.AccountId IS NOT NULL AND t.Direction = N'پرداختی' AND ISNULL(t.Description, N'') LIKE N'شارژ تنخواه%')) -- شارژ تنخواه (خروج از بانک) هم نمایش داده شود
                    
                    UNION ALL
                    
                    SELECT 
                        k.Id, 'CashChecks' AS TableName, k.PaymentScope, k.ContractId, k.ContractorContractId, 
                        k.ContractTitle, k.ClientStatementId, k.DueDate AS TransactionDate, k.Amount, 0 AS Discount, 0 AS Shipping, 0 AS VAT, 
                        N'چک' AS TransactionType, k.CheckType AS Direction, k.AccountId, a.Title AS AccountTitle, N'چک' AS Category, k.CostType, 
                        k.RecipientName, CAST(NULL AS NVARCHAR(50)) AS RecipientSheba, '' AS TafsiliCode, k.RecipientName AS TafsiliTitle, k.Description, k.CreatedAt
                    FROM CashChecks k
                    LEFT JOIN CompanyBankAccounts a ON k.AccountId = a.Id

                    UNION ALL
                    
                    SELECT 
                        t.Id, 'AccountTransfers' AS TableName, N'Administrative' AS PaymentScope, NULL AS ContractId, NULL AS ContractorContractId, 
                        N'' AS ContractTitle, NULL AS ClientStatementId, t.TransferDate AS TransactionDate, t.Amount, 0 AS Discount, 0 AS Shipping, 0 AS VAT, 
                        N'انتقال' AS TransactionType, N'پرداختی' AS Direction, t.SourceAccountId AS AccountId, s.Title AS AccountTitle, N'انتقال بین حساب‌ها' AS Category, N'سایر' AS CostType, 
                        NULL AS RecipientName, NULL AS RecipientSheba, NULL AS TafsiliCode, NULL AS TafsiliTitle, 
                        N'انتقال به ' + ISNULL(d.Title, N'حساب حذف شده') + N' - ' + ISNULL(t.Description, N'') + N' [TransferId:' + CAST(t.Id AS NVARCHAR) + N']' AS Description, 
                        t.CreatedAt
                    FROM AccountTransfers t
                    LEFT JOIN CompanyBankAccounts s ON t.SourceAccountId = s.Id
                    LEFT JOIN CompanyBankAccounts d ON t.DestinationAccountId = d.Id

                    UNION ALL
                    
                    SELECT 
                        t.Id, 'AccountTransfers' AS TableName, N'Administrative' AS PaymentScope, NULL AS ContractId, NULL AS ContractorContractId, 
                        N'' AS ContractTitle, NULL AS ClientStatementId, t.TransferDate AS TransactionDate, t.Amount, 0 AS Discount, 0 AS Shipping, 0 AS VAT, 
                        N'انتقال' AS TransactionType, N'دریافتی' AS Direction, t.DestinationAccountId AS AccountId, d.Title AS AccountTitle, N'انتقال بین حساب‌ها' AS Category, N'سایر' AS CostType, 
                        NULL AS RecipientName, NULL AS RecipientSheba, NULL AS TafsiliCode, NULL AS TafsiliTitle, 
                        N'انتقال از ' + ISNULL(s.Title, N'حساب حذف شده') + N' - ' + ISNULL(t.Description, N'') + N' [TransferId:' + CAST(t.Id AS NVARCHAR) + N']' AS Description, 
                        t.CreatedAt
                    FROM AccountTransfers t
                    LEFT JOIN CompanyBankAccounts s ON t.SourceAccountId = s.Id
                    LEFT JOIN CompanyBankAccounts d ON t.DestinationAccountId = d.Id
                )";

        // ✅ بهینه‌سازی بسیار مهم: محاسبه سریع تعداد کل رکوردها بدون اسکن کل CTE
        string countSql;
        if (string.IsNullOrWhiteSpace(searchTerm) && !accountId.HasValue)
        {
            // اگر فیلتر و سرچی نیست، سریع تعداد جداول را جمع می‌زنیم (چون انتقال‌ها ۲ بار نمایش داده می‌شوند)
            countSql = @"
                    SELECT 
                        (SELECT COUNT(1) FROM CashTransactions WHERE LTRIM(RTRIM(ISNULL(Category, ''))) <> N'انتقال بین حساب‌ها' OR (TankhahListId IS NOT NULL AND AccountId IS NOT NULL) OR (TankhahListId IS NULL AND AccountId IS NOT NULL AND Direction = N'پرداختی' AND ISNULL(Description, N'') LIKE N'شارژ تنخواه%')) +
                        (SELECT COUNT(1) FROM CashChecks) +
                        (SELECT COUNT(1) FROM AccountTransfers) * 2";
        }
        else
        {
            countSql = $"{baseQuery} SELECT COUNT(1) FROM UnifiedTransactions {whereClause} OPTION (RECOMPILE)";
        }

        using (var countCmd = new SqlCommand(countSql, conn))
        {
            if (accountId.HasValue) countCmd.Parameters.AddWithValue("@AccountId", accountId.Value);
            if (!string.IsNullOrWhiteSpace(searchTerm)) countCmd.Parameters.AddWithValue("@Search", ToLikePattern(searchTerm));
            var countRes = await countCmd.ExecuteScalarAsync();
            totalCount = countRes is not null && countRes != DBNull.Value ? Convert.ToInt32(countRes) : 0;
        }

        string orderByClause = "ORDER BY TransactionDate DESC, Id DESC";
        string dir = sortDirection.Equals("ASC", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        if (!string.IsNullOrWhiteSpace(sortColumn))
        {
            switch (sortColumn)
            {
                case "تاریخ": orderByClause = $"ORDER BY TransactionDate {dir}, Id {dir}"; break;
                case "نوع_پرداخت": orderByClause = $"ORDER BY TransactionType {dir}"; break;
                case "محل_مصرف": orderByClause = $"ORDER BY PaymentScope {dir}"; break;
                case "حساب_بانکی": orderByClause = $"ORDER BY AccountTitle {dir}"; break;
                case "ماهیت_هزینه": orderByClause = $"ORDER BY CostType {dir}"; break;
                case "جهت": orderByClause = $"ORDER BY Direction {dir}"; break;
                case "طرف_حساب": orderByClause = $"ORDER BY ISNULL(TafsiliTitle, ISNULL(RecipientName, '---')) {dir}"; break;
                case "بدهکار": orderByClause = $"ORDER BY CASE WHEN Direction = N'دریافتی' THEN Amount ELSE 0 END {dir}"; break;
                case "بستانکار": orderByClause = $"ORDER BY CASE WHEN Direction = N'پرداختی' THEN Amount ELSE 0 END {dir}"; break;
                case "وضعیت": orderByClause = $"ORDER BY ClientStatementId {dir}"; break;
                default: orderByClause = $"ORDER BY TransactionDate {dir}"; break;
            }
        }

        int offset = (page - 1) * pageSize;
        string sql = $@"
                {baseQuery}
                SELECT * FROM UnifiedTransactions 
                {whereClause}
                {orderByClause}
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
                OPTION (RECOMPILE)";

        using (var cmd = new SqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@Offset", offset);
            cmd.Parameters.AddWithValue("@PageSize", pageSize);
            if (accountId.HasValue) cmd.Parameters.AddWithValue("@AccountId", accountId.Value);
            if (!string.IsNullOrWhiteSpace(searchTerm)) cmd.Parameters.AddWithValue("@Search", ToLikePattern(searchTerm));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapReader(reader));
        }

        return (list, totalCount);
    }

    public async Task DeleteBatchAsync(List<int> ids)
    {
        if (ids is null || ids.Count == 0) return;

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            foreach (var id in ids)
            {
                decimal amount = 0;
                string direction = "";
                int? accountId = null;

                using (var cmdSelect = new SqlCommand("SELECT Amount, Direction, AccountId, ClientStatementId FROM CashTransactions WITH (UPDLOCK, ROWLOCK) WHERE Id = @Id", conn, tx))
                {
                    cmdSelect.Parameters.AddWithValue("@Id", id);
                    using var reader = await cmdSelect.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        if (!reader.IsDBNull(3))
                            throw new InvalidOperationException("حذف ممکن نیست: یکی از تراکنش‌های انتخابی در صورت‌حساب ثبت شده است.");
                        amount = reader.IsDBNull(0) ? 0m : reader.GetDecimal(0);
                        direction = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        accountId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                    }
                }

                if (accountId.HasValue)
                {
                    string revertSql = direction == DirectionTypes.Outgoing
                        ? "UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance + @Amount WHERE Id = @AccId"
                        : "UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance - @Amount WHERE Id = @AccId";

                    using var cmdRevert = new SqlCommand(revertSql, conn, tx);
                    cmdRevert.Parameters.AddWithValue("@Amount", amount);
                    cmdRevert.Parameters.AddWithValue("@AccId", accountId.Value);
                    await cmdRevert.ExecuteNonQueryAsync();
                }

                using var cmdDel = new SqlCommand("DELETE FROM CashTransactions WHERE Id = @Id", conn, tx);
                cmdDel.Parameters.AddWithValue("@Id", id);
                await cmdDel.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
        Smp.Helpers.AuditHelper.Log("حذف", "تراکنش نقدی", $"{ids.Count} تراکنش، شناسه‌ها: {string.Join(",", ids)}");
    }

    public async Task BulkInsertAsync(List<CashTransaction> transactions)
    {
        if (transactions is null || transactions.Count == 0) return;

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            var dt = new DataTable();
            dt.Columns.Add("PaymentScope", typeof(string));
            dt.Columns.Add("TransactionDate", typeof(DateTime));
            dt.Columns.Add("Amount", typeof(decimal));
            dt.Columns.Add("Discount", typeof(decimal));
            dt.Columns.Add("Shipping", typeof(decimal));
            dt.Columns.Add("VAT", typeof(decimal));
            dt.Columns.Add("TransactionType", typeof(string));
            dt.Columns.Add("Direction", typeof(string));
            dt.Columns.Add("AccountId", typeof(int));
            dt.Columns.Add("AccountTitle", typeof(string));
            dt.Columns.Add("Category", typeof(string));
            dt.Columns.Add("CostType", typeof(string));
            dt.Columns.Add("Description", typeof(string));
            dt.Columns.Add("CreatedAt", typeof(DateTime));
            dt.Columns.Add("ContractId", typeof(int));
            dt.Columns.Add("ContractorContractId", typeof(int));
            dt.Columns.Add("ContractTitle", typeof(string));
            dt.Columns.Add("ClientStatementId", typeof(int));
            dt.Columns.Add("RecipientName", typeof(string));
            dt.Columns.Add("RecipientSheba", typeof(string));
            dt.Columns.Add("TafsiliCode", typeof(string));
            dt.Columns.Add("TafsiliTitle", typeof(string));

            foreach (var t in transactions)
            {
                dt.Rows.Add(t.PaymentScope ?? (object)DBNull.Value, t.TransactionDate, t.Amount, t.Discount, t.Shipping, t.VAT, t.TransactionType ?? "", t.Direction ?? "",
                    t.AccountId.HasValue ? (object)t.AccountId.Value : DBNull.Value, t.AccountTitle ?? (object)DBNull.Value, t.Category ?? (object)DBNull.Value, t.CostType ?? (object)DBNull.Value, t.Description ?? (object)DBNull.Value, DateTime.Now,
                    t.ContractId.HasValue ? (object)t.ContractId.Value : DBNull.Value,
                    t.ContractorContractId.HasValue ? (object)t.ContractorContractId.Value : DBNull.Value,
                    t.ContractTitle ?? (object)DBNull.Value,
                    t.ClientStatementId.HasValue ? (object)t.ClientStatementId.Value : DBNull.Value,
                    t.RecipientName ?? (object)DBNull.Value, t.RecipientSheba ?? (object)DBNull.Value,
                    t.TafsiliCode ?? (object)DBNull.Value, t.TafsiliTitle ?? (object)DBNull.Value);
            }

            using var bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.FireTriggers, tx);
            bulkCopy.DestinationTableName = "CashTransactions";
            bulkCopy.ColumnMappings.Add("PaymentScope", "PaymentScope");
            bulkCopy.ColumnMappings.Add("TransactionDate", "TransactionDate");
            bulkCopy.ColumnMappings.Add("Amount", "Amount");
            bulkCopy.ColumnMappings.Add("Discount", "Discount");
            bulkCopy.ColumnMappings.Add("Shipping", "Shipping");
            bulkCopy.ColumnMappings.Add("VAT", "VAT");
            bulkCopy.ColumnMappings.Add("TransactionType", "TransactionType");
            bulkCopy.ColumnMappings.Add("Direction", "Direction");
            bulkCopy.ColumnMappings.Add("AccountId", "AccountId");
            bulkCopy.ColumnMappings.Add("AccountTitle", "AccountTitle");
            bulkCopy.ColumnMappings.Add("Category", "Category");
            bulkCopy.ColumnMappings.Add("CostType", "CostType");
            bulkCopy.ColumnMappings.Add("Description", "Description");
            bulkCopy.ColumnMappings.Add("CreatedAt", "CreatedAt");
            foreach (var extra in new[] { "ContractId", "ContractorContractId", "ContractTitle", "ClientStatementId", "RecipientName", "RecipientSheba", "TafsiliCode", "TafsiliTitle" })
                bulkCopy.ColumnMappings.Add(extra, extra);

            await bulkCopy.WriteToServerAsync(dt);

            var accountUpdates = transactions
                .Where(t => t.AccountId.HasValue)
                .GroupBy(t => t.AccountId!.Value)
                .Select(g => new
                {
                    AccountId = g.Key,
                    TotalOutgoing = g.Where(x => x.Direction == DirectionTypes.Outgoing).Sum(x => x.Amount),
                    TotalIncoming = g.Where(x => x.Direction == DirectionTypes.Incoming).Sum(x => x.Amount)
                }).ToList();

            foreach (var acc in accountUpdates)
            {
                decimal netChange = acc.TotalIncoming - acc.TotalOutgoing;
                if (netChange == 0) continue;

                using var cmdUpdate = new SqlCommand("UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance + @NetChange WHERE Id = @Id", conn, tx);
                cmdUpdate.Parameters.AddWithValue("@NetChange", netChange);
                cmdUpdate.Parameters.AddWithValue("@Id", acc.AccountId);
                await cmdUpdate.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
        Smp.Helpers.AuditHelper.Log("ثبت گروهی", "تراکنش نقدی", $"{transactions.Count} تراکنش");
    }

    public async Task<Dictionary<int, decimal>> GetTotalOutgoingByAccountIdsAsync(List<int> accountIds)
    {
        var result = new Dictionary<int, decimal>();
        if (accountIds is null || accountIds.Count == 0) return result;

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        string idList = string.Join(",", accountIds);
        string sql = $@"SELECT AccountId, SUM(Amount) AS TotalSpent FROM CashTransactions WHERE AccountId IN ({idList}) AND Direction = N'پرداختی' GROUP BY AccountId";

        using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(0)) result[reader.GetInt32(0)] = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
        }
        return result;
    }

    // الگوی LIKE امن: کاراکترهای ویژه با \\ escape می‌شوند
    private static string ToLikePattern(string term)
    {
        string t = term.Trim().Replace("\\", "\\\\").Replace("[", "\\[").Replace("%", "\\%").Replace("_", "\\_");
        return "%" + t + "%";
    }

    private void AddTransactionParameters(SqlCommand cmd, CashTransaction t)
    {
        cmd.Parameters.AddWithValue("@PaymentScope", (object?)t.PaymentScope ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ContractId", (object?)t.ContractId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ContractorContractId", (object?)t.ContractorContractId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ContractTitle", (object?)t.ContractTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ClientStatementId", (object?)t.ClientStatementId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TransactionDate", t.TransactionDate);
        cmd.Parameters.AddWithValue("@Amount", t.Amount);
        cmd.Parameters.AddWithValue("@Discount", t.Discount);
        cmd.Parameters.AddWithValue("@Shipping", t.Shipping);
        cmd.Parameters.AddWithValue("@VAT", t.VAT);
        cmd.Parameters.AddWithValue("@TransactionType", (object?)t.TransactionType ?? "نقدی");
        cmd.Parameters.AddWithValue("@Direction", (object?)t.Direction ?? "پرداختی");
        cmd.Parameters.AddWithValue("@AccountId", (object?)t.AccountId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AccountTitle", (object?)t.AccountTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Category", (object?)t.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CostType", (object?)t.CostType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RecipientName", (object?)t.RecipientName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RecipientSheba", (object?)t.RecipientSheba ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TafsiliCode", (object?)t.TafsiliCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TafsiliTitle", (object?)t.TafsiliTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Description", (object?)t.Description ?? DBNull.Value);
    }

    private CashTransaction MapReader(SqlDataReader reader)
    {
        bool hasTableName = false; try { hasTableName = reader.GetOrdinal("TableName") >= 0; } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CashTransactionRepository"); }
        bool hasRecipientSheba = false; try { hasRecipientSheba = reader.GetOrdinal("RecipientSheba") >= 0; } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "CashTransactionRepository"); }

        return new CashTransaction
        {
            Id = reader.IsDBNull(reader.GetOrdinal("Id")) ? 0 : Convert.ToInt32(reader["Id"]),
            TableName = hasTableName && !reader.IsDBNull(reader.GetOrdinal("TableName")) ? reader["TableName"].ToString()! : "CashTransactions",
            PaymentScope = reader.IsDBNull(reader.GetOrdinal("PaymentScope")) ? null : reader["PaymentScope"].ToString(),
            ContractId = reader.IsDBNull(reader.GetOrdinal("ContractId")) ? (int?)null : Convert.ToInt32(reader["ContractId"]),
            ContractorContractId = reader.IsDBNull(reader.GetOrdinal("ContractorContractId")) ? (int?)null : Convert.ToInt32(reader["ContractorContractId"]),
            ContractTitle = reader.IsDBNull(reader.GetOrdinal("ContractTitle")) ? null : reader["ContractTitle"].ToString(),
            ClientStatementId = reader.IsDBNull(reader.GetOrdinal("ClientStatementId")) ? (int?)null : Convert.ToInt32(reader["ClientStatementId"]),
            TransactionDate = reader.IsDBNull(reader.GetOrdinal("TransactionDate")) ? DateTime.Now : Convert.ToDateTime(reader["TransactionDate"]),
            Amount = reader.IsDBNull(reader.GetOrdinal("Amount")) ? 0m : Convert.ToDecimal(reader["Amount"]),
            Discount = reader.IsDBNull(reader.GetOrdinal("Discount")) ? 0m : Convert.ToDecimal(reader["Discount"]),
            Shipping = reader.IsDBNull(reader.GetOrdinal("Shipping")) ? 0m : Convert.ToDecimal(reader["Shipping"]),
            VAT = reader.IsDBNull(reader.GetOrdinal("VAT")) ? 0m : Convert.ToDecimal(reader["VAT"]),
            TransactionType = reader.IsDBNull(reader.GetOrdinal("TransactionType")) ? string.Empty : reader["TransactionType"].ToString()!,
            Direction = reader.IsDBNull(reader.GetOrdinal("Direction")) ? string.Empty : reader["Direction"].ToString()!,
            AccountId = reader.IsDBNull(reader.GetOrdinal("AccountId")) ? (int?)null : Convert.ToInt32(reader["AccountId"]),
            AccountTitle = reader.IsDBNull(reader.GetOrdinal("AccountTitle")) ? null : reader["AccountTitle"].ToString(),
            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader["Category"].ToString(),
            CostType = reader.IsDBNull(reader.GetOrdinal("CostType")) ? null : reader["CostType"].ToString(),
            RecipientName = reader.IsDBNull(reader.GetOrdinal("RecipientName")) ? null : reader["RecipientName"].ToString(),
            RecipientSheba = hasRecipientSheba && !reader.IsDBNull(reader.GetOrdinal("RecipientSheba")) ? reader["RecipientSheba"].ToString() : null,
            TafsiliCode = reader.IsDBNull(reader.GetOrdinal("TafsiliCode")) ? null : reader["TafsiliCode"].ToString(),
            TafsiliTitle = reader.IsDBNull(reader.GetOrdinal("TafsiliTitle")) ? null : reader["TafsiliTitle"].ToString(),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader["Description"].ToString(),
            CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTime.Now : Convert.ToDateTime(reader["CreatedAt"])
        };
    }
}
