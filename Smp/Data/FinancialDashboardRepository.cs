using System;
using Microsoft.Data.SqlClient;
using SMP.Models;

namespace SMP.Data
{
    public class FinancialDashboardRepository
    {
        private readonly string _connectionString;

        public FinancialDashboardRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public FinancialSummary GetSummary(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var summary = new FinancialSummary();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string sqlClientContracts = @"
                SELECT ISNULL(SUM(Amount), 0) 
                FROM ClientContracts 
                WHERE (@From IS NULL OR ContractDate >= @From)
                  AND (@To IS NULL OR ContractDate <= @To)";
            using (var cmd = new SqlCommand(sqlClientContracts, conn))
            {
                cmd.Parameters.AddWithValue("@From", (object?)fromDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@To", (object?)toDate ?? DBNull.Value);
                summary.TotalClientContractAmount = Convert.ToDecimal(cmd.ExecuteScalar());
            }

            string sqlClientStatements = @"
                SELECT ISNULL(SUM(NetReceivableAmount), 0) 
                FROM ClientStatements 
                WHERE (@From IS NULL OR StatementDate >= @From) 
                  AND (@To IS NULL OR StatementDate <= @To)";
            using (var cmd = new SqlCommand(sqlClientStatements, conn))
            {
                cmd.Parameters.AddWithValue("@From", (object?)fromDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@To", (object?)toDate ?? DBNull.Value);
                summary.TotalClientInvoiced = Convert.ToDecimal(cmd.ExecuteScalar());
            }

            string sqlClientPayments = @"
                SELECT 
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions 
                     WHERE Direction = N'دریافتی' 
                       AND (@From IS NULL OR TransactionDate >= @From) 
                       AND (@To IS NULL OR TransactionDate <= @To))
                    +
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks 
                     WHERE CheckType = N'دریافتی' AND Status NOT IN (N'باطل شده', N'برگشتی')
                       AND (@From IS NULL OR DueDate >= @From) 
                       AND (@To IS NULL OR DueDate <= @To))";
            using (var cmd = new SqlCommand(sqlClientPayments, conn))
            {
                cmd.Parameters.AddWithValue("@From", (object?)fromDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@To", (object?)toDate ?? DBNull.Value);
                summary.TotalClientReceived = Convert.ToDecimal(cmd.ExecuteScalar());
            }

            string sqlContractorContracts = @"
                SELECT ISNULL(SUM(Amount), 0) 
                FROM ContractorContracts 
                WHERE (@From IS NULL OR ContractDate >= @From)
                  AND (@To IS NULL OR ContractDate <= @To)";
            using (var cmd = new SqlCommand(sqlContractorContracts, conn))
            {
                cmd.Parameters.AddWithValue("@From", (object?)fromDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@To", (object?)toDate ?? DBNull.Value);
                summary.TotalContractorContractAmount = Convert.ToDecimal(cmd.ExecuteScalar());
            }

            string sqlContractorStatements = @"
                SELECT ISNULL(SUM(NetPayableAmount), 0) 
                FROM ContractorStatements 
                WHERE (@From IS NULL OR StatementDate >= @From) 
                  AND (@To IS NULL OR StatementDate <= @To)";
            using (var cmd = new SqlCommand(sqlContractorStatements, conn))
            {
                cmd.Parameters.AddWithValue("@From", (object?)fromDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@To", (object?)toDate ?? DBNull.Value);
                summary.TotalContractorInvoiced = Convert.ToDecimal(cmd.ExecuteScalar());
            }

            string sqlContractorPayments = @"
                SELECT 
                    (SELECT ISNULL(SUM(Amount), 0) FROM ContractorPayments WHERE (@From IS NULL OR PaymentDate >= @From) AND (@To IS NULL OR PaymentDate <= @To))
                    +
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE ContractorContractId IS NOT NULL AND Direction = N'پرداختی' AND (@From IS NULL OR TransactionDate >= @From) AND (@To IS NULL OR TransactionDate <= @To))
                    +
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE ContractorContractId IS NOT NULL AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND (@From IS NULL OR DueDate >= @From) AND (@To IS NULL OR DueDate <= @To))";
            using (var cmd = new SqlCommand(sqlContractorPayments, conn))
            {
                cmd.Parameters.AddWithValue("@From", (object?)fromDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@To", (object?)toDate ?? DBNull.Value);
                summary.TotalContractorPaid = Convert.ToDecimal(cmd.ExecuteScalar());
            }

            string sqlAdminExpenses = @"
                SELECT 
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE Direction = N'پرداختی' AND PaymentScope = N'Administrative' AND (@From IS NULL OR TransactionDate >= @From) AND (@To IS NULL OR TransactionDate <= @To))
                    +
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND PaymentScope = N'Administrative' AND (@From IS NULL OR DueDate >= @From) AND (@To IS NULL OR DueDate <= @To))";
            using (var cmd = new SqlCommand(sqlAdminExpenses, conn))
            {
                cmd.Parameters.AddWithValue("@From", (object?)fromDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@To", (object?)toDate ?? DBNull.Value);
                summary.TotalAdminExpenses = Convert.ToDecimal(cmd.ExecuteScalar());
            }

            string sqlDirectProjectExpenses = @"
                SELECT 
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE Direction = N'پرداختی' AND (PaymentScope = N'DirectPurchase' OR PaymentScope = N'InformalContractor') AND (@From IS NULL OR TransactionDate >= @From) AND (@To IS NULL OR TransactionDate <= @To))
                    +
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی') AND (PaymentScope = N'DirectPurchase' OR PaymentScope = N'InformalContractor') AND (@From IS NULL OR DueDate >= @From) AND (@To IS NULL OR DueDate <= @To))";
            using (var cmd = new SqlCommand(sqlDirectProjectExpenses, conn))
            {
                cmd.Parameters.AddWithValue("@From", (object?)fromDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@To", (object?)toDate ?? DBNull.Value);
                summary.TotalDirectProjectExpenses = Convert.ToDecimal(cmd.ExecuteScalar());
            }

            // ✅ FIX: TotalInvoiceExpenses هرگز پر نمی‌شد — منجر به محاسبهٔ نادرست سود ناخالص و جریان نقدی
            string sqlInvoiceExpenses = @"
                SELECT ISNULL(SUM(Amount), 0) 
                FROM ProjectExpenses 
                WHERE (@From IS NULL OR InvoiceDate >= @From) 
                  AND (@To IS NULL OR InvoiceDate <= @To)";
            using (var cmd = new SqlCommand(sqlInvoiceExpenses, conn))
            {
                cmd.Parameters.AddWithValue("@From", (object?)fromDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@To", (object?)toDate ?? DBNull.Value);
                summary.TotalInvoiceExpenses = Convert.ToDecimal(cmd.ExecuteScalar());
            }

            return summary;
        }
    }
}