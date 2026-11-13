using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using SMP.Models;

namespace SMP.Data
{
    public class ContractorGuaranteeRepository
    {
        private readonly string _connectionString;

        public ContractorGuaranteeRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<ContractorGuarantee> GetByContractId(int contractId)
        {
            var list = new List<ContractorGuarantee>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand("SELECT * FROM ContractorGuarantees WHERE ContractId = @cid ORDER BY Id DESC", conn);
            cmd.Parameters.AddWithValue("@cid", contractId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(MapToGuarantee(reader));
            }
            return list;
        }

        public ContractorGuarantee GetById(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand("SELECT * FROM ContractorGuarantees WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return MapToGuarantee(reader);
            }
            return null!;
        }

        public List<ContractorGuarantee> GetAll()
        {
            var list = new List<ContractorGuarantee>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string query = @"
                SELECT g.*, c.ContractNumber, 
                       CASE WHEN p.PartyType = N'حقیقی' THEN p.FirstName + N' ' + p.LastName ELSE p.CompanyName END AS ContractorName
                FROM ContractorGuarantees g
                INNER JOIN ContractorContracts c ON g.ContractId = c.Id
                INNER JOIN Parties p ON c.PartyId = p.Id
                ORDER BY g.MaturityDate ASC";

            using var cmd = new SqlCommand(query, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var guarantee = MapToGuarantee(reader);
                guarantee.ContractNumber = reader.IsDBNull(reader.GetOrdinal("ContractNumber")) ? "" : reader.GetString(reader.GetOrdinal("ContractNumber"));
                guarantee.ContractorName = reader.IsDBNull(reader.GetOrdinal("ContractorName")) ? "" : reader.GetString(reader.GetOrdinal("ContractorName"));
                list.Add(guarantee);
            }
            return list;
        }

        // ✅ FIX P1: SCOPE_IDENTITY برای دریافت Id واقعی
        public void Add(ContractorGuarantee g)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(@"
                INSERT INTO ContractorGuarantees (ContractId, GuaranteeNumber, BankName, Amount, IssueDate, MaturityDate, GuaranteeType, Status, Description, CreatedAt)
                VALUES (@cid, @num, @bank, @amt, @issue, @mature, @type, @status, @desc, @created);
                SELECT SCOPE_IDENTITY();", conn);

            AddParameters(cmd, g);

            var result = cmd.ExecuteScalar();
            if (result != null && result != DBNull.Value)
                g.Id = Convert.ToInt32(result);
        }

        public void Update(ContractorGuarantee g)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(@"
                UPDATE ContractorGuarantees SET
                    GuaranteeNumber = @num, BankName = @bank, Amount = @amt, 
                    IssueDate = @issue, MaturityDate = @mature, GuaranteeType = @type, 
                    Status = @status, Description = @desc
                WHERE Id = @id", conn);

            cmd.Parameters.AddWithValue("@id", g.Id);
            AddParameters(cmd, g);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM ContractorGuarantees WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private void AddParameters(SqlCommand cmd, ContractorGuarantee g)
        {
            cmd.Parameters.AddWithValue("@cid", g.ContractId);
            cmd.Parameters.AddWithValue("@num", g.GuaranteeNumber ?? "");
            cmd.Parameters.AddWithValue("@bank", g.BankName ?? "");
            cmd.Parameters.AddWithValue("@amt", g.Amount);
            cmd.Parameters.AddWithValue("@issue", g.IssueDate);
            cmd.Parameters.AddWithValue("@mature", g.MaturityDate);
            cmd.Parameters.AddWithValue("@type", g.GuaranteeType ?? "");
            cmd.Parameters.AddWithValue("@status", g.Status ?? "فعال");
            cmd.Parameters.AddWithValue("@desc", g.Description ?? "");
            cmd.Parameters.AddWithValue("@created", DateTime.Now);
        }

        private ContractorGuarantee MapToGuarantee(SqlDataReader reader)
        {
            return new ContractorGuarantee
            {
                Id = reader.GetInt32(0),
                ContractId = reader.GetInt32(1),
                GuaranteeNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                BankName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Amount = Convert.ToDecimal(reader[4]),
                IssueDate = Convert.ToDateTime(reader[5]),
                MaturityDate = Convert.ToDateTime(reader[6]),
                GuaranteeType = reader.IsDBNull(7) ? "" : reader.GetString(7),
                Status = reader.IsDBNull(8) ? "فعال" : reader.GetString(8),
                Description = reader.IsDBNull(9) ? "" : reader.GetString(9),
                CreatedAt = Convert.ToDateTime(reader[10])
            };
        }
    }
}