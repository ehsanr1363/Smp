using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SMP.Models;
using SMP.Helpers;
using Microsoft.Data.SqlClient;

namespace SMP.Data
{
    public class CashCheckRepository
    {
        private readonly string _connectionString;

        public CashCheckRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<(List<CashCheck> list, int totalCount)> GetPagedAsync(int page, int pageSize, string searchTerm = "")
        {
            var list = new List<CashCheck>();
            int totalCount = 0;

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string whereClause = "";
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                whereClause = @" WHERE CheckNumber LIKE @Search 
                                 OR BankName LIKE @Search 
                                 OR RecipientName LIKE @Search 
                                 OR PaymentScope LIKE @Search 
                                 OR CostType LIKE @Search 
                                 OR Description LIKE @Search 
                                 OR Status LIKE @Search 
                                 OR CheckType LIKE @Search";
            }

            using (var countCmd = new SqlCommand($"SELECT COUNT(1) FROM CashChecks {whereClause}", connection))
            {
                if (!string.IsNullOrWhiteSpace(searchTerm))
                    countCmd.Parameters.AddWithValue("@Search", $"%{searchTerm.Trim().Replace("[", "[[]").Replace("%", "[%]")}%");
                var countRes = await countCmd.ExecuteScalarAsync();
                totalCount = countRes != null && countRes != DBNull.Value ? Convert.ToInt32(countRes) : 0;
            }

            int offset = (page - 1) * pageSize;
            string sql = $@"
                SELECT Id, CheckNumber, BankName, DueDate, Amount, CheckType, Status, 
                       AccountId, Description, SayadNumber, RecipientName, RecipientNationalId, 
                       IsLegalEntity, CreatedAt, ContractId, ContractorContractId, ContractTitle, CostType, PaymentScope, ClientStatementId 
                FROM CashChecks 
                {whereClause}
                ORDER BY DueDate DESC, Id DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Offset", offset);
            cmd.Parameters.AddWithValue("@PageSize", pageSize);

            if (!string.IsNullOrWhiteSpace(searchTerm))
                cmd.Parameters.AddWithValue("@Search", $"%{searchTerm.Trim().Replace("[", "[[]").Replace("%", "[%]")}%");

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapReaderToCashCheck(reader));
            }

            return (list, totalCount);
        }

        public async Task<List<CashCheck>> GetByContractorContractIdAsync(int contractorContractId)
        {
            var checks = new List<CashCheck>();
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
                SELECT Id, CheckNumber, BankName, DueDate, Amount, CheckType, Status, 
                       AccountId, Description, SayadNumber, RecipientName, RecipientNationalId, 
                       IsLegalEntity, CreatedAt, ContractId, ContractorContractId, ContractTitle, CostType, PaymentScope, ClientStatementId 
                FROM CashChecks 
                WHERE ContractorContractId = @Id
                ORDER BY DueDate DESC, Id DESC";

            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Id", contractorContractId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                checks.Add(MapReaderToCashCheck(reader));
            }
            return checks;
        }

        public async Task<List<CashCheck>> GetByContractIdAsync(int contractId)
        {
            var checks = new List<CashCheck>();
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
                SELECT Id, CheckNumber, BankName, DueDate, Amount, CheckType, Status, 
                       AccountId, Description, SayadNumber, RecipientName, RecipientNationalId, 
                       IsLegalEntity, CreatedAt, ContractId, ContractorContractId, ContractTitle, CostType, PaymentScope, ClientStatementId 
                FROM CashChecks 
                WHERE ContractId = @ContractId OR ContractorContractId = @ContractId
                ORDER BY DueDate DESC, Id DESC";

            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@ContractId", contractId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                checks.Add(MapReaderToCashCheck(reader));
            }
            return checks;
        }

        public async Task<List<CashCheck>> GetByAccountIdAsync(int accountId)
        {
            var list = new List<CashCheck>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT Id, CheckNumber, BankName, DueDate, Amount, CheckType, Status, 
                       AccountId, Description, SayadNumber, RecipientName, RecipientNationalId, 
                       IsLegalEntity, CreatedAt, ContractId, ContractorContractId, ContractTitle, CostType, PaymentScope, ClientStatementId 
                FROM CashChecks 
                WHERE AccountId = @AccountId 
                ORDER BY DueDate DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@AccountId", accountId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapReaderToCashCheck(reader));
            }
            return list;
        }

        // ✅ FIX 1: استفاده از TRY_CAST به جای CAST برای جلوگیری از کرش در صورت وجود حروف در شماره چک
        public async Task<(string? NextCheckNumber, string? SayadSequence)> GetNextCheckAndSayadAsync(int accountId)
        {
            string? nextCheck = null;
            string? sayadSeq = null;

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string sayadQuery = "SELECT TOP 1 StartSayadNumber FROM Checkbooks WHERE AccountId = @AccountId ORDER BY CreatedAt DESC";
                using (var cmdSayad = new SqlCommand(sayadQuery, connection))
                {
                    cmdSayad.Parameters.AddWithValue("@AccountId", accountId);
                    var result = await cmdSayad.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        string fullSayad = result.ToString()!;
                        if (fullSayad.Length == 16) sayadSeq = fullSayad.Substring(4, 12);
                        else if (fullSayad.Length == 12) sayadSeq = fullSayad;
                    }
                }

                // استفاده از TRY_CAST برای جلوگیری از خطای تبدیل نوع
                string checkQuery = "SELECT MAX(TRY_CAST(CheckNumber AS INT)) FROM CashChecks WHERE AccountId = @AccountId";
                using (var cmdCheck = new SqlCommand(checkQuery, connection))
                {
                    cmdCheck.Parameters.AddWithValue("@AccountId", accountId);
                    var result = await cmdCheck.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value && int.TryParse(result.ToString(), out int maxCheck))
                    {
                        nextCheck = (maxCheck + 1).ToString();
                    }
                }
            }
            return (nextCheck, sayadSeq);
        }

        public async Task AddAsync(CashCheck check)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var tx = connection.BeginTransaction();

            try
            {
                string query = @"
                    INSERT INTO CashChecks 
                    (CheckNumber, BankName, DueDate, Amount, CheckType, Status, AccountId, Description, SayadNumber, 
                     RecipientName, RecipientNationalId, IsLegalEntity, CreatedAt, ContractId, ContractorContractId, ContractTitle, CostType, PaymentScope, ClientStatementId)
                    VALUES 
                    (@CheckNumber, @BankName, @DueDate, @Amount, @CheckType, @Status, @AccountId, @Description, @SayadNumber, 
                     @RecipientName, @RecipientNationalId, @IsLegalEntity, @CreatedAt, @ContractId, @ContractorContractId, @ContractTitle, @CostType, @PaymentScope, @ClientStatementId);
                    SELECT SCOPE_IDENTITY();";

                using var cmd = new SqlCommand(query, connection, tx);
                cmd.Parameters.AddWithValue("@CheckNumber", check.CheckNumber ?? string.Empty);
                cmd.Parameters.AddWithValue("@BankName", (object?)check.BankName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DueDate", check.DueDate);
                cmd.Parameters.AddWithValue("@Amount", check.Amount);
                cmd.Parameters.AddWithValue("@CheckType", check.CheckType ?? string.Empty);
                cmd.Parameters.AddWithValue("@Status", check.Status ?? "در جریان");
                cmd.Parameters.AddWithValue("@AccountId", (object?)check.AccountId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Description", (object?)check.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SayadNumber", (object?)check.SayadNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@RecipientName", (object?)check.RecipientName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@RecipientNationalId", (object?)check.RecipientNationalId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@IsLegalEntity", check.IsLegalEntity);
                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                cmd.Parameters.AddWithValue("@ContractId", (object?)check.ContractId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ContractorContractId", (object?)check.ContractorContractId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ContractTitle", (object?)check.ContractTitle ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CostType", (object?)check.CostType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PaymentScope", (object?)check.PaymentScope ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ClientStatementId", (object?)check.ClientStatementId ?? DBNull.Value);

                var id = await cmd.ExecuteScalarAsync();
                if (id != null && id != DBNull.Value) check.Id = Convert.ToInt32(id);

                bool isInvalid = check.Status == "باطل شده" || check.Status == "برگشتی";

                if (check.AccountId.HasValue && !isInvalid)
                {
                    string balanceSql = check.CheckType == DirectionTypes.Outgoing
                        ? "UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance - @Amount WHERE Id = @AccId"
                        : "UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance + @Amount WHERE Id = @AccId";

                    using var cmdBalance = new SqlCommand(balanceSql, connection, tx);
                    cmdBalance.Parameters.AddWithValue("@Amount", check.Amount);
                    cmdBalance.Parameters.AddWithValue("@AccId", check.AccountId.Value);
                    await cmdBalance.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<List<CashCheck>> GetAllAsync()
        {
            var checks = new List<CashCheck>();
            using (var connection = new SqlConnection(_connectionString))
            {
                string query = @"
                    SELECT Id, CheckNumber, BankName, DueDate, Amount, CheckType, Status, 
                           AccountId, Description, SayadNumber, RecipientName, RecipientNationalId, 
                           IsLegalEntity, CreatedAt, ContractId, ContractorContractId, ContractTitle, CostType, PaymentScope, ClientStatementId 
                    FROM CashChecks 
                    ORDER BY Id DESC";

                using (var cmd = new SqlCommand(query, connection))
                {
                    await connection.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync()) checks.Add(MapReaderToCashCheck(reader));
                    }
                }
            }
            return checks;
        }

        public async Task<CashCheck?> GetByIdAsync(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                string query = @"
                    SELECT Id, CheckNumber, BankName, DueDate, Amount, CheckType, Status, 
                           AccountId, Description, SayadNumber, RecipientName, RecipientNationalId, 
                           IsLegalEntity, CreatedAt, ContractId, ContractorContractId, ContractTitle, CostType, PaymentScope, ClientStatementId 
                    FROM CashChecks 
                    WHERE Id = @Id";

                using (var cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    await connection.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync()) return MapReaderToCashCheck(reader);
                    }
                }
            }
            return null;
        }

        public async Task UpdateAsync(CashCheck check)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var tx = connection.BeginTransaction();

            try
            {
                decimal oldAmount = 0;
                string oldType = "";
                string oldStatus = "";
                int? oldAccountId = null;

                using (var cmdOld = new SqlCommand("SELECT Amount, CheckType, Status, AccountId FROM CashChecks WHERE Id = @Id", connection, tx))
                {
                    cmdOld.Parameters.AddWithValue("@Id", check.Id);
                    using var reader = await cmdOld.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        throw new InvalidOperationException("چک مورد نظر یافت نشد.");

                    oldAmount = reader.IsDBNull(0) ? 0m : reader.GetDecimal(0);
                    oldType = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    oldStatus = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    oldAccountId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
                }

                bool oldIsInvalid = oldStatus == "باطل شده" || oldStatus == "برگشتی";
                bool newIsInvalid = check.Status == "باطل شده" || check.Status == "برگشتی";

                if (oldAccountId.HasValue && !oldIsInvalid)
                {
                    string revertSql = oldType == DirectionTypes.Outgoing
                        ? "UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance + @Amount WHERE Id = @AccId"
                        : "UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance - @Amount WHERE Id = @AccId";

                    using var cmdRevert = new SqlCommand(revertSql, connection, tx);
                    cmdRevert.Parameters.AddWithValue("@Amount", oldAmount);
                    cmdRevert.Parameters.AddWithValue("@AccId", oldAccountId.Value);
                    await cmdRevert.ExecuteNonQueryAsync();
                }

                string query = @"
                    UPDATE CashChecks 
                    SET DueDate = @DueDate, Amount = @Amount, Description = @Description, Status = @Status,
                        RecipientName = @RecipientName, RecipientNationalId = @RecipientNationalId, 
                        IsLegalEntity = @IsLegalEntity, ContractId = @ContractId, ContractorContractId = @ContractorContractId, 
                        ContractTitle = @ContractTitle, CostType = @CostType, PaymentScope = @PaymentScope
                    WHERE Id = @Id";

                using (var cmd = new SqlCommand(query, connection, tx))
                {
                    cmd.Parameters.AddWithValue("@Id", check.Id);
                    cmd.Parameters.AddWithValue("@DueDate", check.DueDate);
                    cmd.Parameters.AddWithValue("@Amount", check.Amount);
                    cmd.Parameters.AddWithValue("@Status", check.Status ?? "در جریان");
                    cmd.Parameters.AddWithValue("@Description", (object?)check.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@RecipientName", (object?)check.RecipientName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@RecipientNationalId", (object?)check.RecipientNationalId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsLegalEntity", check.IsLegalEntity);
                    cmd.Parameters.AddWithValue("@ContractId", (object?)check.ContractId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ContractorContractId", (object?)check.ContractorContractId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ContractTitle", (object?)check.ContractTitle ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CostType", (object?)check.CostType ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@PaymentScope", (object?)check.PaymentScope ?? DBNull.Value);

                    await cmd.ExecuteNonQueryAsync();
                }

                if (check.AccountId.HasValue && !newIsInvalid)
                {
                    string applySql = check.CheckType == DirectionTypes.Outgoing
                        ? "UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance - @Amount WHERE Id = @AccId"
                        : "UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance + @Amount WHERE Id = @AccId";

                    using var cmdApply = new SqlCommand(applySql, connection, tx);
                    cmdApply.Parameters.AddWithValue("@Amount", check.Amount);
                    cmdApply.Parameters.AddWithValue("@AccId", check.AccountId.Value);
                    await cmdApply.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateStatusBatchAsync(List<int> checkIds, string newStatus)
        {
            if (checkIds == null || checkIds.Count == 0) return;

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                string idList = string.Join(",", checkIds);
                var checks = new List<(int Id, decimal Amount, string CheckType, string OldStatus, int? AccountId)>();

                using (var cmd = new SqlCommand($"SELECT Id, Amount, CheckType, Status, AccountId FROM CashChecks WHERE Id IN ({idList})", connection, transaction))
                {
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        checks.Add((
                            reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                            reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
                            reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                            reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4)
                        ));
                    }
                }

                var adjustments = new Dictionary<int, decimal>();
                foreach (var c in checks)
                {
                    if (!c.AccountId.HasValue || c.OldStatus == newStatus) continue;

                    bool wasInvalid = c.OldStatus == "باطل شده" || c.OldStatus == "برگشتی";
                    bool isNowInvalid = newStatus == "باطل شده" || newStatus == "برگشتی";

                    if (!wasInvalid && isNowInvalid)
                    {
                        decimal refund = c.CheckType == DirectionTypes.Outgoing ? c.Amount : -c.Amount;
                        if (!adjustments.ContainsKey(c.AccountId.Value)) adjustments[c.AccountId.Value] = 0;
                        adjustments[c.AccountId.Value] += refund;
                    }
                    else if (wasInvalid && !isNowInvalid)
                    {
                        decimal deduct = c.CheckType == DirectionTypes.Outgoing ? -c.Amount : c.Amount;
                        if (!adjustments.ContainsKey(c.AccountId.Value)) adjustments[c.AccountId.Value] = 0;
                        adjustments[c.AccountId.Value] += deduct;
                    }
                }

                foreach (var adj in adjustments)
                {
                    if (adj.Value == 0) continue;
                    using var cmdAdj = new SqlCommand("UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance + @Val WHERE Id = @AccId", connection, transaction);
                    cmdAdj.Parameters.AddWithValue("@Val", adj.Value);
                    cmdAdj.Parameters.AddWithValue("@AccId", adj.Key);
                    await cmdAdj.ExecuteNonQueryAsync();
                }

                using (var cmdUpdate = new SqlCommand($"UPDATE CashChecks SET Status = @Status WHERE Id IN ({idList})", connection, transaction))
                {
                    cmdUpdate.Parameters.AddWithValue("@Status", newStatus ?? "در جریان");
                    await cmdUpdate.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteBatchAsync(List<int> checkIds)
        {
            if (checkIds == null || checkIds.Count == 0) return;

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                string idList = string.Join(",", checkIds);
                var checks = new List<(int Id, decimal Amount, string CheckType, string Status, int? AccountId)>();

                using (var cmd = new SqlCommand($"SELECT Id, Amount, CheckType, Status, AccountId FROM CashChecks WHERE Id IN ({idList})", connection, transaction))
                {
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        checks.Add((
                            reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                            reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
                            reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                            reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4)
                        ));
                    }
                }

                var adjustments = new Dictionary<int, decimal>();
                foreach (var c in checks)
                {
                    if (!c.AccountId.HasValue) continue;

                    bool isInvalid = c.Status == "باطل شده" || c.Status == "برگشتی";
                    if (!isInvalid)
                    {
                        decimal refund = c.CheckType == DirectionTypes.Outgoing ? c.Amount : -c.Amount;
                        if (!adjustments.ContainsKey(c.AccountId.Value)) adjustments[c.AccountId.Value] = 0;
                        adjustments[c.AccountId.Value] += refund;
                    }
                }

                foreach (var adj in adjustments)
                {
                    if (adj.Value == 0) continue;
                    using var cmdAdj = new SqlCommand("UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance + @Val WHERE Id = @AccId", connection, transaction);
                    cmdAdj.Parameters.AddWithValue("@Val", adj.Value);
                    cmdAdj.Parameters.AddWithValue("@AccId", adj.Key);
                    await cmdAdj.ExecuteNonQueryAsync();
                }

                using (var cmdDel = new SqlCommand($"DELETE FROM CashChecks WHERE Id IN ({idList})", connection, transaction))
                {
                    await cmdDel.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ✅ FIX 2: استفاده از خواندن بر اساس نام ستون برای پایداری در برابر تغییرات دیتابیس
        private CashCheck MapReaderToCashCheck(SqlDataReader reader)
        {
            return new CashCheck
            {
                Id = reader.IsDBNull(reader.GetOrdinal("Id")) ? 0 : Convert.ToInt32(reader["Id"]),
                CheckNumber = reader.IsDBNull(reader.GetOrdinal("CheckNumber")) ? string.Empty : reader["CheckNumber"].ToString()!,
                BankName = reader.IsDBNull(reader.GetOrdinal("BankName")) ? null : reader["BankName"].ToString(),
                DueDate = reader.IsDBNull(reader.GetOrdinal("DueDate")) ? DateTime.Now : Convert.ToDateTime(reader["DueDate"]),
                Amount = reader.IsDBNull(reader.GetOrdinal("Amount")) ? 0m : Convert.ToDecimal(reader["Amount"]),
                CheckType = reader.IsDBNull(reader.GetOrdinal("CheckType")) ? string.Empty : reader["CheckType"].ToString()!,
                Status = reader.IsDBNull(reader.GetOrdinal("Status")) ? "در جریان" : reader["Status"].ToString()!,
                AccountId = reader.IsDBNull(reader.GetOrdinal("AccountId")) ? (int?)null : Convert.ToInt32(reader["AccountId"]),
                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader["Description"].ToString(),
                SayadNumber = reader.IsDBNull(reader.GetOrdinal("SayadNumber")) ? null : reader["SayadNumber"].ToString(),
                RecipientName = reader.IsDBNull(reader.GetOrdinal("RecipientName")) ? null : reader["RecipientName"].ToString(),
                RecipientNationalId = reader.IsDBNull(reader.GetOrdinal("RecipientNationalId")) ? null : reader["RecipientNationalId"].ToString(),
                IsLegalEntity = !reader.IsDBNull(reader.GetOrdinal("IsLegalEntity")) && Convert.ToBoolean(reader["IsLegalEntity"]),
                CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTime.Now : Convert.ToDateTime(reader["CreatedAt"]),
                ContractId = reader.IsDBNull(reader.GetOrdinal("ContractId")) ? (int?)null : Convert.ToInt32(reader["ContractId"]),
                ContractorContractId = reader.IsDBNull(reader.GetOrdinal("ContractorContractId")) ? (int?)null : Convert.ToInt32(reader["ContractorContractId"]),
                ContractTitle = reader.IsDBNull(reader.GetOrdinal("ContractTitle")) ? null : reader["ContractTitle"].ToString(),
                CostType = reader.IsDBNull(reader.GetOrdinal("CostType")) ? null : reader["CostType"].ToString(),
                PaymentScope = reader.IsDBNull(reader.GetOrdinal("PaymentScope")) ? null : reader["PaymentScope"].ToString(),
                ClientStatementId = reader.IsDBNull(reader.GetOrdinal("ClientStatementId")) ? (int?)null : Convert.ToInt32(reader["ClientStatementId"])
            };
        }
    }
}