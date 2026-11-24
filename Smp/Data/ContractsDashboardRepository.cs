using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Smp.Data;

public class ContractsDashboardRepository
{
    private readonly string _connectionString;

    // ✅ اصلاح ۳: دریافتی‌هایی که «دریافت از کارفرما» نیستند (همان فیلتر لیست دریافتی‌ها)
    private const string NonClientReceiptCategories = "N'انتقال بین حساب‌ها', N'سود بانکی', N'سود', N'شارژ تنخواه'";

    // ✅ اصلاح ۲: کرایه حمل و هزینه‌های پروژه هم جزو هزینه‌های پروژه‌اند
    private const string ProjectCostScopes = "N'DirectPurchase', N'InformalContractor', N'ProjectShipping', N'ProjectExpense'";

    public ContractsDashboardRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public DataSet GetDashboardData()
    {
        var ds = new DataSet();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        // مطالبات و بدهی‌ها «به‌ازای هر قرارداد» محاسبه می‌شوند و فقط مانده‌های مثبت جمع می‌شوند.
        // (قبلاً کل صورت‌وضعیت‌ها منهای کل دریافتی/پرداختی‌ها حساب می‌شد؛ پیش‌دریافت یک قرارداد
        //  از مطالبات قرارداد دیگر کم می‌شد و سود بانکی هم جزو دریافتی کارفرما حساب می‌شد.)
        string sqlKpi = $@"
                SELECT 
                    (SELECT COUNT(1) FROM ClientContracts) AS ClientContractsCount,
                    (SELECT COUNT(1) FROM ContractorContracts) AS ContractorContractsCount,
                    (SELECT ISNULL(SUM(Amount), 0) FROM ClientContracts) AS TotalClientValue,
                    (SELECT ISNULL(SUM(Amount), 0) FROM ContractorContracts) AS TotalContractorValue,
                    
                    (SELECT ISNULL(SUM(CASE WHEN x.Billed - x.Received > 0 THEN x.Billed - x.Received ELSE 0 END), 0)
                     FROM (
                         SELECT c.Id,
                                ISNULL((SELECT SUM(NetReceivableAmount) FROM ClientStatements WHERE ContractId = c.Id), 0) AS Billed,
                                ISNULL((SELECT SUM(Amount) FROM CashTransactions 
                                        WHERE ContractId = c.Id 
                                          AND LTRIM(RTRIM(Direction)) = N'دریافتی'
                                          AND LTRIM(RTRIM(ISNULL(Category, N''))) NOT IN ({NonClientReceiptCategories})), 0) +
                                ISNULL((SELECT SUM(Amount) FROM CashChecks 
                                        WHERE ContractId = c.Id AND CheckType = N'دریافتی' 
                                          AND Status NOT IN (N'باطل شده', N'برگشتی')), 0) AS Received
                         FROM ClientContracts c
                     ) x) AS TotalReceivables,

                    (SELECT ISNULL(SUM(CASE WHEN y.Billed - y.Paid > 0 THEN y.Billed - y.Paid ELSE 0 END), 0)
                     FROM (
                         SELECT k.Id,
                                ISNULL((SELECT SUM(NetPayableAmount) FROM ContractorStatements WHERE ContractId = k.Id), 0) AS Billed,
                                ISNULL((SELECT SUM(Amount) FROM ContractorPayments WHERE ContractId = k.Id), 0) +
                                ISNULL((SELECT SUM(Amount) FROM CashTransactions 
                                        WHERE ContractorContractId = k.Id AND LTRIM(RTRIM(Direction)) = N'پرداختی'), 0) +
                                ISNULL((SELECT SUM(Amount) FROM CashChecks 
                                        WHERE ContractorContractId = k.Id AND CheckType = N'پرداختی' 
                                          AND Status NOT IN (N'باطل شده', N'برگشتی')), 0) AS Paid
                         FROM ContractorContracts k
                     ) y) AS TotalPayables,

                    -- ✅ اصلاح: قراردادی که امروز تمام می‌شود هم جزو «در حال انقضا» است (مقایسه فقط روی تاریخ)
                    (SELECT COUNT(1) FROM ClientContracts WHERE Status = N'فعال' AND EndDate IS NOT NULL AND EndDate <= DATEADD(day, 30, CAST(GETDATE() AS DATE)) AND EndDate >= CAST(GETDATE() AS DATE)) +
                    (SELECT COUNT(1) FROM ContractorContracts WHERE Status = N'فعال' AND EndDate IS NOT NULL AND EndDate <= DATEADD(day, 30, CAST(GETDATE() AS DATE)) AND EndDate >= CAST(GETDATE() AS DATE)) AS ExpiringContractsCount";

        using (var cmd = new SqlCommand(sqlKpi, conn))
        using (var adapter = new SqlDataAdapter(cmd))
            adapter.Fill(ds, "KPI");

        string sqlStatus = @"
                SELECT Status, COUNT(1) AS RecordCount 
                FROM ClientContracts 
                GROUP BY Status";
        using (var cmd = new SqlCommand(sqlStatus, conn))
        using (var adapter = new SqlDataAdapter(cmd))
            adapter.Fill(ds, "StatusDistribution");

        string sqlExpiring = @"
                SELECT c.Id, c.ContractNumber, 
                       CASE WHEN p.PartyType = N'حقیقی' THEN p.FirstName + N' ' + p.LastName ELSE p.CompanyName END AS PartyName,
                       c.EndDate, c.Status, c.Amount
                FROM ClientContracts c
                INNER JOIN Parties p ON c.PartyId = p.Id
                WHERE c.Status = N'فعال' AND c.EndDate IS NOT NULL 
                  AND c.EndDate <= DATEADD(day, 30, CAST(GETDATE() AS DATE)) AND c.EndDate >= CAST(GETDATE() AS DATE)
                ORDER BY c.EndDate ASC";
        using (var cmd = new SqlCommand(sqlExpiring, conn))
        using (var adapter = new SqlDataAdapter(cmd))
            adapter.Fill(ds, "ExpiringContracts");

        string sqlAtRisk = $@"
                SELECT c.Id, c.ContractNumber, 
                       CASE WHEN p.PartyType = N'حقیقی' THEN p.FirstName + N' ' + p.LastName ELSE p.CompanyName END AS PartyName,
                       c.Amount AS ClientAmount,
                       (
                           ISNULL((SELECT SUM(Amount) FROM ContractorContracts WHERE ClientContractId = c.Id), 0) + 
                           ISNULL((SELECT SUM(Amount) FROM ProjectExpenses WHERE ClientContractId = c.Id), 0) +
                           ISNULL((SELECT SUM(Amount) FROM CashTransactions WHERE ContractId = c.Id AND LTRIM(RTRIM(Direction)) = N'پرداختی' AND PaymentScope IN ({ProjectCostScopes})), 0) +
                           ISNULL((SELECT SUM(Amount) FROM CashChecks WHERE ContractId = c.Id AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND PaymentScope IN ({ProjectCostScopes})), 0)
                       ) AS TotalCosts
                FROM ClientContracts c
                INNER JOIN Parties p ON c.PartyId = p.Id
                WHERE c.Amount < (
                           ISNULL((SELECT SUM(Amount) FROM ContractorContracts WHERE ClientContractId = c.Id), 0) + 
                           ISNULL((SELECT SUM(Amount) FROM ProjectExpenses WHERE ClientContractId = c.Id), 0) +
                           ISNULL((SELECT SUM(Amount) FROM CashTransactions WHERE ContractId = c.Id AND LTRIM(RTRIM(Direction)) = N'پرداختی' AND PaymentScope IN ({ProjectCostScopes})), 0) +
                           ISNULL((SELECT SUM(Amount) FROM CashChecks WHERE ContractId = c.Id AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND PaymentScope IN ({ProjectCostScopes})), 0)
                       )
                ORDER BY c.Amount ASC";
        using (var cmd = new SqlCommand(sqlAtRisk, conn))
        using (var adapter = new SqlDataAdapter(cmd))
            adapter.Fill(ds, "AtRiskContracts");

        return ds;
    }
}
