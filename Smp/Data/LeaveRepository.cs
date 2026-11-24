using Microsoft.Data.SqlClient;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Smp.Data;

public class LeaveRepository
{
    private readonly string _connectionString;

    public LeaveRepository(string connectionString)
    {
        _connectionString = connectionString;
        // 👈 EnsureTableExists حذف شد
    }

    public async Task<List<LeaveRequest>> GetAllRequestsAsync()
    {
        var list = new List<LeaveRequest>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
                SELECT l.*, e.FirstName + ' ' + e.LastName AS EmployeeName
                FROM LeaveRequests l
                INNER JOIN Employees e ON l.EmployeeId = e.Id
                ORDER BY l.CreatedAt DESC";

        using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new LeaveRequest
            {
                Id = reader.GetInt32(0),
                EmployeeId = reader.GetInt32(1),
                LeaveType = reader.GetString(2),
                StartDate = reader.GetDateTime(3),
                EndDate = reader.GetDateTime(4),
                DaysCount = reader.GetInt32(5),
                Description = reader.IsDBNull(6) ? "" : reader.GetString(6),
                Status = reader.GetString(7),
                EmployeeName = reader.GetString(9)
            });
        }
        return list;
    }

    public async Task<List<LeaveRequest>> GetLeavesByEmployeeIdAsync(int employeeId)
    {
        var list = new List<LeaveRequest>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
                SELECT l.*, e.FirstName + ' ' + e.LastName AS EmployeeName
                FROM LeaveRequests l
                INNER JOIN Employees e ON l.EmployeeId = e.Id
                WHERE l.EmployeeId = @EmpId
                ORDER BY l.StartDate DESC";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@EmpId", employeeId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new LeaveRequest
            {
                Id = reader.GetInt32(0),
                EmployeeId = reader.GetInt32(1),
                LeaveType = reader.GetString(2),
                StartDate = reader.GetDateTime(3),
                EndDate = reader.GetDateTime(4),
                DaysCount = reader.GetInt32(5),
                Description = reader.IsDBNull(6) ? "" : reader.GetString(6),
                Status = reader.GetString(7),
                EmployeeName = reader.GetString(9)
            });
        }
        return list;
    }

    public async Task AddRequestAsync(LeaveRequest request)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
                INSERT INTO LeaveRequests (EmployeeId, LeaveType, StartDate, EndDate, DaysCount, Description, Status)
                VALUES (@EmpId, @Type, @Start, @End, @Days, @Desc, N'در انتظار')";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@EmpId", request.EmployeeId);
        cmd.Parameters.AddWithValue("@Type", request.LeaveType);
        cmd.Parameters.AddWithValue("@Start", request.StartDate);
        cmd.Parameters.AddWithValue("@End", request.EndDate);
        cmd.Parameters.AddWithValue("@Days", request.DaysCount);
        cmd.Parameters.AddWithValue("@Desc", (object?)request.Description ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateStatusAsync(int requestId, string newStatus)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = "UPDATE LeaveRequests SET Status = @Status WHERE Id = @Id";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Status", newStatus);
        cmd.Parameters.AddWithValue("@Id", requestId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> GetTotalApprovedLeaveDaysAsync(int employeeId)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = "SELECT ISNULL(SUM(DaysCount), 0) FROM LeaveRequests WHERE EmployeeId = @EmpId AND Status = N'تایید شده'";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@EmpId", employeeId);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
}
