using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace SMP.Data
{
    public class ContractsDashboardRepository
    {
        private readonly string _connectionString;

        public ContractsDashboardRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public DataSet GetDashboardData()
        {
            var ds = new DataSet();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string sqlKpi = @"
                SELECT 
                    (SELECT COUNT(1) FROM ClientContracts) AS ClientContractsCount,
                    (SELECT COUNT(1) FROM ContractorContracts) AS ContractorContractsCount,
                    (SELECT ISNULL(SUM(Amount), 0) FROM ClientContracts) AS TotalClientValue,
                    (SELECT ISNULL(SUM(Amount), 0) FROM ContractorContracts) AS TotalContractorValue,
                    
                    (SELECT ISNULL(SUM(NetReceivableAmount), 0) FROM ClientStatements) - 
                    ( (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE Direction = N'دریافتی') + 
                      (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE CheckType = N'دریافتی' AND Status NOT IN (N'باطل شده', N'برگشتی')) ) AS TotalReceivables,

                    (SELECT ISNULL(SUM(NetPayableAmount), 0) FROM ContractorStatements) - 
                    ( (SELECT ISNULL(SUM(Amount), 0) FROM ContractorPayments) + 
                      (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE ContractorContractId IS NOT NULL AND Direction = N'پرداختی') +
                      (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE ContractorContractId IS NOT NULL AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی')) ) AS TotalPayables,

                    (SELECT COUNT(1) FROM ClientContracts WHERE Status = N'فعال' AND EndDate IS NOT NULL AND EndDate <= DATEADD(day, 30, GETDATE()) AND EndDate >= GETDATE()) +
                    (SELECT COUNT(1) FROM ContractorContracts WHERE Status = N'فعال' AND EndDate IS NOT NULL AND EndDate <= DATEADD(day, 30, GETDATE()) AND EndDate >= GETDATE()) AS ExpiringContractsCount";

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
                  AND c.EndDate <= DATEADD(day, 30, GETDATE()) AND c.EndDate >= GETDATE()
                ORDER BY c.EndDate ASC";
            using (var cmd = new SqlCommand(sqlExpiring, conn))
            using (var adapter = new SqlDataAdapter(cmd))
                adapter.Fill(ds, "ExpiringContracts");

            string sqlAtRisk = @"
                SELECT c.Id, c.ContractNumber, 
                       CASE WHEN p.PartyType = N'حقیقی' THEN p.FirstName + N' ' + p.LastName ELSE p.CompanyName END AS PartyName,
                       c.Amount AS ClientAmount,
                       (
                           ISNULL((SELECT SUM(Amount) FROM ContractorContracts WHERE ClientContractId = c.Id), 0) + 
                           ISNULL((SELECT SUM(Amount) FROM ProjectExpenses WHERE ClientContractId = c.Id), 0) +
                           ISNULL((SELECT SUM(Amount) FROM CashTransactions WHERE ContractId = c.Id AND Direction = N'پرداختی' AND PaymentScope IN (N'DirectPurchase', N'InformalContractor')), 0) +
                           ISNULL((SELECT SUM(Amount) FROM CashChecks WHERE ContractId = c.Id AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND PaymentScope IN (N'DirectPurchase', N'InformalContractor')), 0)
                       ) AS TotalCosts
                FROM ClientContracts c
                INNER JOIN Parties p ON c.PartyId = p.Id
                WHERE c.Amount < (
                           ISNULL((SELECT SUM(Amount) FROM ContractorContracts WHERE ClientContractId = c.Id), 0) + 
                           ISNULL((SELECT SUM(Amount) FROM ProjectExpenses WHERE ClientContractId = c.Id), 0) +
                           ISNULL((SELECT SUM(Amount) FROM CashTransactions WHERE ContractId = c.Id AND Direction = N'پرداختی' AND PaymentScope IN (N'DirectPurchase', N'InformalContractor')), 0) +
                           ISNULL((SELECT SUM(Amount) FROM CashChecks WHERE ContractId = c.Id AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND PaymentScope IN (N'DirectPurchase', N'InformalContractor')), 0)
                       )
                ORDER BY c.Amount ASC";
            using (var cmd = new SqlCommand(sqlAtRisk, conn))
            using (var adapter = new SqlDataAdapter(cmd))
                adapter.Fill(ds, "AtRiskContracts");

            return ds;
        }
    }
}