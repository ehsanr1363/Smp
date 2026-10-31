using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using PersonnelApp.Models;

namespace PersonnelApp.Data
{
    public class ProjectExpenseRepository
    {
        private readonly string _connectionString;

        public ProjectExpenseRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<ProjectExpense> GetByClientContractId(int clientContractId)
        {
            var list = new List<ProjectExpense>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string query = @"
                SELECT e.Id, e.ClientContractId, e.PartyId, e.InvoiceNumber, e.InvoiceDate, e.Amount, e.Description, e.CreatedAt,
                       CASE WHEN p.PartyType = N'حقیقی' THEN p.FirstName + N' ' + p.LastName ELSE p.CompanyName END AS PartyName
                FROM ProjectExpenses e
                LEFT JOIN Parties p ON e.PartyId = p.Id
                WHERE e.ClientContractId = @cid
                ORDER BY e.InvoiceDate DESC";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@cid", clientContractId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ProjectExpense
                {
                    Id = reader.GetInt32(0),
                    ClientContractId = reader.GetInt32(1),
                    PartyId = reader.IsDBNull(2) ? (int?)null : Convert.ToInt32(reader[2]),
                    InvoiceNumber = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    InvoiceDate = Convert.ToDateTime(reader[4]),
                    Amount = Convert.ToDecimal(reader[5]),
                    Description = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    CreatedAt = Convert.ToDateTime(reader[7]),
                    PartyName = reader.IsDBNull(8) ? "" : reader.GetString(8)
                });
            }
            return list;
        }

        public ProjectExpense GetById(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand("SELECT * FROM ProjectExpenses WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new ProjectExpense
                {
                    Id = reader.GetInt32(0),
                    ClientContractId = reader.GetInt32(1),
                    PartyId = reader.IsDBNull(2) ? (int?)null : Convert.ToInt32(reader[2]),
                    InvoiceNumber = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    InvoiceDate = Convert.ToDateTime(reader[4]),
                    Amount = Convert.ToDecimal(reader[5]),
                    Description = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    CreatedAt = Convert.ToDateTime(reader[7])
                };
            }
            return null!;
        }

        public void Add(ProjectExpense e)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(@"
                INSERT INTO ProjectExpenses (ClientContractId, PartyId, InvoiceNumber, InvoiceDate, Amount, Description, CreatedAt)
                VALUES (@cid, @pid, @num, @date, @amount, @desc, @created)", conn);

            cmd.Parameters.AddWithValue("@cid", e.ClientContractId);
            cmd.Parameters.AddWithValue("@pid", (object?)e.PartyId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@num", e.InvoiceNumber ?? "");
            cmd.Parameters.AddWithValue("@date", e.InvoiceDate);
            cmd.Parameters.AddWithValue("@amount", e.Amount);
            cmd.Parameters.AddWithValue("@desc", e.Description ?? "");
            cmd.Parameters.AddWithValue("@created", DateTime.Now);

            cmd.ExecuteNonQuery();
        }

        public void Update(ProjectExpense e)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(@"
                UPDATE ProjectExpenses SET
                    PartyId = @pid, InvoiceNumber = @num, InvoiceDate = @date,
                    Amount = @amount, Description = @desc
                WHERE Id = @id", conn);

            cmd.Parameters.AddWithValue("@pid", (object?)e.PartyId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@num", e.InvoiceNumber ?? "");
            cmd.Parameters.AddWithValue("@date", e.InvoiceDate);
            cmd.Parameters.AddWithValue("@amount", e.Amount);
            cmd.Parameters.AddWithValue("@desc", e.Description ?? "");
            cmd.Parameters.AddWithValue("@id", e.Id);

            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM ProjectExpenses WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}