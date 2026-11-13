using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SMP.Data
{
    public class ComprehensiveLedgerRepository
    {
        private readonly string _connectionString;

        public ComprehensiveLedgerRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<string>> GetDistinctContractsAsync()
        {
            var list = new List<string>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT DISTINCT ContractTitle FROM CashTransactions WHERE ContractTitle IS NOT NULL AND ContractTitle <> ''
                UNION
                SELECT DISTINCT ContractTitle FROM CashChecks WHERE ContractTitle IS NOT NULL AND ContractTitle <> ''
                ORDER BY ContractTitle";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(reader.GetString(0));
            }
            return list;
        }

        public async Task<DataTable> GetFilteredLedgerAsync(string search, string contractFilter, string scopeFilter, string partyFilter)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string whereClause = " WHERE 1=1 ";
            if (!string.IsNullOrWhiteSpace(search))
                whereClause += " AND (PartyName LIKE @Search OR Contract LIKE @Search OR Reference LIKE @Search OR Description LIKE @Search OR AccountTitle LIKE @Search) ";
            if (!string.IsNullOrWhiteSpace(contractFilter) && contractFilter != "همه قراردادها")
                whereClause += " AND Contract = @Contract ";
            if (!string.IsNullOrWhiteSpace(scopeFilter) && scopeFilter != "همه")
                whereClause += " AND (CASE WHEN PaymentScope = N'Administrative' THEN N'اداری' WHEN PaymentScope = N'DirectPurchase' THEN N'خرید مستقیم پروژه' WHEN PaymentScope = N'InformalContractor' THEN N'پیمانکار بدون قرارداد' WHEN PaymentScope = N'ContractorContract' THEN N'پیمانکار دارای قرارداد' ELSE N'---' END) = @Scope ";
            if (!string.IsNullOrWhiteSpace(partyFilter))
                whereClause += " AND PartyName LIKE @Party ";

            // ✅ محاسبه دقیق مانده به صورت تفکیک‌شده برای هر حساب بانکی (حذف تراکنش‌های دستی انتقال برای جلوگیری از دوباره‌شماری)
            string sql = $@"
                WITH CombinedLedger AS (
                    SELECT 
                        t.Id,
                        t.AccountId,
                        ISNULL(t.AccountTitle, N'---') AS AccountTitle,
                        t.TransactionDate AS [Date],
                        t.TransactionType + N' ' + t.[Direction] AS [Type],
                        N'---' AS [Reference],
                        ISNULL(t.TafsiliTitle, ISNULL(t.RecipientName, N'---')) AS [PartyName],
                        ISNULL(t.ContractTitle, N'---') AS [Contract],
                        t.PaymentScope,
                        ISNULL(t.CostType, N'---') AS [CostType],
                        t.Amount,
                        CASE WHEN t.[Direction] = N'دریافتی' THEN t.Amount ELSE 0 END AS [Debit],
                        CASE WHEN t.[Direction] = N'پرداختی' THEN t.Amount ELSE 0 END AS [Credit],
                        N'ثبت شده' AS [Status],
                        ISNULL(t.Description, N'---') AS [Description]
                    FROM CashTransactions t
                    WHERE ISNULL(t.Category, '') <> N'انتقال بین حساب‌ها' -- ✅ حذف تراکنش‌های دستی انتقال
                    
                    UNION ALL
                    
                    SELECT 
                        -c.Id,
                        c.AccountId,
                        ISNULL((SELECT Title FROM CompanyBankAccounts WHERE Id = c.AccountId), N'---') AS AccountTitle,
                        c.DueDate,
                        N'چک ' + c.CheckType,
                        c.CheckNumber,
                        ISNULL(c.RecipientName, N'---'),
                        ISNULL(c.ContractTitle, N'---'),
                        c.PaymentScope,
                        ISNULL(c.CostType, N'---'),
                        c.Amount,
                        CASE WHEN c.CheckType = N'دریافتی' THEN c.Amount ELSE 0 END AS [Debit],
                        CASE WHEN c.CheckType = N'پرداختی' THEN c.Amount ELSE 0 END AS [Credit],
                        c.Status,
                        ISNULL(c.Description, N'---')
                    FROM CashChecks c
                    WHERE c.Status NOT IN (N'باطل شده', N'برگشتی')

                    UNION ALL

                    SELECT 
                        -at.Id,
                        at.SourceAccountId,
                        ISNULL((SELECT Title FROM CompanyBankAccounts WHERE Id = at.SourceAccountId), N'---') AS AccountTitle,
                        at.TransferDate,
                        N'انتقال وجه (خروج)',
                        N'---',
                        N'---',
                        N'---',
                        N'---',
                        N'---',
                        at.Amount,
                        0 AS [Debit],
                        at.Amount AS [Credit],
                        N'ثبت شده',
                        N'انتقال به حساب مقصد. شرح: ' + ISNULL(at.Description, N'---')
                    FROM AccountTransfers at

                    UNION ALL

                    SELECT 
                        -at.Id,
                        at.DestinationAccountId,
                        ISNULL((SELECT Title FROM CompanyBankAccounts WHERE Id = at.DestinationAccountId), N'---') AS AccountTitle,
                        at.TransferDate,
                        N'انتقال وجه (ورود)',
                        N'---',
                        N'---',
                        N'---',
                        N'---',
                        N'---',
                        at.Amount,
                        at.Amount AS [Debit],
                        0 AS [Credit],
                        N'ثبت شده',
                        N'انتقال از حساب مبدا. شرح: ' + ISNULL(at.Description, N'---')
                    FROM AccountTransfers at
                ),
                OrderedLedger AS (
                    SELECT *,
                           ROW_NUMBER() OVER (PARTITION BY AccountId ORDER BY [Date] ASC, Id ASC, [Type] ASC) AS Seq
                    FROM CombinedLedger
                ),
                CalculatedLedger AS (
                    SELECT *,
                           SUM(Debit - Credit) OVER (PARTITION BY AccountId ORDER BY Seq ASC) AS RunningTotal
                    FROM OrderedLedger
                )
                SELECT 
                    *,
                    (SELECT ISNULL(InitialBalance, 0) FROM CompanyBankAccounts WHERE Id = CalculatedLedger.AccountId) + RunningTotal AS [Balance]
                FROM CalculatedLedger
                {whereClause}
                ORDER BY [Date] ASC, Id ASC, [Type] ASC";

            using var cmd = new SqlCommand(sql, conn);
            if (!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("@Search", $"%{search.Trim()}%");
            if (!string.IsNullOrWhiteSpace(contractFilter) && contractFilter != "همه قراردادها")
                cmd.Parameters.AddWithValue("@Contract", contractFilter);
            if (!string.IsNullOrWhiteSpace(scopeFilter) && scopeFilter != "همه")
                cmd.Parameters.AddWithValue("@Scope", scopeFilter);
            if (!string.IsNullOrWhiteSpace(partyFilter))
                cmd.Parameters.AddWithValue("@Party", $"%{partyFilter.Trim()}%");

            using var adapter = new SqlDataAdapter(cmd);
            var dt = new DataTable();
            await Task.Run(() => adapter.Fill(dt));
            return dt;
        }
    }
}