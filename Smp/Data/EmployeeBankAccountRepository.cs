using Microsoft.Data.SqlClient;
using SMP.Helpers;
using SMP.Models;
using SMP.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SMP.Data
{
    public class EmployeeBankAccountRepository
    {
        private readonly string _connectionString;

        public EmployeeBankAccountRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<EmployeeBankAccount>> GetByEmployeeIdsAsync(List<int> employeeIds)
        {
            var list = new List<EmployeeBankAccount>();
            if (employeeIds == null || employeeIds.Count == 0) return list;

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string ids = string.Join(",", employeeIds);
            string query = $@"
                SELECT Id, EmployeeId, AccountNumber, ShebaNumber, BankCode, IsMainAccount, ISNULL(AccountType, N'حقوق') AS AccountType, ISNULL(IsActive, 1) AS IsActive
                FROM EmployeeBankAccounts
                WHERE EmployeeId IN ({ids})";

            using var cmd = new SqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapToBankAccount(reader));
            }
            return list;
        }

        public async Task<List<EmployeeBankAccount>> GetByEmployeeIdAsync(int employeeId)
        {
            var list = new List<EmployeeBankAccount>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string query = @"
                SELECT Id, EmployeeId, AccountNumber, ShebaNumber, BankCode, IsMainAccount, ISNULL(AccountType, N'حقوق') AS AccountType, ISNULL(IsActive, 1) AS IsActive
                FROM EmployeeBankAccounts
                WHERE EmployeeId = @empId";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@empId", employeeId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapToBankAccount(reader));
            }
            return list;
        }

        public async Task<List<EmployeeBankAccount>> GetAllAsync()
        {
            var list = new List<EmployeeBankAccount>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string query = @"
                SELECT Id, EmployeeId, AccountNumber, ShebaNumber, BankCode, IsMainAccount, ISNULL(AccountType, N'حقوق') AS AccountType, ISNULL(IsActive, 1) AS IsActive
                FROM EmployeeBankAccounts";

            using var cmd = new SqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapToBankAccount(reader));
            }
            return list;
        }

        public async Task AddAsync(EmployeeBankAccount account)
        {
            string bankCode = NormalizeBankCode(account.BankCode);

            if (string.IsNullOrEmpty(bankCode) && !string.IsNullOrWhiteSpace(account.ShebaNumber))
                bankCode = BankHelper.GetBankCodeFromSheba(account.ShebaNumber) ?? "";

            if (string.IsNullOrEmpty(bankCode) || bankCode.Length != 3)
                throw new Exception("کد بانک قابل تشخیص نیست. لطفاً شبا یا کد بانک را بررسی کنید.");

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();

            try
            {
                using (var checkEmp = new SqlCommand("SELECT COUNT(1) FROM Employees WHERE Id = @id", conn, tx))
                {
                    checkEmp.Parameters.AddWithValue("@id", account.EmployeeId);
                    if (Convert.ToInt32(await checkEmp.ExecuteScalarAsync()) == 0)
                        throw new Exception($"کارمند با شناسه {account.EmployeeId} در سیستم وجود ندارد.");
                }

                int existingCount;
                using (var countCmd = new SqlCommand("SELECT COUNT(1) FROM EmployeeBankAccounts WHERE EmployeeId = @empId AND AccountType = @type", conn, tx))
                {
                    countCmd.Parameters.AddWithValue("@empId", account.EmployeeId);
                    countCmd.Parameters.AddWithValue("@type", account.AccountType);
                    existingCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                }

                bool isMain = account.IsMainAccount || existingCount == 0;

                if (isMain)
                {
                    using var reset = new SqlCommand("UPDATE EmployeeBankAccounts SET IsMainAccount = 0 WHERE EmployeeId = @id AND AccountType = @type", conn, tx);
                    reset.Parameters.AddWithValue("@id", account.EmployeeId);
                    reset.Parameters.AddWithValue("@type", account.AccountType);
                    await reset.ExecuteNonQueryAsync();
                }

                using (var cmd = new SqlCommand(@"
                    INSERT INTO EmployeeBankAccounts (EmployeeId, AccountNumber, ShebaNumber, BankCode, IsMainAccount, AccountType, IsActive)
                    VALUES (@empId, @acc, @sheba, @code, @main, @type, @isActive);
                    SELECT SCOPE_IDENTITY();", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@empId", account.EmployeeId);
                    cmd.Parameters.AddWithValue("@acc", account.AccountNumber ?? "");
                    cmd.Parameters.AddWithValue("@sheba", account.ShebaNumber ?? "");
                    cmd.Parameters.AddWithValue("@code", bankCode);
                    cmd.Parameters.AddWithValue("@main", isMain ? 1 : 0);
                    cmd.Parameters.AddWithValue("@type", account.AccountType);
                    cmd.Parameters.AddWithValue("@isActive", account.IsActive ? 1 : 0);

                    account.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task UpdateAsync(EmployeeBankAccount account)
        {
            string bankCode = NormalizeBankCode(account.BankCode);

            if (string.IsNullOrEmpty(bankCode) && !string.IsNullOrWhiteSpace(account.ShebaNumber))
                bankCode = BankHelper.GetBankCodeFromSheba(account.ShebaNumber) ?? "";

            if (string.IsNullOrEmpty(bankCode) || bankCode.Length != 3)
                throw new Exception("کد بانک قابل تشخیص نیست. لطفاً شبا یا کد بانک را بررسی کنید.");

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();

            try
            {
                if (account.IsMainAccount)
                {
                    using var reset = new SqlCommand(
                        "UPDATE EmployeeBankAccounts SET IsMainAccount = 0 WHERE EmployeeId = @empId AND AccountType = @type AND Id != @id", conn, tx);
                    reset.Parameters.AddWithValue("@empId", account.EmployeeId);
                    reset.Parameters.AddWithValue("@type", account.AccountType);
                    reset.Parameters.AddWithValue("@id", account.Id);
                    await reset.ExecuteNonQueryAsync();
                }

                using (var cmd = new SqlCommand(@"
                    UPDATE EmployeeBankAccounts 
                    SET ShebaNumber = @sheba, BankCode = @code, IsMainAccount = @main, AccountNumber = @acc, AccountType = @type, IsActive = @isActive
                    WHERE Id = @id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@sheba", account.ShebaNumber ?? "");
                    cmd.Parameters.AddWithValue("@code", bankCode);
                    cmd.Parameters.AddWithValue("@main", account.IsMainAccount ? 1 : 0);
                    cmd.Parameters.AddWithValue("@acc", account.AccountNumber ?? "");
                    cmd.Parameters.AddWithValue("@type", account.AccountType);
                    cmd.Parameters.AddWithValue("@isActive", account.IsActive ? 1 : 0);
                    cmd.Parameters.AddWithValue("@id", account.Id);
                    await cmd.ExecuteNonQueryAsync();
                }

                if (!account.IsMainAccount)
                {
                    using var checkMain = new SqlCommand("SELECT COUNT(1) FROM EmployeeBankAccounts WHERE EmployeeId = @empId AND AccountType = @type AND IsMainAccount = 1", conn, tx);
                    checkMain.Parameters.AddWithValue("@empId", account.EmployeeId);
                    checkMain.Parameters.AddWithValue("@type", account.AccountType);
                    int mainCount = Convert.ToInt32(await checkMain.ExecuteScalarAsync());

                    if (mainCount == 0)
                    {
                        using var forceMain = new SqlCommand("UPDATE EmployeeBankAccounts SET IsMainAccount = 1 WHERE Id = @id", conn, tx);
                        forceMain.Parameters.AddWithValue("@id", account.Id);
                        await forceMain.ExecuteNonQueryAsync();
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

        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("DELETE FROM EmployeeBankAccounts WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        private EmployeeBankAccount MapToBankAccount(SqlDataReader reader)
        {
            string sheba = reader.IsDBNull(3) ? "" : reader.GetString(3);
            string dbCode = reader.IsDBNull(4) ? "" : reader.GetString(4);
            string dbName = BankHelper.GetBankNameByCode(dbCode);

            if (string.IsNullOrWhiteSpace(dbName) || dbName.Contains("ناشناخته"))
            {
                if (!string.IsNullOrWhiteSpace(sheba))
                {
                    string computed = BankHelper.GetBankNameFromSheba(sheba);
                    if (!string.IsNullOrEmpty(computed) && !computed.Contains("ناشناخته")) dbName = computed;
                }
            }
            if (string.IsNullOrWhiteSpace(dbName)) dbName = "نامشخص";

            return new EmployeeBankAccount
            {
                Id = reader.GetInt32(0),
                EmployeeId = reader.GetInt32(1),
                AccountNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                ShebaNumber = sheba,
                BankCode = dbCode,
                BankName = dbName,
                IsMainAccount = !reader.IsDBNull(5) && Convert.ToInt32(reader[5]) == 1,
                AccountType = reader.GetString(6),
                IsActive = !reader.IsDBNull(7) && Convert.ToBoolean(reader[7])
            };
        }

        private static string NormalizeBankCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "";
            var digits = BankHelper.NormalizeDigits(code.Trim());
            var sb = new System.Text.StringBuilder();
            foreach (char c in digits)
                if (c >= '0' && c <= '9') sb.Append(c);
            return sb.ToString();
        }
    }
}