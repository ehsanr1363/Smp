using Microsoft.Data.SqlClient;
using PersonnelApp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PersonnelApp.Data
{
    public class CashCheckRepository
    {
        private readonly string _connectionString;
        public CashCheckRepository(string connectionString) => _connectionString = connectionString;

        public async Task<List<CashCheck>> GetAllAsync()
        {
            var list = new List<CashCheck>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = @"SELECT c.Id, c.CheckNumber, c.BankName, c.DueDate, c.Amount, c.CheckType, c.Status, c.PartyId, 
                           CASE WHEN p.PartyType = N'حقیقی' THEN p.FirstName + ' ' + p.LastName ELSE p.CompanyName END, c.Description, c.CreatedAt
                           FROM CashChecks c LEFT JOIN Parties p ON c.PartyId = p.Id ORDER BY c.DueDate ASC";
            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new CashCheck
                {
                    Id = reader.GetInt32(0),
                    CheckNumber = reader.GetString(1),
                    BankName = reader.IsDBNull(2) ? null : reader.GetString(2),
                    DueDate = reader.GetDateTime(3),
                    Amount = reader.GetDecimal(4),
                    CheckType = reader.GetString(5),
                    Status = reader.GetString(6),
                    PartyId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    PartyName = reader.IsDBNull(8) ? null : reader.GetString(8),
                    Description = reader.IsDBNull(9) ? null : reader.GetString(9),
                    CreatedAt = reader.GetDateTime(10)
                });
            }
            return list;
        }

        public async Task AddAsync(CashCheck check)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = @"INSERT INTO CashChecks (CheckNumber, BankName, DueDate, Amount, CheckType, Status, PartyId, Description)
                           VALUES (@Num, @Bank, @Date, @Amount, @Type, @Status, @PartyId, @Desc); SELECT SCOPE_IDENTITY();";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Num", check.CheckNumber); cmd.Parameters.AddWithValue("@Bank", (object?)check.BankName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Date", check.DueDate); cmd.Parameters.AddWithValue("@Amount", check.Amount);
            cmd.Parameters.AddWithValue("@Type", check.CheckType); cmd.Parameters.AddWithValue("@Status", check.Status);
            cmd.Parameters.AddWithValue("@PartyId", (object?)check.PartyId ?? DBNull.Value); cmd.Parameters.AddWithValue("@Desc", (object?)check.Description ?? DBNull.Value);
            check.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("DELETE FROM CashChecks WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}