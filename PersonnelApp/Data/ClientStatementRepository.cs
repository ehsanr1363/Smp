using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using PersonnelApp.Models;
using PersonnelApp.Helpers;

namespace PersonnelApp.Data
{
    public class ClientStatementRepository
    {
        private readonly string _connectionString;

        public ClientStatementRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<ClientStatement> GetByContractId(int contractId)
        {
            var list = new List<ClientStatement>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string query = @"
                SELECT Id, ContractId, StatementNumber, StatementDate, GrossAmount, VAT, Deductions, NetReceivableAmount, CreatedAt
                FROM ClientStatements 
                WHERE ContractId = @cid 
                ORDER BY Id ASC";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@cid", contractId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ClientStatement
                {
                    Id = reader.GetInt32(0),
                    ContractId = reader.GetInt32(1),
                    StatementNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    StatementDate = reader.IsDBNull(3) ? DateTime.Now : Convert.ToDateTime(reader[3]),
                    GrossAmount = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader[4]),
                    VAT = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader[5]),
                    Deductions = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader[6]),
                    NetReceivableAmount = reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader[7]),
                    CreatedAt = reader.IsDBNull(8) ? DateTime.Now : Convert.ToDateTime(reader[8])
                });
            }
            return list;
        }

        public ClientStatement GetById(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand("SELECT * FROM ClientStatements WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new ClientStatement
                {
                    Id = reader.GetInt32(0),
                    ContractId = reader.GetInt32(1),
                    StatementNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    StatementDate = reader.IsDBNull(3) ? DateTime.Now : Convert.ToDateTime(reader[3]),
                    GrossAmount = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader[4]),
                    VAT = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader[5]),
                    Deductions = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader[6]),
                    NetReceivableAmount = reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader[7]),
                    CreatedAt = reader.IsDBNull(8) ? DateTime.Now : Convert.ToDateTime(reader[8])
                };
            }
            return null!;
        }

        // ✅ FIX P1: SCOPE_IDENTITY برای دریافت Id واقعی
        public void Add(ClientStatement s)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(@"
                INSERT INTO ClientStatements (ContractId, StatementNumber, StatementDate, GrossAmount, VAT, Deductions, NetReceivableAmount, CreatedAt)
                VALUES (@cid, @num, @date, @gross, @vat, @ded, @net, @created);
                SELECT SCOPE_IDENTITY();", conn);

            cmd.Parameters.AddWithValue("@cid", s.ContractId);
            cmd.Parameters.AddWithValue("@num", s.StatementNumber ?? "");
            cmd.Parameters.AddWithValue("@date", s.StatementDate);
            cmd.Parameters.AddWithValue("@gross", s.GrossAmount);
            cmd.Parameters.AddWithValue("@vat", s.VAT);
            cmd.Parameters.AddWithValue("@ded", s.Deductions);
            cmd.Parameters.AddWithValue("@net", s.NetReceivableAmount);
            cmd.Parameters.AddWithValue("@created", DateTime.Now);

            var result = cmd.ExecuteScalar();
            if (result != null && result != DBNull.Value)
                s.Id = Convert.ToInt32(result);
        }

        public void Update(ClientStatement s)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(@"
                UPDATE ClientStatements SET
                    StatementNumber = @num, StatementDate = @date,
                    GrossAmount = @gross, VAT = @vat, Deductions = @ded, NetReceivableAmount = @net
                WHERE Id = @id", conn);

            cmd.Parameters.AddWithValue("@num", s.StatementNumber ?? "");
            cmd.Parameters.AddWithValue("@date", s.StatementDate);
            cmd.Parameters.AddWithValue("@gross", s.GrossAmount);
            cmd.Parameters.AddWithValue("@vat", s.VAT);
            cmd.Parameters.AddWithValue("@ded", s.Deductions);
            cmd.Parameters.AddWithValue("@net", s.NetReceivableAmount);
            cmd.Parameters.AddWithValue("@id", s.Id);

            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM ClientStatements WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}