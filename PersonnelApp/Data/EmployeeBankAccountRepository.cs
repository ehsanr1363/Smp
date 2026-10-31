using Microsoft.Data.SqlClient;
using PersonnelApp.Models;
using PersonnelApp.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PersonnelApp.Data
{
    public class EmployeeBankAccountRepository
    {
        private readonly string _connectionString;

        public EmployeeBankAccountRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<EmployeeBankAccount>> GetByEmployeeIdAsync(int employeeId)
        {
            var list = new List<EmployeeBankAccount>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string query = @"
                SELECT a.Id, a.EmployeeId, a.AccountNumber, a.ShebaNumber,
                       a.BankCode, b.BankName, a.IsMainAccount
                FROM EmployeeBankAccounts a
                LEFT JOIN Banks b ON a.BankCode = b.BankCode
                WHERE a.EmployeeId = @empId";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@empId", employeeId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string sheba = reader.IsDBNull(3) ? "" : reader.GetString(3);
                string dbName = reader.IsDBNull(5) ? "" : reader.GetString(5);
                string dbCode = reader.IsDBNull(4) ? "" : reader.GetString(4);

                if (string.IsNullOrWhiteSpace(dbName) || dbName.Contains("ناشناخته"))
                {
                    if (!string.IsNullOrWhiteSpace(sheba))
                    {
                        string computed = BankHelper.GetBankNameFromSheba(sheba);
                        if (!string.IsNullOrEmpty(computed) && !computed.Contains("ناشناخته"))
                            dbName = computed;
                    }
                    else
                    {
                        string byCode = BankHelper.GetBankNameByCode(dbCode);
                        if (!string.IsNullOrEmpty(byCode)) dbName = byCode;
                    }
                }

                if (string.IsNullOrWhiteSpace(dbName)) dbName = "نامشخص";

                list.Add(new EmployeeBankAccount
                {
                    Id = reader.GetInt32(0),
                    EmployeeId = reader.GetInt32(1),
                    AccountNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ShebaNumber = sheba,
                    BankCode = dbCode,
                    BankName = dbName,
                    IsMainAccount = !reader.IsDBNull(6) && Convert.ToInt32(reader[6]) == 1
                });
            }
            return list;
        }

        public async Task<List<EmployeeBankAccount>> GetAllAsync()
        {
            var list = new List<EmployeeBankAccount>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string query = @"
                SELECT a.Id, a.EmployeeId, a.AccountNumber, a.ShebaNumber,
                       a.BankCode, b.BankName, a.IsMainAccount
                FROM EmployeeBankAccounts a
                LEFT JOIN Banks b ON a.BankCode = b.BankCode";

            using var cmd = new SqlCommand(query, conn);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string sheba = reader.IsDBNull(3) ? "" : reader.GetString(3);
                string dbName = reader.IsDBNull(5) ? "" : reader.GetString(5);
                string dbCode = reader.IsDBNull(4) ? "" : reader.GetString(4);

                if (string.IsNullOrWhiteSpace(dbName) || dbName.Contains("ناشناخته"))
                {
                    if (!string.IsNullOrWhiteSpace(sheba))
                    {
                        string computed = BankHelper.GetBankNameFromSheba(sheba);
                        if (!string.IsNullOrEmpty(computed) && !computed.Contains("ناشناخته"))
                            dbName = computed;
                    }
                    else
                    {
                        string byCode = BankHelper.GetBankNameByCode(dbCode);
                        if (!string.IsNullOrEmpty(byCode)) dbName = byCode;
                    }
                }

                if (string.IsNullOrWhiteSpace(dbName)) dbName = "نامشخص";

                list.Add(new EmployeeBankAccount
                {
                    Id = reader.GetInt32(0),
                    EmployeeId = reader.GetInt32(1),
                    AccountNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ShebaNumber = sheba,
                    BankCode = dbCode,
                    BankName = dbName,
                    IsMainAccount = !reader.IsDBNull(6) && Convert.ToInt32(reader[6]) == 1
                });
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

            string bankName = BankHelper.GetBankNameByCode(bankCode);
            if (string.IsNullOrEmpty(bankName) && !string.IsNullOrWhiteSpace(account.ShebaNumber))
            {
                string fromSheba = BankHelper.GetBankNameFromSheba(account.ShebaNumber);
                if (!string.IsNullOrEmpty(fromSheba) && !fromSheba.Contains("ناشناخته"))
                    bankName = fromSheba;
            }
            if (string.IsNullOrEmpty(bankName)) bankName = "نامشخص";

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
                using (var countCmd = new SqlCommand("SELECT COUNT(1) FROM EmployeeBankAccounts WHERE EmployeeId = @empId", conn, tx))
                {
                    countCmd.Parameters.AddWithValue("@empId", account.EmployeeId);
                    existingCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                }

                bool isMain = account.IsMainAccount || existingCount == 0;

                using (var checkBank = new SqlCommand("SELECT COUNT(1) FROM Banks WHERE BankCode = @c", conn, tx))
                {
                    checkBank.Parameters.AddWithValue("@c", bankCode);
                    bool exists = Convert.ToInt32(await checkBank.ExecuteScalarAsync()) > 0;

                    if (exists)
                    {
                        using var upd = new SqlCommand("UPDATE Banks SET BankName = @n WHERE BankCode = @c", conn, tx);
                        upd.Parameters.AddWithValue("@n", bankName);
                        upd.Parameters.AddWithValue("@c", bankCode);
                        await upd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        using var ins = new SqlCommand("INSERT INTO Banks (BankCode, BankName) VALUES (@c, @n)", conn, tx);
                        ins.Parameters.AddWithValue("@c", bankCode);
                        ins.Parameters.AddWithValue("@n", bankName);
                        await ins.ExecuteNonQueryAsync();
                    }
                }

                if (isMain)
                {
                    using var reset = new SqlCommand("UPDATE EmployeeBankAccounts SET IsMainAccount = 0 WHERE EmployeeId = @id", conn, tx);
                    reset.Parameters.AddWithValue("@id", account.EmployeeId);
                    await reset.ExecuteNonQueryAsync();
                }

                // ✅ FIX P1: دریافت Id واقعی از SCOPE_IDENTITY
                using (var cmd = new SqlCommand(@"
                    INSERT INTO EmployeeBankAccounts (EmployeeId, AccountNumber, ShebaNumber, BankCode, IsMainAccount)
                    VALUES (@empId, @acc, @sheba, @code, @main);
                    SELECT SCOPE_IDENTITY();", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@empId", account.EmployeeId);
                    cmd.Parameters.AddWithValue("@acc", account.AccountNumber ?? "");
                    cmd.Parameters.AddWithValue("@sheba", account.ShebaNumber ?? "");
                    cmd.Parameters.AddWithValue("@code", bankCode);
                    cmd.Parameters.AddWithValue("@main", isMain ? 1 : 0);

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

            string bankName = BankHelper.GetBankNameByCode(bankCode);
            if (string.IsNullOrEmpty(bankName) && !string.IsNullOrWhiteSpace(account.ShebaNumber))
            {
                string fromSheba = BankHelper.GetBankNameFromSheba(account.ShebaNumber);
                if (!string.IsNullOrEmpty(fromSheba) && !fromSheba.Contains("ناشناخته"))
                    bankName = fromSheba;
            }
            if (string.IsNullOrEmpty(bankName)) bankName = "نامشخص";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();

            try
            {
                if (account.IsMainAccount)
                {
                    using var reset = new SqlCommand(
                        "UPDATE EmployeeBankAccounts SET IsMainAccount = 0 WHERE EmployeeId = @empId AND Id != @id", conn, tx);
                    reset.Parameters.AddWithValue("@empId", account.EmployeeId);
                    reset.Parameters.AddWithValue("@id", account.Id);
                    await reset.ExecuteNonQueryAsync();
                }

                using (var cmd = new SqlCommand(@"
                    UPDATE EmployeeBankAccounts 
                    SET ShebaNumber = @sheba, BankCode = @code, IsMainAccount = @main, AccountNumber = @acc
                    WHERE Id = @id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@sheba", account.ShebaNumber ?? "");
                    cmd.Parameters.AddWithValue("@code", bankCode);
                    cmd.Parameters.AddWithValue("@main", account.IsMainAccount ? 1 : 0);
                    cmd.Parameters.AddWithValue("@acc", account.AccountNumber ?? "");
                    cmd.Parameters.AddWithValue("@id", account.Id);
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var checkBank = new SqlCommand("SELECT COUNT(1) FROM Banks WHERE BankCode = @c", conn, tx))
                {
                    checkBank.Parameters.AddWithValue("@c", bankCode);
                    bool exists = Convert.ToInt32(await checkBank.ExecuteScalarAsync()) > 0;

                    if (!exists)
                    {
                        using var ins = new SqlCommand("INSERT INTO Banks (BankCode, BankName) VALUES (@c, @n)", conn, tx);
                        ins.Parameters.AddWithValue("@c", bankCode);
                        ins.Parameters.AddWithValue("@n", bankName);
                        await ins.ExecuteNonQueryAsync();
                    }
                }

                if (!account.IsMainAccount)
                {
                    using var checkMain = new SqlCommand("SELECT COUNT(1) FROM EmployeeBankAccounts WHERE EmployeeId = @empId AND IsMainAccount = 1", conn, tx);
                    checkMain.Parameters.AddWithValue("@empId", account.EmployeeId);
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

            int? employeeId = null;
            using (var find = new SqlCommand("SELECT EmployeeId FROM EmployeeBankAccounts WHERE Id = @id", conn))
            {
                find.Parameters.AddWithValue("@id", id);
                var result = await find.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                    employeeId = Convert.ToInt32(result);
            }

            using var tx = conn.BeginTransaction();

            try
            {
                using (var cmd = new SqlCommand("DELETE FROM EmployeeBankAccounts WHERE Id = @id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    await cmd.ExecuteNonQueryAsync();
                }

                if (employeeId.HasValue)
                {
                    using var checkMain = new SqlCommand("SELECT COUNT(1) FROM EmployeeBankAccounts WHERE EmployeeId = @empId AND IsMainAccount = 1", conn, tx);
                    checkMain.Parameters.AddWithValue("@empId", employeeId.Value);
                    int mainCount = Convert.ToInt32(await checkMain.ExecuteScalarAsync());

                    if (mainCount == 0)
                    {
                        using var promote = new SqlCommand("UPDATE EmployeeBankAccounts SET IsMainAccount = 1 WHERE Id = (SELECT TOP 1 Id FROM EmployeeBankAccounts WHERE EmployeeId = @empId)", conn, tx);
                        promote.Parameters.AddWithValue("@empId", employeeId.Value);
                        await promote.ExecuteNonQueryAsync();
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

        private static string NormalizeBankCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "";
            var digits = BankHelper.NormalizeDigits(code.Trim());
            var sb = new System.Text.StringBuilder();
            foreach (char c in digits)
                if (c >= '0' && c <= '9') sb.Append(c);
            return sb.ToString();
        }

        public int RepairBankData()
        {
            int fixedAccounts = 0;
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                var updates = new List<(int Id, string NewCode, string BankName)>();
                using (var read = new SqlCommand("SELECT Id, ShebaNumber, BankCode FROM EmployeeBankAccounts", conn, tx))
                using (var r = read.ExecuteReader())
                {
                    while (r.Read())
                    {
                        int id = r.GetInt32(0);
                        string sheba = r.IsDBNull(1) ? "" : r.GetString(1);
                        string dbCode = r.IsDBNull(2) ? "" : r.GetString(2);
                        string codeFromSheba = BankHelper.GetBankCodeFromSheba(sheba);
                        string codeNormalized = NormalizeBankCode(dbCode);
                        if (codeNormalized.Length != 3) codeNormalized = null;
                        string finalCode = codeFromSheba ?? codeNormalized;
                        if (string.IsNullOrEmpty(finalCode)) continue;
                        string name = BankHelper.GetBankNameByCode(finalCode) ?? "نامشخص";
                        updates.Add((id, finalCode, name));
                    }
                }

                foreach (var (id, code, name) in updates)
                {
                    using var updAcc = new SqlCommand("UPDATE EmployeeBankAccounts SET BankCode = @c WHERE Id = @id", conn, tx);
                    updAcc.Parameters.AddWithValue("@c", code);
                    updAcc.Parameters.AddWithValue("@id", id);
                    updAcc.ExecuteNonQuery();
                    fixedAccounts++;
                }

                using (var del = new SqlCommand("DELETE FROM Banks WHERE BankName IS NULL OR BankName = '' OR BankName LIKE N'%ناشناخته%' OR BankName = N'نامشخص'", conn, tx))
                {
                    del.ExecuteNonQuery();
                }

                foreach (var kvp in BankHelper.BankList)
                {
                    using var check = new SqlCommand("SELECT COUNT(1) FROM Banks WHERE BankCode = @c", conn, tx);
                    check.Parameters.AddWithValue("@c", kvp.Key);
                    bool exists = Convert.ToInt32(check.ExecuteScalar()) > 0;

                    if (exists)
                    {
                        using var upd = new SqlCommand("UPDATE Banks SET BankName = @n WHERE BankCode = @c", conn, tx);
                        upd.Parameters.AddWithValue("@n", kvp.Value);
                        upd.Parameters.AddWithValue("@c", kvp.Key);
                        upd.ExecuteNonQuery();
                    }
                    else
                    {
                        using var ins = new SqlCommand("INSERT INTO Banks (BankCode, BankName) VALUES (@c, @n)", conn, tx);
                        ins.Parameters.AddWithValue("@c", kvp.Key);
                        ins.Parameters.AddWithValue("@n", kvp.Value);
                        ins.ExecuteNonQuery();
                    }
                }

                tx.Commit();
                return fixedAccounts;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }
}