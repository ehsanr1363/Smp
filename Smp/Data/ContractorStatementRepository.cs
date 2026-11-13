using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using SMP.Models;

namespace SMP.Data
{
    public class ContractorStatementRepository
    {
        private readonly string _connectionString;

        public ContractorStatementRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<ContractorStatement> GetByContractId(int contractId)
        {
            var list = new List<ContractorStatement>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string query = @"
                SELECT Id, ContractId, StatementNumber, StatementDate, GrossAmount, VAT, 
                       RetentionDeduction, InsuranceDeduction, OtherDeductions, AdvanceRecovery, NetPayableAmount, CreatedAt
                FROM ContractorStatements 
                WHERE ContractId = @cid 
                ORDER BY Id ASC";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@cid", contractId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ContractorStatement
                {
                    Id = reader.GetInt32(0),
                    ContractId = reader.GetInt32(1),
                    StatementNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    StatementDate = reader.IsDBNull(3) ? DateTime.Now : Convert.ToDateTime(reader[3]),
                    GrossAmount = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader[4]),
                    VAT = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader[5]),
                    RetentionDeduction = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader[6]),
                    InsuranceDeduction = reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader[7]),
                    OtherDeductions = reader.IsDBNull(8) ? 0 : Convert.ToDecimal(reader[8]),
                    AdvanceRecovery = reader.IsDBNull(9) ? 0 : Convert.ToDecimal(reader[9]),
                    NetPayableAmount = reader.IsDBNull(10) ? 0 : Convert.ToDecimal(reader[10]),
                    CreatedAt = reader.IsDBNull(11) ? DateTime.Now : Convert.ToDateTime(reader[11])
                });
            }
            return list;
        }

        public ContractorStatement GetById(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string query = @"
                SELECT Id, ContractId, StatementNumber, StatementDate, GrossAmount, VAT, 
                       RetentionDeduction, InsuranceDeduction, OtherDeductions, AdvanceRecovery, NetPayableAmount, CreatedAt
                FROM ContractorStatements 
                WHERE Id = @id";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new ContractorStatement
                {
                    Id = reader.GetInt32(0),
                    ContractId = reader.GetInt32(1),
                    StatementNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    StatementDate = reader.IsDBNull(3) ? DateTime.Now : Convert.ToDateTime(reader[3]),
                    GrossAmount = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader[4]),
                    VAT = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader[5]),
                    RetentionDeduction = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader[6]),
                    InsuranceDeduction = reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader[7]),
                    OtherDeductions = reader.IsDBNull(8) ? 0 : Convert.ToDecimal(reader[8]),
                    AdvanceRecovery = reader.IsDBNull(9) ? 0 : Convert.ToDecimal(reader[9]),
                    NetPayableAmount = reader.IsDBNull(10) ? 0 : Convert.ToDecimal(reader[10]),
                    CreatedAt = reader.IsDBNull(11) ? DateTime.Now : Convert.ToDateTime(reader[11])
                };
            }
            return null!;
        }

        // ✅ FIX P1: SCOPE_IDENTITY برای دریافت Id واقعی
        public void Add(ContractorStatement s)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(@"
                INSERT INTO ContractorStatements (ContractId, StatementNumber, StatementDate, GrossAmount, VAT, 
                                                 RetentionDeduction, InsuranceDeduction, OtherDeductions, AdvanceRecovery, NetPayableAmount, CreatedAt)
                VALUES (@cid, @num, @date, @gross, @vat, 
                        @retention, @insurance, @other, @advance, @net, @created);
                SELECT SCOPE_IDENTITY();", conn);

            cmd.Parameters.AddWithValue("@cid", s.ContractId);
            cmd.Parameters.AddWithValue("@num", s.StatementNumber ?? "");
            cmd.Parameters.AddWithValue("@date", s.StatementDate);
            cmd.Parameters.AddWithValue("@gross", s.GrossAmount);
            cmd.Parameters.AddWithValue("@vat", s.VAT);
            cmd.Parameters.AddWithValue("@retention", s.RetentionDeduction);
            cmd.Parameters.AddWithValue("@insurance", s.InsuranceDeduction);
            cmd.Parameters.AddWithValue("@other", s.OtherDeductions);
            cmd.Parameters.AddWithValue("@advance", s.AdvanceRecovery);
            cmd.Parameters.AddWithValue("@net", s.NetPayableAmount);
            cmd.Parameters.AddWithValue("@created", DateTime.Now);

            var result = cmd.ExecuteScalar();
            if (result != null && result != DBNull.Value)
                s.Id = Convert.ToInt32(result);
        }

        public void Update(ContractorStatement s)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(@"
                UPDATE ContractorStatements SET
                    StatementNumber = @num, StatementDate = @date,
                    GrossAmount = @gross, VAT = @vat,
                    RetentionDeduction = @retention, InsuranceDeduction = @insurance,
                    OtherDeductions = @other, AdvanceRecovery = @advance,
                    NetPayableAmount = @net
                WHERE Id = @id", conn);

            cmd.Parameters.AddWithValue("@num", s.StatementNumber ?? "");
            cmd.Parameters.AddWithValue("@date", s.StatementDate);
            cmd.Parameters.AddWithValue("@gross", s.GrossAmount);
            cmd.Parameters.AddWithValue("@vat", s.VAT);
            cmd.Parameters.AddWithValue("@retention", s.RetentionDeduction);
            cmd.Parameters.AddWithValue("@insurance", s.InsuranceDeduction);
            cmd.Parameters.AddWithValue("@other", s.OtherDeductions);
            cmd.Parameters.AddWithValue("@advance", s.AdvanceRecovery);
            cmd.Parameters.AddWithValue("@net", s.NetPayableAmount);
            cmd.Parameters.AddWithValue("@id", s.Id);

            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM ContractorStatements WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public (decimal TotalRetention, decimal TotalAdvance) GetDeductionsTotal(int contractId, int? excludeStatementId = null)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string query = @"
                SELECT ISNULL(SUM(RetentionDeduction), 0) AS TotalRetention, 
                       ISNULL(SUM(AdvanceRecovery), 0) AS TotalAdvance 
                FROM ContractorStatements 
                WHERE ContractId = @cid";

            if (excludeStatementId.HasValue)
            {
                query += " AND Id <> @excludeId";
            }

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@cid", contractId);
            if (excludeStatementId.HasValue)
            {
                cmd.Parameters.AddWithValue("@excludeId", excludeStatementId.Value);
            }

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return (Convert.ToDecimal(reader[0]), Convert.ToDecimal(reader[1]));
            }
            return (0, 0);
        }
    }
}