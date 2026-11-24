using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Smp.Data;

public class UnbilledExpenseItem
{
    public string SourceType { get; set; } = ""; // Tx یا Exp یا Chk
    public int Id { get; set; }
    public int ContractId { get; set; }
    public string ContractTitle { get; set; } = "";
    public DateTime Date { get; set; }
    public string Type { get; set; } = "";
    public string CostType { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
}

public class UnbilledExpensesRepository
{
    private readonly string _connectionString;

    public UnbilledExpensesRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public List<UnbilledExpenseItem> GetUnbilledExpenses()
    {
        var list = new List<UnbilledExpenseItem>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        // دریافت هزینه‌هایی که ClientStatementId آن‌ها NULL است (یعنی هنوز صورت وضعیت نشده‌اند)
        string query = @"
                -- ۱. تراکنش‌های نقدی/حواله
                SELECT 
                    'Tx' AS SourceType,
                    t.Id,
                    t.ContractId,
                    ISNULL(c.ContractNumber, '') + ' - ' + ISNULL(c.Subject, '') AS ContractTitle,
                    t.TransactionDate AS [Date],
                    CASE 
                        WHEN t.PaymentScope = N'DirectPurchase' THEN N'خرید مستقیم'
                        WHEN t.PaymentScope = N'InformalContractor' THEN N'پیمانکار (بدون قرارداد)'
                        WHEN t.PaymentScope = N'ContractorContract' THEN N'پیمانکار (رسمی)'
                        WHEN t.PaymentScope = N'ProjectShipping' THEN N'کرایه حمل'
                        WHEN t.PaymentScope = N'ProjectExpense' THEN N'هزینه پروژه'
                        ELSE N'سایر'
                    END AS [Type],
                    ISNULL(t.CostType, N'---') AS CostType,
                    ISNULL(t.Description, ISNULL(t.RecipientName, N'---')) AS [Description],
                    t.Amount
                FROM CashTransactions t
                INNER JOIN ClientContracts c ON t.ContractId = c.Id
                WHERE t.Direction = N'پرداختی' 
                  AND t.ClientStatementId IS NULL 
                  AND t.ContractId IS NOT NULL

                UNION ALL

                -- ۲. فاکتورهای پروژه
                SELECT 
                    'Exp' AS SourceType,
                    e.Id,
                    e.ClientContractId AS ContractId,
                    ISNULL(c.ContractNumber, '') + ' - ' + ISNULL(c.Subject, '') AS ContractTitle,
                    e.InvoiceDate AS [Date],
                    N'فاکتور هزینه' AS [Type],
                    ISNULL(e.CostType, N'---') AS CostType,
                    ISNULL(e.Description, N'---') AS [Description],
                    e.Amount
                FROM ProjectExpenses e
                INNER JOIN ClientContracts c ON e.ClientContractId = c.Id
                WHERE e.ClientStatementId IS NULL

                UNION ALL

                -- ۳. ✅ اصلاح ۶: چک‌های پرداختی (چک‌ها هم به صورت‌وضعیت وصل می‌شوند و قبلاً در این گزارش نبودند)
                SELECT 
                    'Chk' AS SourceType,
                    k.Id,
                    k.ContractId,
                    ISNULL(c.ContractNumber, '') + ' - ' + ISNULL(c.Subject, '') AS ContractTitle,
                    k.DueDate AS [Date],
                    N'چک - ' + CASE 
                        WHEN k.PaymentScope = N'DirectPurchase' THEN N'خرید مستقیم'
                        WHEN k.PaymentScope = N'InformalContractor' THEN N'پیمانکار (بدون قرارداد)'
                        WHEN k.PaymentScope = N'ContractorContract' THEN N'پیمانکار (رسمی)'
                        WHEN k.PaymentScope = N'ProjectShipping' THEN N'کرایه حمل'
                        WHEN k.PaymentScope = N'ProjectExpense' THEN N'هزینه پروژه'
                        ELSE N'سایر'
                    END AS [Type],
                    ISNULL(k.CostType, N'---') AS CostType,
                    ISNULL(k.Description, ISNULL(k.RecipientName, N'---')) AS [Description],
                    k.Amount
                FROM CashChecks k
                INNER JOIN ClientContracts c ON k.ContractId = c.Id
                WHERE k.CheckType = N'پرداختی'
                  AND k.Status NOT IN (N'باطل شده', N'برگشتی')
                  AND k.ClientStatementId IS NULL
                  AND k.ContractId IS NOT NULL

                ORDER BY [Date] DESC";

        using var cmd = new SqlCommand(query, conn);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            list.Add(new UnbilledExpenseItem
            {
                SourceType = reader.GetString(0),
                Id = reader.GetInt32(1),
                ContractId = reader.GetInt32(2),
                ContractTitle = reader.GetString(3),
                Date = Convert.ToDateTime(reader[4]),
                Type = reader.GetString(5),
                CostType = reader.GetString(6),
                Description = reader.GetString(7),
                Amount = Convert.ToDecimal(reader[8])
            });
        }

        return list;
    }
}
