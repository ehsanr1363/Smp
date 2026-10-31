using Microsoft.Data.SqlClient;
using PersonnelApp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PersonnelApp.Data
{
    public class CarInsuranceRepository
    {
        private readonly string _connectionString;

        public CarInsuranceRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<EmployeeCarInsurance>> GetActiveCarInsurancesAsync()
        {
            var list = new List<EmployeeCarInsurance>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT i.Id, i.EmployeeId, e.FirstName + ' ' + e.LastName AS EmployeeName,
                       i.TotalAmount, i.MonthlyInstallment, i.RemainingAmount,
                       i.StartYear, i.StartMonth, i.StartDate, i.Description, i.IsActive, i.CreatedAt
                FROM EmployeeCarInsurances i
                INNER JOIN Employees e ON i.EmployeeId = e.Id
                WHERE i.IsActive = 1 AND i.RemainingAmount > 0
                ORDER BY i.StartYear DESC, i.StartMonth DESC, i.Id DESC";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapToInsurance(reader));
            }
            return list;
        }

        public async Task<List<EmployeeCarInsurance>> GetByEmployeeIdAsync(int employeeId)
        {
            var list = new List<EmployeeCarInsurance>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT i.Id, i.EmployeeId, e.FirstName + ' ' + e.LastName AS EmployeeName,
                       i.TotalAmount, i.MonthlyInstallment, i.RemainingAmount,
                       i.StartYear, i.StartMonth, i.StartDate, i.Description, i.IsActive, i.CreatedAt
                FROM EmployeeCarInsurances i
                INNER JOIN Employees e ON i.EmployeeId = e.Id
                WHERE i.EmployeeId = @EmpId
                ORDER BY i.StartYear DESC, i.StartMonth DESC, i.Id DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EmpId", employeeId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapToInsurance(reader));
            }
            return list;
        }

        public async Task AddAsync(EmployeeCarInsurance insurance)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                INSERT INTO EmployeeCarInsurances 
                    (EmployeeId, TotalAmount, MonthlyInstallment, RemainingAmount, StartYear, StartMonth, StartDate, Description, IsActive)
                VALUES 
                    (@EmpId, @Total, @Installment, @Remaining, @StartYear, @StartMonth, @StartDate, @Desc, 1);
                SELECT SCOPE_IDENTITY();";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EmpId", insurance.EmployeeId);
            cmd.Parameters.AddWithValue("@Total", insurance.TotalAmount);
            cmd.Parameters.AddWithValue("@Installment", insurance.MonthlyInstallment);
            cmd.Parameters.AddWithValue("@Remaining", insurance.RemainingAmount);
            cmd.Parameters.AddWithValue("@StartYear", insurance.StartYear);
            cmd.Parameters.AddWithValue("@StartMonth", insurance.StartMonth);
            cmd.Parameters.AddWithValue("@StartDate", insurance.StartDate);
            cmd.Parameters.AddWithValue("@Desc", (object?)insurance.Description ?? DBNull.Value);

            insurance.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task UpdateAsync(EmployeeCarInsurance insurance)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                UPDATE EmployeeCarInsurances 
                SET TotalAmount = @Total, 
                    MonthlyInstallment = @Installment, 
                    RemainingAmount = @Remaining,
                    StartYear = @StartYear,
                    StartMonth = @StartMonth,
                    Description = @Desc
                WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Total", insurance.TotalAmount);
            cmd.Parameters.AddWithValue("@Installment", insurance.MonthlyInstallment);
            cmd.Parameters.AddWithValue("@Remaining", insurance.RemainingAmount);
            cmd.Parameters.AddWithValue("@StartYear", insurance.StartYear);
            cmd.Parameters.AddWithValue("@StartMonth", insurance.StartMonth);
            cmd.Parameters.AddWithValue("@Desc", (object?)insurance.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Id", insurance.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("DELETE FROM EmployeeCarInsurances WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        private static EmployeeCarInsurance MapToInsurance(SqlDataReader reader)
        {
            return new EmployeeCarInsurance
            {
                Id = reader.GetInt32(0),
                EmployeeId = reader.GetInt32(1),
                EmployeeName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                TotalAmount = reader.GetDecimal(3),
                MonthlyInstallment = reader.GetDecimal(4),
                RemainingAmount = reader.GetDecimal(5),
                StartYear = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                StartMonth = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                StartDate = reader.IsDBNull(8) ? DateTime.Now : reader.GetDateTime(8),
                Description = reader.IsDBNull(9) ? null : reader.GetString(9),
                // ✅ اصلاح شد: استفاده از Convert.ToBoolean برای پشتیبانی از BIT و INT
                IsActive = !reader.IsDBNull(10) && Convert.ToBoolean(reader[10]),
                CreatedAt = reader.GetDateTime(11)
            };
        }
    }
}