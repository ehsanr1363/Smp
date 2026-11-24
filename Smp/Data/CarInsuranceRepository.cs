using Microsoft.Data.SqlClient;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Smp.Data;

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

        // حذف BeginTransaction برای یک INSERT ساده (بهبود کارایی)
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

        var result = await cmd.ExecuteScalarAsync();
        insurance.Id = result is not null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
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

    // ✅ FIX: خواندن با نام ستون‌ها و تبدیل ایمن انواع داده‌ها
    private static EmployeeCarInsurance MapToInsurance(SqlDataReader reader)
    {
        return new EmployeeCarInsurance
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            EmployeeId = reader.GetInt32(reader.GetOrdinal("EmployeeId")),
            EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? string.Empty : reader.GetString(reader.GetOrdinal("EmployeeName")),

            TotalAmount = reader.IsDBNull(reader.GetOrdinal("TotalAmount")) ? 0m : Convert.ToDecimal(reader["TotalAmount"]),
            MonthlyInstallment = reader.IsDBNull(reader.GetOrdinal("MonthlyInstallment")) ? 0m : Convert.ToDecimal(reader["MonthlyInstallment"]),
            RemainingAmount = reader.IsDBNull(reader.GetOrdinal("RemainingAmount")) ? 0m : Convert.ToDecimal(reader["RemainingAmount"]),

            StartYear = reader.IsDBNull(reader.GetOrdinal("StartYear")) ? 0 : Convert.ToInt32(reader["StartYear"]),
            StartMonth = reader.IsDBNull(reader.GetOrdinal("StartMonth")) ? 0 : Convert.ToInt32(reader["StartMonth"]),
            StartDate = reader.IsDBNull(reader.GetOrdinal("StartDate")) ? DateTime.Now : Convert.ToDateTime(reader["StartDate"]),

            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            IsActive = !reader.IsDBNull(reader.GetOrdinal("IsActive")) && Convert.ToBoolean(reader["IsActive"]),
            CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTime.Now : Convert.ToDateTime(reader["CreatedAt"])
        };
    }
}
