using System;
using Microsoft.Data.SqlClient;
using PersonnelApp.Models;

namespace PersonnelApp.Data
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

            // ✅ FIX: همه‌ی ستون‌های تاریخ در DB از نوع DATETIME هستند → مستقیم DateTime پاس بده

            // ۱. مبلغ کل قراردادهای کارفرما
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

            // مجموع صورت‌وضعیت‌های کارفرما
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

            // مجموع دریافتی‌های کارفرما
            string sqlClientPayments = @"
                SELECT ISNULL(SUM(Amount), 0) 
                FROM ClientPayments 
                WHERE (@From IS NULL OR PaymentDate >= @From) 
                  AND (@To IS NULL OR PaymentDate <= @To)";
            using (var cmd = new SqlCommand(sqlClientPayments, conn))
            {
                cmd.Parameters.AddWithValue("@From", (object?)fromDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@To", (object?)toDate ?? DBNull.Value);
                summary.TotalClientReceived = Convert.ToDecimal(cmd.ExecuteScalar());
            }

            // ۲. مبلغ کل قراردادهای پیمانکار
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

            // مجموع صورت‌وضعیت‌های پیمانکار
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

            // مجموع پرداختی‌های پیمانکار
            string sqlContractorPayments = @"
                SELECT ISNULL(SUM(Amount), 0) 
                FROM ContractorPayments 
                WHERE (@From IS NULL OR PaymentDate >= @From) 
                  AND (@To IS NULL OR PaymentDate <= @To)";
            using (var cmd = new SqlCommand(sqlContractorPayments, conn))
            {
                cmd.Parameters.AddWithValue("@From", (object?)fromDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@To", (object?)toDate ?? DBNull.Value);
                summary.TotalContractorPaid = Convert.ToDecimal(cmd.ExecuteScalar());
            }

            // ۳. هزینه‌های فاکتوری
            string sqlExpenses = @"
                SELECT ISNULL(SUM(Amount), 0) 
                FROM ProjectExpenses 
                WHERE (@From IS NULL OR InvoiceDate >= @From) 
                  AND (@To IS NULL OR InvoiceDate <= @To)";
            using (var cmd = new SqlCommand(sqlExpenses, conn))
            {
                cmd.Parameters.AddWithValue("@From", (object?)fromDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@To", (object?)toDate ?? DBNull.Value);
                summary.TotalInvoiceExpenses = Convert.ToDecimal(cmd.ExecuteScalar());
            }

            return summary;
        }
    }
}