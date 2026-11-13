using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using SMP.Models;

namespace SMP.Data
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
                SELECT Id, ContractId, StatementNumber, StatementDate, GrossAmount, VAT, 
                       RetentionDeduction, InsuranceDeduction, AdvanceRecovery, OtherDeductions, NetReceivableAmount, CreatedAt
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
                    RetentionDeduction = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader[6]),
                    InsuranceDeduction = reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader[7]),
                    AdvanceRecovery = reader.IsDBNull(8) ? 0 : Convert.ToDecimal(reader[8]),
                    OtherDeductions = reader.IsDBNull(9) ? 0 : Convert.ToDecimal(reader[9]),
                    NetReceivableAmount = reader.IsDBNull(10) ? 0 : Convert.ToDecimal(reader[10]),
                    CreatedAt = reader.IsDBNull(11) ? DateTime.Now : Convert.ToDateTime(reader[11])
                });
            }
            return list;
        }

        public ClientStatement GetById(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string query = @"
                SELECT Id, ContractId, StatementNumber, StatementDate, GrossAmount, VAT, 
                       RetentionDeduction, InsuranceDeduction, AdvanceRecovery, OtherDeductions, NetReceivableAmount, CreatedAt
                FROM ClientStatements 
                WHERE Id = @id";

            using var cmd = new SqlCommand(query, conn);
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
                    RetentionDeduction = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader[6]),
                    InsuranceDeduction = reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader[7]),
                    AdvanceRecovery = reader.IsDBNull(8) ? 0 : Convert.ToDecimal(reader[8]),
                    OtherDeductions = reader.IsDBNull(9) ? 0 : Convert.ToDecimal(reader[9]),
                    NetReceivableAmount = reader.IsDBNull(10) ? 0 : Convert.ToDecimal(reader[10]),
                    CreatedAt = reader.IsDBNull(11) ? DateTime.Now : Convert.ToDateTime(reader[11])
                };
            }
            return null!;
        }

        public void Add(ClientStatement s)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(@"
                INSERT INTO ClientStatements 
                (ContractId, StatementNumber, StatementDate, GrossAmount, VAT, RetentionDeduction, InsuranceDeduction, AdvanceRecovery, OtherDeductions, NetReceivableAmount, CreatedAt)
                VALUES 
                (@cid, @num, @date, @gross, @vat, @retention, @insurance, @advance, @other, @net, @created);
                SELECT SCOPE_IDENTITY();", conn);

            cmd.Parameters.AddWithValue("@cid", s.ContractId);
            cmd.Parameters.AddWithValue("@num", s.StatementNumber ?? "");
            cmd.Parameters.AddWithValue("@date", s.StatementDate);
            cmd.Parameters.AddWithValue("@gross", s.GrossAmount);
            cmd.Parameters.AddWithValue("@vat", s.VAT);
            cmd.Parameters.AddWithValue("@retention", s.RetentionDeduction);
            cmd.Parameters.AddWithValue("@insurance", s.InsuranceDeduction);
            cmd.Parameters.AddWithValue("@advance", s.AdvanceRecovery);
            cmd.Parameters.AddWithValue("@other", s.OtherDeductions);
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
                    GrossAmount = @gross, VAT = @vat, 
                    RetentionDeduction = @retention, InsuranceDeduction = @insurance, 
                    AdvanceRecovery = @advance, OtherDeductions = @other,
                    NetReceivableAmount = @net
                WHERE Id = @id", conn);

            cmd.Parameters.AddWithValue("@num", s.StatementNumber ?? "");
            cmd.Parameters.AddWithValue("@date", s.StatementDate);
            cmd.Parameters.AddWithValue("@gross", s.GrossAmount);
            cmd.Parameters.AddWithValue("@vat", s.VAT);
            cmd.Parameters.AddWithValue("@retention", s.RetentionDeduction);
            cmd.Parameters.AddWithValue("@insurance", s.InsuranceDeduction);
            cmd.Parameters.AddWithValue("@advance", s.AdvanceRecovery);
            cmd.Parameters.AddWithValue("@other", s.OtherDeductions);
            cmd.Parameters.AddWithValue("@net", s.NetReceivableAmount);
            cmd.Parameters.AddWithValue("@id", s.Id);

            cmd.ExecuteNonQuery();
        }

        // ✅ متد به‌روزرسانی شده: پشتیبانی از چک‌ها (checkIds)
        public void LinkExpensesToStatement(int statementId, List<int> transactionIds, List<int> projectExpenseIds, List<int> checkIds = null)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                // ۱. آزادسازی هزینه‌هایی که قبلاً به این صورت وضعیت متصل بودند
                using (var cmdUnlink1 = new SqlCommand("UPDATE CashTransactions SET ClientStatementId = NULL WHERE ClientStatementId = @stId", conn, tx))
                {
                    cmdUnlink1.Parameters.AddWithValue("@stId", statementId);
                    cmdUnlink1.ExecuteNonQuery();
                }

                using (var cmdUnlink2 = new SqlCommand("UPDATE ProjectExpenses SET ClientStatementId = NULL WHERE ClientStatementId = @stId", conn, tx))
                {
                    cmdUnlink2.Parameters.AddWithValue("@stId", statementId);
                    cmdUnlink2.ExecuteNonQuery();
                }

                using (var cmdUnlink3 = new SqlCommand("UPDATE CashChecks SET ClientStatementId = NULL WHERE ClientStatementId = @stId", conn, tx))
                {
                    cmdUnlink3.Parameters.AddWithValue("@stId", statementId);
                    cmdUnlink3.ExecuteNonQuery();
                }

                // ۲. اتصال هزینه‌های جدید (تراکنش‌ها)
                if (transactionIds != null && transactionIds.Count > 0)
                {
                    foreach (var id in transactionIds)
                    {
                        using var cmd = new SqlCommand("UPDATE CashTransactions SET ClientStatementId = @stId WHERE Id = @id", conn, tx);
                        cmd.Parameters.AddWithValue("@stId", statementId);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }
                }

                // ۳. اتصال هزینه‌های جدید (فاکتورها)
                if (projectExpenseIds != null && projectExpenseIds.Count > 0)
                {
                    foreach (var id in projectExpenseIds)
                    {
                        using var cmd = new SqlCommand("UPDATE ProjectExpenses SET ClientStatementId = @stId WHERE Id = @id", conn, tx);
                        cmd.Parameters.AddWithValue("@stId", statementId);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }
                }

                // ۴. اتصال هزینه‌های جدید (چک‌ها)
                if (checkIds != null && checkIds.Count > 0)
                {
                    foreach (var id in checkIds)
                    {
                        using var cmd = new SqlCommand("UPDATE CashChecks SET ClientStatementId = @stId WHERE Id = @id", conn, tx);
                        cmd.Parameters.AddWithValue("@stId", statementId);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public void Delete(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                // آزادسازی ارتباط تراکنش‌ها
                using (var cmd1 = new SqlCommand("UPDATE CashTransactions SET ClientStatementId = NULL WHERE ClientStatementId = @id", conn, tx))
                {
                    cmd1.Parameters.AddWithValue("@id", id);
                    cmd1.ExecuteNonQuery();
                }

                // آزادسازی ارتباط فاکتورها
                using (var cmd2 = new SqlCommand("UPDATE ProjectExpenses SET ClientStatementId = NULL WHERE ClientStatementId = @id", conn, tx))
                {
                    cmd2.Parameters.AddWithValue("@id", id);
                    cmd2.ExecuteNonQuery();
                }

                // آزادسازی ارتباط چک‌ها
                using (var cmd3 = new SqlCommand("UPDATE CashChecks SET ClientStatementId = NULL WHERE ClientStatementId = @id", conn, tx))
                {
                    cmd3.Parameters.AddWithValue("@id", id);
                    cmd3.ExecuteNonQuery();
                }

                // حذف صورت وضعیت
                using (var cmdDel = new SqlCommand("DELETE FROM ClientStatements WHERE Id = @id", conn, tx))
                {
                    cmdDel.Parameters.AddWithValue("@id", id);
                    cmdDel.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }
}