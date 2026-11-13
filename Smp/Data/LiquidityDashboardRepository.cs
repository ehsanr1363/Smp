using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SMP.Data
{
    public class LiquidityDashboardRepository
    {
        private readonly string _connectionString;

        public LiquidityDashboardRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<DataSet> GetDashboardDataAsync(DateTime fromDate, DateTime toDate)
        {
            if (fromDate < new DateTime(1753, 1, 1)) fromDate = new DateTime(1753, 1, 1);
            if (toDate < fromDate) toDate = fromDate;

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                -- 1. خلاصه KPI ها
                SELECT 
                    (SELECT ISNULL(SUM(CurrentBalance), 0) FROM CompanyBankAccounts WHERE IsActive = 1) AS TotalLiquidity,

                    (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE Direction = N'دریافتی' AND TransactionDate >= @From AND TransactionDate <= @To) +
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE CheckType = N'دریافتی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND DueDate >= @From AND DueDate <= @To) AS TotalIncome,
                    
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE Direction = N'پرداختی' AND TransactionDate >= @From AND TransactionDate <= @To) +
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND DueDate >= @From AND DueDate <= @To) AS TotalExpense,

                    (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE Direction = N'پرداختی' AND PaymentScope = N'Administrative' AND TransactionDate >= @From AND TransactionDate <= @To) +
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE CheckType = N'پرداختی' AND PaymentScope = N'Administrative' AND Status NOT IN (N'باطل شده', N'برگشتی') AND DueDate >= @From AND DueDate <= @To) AS AdminExp,

                    (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE Direction = N'پرداختی' AND PaymentScope = N'DirectPurchase' AND TransactionDate >= @From AND TransactionDate <= @To) +
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE CheckType = N'پرداختی' AND PaymentScope = N'DirectPurchase' AND Status NOT IN (N'باطل شده', N'برگشتی') AND DueDate >= @From AND DueDate <= @To) AS DirectExp,

                    (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE Direction = N'پرداختی' AND PaymentScope = N'InformalContractor' AND TransactionDate >= @From AND TransactionDate <= @To) +
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE CheckType = N'پرداختی' AND PaymentScope = N'InformalContractor' AND Status NOT IN (N'باطل شده', N'برگشتی') AND DueDate >= @From AND DueDate <= @To) AS InformalExp,

                    (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE Direction = N'پرداختی' AND PaymentScope = N'ContractorContract' AND TransactionDate >= @From AND TransactionDate <= @To) +
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE CheckType = N'پرداختی' AND PaymentScope = N'ContractorContract' AND Status NOT IN (N'باطل شده', N'برگشتی') AND DueDate >= @From AND DueDate <= @To) AS FormalExp;

                -- 2. داده‌های نمودار
                WITH ChartData AS (
                    SELECT CAST(TransactionDate AS DATE) AS TxDate, 
                           SUM(CASE WHEN Direction = N'دریافتی' THEN Amount ELSE 0 END) AS Income,
                           SUM(CASE WHEN Direction = N'پرداختی' THEN Amount ELSE 0 END) AS Expense
                    FROM CashTransactions
                    WHERE TransactionDate >= @From AND TransactionDate <= @To
                    GROUP BY CAST(TransactionDate AS DATE)
                    
                    UNION ALL
                    
                    SELECT CAST(DueDate AS DATE) AS TxDate, 
                           SUM(CASE WHEN CheckType = N'دریافتی' THEN Amount ELSE 0 END) AS Income,
                           SUM(CASE WHEN CheckType = N'پرداختی' THEN Amount ELSE 0 END) AS Expense
                    FROM CashChecks
                    WHERE DueDate >= @From AND DueDate <= @To AND Status NOT IN (N'باطل شده', N'برگشتی')
                    GROUP BY CAST(DueDate AS DATE)
                )
                SELECT TxDate, SUM(Income) AS TotalIncome, SUM(Expense) AS TotalExpense
                FROM ChartData
                GROUP BY TxDate
                ORDER BY TxDate ASC;

                -- 3. موجودی تفکیکی حساب‌های بانکی
                SELECT Id, Title, ISNULL(BankCode, '') AS BankCode, ISNULL(AccountNumber, '') AS AccountNumber, CurrentBalance
                FROM CompanyBankAccounts
                WHERE IsActive = 1 AND IsCashDesk = 0;

                -- 4. چک‌های پاس‌نشده (✅ اصلاح شد: استفاده از ISNULL برای جلوگیری از خطای عدم وجود ستون)
                SELECT Id, CheckNumber, DueDate, Amount, CheckType, Status, 
                       ISNULL(RecipientName, '') AS RecipientName, 
                       ISNULL(BankName, '') AS BankName, 
                       ISNULL(Description, '') AS Description
                FROM CashChecks
                WHERE Status != N'پاس شده' AND Status != N'باطل شده';

                -- 5. تنخواه‌های فعال
                SELECT Id, Title, CurrentBalance, 
                       ISNULL((SELECT MAX(TransactionDate) FROM CashTransactions WHERE AccountId = CompanyBankAccounts.Id), DATEADD(year, -10, GETDATE())) AS LastActivityDate
                FROM CompanyBankAccounts
                WHERE IsCashDesk = 1 AND IsActive = 1 AND CurrentBalance > 0;
            ";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@From", fromDate);
            cmd.Parameters.AddWithValue("@To", toDate);

            using var adapter = new SqlDataAdapter(cmd);
            var ds = new DataSet();
            await Task.Run(() => adapter.Fill(ds));
            return ds;
        }
    }
}