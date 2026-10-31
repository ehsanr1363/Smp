using Microsoft.Data.SqlClient;
using PersonnelApp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PersonnelApp.Data
{
    public class LoanRepository
    {
        private readonly string _connectionString;

        public LoanRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<EmployeeLoan>> GetActiveLoansAsync()
        {
            var list = new List<EmployeeLoan>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT l.Id, l.EmployeeId, l.LoanType, l.TotalAmount, l.MonthlyInstallment, l.RemainingAmount,
                       l.StartYear, l.StartMonth, l.LoanDate, l.Description, l.IsActive,
                       e.FirstName + ' ' + e.LastName AS EmployeeName
                FROM EmployeeLoans l
                INNER JOIN Employees e ON l.EmployeeId = e.Id
                WHERE l.IsActive = 1 AND l.RemainingAmount > 0
                ORDER BY l.StartYear DESC, l.StartMonth DESC, l.Id DESC";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapReader(reader));
            }
            return list;
        }

        public async Task<List<EmployeeLoan>> GetLoansByEmployeeIdAsync(int employeeId)
        {
            var list = new List<EmployeeLoan>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT l.Id, l.EmployeeId, l.LoanType, l.TotalAmount, l.MonthlyInstallment, l.RemainingAmount,
                       l.StartYear, l.StartMonth, l.LoanDate, l.Description, l.IsActive,
                       e.FirstName + ' ' + e.LastName AS EmployeeName
                FROM EmployeeLoans l
                INNER JOIN Employees e ON l.EmployeeId = e.Id
                WHERE l.EmployeeId = @EmpId
                ORDER BY l.StartYear DESC, l.StartMonth DESC, l.Id DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EmpId", employeeId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapReader(reader));
            }
            return list;
        }

        public async Task AddLoanAsync(EmployeeLoan loan)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                INSERT INTO EmployeeLoans 
                    (EmployeeId, LoanType, TotalAmount, MonthlyInstallment, RemainingAmount, StartYear, StartMonth, LoanDate, Description, IsActive)
                VALUES 
                    (@EmpId, @LoanType, @Total, @Installment, @Remaining, @StartYear, @StartMonth, @LoanDate, @Desc, 1)";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EmpId", loan.EmployeeId);
            cmd.Parameters.AddWithValue("@LoanType", loan.LoanType ?? "وام");
            cmd.Parameters.AddWithValue("@Total", loan.TotalAmount);
            cmd.Parameters.AddWithValue("@Installment", loan.MonthlyInstallment);
            cmd.Parameters.AddWithValue("@Remaining", loan.RemainingAmount);
            cmd.Parameters.AddWithValue("@StartYear", loan.StartYear);
            cmd.Parameters.AddWithValue("@StartMonth", loan.StartMonth);
            cmd.Parameters.AddWithValue("@LoanDate", loan.LoanDate);
            cmd.Parameters.AddWithValue("@Desc", (object?)loan.Description ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateLoanAsync(EmployeeLoan loan)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                UPDATE EmployeeLoans 
                SET LoanType = @LoanType,
                    TotalAmount = @Total, 
                    MonthlyInstallment = @Installment, 
                    RemainingAmount = @Remaining,
                    StartYear = @StartYear,
                    StartMonth = @StartMonth,
                    Description = @Desc
                WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@LoanType", loan.LoanType ?? "وام");
            cmd.Parameters.AddWithValue("@Total", loan.TotalAmount);
            cmd.Parameters.AddWithValue("@Installment", loan.MonthlyInstallment);
            cmd.Parameters.AddWithValue("@Remaining", loan.RemainingAmount);
            cmd.Parameters.AddWithValue("@StartYear", loan.StartYear);
            cmd.Parameters.AddWithValue("@StartMonth", loan.StartMonth);
            cmd.Parameters.AddWithValue("@Desc", (object?)loan.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Id", loan.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteLoanAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            int empId = 0;
            int startYear = 0;
            int startMonth = 0;

            // استفاده از ISNULL برای جلوگیری از خطای DBNull
            using (var cmdCheck = new SqlCommand("SELECT EmployeeId, ISNULL(StartYear, 0), ISNULL(StartMonth, 0) FROM EmployeeLoans WHERE Id = @Id", conn))
            {
                cmdCheck.Parameters.AddWithValue("@Id", id);
                using var reader = await cmdCheck.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    empId = reader.GetInt32(0);
                    startYear = reader.GetInt32(1);
                    startMonth = reader.GetInt32(2);
                }
                else
                {
                    throw new InvalidOperationException("رکورد وام در دیتابیس یافت نشد. ممکن است قبلاً حذف شده باشد.");
                }
            }

            string sqlPayroll = @"
                SELECT COUNT(1) FROM Payrolls 
                WHERE EmployeeId = @EmpId 
                  AND LoanDeduction > 0";

            // فقط اگر تاریخ شروع معتبر باشد، شرط تاریخ اعمال می‌شود
            if (startYear > 0 && startMonth > 0)
            {
                sqlPayroll += " AND (Year > @StartYear OR (Year = @StartYear AND Month >= @StartMonth))";
            }

            using (var cmdPayroll = new SqlCommand(sqlPayroll, conn))
            {
                cmdPayroll.Parameters.AddWithValue("@EmpId", empId);
                if (startYear > 0 && startMonth > 0)
                {
                    cmdPayroll.Parameters.AddWithValue("@StartYear", startYear);
                    cmdPayroll.Parameters.AddWithValue("@StartMonth", startMonth);
                }

                int payrollCount = Convert.ToInt32(await cmdPayroll.ExecuteScalarAsync());
                if (payrollCount > 0)
                {
                    throw new InvalidOperationException(
                        "امکان حذف این وام وجود ندارد.\n\n" +
                        "این وام/مساعده در محاسبه حقوق ماه‌های گذشته کسر شده است. " +
                        "برای حفظ سوابق مالی، ابتدا باید فیش‌های حقوقی مربوطه را از منوی «تاریخچه حقوق» حذف کنید.");
                }
            }

            using var cmdDel = new SqlCommand("DELETE FROM EmployeeLoans WHERE Id = @Id", conn);
            cmdDel.Parameters.AddWithValue("@Id", id);
            await cmdDel.ExecuteNonQueryAsync();
        }

        public async Task DeductInstallmentAsync(int loanId, decimal amount)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                UPDATE EmployeeLoans 
                SET RemainingAmount = CASE 
                    WHEN RemainingAmount - @Amount < 0 THEN 0 
                    ELSE RemainingAmount - @Amount 
                END
                WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Amount", amount);
            cmd.Parameters.AddWithValue("@Id", loanId);
            await cmd.ExecuteNonQueryAsync();

            string checkSql = "UPDATE EmployeeLoans SET IsActive = 0 WHERE Id = @Id AND RemainingAmount <= 0";
            using var checkCmd = new SqlCommand(checkSql, conn);
            checkCmd.Parameters.AddWithValue("@Id", loanId);
            await checkCmd.ExecuteNonQueryAsync();
        }

        private static EmployeeLoan MapReader(SqlDataReader reader)
        {
            return new EmployeeLoan
            {
                Id = reader.GetInt32(0),
                EmployeeId = reader.GetInt32(1),
                LoanType = reader.IsDBNull(2) ? "وام" : reader.GetString(2),
                TotalAmount = reader.GetDecimal(3),
                MonthlyInstallment = reader.GetDecimal(4),
                RemainingAmount = reader.GetDecimal(5),
                StartYear = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                StartMonth = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                LoanDate = reader.IsDBNull(8) ? DateTime.Now : reader.GetDateTime(8),
                Description = reader.IsDBNull(9) ? "" : reader.GetString(9),
                IsActive = !reader.IsDBNull(10) && Convert.ToBoolean(reader[10]),
                EmployeeName = reader.IsDBNull(11) ? "" : reader.GetString(11)
            };
        }
    }
}