using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Smp.Data;

public class LiquidityDashboardRepository
{
    private readonly string _connectionString;

    // ✅ KPI ها فقط نقدینگیِ واقعی را می‌شمارند: چک‌ها فقط وقتی «پاس شده» باشند. چک‌های در جریان در ویجت تعهدات می‌آیند.
    // ✅ اصلاح ۱: شارژ تنخواه (انتقال بین حساب‌ها) نه درآمد است و نه هزینه.
    // خرج‌های واقعی تنخواه جداگانه ثبت می‌شوند؛ پس این ردیف‌ها باید کنار گذاشته شوند.
    private const string NoTransfer = "ISNULL(Category, N'') <> N'انتقال بین حساب‌ها'";

    public LiquidityDashboardRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<DataSet> GetDashboardDataAsync(DateTime fromDate, DateTime toDate)
    {
        if (fromDate < new DateTime(1753, 1, 1)) fromDate = new DateTime(1753, 1, 1);

        // ✅ اصلاح ۵: اگر تاریخ پایان ساعت ندارد (۰۰:۰۰)، تا آخر همان روز حساب شود.
        // (پرداخت حقوق با DateTime.Now ثبت می‌شود و در «بازه دلخواه» از قلم می‌افتاد.)
        if (toDate.TimeOfDay == TimeSpan.Zero && toDate.Date < DateTime.MaxValue.Date)
            toDate = toDate.Date.AddDays(1).AddSeconds(-1);

        if (toDate < fromDate) toDate = fromDate;

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = $@"
                -- 1. خلاصه KPI ها
                SELECT 
                    (SELECT ISNULL(SUM(CurrentBalance), 0) FROM CompanyBankAccounts WHERE IsActive = 1) AS TotalLiquidity,

                    (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE Direction = N'دریافتی' AND {NoTransfer} AND TransactionDate >= @From AND TransactionDate <= @To) +
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE CheckType = N'دریافتی' AND Status = N'پاس شده' AND DueDate >= @From AND DueDate <= @To) AS TotalIncome,
                    
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE Direction = N'پرداختی' AND {NoTransfer} AND TransactionDate >= @From AND TransactionDate <= @To) +
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE CheckType = N'پرداختی' AND Status = N'پاس شده' AND DueDate >= @From AND DueDate <= @To) AS TotalExpense,

                    (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE Direction = N'پرداختی' AND {NoTransfer} AND PaymentScope = N'Administrative' AND TransactionDate >= @From AND TransactionDate <= @To) +
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE CheckType = N'پرداختی' AND PaymentScope = N'Administrative' AND Status = N'پاس شده' AND DueDate >= @From AND DueDate <= @To) AS AdminExp,

                    -- ✅ اصلاح ۲: کرایه حمل و هزینه‌های پروژه هم در این کارت حساب می‌شوند تا جمع کارت‌ها با جمع کل بخواند
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE Direction = N'پرداختی' AND {NoTransfer} AND PaymentScope IN (N'DirectPurchase', N'ProjectShipping', N'ProjectExpense') AND TransactionDate >= @From AND TransactionDate <= @To) +
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE CheckType = N'پرداختی' AND PaymentScope IN (N'DirectPurchase', N'ProjectShipping', N'ProjectExpense') AND Status = N'پاس شده' AND DueDate >= @From AND DueDate <= @To) AS DirectExp,

                    (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE Direction = N'پرداختی' AND {NoTransfer} AND PaymentScope = N'InformalContractor' AND TransactionDate >= @From AND TransactionDate <= @To) +
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE CheckType = N'پرداختی' AND PaymentScope = N'InformalContractor' AND Status = N'پاس شده' AND DueDate >= @From AND DueDate <= @To) AS InformalExp,

                    (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE Direction = N'پرداختی' AND {NoTransfer} AND PaymentScope = N'ContractorContract' AND TransactionDate >= @From AND TransactionDate <= @To) +
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE CheckType = N'پرداختی' AND PaymentScope = N'ContractorContract' AND Status = N'پاس شده' AND DueDate >= @From AND DueDate <= @To) AS FormalExp;

                -- 2. داده‌های نمودار
                WITH ChartData AS (
                    SELECT CAST(TransactionDate AS DATE) AS TxDate, 
                           SUM(CASE WHEN Direction = N'دریافتی' THEN Amount ELSE 0 END) AS Income,
                           SUM(CASE WHEN Direction = N'پرداختی' THEN Amount ELSE 0 END) AS Expense
                    FROM CashTransactions
                    WHERE TransactionDate >= @From AND TransactionDate <= @To AND {NoTransfer}
                    GROUP BY CAST(TransactionDate AS DATE)
                    
                    UNION ALL
                    
                    SELECT CAST(DueDate AS DATE) AS TxDate, 
                           SUM(CASE WHEN CheckType = N'دریافتی' THEN Amount ELSE 0 END) AS Income,
                           SUM(CASE WHEN CheckType = N'پرداختی' THEN Amount ELSE 0 END) AS Expense
                    FROM CashChecks
                    WHERE DueDate >= @From AND DueDate <= @To AND Status = N'پاس شده'
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

                -- 4. چک‌های پرداختی پاس‌نشده (تعهد واقعی)
                SELECT c.Id, c.CheckNumber, c.DueDate, c.Amount, c.CheckType, c.Status, 
                       ISNULL(c.RecipientName, '') AS RecipientName, 
                       ISNULL(c.BankName, '') AS BankName, 
                       ISNULL(a.BankCode, '') AS AccountBankCode, 
                       ISNULL(c.Description, '') AS Description
                FROM CashChecks c
                LEFT JOIN CompanyBankAccounts a ON a.Id = c.AccountId
                WHERE c.CheckType = N'پرداختی' AND c.Status IN (N'در جریان', N'برگشتی');

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
