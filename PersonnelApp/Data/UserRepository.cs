using Microsoft.Data.SqlClient;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PersonnelApp.Data
{
    public class UserRepository
    {
        private readonly string _connectionString;

        public UserRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<User?> AuthenticateAsync(string username, string password)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = @"SELECT u.Id, u.EmployeeId, u.Username, u.Role, u.IsActive, u.PasswordHash, e.FirstName, e.LastName FROM Users u LEFT JOIN Employees e ON u.EmployeeId = e.Id WHERE u.Username = @User";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@User", username.Trim());
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            bool isActive = reader.IsDBNull(4) || Convert.ToInt32(reader[4]) == 1;
            if (!isActive) return null;

            string storedHash = reader.IsDBNull(5) ? "" : reader.GetString(5);
            if (!SecurityHelper.VerifyPassword(password, storedHash)) return null;

            string firstName = reader.IsDBNull(6) ? "" : reader.GetString(6);
            string lastName = reader.IsDBNull(7) ? "" : reader.GetString(7);
            string fullName = $"{firstName} {lastName}".Trim();
            if (string.IsNullOrWhiteSpace(fullName)) fullName = "مدیر سیستم";

            var user = new User
            {
                Id = reader.GetInt32(0),
                EmployeeId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                Username = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Role = reader.IsDBNull(3) ? "Viewer" : reader.GetString(3),
                EmployeeFullName = fullName
            };

            await reader.CloseAsync();
            using var updateCmd = new SqlCommand("UPDATE Users SET LastLogin = GETDATE() WHERE Id = @Id", conn);
            updateCmd.Parameters.AddWithValue("@Id", user.Id);
            await updateCmd.ExecuteNonQueryAsync();
            return user;
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            var list = new List<User>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = @"SELECT u.Id, u.EmployeeId, u.Username, u.Role, u.IsActive, u.LastLogin, u.CreatedAt, e.FirstName, e.LastName FROM Users u LEFT JOIN Employees e ON u.EmployeeId = e.Id ORDER BY u.Username";
            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string fName = reader.IsDBNull(7) ? "" : reader.GetString(7);
                string lName = reader.IsDBNull(8) ? "" : reader.GetString(8);
                list.Add(new User
                {
                    Id = reader.GetInt32(0),
                    EmployeeId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    Username = reader.GetString(2),
                    Role = reader.IsDBNull(3) ? "Viewer" : reader.GetString(3),
                    IsActive = !reader.IsDBNull(4) && Convert.ToInt32(reader[4]) == 1,
                    LastLogin = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    CreatedAt = reader.IsDBNull(6) ? DateTime.Now : reader.GetDateTime(6),
                    EmployeeFullName = string.IsNullOrWhiteSpace(fName) && string.IsNullOrWhiteSpace(lName) ? "--- بدون اتصال ---" : $"{fName} {lName}".Trim()
                });
            }
            return list;
        }

        public async Task<bool> IsEmployeeAssignedToAnotherUserAsync(int employeeId, int? excludeUserId = null)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = "SELECT COUNT(1) FROM Users WHERE EmployeeId = @EmpId AND IsActive = 1";
            if (excludeUserId.HasValue) sql += " AND Id != @ExcludeId";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EmpId", employeeId);
            if (excludeUserId.HasValue) cmd.Parameters.AddWithValue("@ExcludeId", excludeUserId.Value);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        public async Task<bool> IsUsernameTakenAsync(string username, int? excludeUserId = null)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = "SELECT COUNT(1) FROM Users WHERE Username = @User";
            if (excludeUserId.HasValue) sql += " AND Id != @ExcludeId";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@User", username.Trim());
            if (excludeUserId.HasValue) cmd.Parameters.AddWithValue("@ExcludeId", excludeUserId.Value);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        public async Task<User?> GetByEmployeeIdAsync(int employeeId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = @"SELECT u.Id, u.EmployeeId, u.Username, u.Role, u.IsActive, e.FirstName, e.LastName FROM Users u LEFT JOIN Employees e ON u.EmployeeId = e.Id WHERE u.EmployeeId = @EmpId";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EmpId", employeeId);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                string firstName = reader.IsDBNull(5) ? "" : reader.GetString(5);
                string lastName = reader.IsDBNull(6) ? "" : reader.GetString(6);
                return new User
                {
                    Id = reader.GetInt32(0),
                    EmployeeId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    Username = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Role = reader.IsDBNull(3) ? "Viewer" : reader.GetString(3),
                    IsActive = !reader.IsDBNull(4) && Convert.ToInt32(reader[4]) == 1,
                    EmployeeFullName = $"{firstName} {lastName}".Trim()
                };
            }
            return null;
        }

        public async Task AddAsync(User user)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = @"INSERT INTO Users (Username, PasswordHash, Role, EmployeeId, IsActive, CreatedAt) VALUES (@User, @Pass, @Role, @EmpId, @IsActive, GETDATE()); SELECT SCOPE_IDENTITY();";
            string finalHash = string.IsNullOrWhiteSpace(user.PasswordHash) ? SecurityHelper.HashPassword("123456") : user.PasswordHash;
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@User", user.Username); cmd.Parameters.AddWithValue("@Pass", finalHash); cmd.Parameters.AddWithValue("@Role", user.Role ?? "Viewer");
            cmd.Parameters.AddWithValue("@EmpId", (object?)user.EmployeeId ?? DBNull.Value); cmd.Parameters.AddWithValue("@IsActive", user.IsActive ? 1 : 0);
            user.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task UpdateAsync(User user)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = @"UPDATE Users SET Username = @User, Role = @Role, EmployeeId = @EmpId, IsActive = @IsActive " + (!string.IsNullOrWhiteSpace(user.PasswordHash) ? ", PasswordHash = @Pass " : "") + @"WHERE Id = @Id";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@User", user.Username); cmd.Parameters.AddWithValue("@Role", user.Role ?? "Viewer");
            cmd.Parameters.AddWithValue("@EmpId", (object?)user.EmployeeId ?? DBNull.Value); cmd.Parameters.AddWithValue("@IsActive", user.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@Id", user.Id);
            if (!string.IsNullOrWhiteSpace(user.PasswordHash)) cmd.Parameters.AddWithValue("@Pass", user.PasswordHash);
            await cmd.ExecuteNonQueryAsync();
        }

        public void Delete(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM Users WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }
    }
}