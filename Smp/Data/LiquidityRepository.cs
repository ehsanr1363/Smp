using Microsoft.Data.SqlClient;
using SMP.Models;
using System;
using System.Data;
using System.Threading.Tasks;

namespace SMP.Data
{
    public class LiquidityRepository
    {
        private readonly string _connectionString;
        public LiquidityRepository(string connectionString) => _connectionString = connectionString;

        public async Task<LiquidityDashboardModel> GetDashboardDataAsync(DateTime fromDate, DateTime toDate)
        {
            var result = new LiquidityDashboardModel();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = @"
                SELECT 
                    (SELECT ISNULL(SUM(CurrentBalance), 0) FROM CompanyBankAccounts WHERE IsActive = 1) AS TotalLiquidity,
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE TransactionType = N'دریافت' AND TransactionDate >= @From AND TransactionDate <= @To) AS TotalIncome,
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE TransactionType = N'پرداخت' AND TransactionDate >= @From AND TransactionDate <= @To) AS TotalExpense,
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE CheckType = N'دریافتی' AND Status = N'در جریان') AS TotalReceivableChecks,
                    (SELECT ISNULL(SUM(Amount), 0) FROM CashChecks WHERE CheckType = N'پرداختی' AND Status = N'در جریان') AS TotalPayableChecks";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@From", fromDate.Date); cmd.Parameters.AddWithValue("@To", toDate.Date.AddDays(1).AddSeconds(-1));
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                result.TotalLiquidity = reader.GetDecimal(0); result.TotalIncome = reader.GetDecimal(1);
                result.TotalExpense = reader.GetDecimal(2); result.TotalReceivableChecks = reader.GetDecimal(3);
                result.TotalPayableChecks = reader.GetDecimal(4);
            }
            return result;
        }

        public async Task<DataTable> GetCashFlowTrendAsync(DateTime fromDate, DateTime toDate)
        {
            using var conn = new SqlConnection(_connectionString); await conn.OpenAsync();
            string sql = @"SELECT CAST(TransactionDate AS DATE) AS TxDate, SUM(CASE WHEN TransactionType = N'دریافت' THEN Amount ELSE 0 END) AS Income, SUM(CASE WHEN TransactionType = N'پرداخت' THEN Amount ELSE 0 END) AS Expense FROM CashTransactions WHERE TransactionDate >= @From AND TransactionDate <= @To GROUP BY CAST(TransactionDate AS DATE) ORDER BY TxDate ASC";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@From", fromDate.Date); cmd.Parameters.AddWithValue("@To", toDate.Date.AddDays(1).AddSeconds(-1));
            using var adapter = new SqlDataAdapter(cmd); var dt = new DataTable(); await Task.Run(() => adapter.Fill(dt)); return dt;
        }

        public async Task<DataTable> GetExpenseBreakdownAsync(DateTime fromDate, DateTime toDate)
        {
            using var conn = new SqlConnection(_connectionString); await conn.OpenAsync();
            string sql = @"SELECT ISNULL(Category, N'سایر') AS Category, SUM(Amount) AS TotalAmount FROM CashTransactions WHERE TransactionType = N'پرداخت' AND TransactionDate >= @From AND TransactionDate <= @To GROUP BY Category";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@From", fromDate.Date); cmd.Parameters.AddWithValue("@To", toDate.Date.AddDays(1).AddSeconds(-1));
            using var adapter = new SqlDataAdapter(cmd); var dt = new DataTable(); await Task.Run(() => adapter.Fill(dt)); return dt;
        }

        public async Task<DataTable> GetAccountBalancesAsync()
        {
            using var conn = new SqlConnection(_connectionString); await conn.OpenAsync();
            string sql = "SELECT Title, CurrentBalance FROM CompanyBankAccounts WHERE IsActive = 1 AND CurrentBalance > 0";
            using var cmd = new SqlCommand(sql, conn); using var adapter = new SqlDataAdapter(cmd); var dt = new DataTable(); await Task.Run(() => adapter.Fill(dt)); return dt;
        }

        public async Task<DataTable> GetUpcomingChecksAsync()
        {
            using var conn = new SqlConnection(_connectionString); await conn.OpenAsync();
            string sql = @"SELECT CheckNumber, BankName, DueDate, Amount, CheckType, 
                           CASE WHEN PartyId IS NOT NULL THEN (SELECT CASE WHEN PartyType = N'حقیقی' THEN FirstName + ' ' + LastName ELSE CompanyName END FROM Parties WHERE Id = CashChecks.PartyId) ELSE '---' END AS PartyName
                           FROM CashChecks WHERE Status = N'در جریان' AND DueDate >= CAST(GETDATE() AS DATE) AND DueDate <= DATEADD(day, 30, GETDATE()) ORDER BY DueDate ASC";
            using var cmd = new SqlCommand(sql, conn); using var adapter = new SqlDataAdapter(cmd); var dt = new DataTable(); await Task.Run(() => adapter.Fill(dt)); return dt;
        }
    }
}