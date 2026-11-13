using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using SMP.Helpers;
using SMP.Models;
using SMP.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SMP.Data
{
    public class TafsiliRepository
    {
        private readonly string _connectionString;

        public TafsiliRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<(List<Tafsili> list, int totalCount)> GetPagedAsync(int page, int pageSize, string searchTerm = "")
        {
            var list = new List<Tafsili>();
            int totalCount = 0;

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string whereClause = " WHERE 1=1 ";
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                whereClause += " AND (Code LIKE @Search OR FirstName LIKE @Search OR LastName LIKE @Search OR CompanyName LIKE @Search OR NationalCode LIKE @Search OR NationalId LIKE @Search) ";
            }

            using (var countCmd = new SqlCommand($"SELECT COUNT(1) FROM Tafsilis {whereClause}", conn))
            {
                if (!string.IsNullOrWhiteSpace(searchTerm))
                    countCmd.Parameters.AddWithValue("@Search", $"%{searchTerm.Trim()}%");
                totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            }

            int offset = (page - 1) * pageSize;
            string sql = $@"
                SELECT Id, Code, ISNULL(PersonType, N'شخص حقیقی') AS PersonType, 
                       ISNULL(FirstName, '') AS FirstName, ISNULL(LastName, '') AS LastName, 
                       ISNULL(CompanyName, '') AS CompanyName,
                       Type, NationalCode, NationalId, EconomicCode, IsActive, CreatedAt,
                       (SELECT TOP 1 ShebaNumber FROM TafsiliBankAccounts WHERE TafsiliId = Tafsilis.Id AND IsMainAccount = 1) AS ShebaNumber
                FROM Tafsilis
                {whereClause}
                ORDER BY Type, Code
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Offset", offset);
            cmd.Parameters.AddWithValue("@PageSize", pageSize);

            if (!string.IsNullOrWhiteSpace(searchTerm))
                cmd.Parameters.AddWithValue("@Search", $"%{searchTerm.Trim()}%");

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new Tafsili
                {
                    Id = reader.GetInt32(0),
                    Code = reader.GetString(1),
                    PersonType = reader.GetString(2),
                    FirstName = reader.GetString(3),
                    LastName = reader.GetString(4),
                    CompanyName = reader.GetString(5),
                    Type = reader.GetString(6),
                    NationalCode = reader.IsDBNull(7) ? null : reader.GetString(7),
                    NationalId = reader.IsDBNull(8) ? null : reader.GetString(8),
                    EconomicCode = reader.IsDBNull(9) ? null : reader.GetString(9),
                    IsActive = reader.GetBoolean(10),
                    CreatedAt = reader.GetDateTime(11),
                    ShebaNumber = reader.IsDBNull(12) ? null : reader.GetString(12)
                });
            }

            return (list, totalCount);
        }

        public async Task<List<Tafsili>> GetAllAsync(string typeFilter = "")
        {
            var list = new List<Tafsili>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT Id, Code, ISNULL(PersonType, N'شخص حقیقی') AS PersonType, 
                       ISNULL(FirstName, '') AS FirstName, ISNULL(LastName, '') AS LastName, 
                       ISNULL(CompanyName, '') AS CompanyName,
                       Type, NationalCode, NationalId, EconomicCode, IsActive, CreatedAt,
                       (SELECT TOP 1 ShebaNumber FROM TafsiliBankAccounts WHERE TafsiliId = Tafsilis.Id AND IsMainAccount = 1) AS ShebaNumber
                FROM Tafsilis WHERE 1=1";
            if (!string.IsNullOrWhiteSpace(typeFilter))
                sql += " AND Type = @Type";
            sql += " ORDER BY Type, Code";

            using var cmd = new SqlCommand(sql, conn);
            if (!string.IsNullOrWhiteSpace(typeFilter))
                cmd.Parameters.AddWithValue("@Type", typeFilter);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new Tafsili
                {
                    Id = reader.GetInt32(0),
                    Code = reader.GetString(1),
                    PersonType = reader.GetString(2),
                    FirstName = reader.GetString(3),
                    LastName = reader.GetString(4),
                    CompanyName = reader.GetString(5),
                    Type = reader.GetString(6),
                    NationalCode = reader.IsDBNull(7) ? null : reader.GetString(7),
                    NationalId = reader.IsDBNull(8) ? null : reader.GetString(8),
                    EconomicCode = reader.IsDBNull(9) ? null : reader.GetString(9),
                    IsActive = reader.GetBoolean(10),
                    CreatedAt = reader.GetDateTime(11),
                    ShebaNumber = reader.IsDBNull(12) ? null : reader.GetString(12)
                });
            }
            return list;
        }

        public async Task<Tafsili?> GetByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT Id, Code, ISNULL(PersonType, N'شخص حقیقی') AS PersonType, 
                       ISNULL(FirstName, '') AS FirstName, ISNULL(LastName, '') AS LastName, 
                       ISNULL(CompanyName, '') AS CompanyName,
                       Type, NationalCode, NationalId, EconomicCode, IsActive, CreatedAt,
                       (SELECT TOP 1 ShebaNumber FROM TafsiliBankAccounts WHERE TafsiliId = Tafsilis.Id AND IsMainAccount = 1) AS ShebaNumber
                FROM Tafsilis WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Tafsili
                {
                    Id = reader.GetInt32(0),
                    Code = reader.GetString(1),
                    PersonType = reader.GetString(2),
                    FirstName = reader.GetString(3),
                    LastName = reader.GetString(4),
                    CompanyName = reader.GetString(5),
                    Type = reader.GetString(6),
                    NationalCode = reader.IsDBNull(7) ? null : reader.GetString(7),
                    NationalId = reader.IsDBNull(8) ? null : reader.GetString(8),
                    EconomicCode = reader.IsDBNull(9) ? null : reader.GetString(9),
                    IsActive = reader.GetBoolean(10),
                    CreatedAt = reader.GetDateTime(11),
                    ShebaNumber = reader.IsDBNull(12) ? null : reader.GetString(12)
                };
            }
            return null;
        }

        public async Task<List<TafsiliBankAccount>> GetBankAccountsAsync(int tafsiliId)
        {
            var list = new List<TafsiliBankAccount>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT Id, TafsiliId, ShebaNumber, BankName, IsMainAccount FROM TafsiliBankAccounts WHERE TafsiliId = @id", conn);
            cmd.Parameters.AddWithValue("@id", tafsiliId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new TafsiliBankAccount
                {
                    Id = reader.GetInt32(0),
                    TafsiliId = reader.GetInt32(1),
                    ShebaNumber = reader.GetString(2),
                    BankName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    IsMainAccount = reader.GetBoolean(4)
                });
            }
            return list;
        }

        // ═══════════════════════════════════════════════════════════════
        // ✅ FIX بحرانی: دیگر حساب‌های EmployeeBankAccounts کارمند حذف نمی‌شوند
        //
        //    سناریوی قبلی (خطرناک):
        //      ۱. کارمند در EmployeeEditForm دو حساب اضافه کرده (A و B)
        //      ۲. کسی تفصیلی این کارمند را ویرایش می‌کند و شباها را ذخیره می‌کند
        //      ۳. کد قبلی: DELETE FROM EmployeeBankAccounts → هر دو A و B حذف می‌شوند
        //      ۴. اگر تفصیلی ۰ شبا داشت، کارمند بدون حساب می‌ماند
        //      ۵. در فایل واریز بانک ظاهر نمی‌شود، حقوق پرداخت نمی‌شود
        //
        //    رفتار جدید (ایمن):
        //      • حذف هیچ‌وقت انجام نمی‌شود
        //      • شباهای جدید اضافه می‌شوند
        //      • IsMainAccount برای شباهای موجود به‌روز می‌شود (اگر تفصیلی می‌گوید main است)
        //      • اطمینان از وجود حداقل یک حساب اصلی
        // ═══════════════════════════════════════════════════════════════
        public async Task SaveBankAccountsAsync(int tafsiliId, List<TafsiliBankAccount> accounts)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();

            try
            {
                // ۱) همگام‌سازی TafsiliBankAccounts (به‌طور کامل بازسازی — این جدول فقط برای تفصیلی است)
                using (var del = new SqlCommand("DELETE FROM TafsiliBankAccounts WHERE TafsiliId = @id", conn, tx))
                {
                    del.Parameters.AddWithValue("@id", tafsiliId);
                    await del.ExecuteNonQueryAsync();
                }

                foreach (var acc in accounts)
                {
                    using var ins = new SqlCommand(@"
                        INSERT INTO TafsiliBankAccounts (TafsiliId, ShebaNumber, BankName, IsMainAccount)
                        VALUES (@id, @sheba, @bank, @isMain)", conn, tx);
                    ins.Parameters.AddWithValue("@id", tafsiliId);
                    ins.Parameters.AddWithValue("@sheba", acc.ShebaNumber);
                    ins.Parameters.AddWithValue("@bank", string.IsNullOrWhiteSpace(acc.BankName) ? DBNull.Value : acc.BankName);
                    ins.Parameters.AddWithValue("@isMain", acc.IsMainAccount ? 1 : 0);
                    await ins.ExecuteNonQueryAsync();
                }

                // ۲) اگر نقش تفصیلی «کارمند» است، حساب‌های بانکی کارمند را همگام کن — ولی به‌صورت ایمن
                string roleType = "";
                string nationalCode = "";

                using (var checkCmd = new SqlCommand("SELECT Type, NationalCode FROM Tafsilis WHERE Id = @id", conn, tx))
                {
                    checkCmd.Parameters.AddWithValue("@id", tafsiliId);
                    using var reader = await checkCmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        roleType = reader.IsDBNull(0) ? "" : reader.GetString(0);
                        nationalCode = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    }
                }

                if (roleType == "کارمند" && !string.IsNullOrWhiteSpace(nationalCode))
                {
                    int? employeeId = null;
                    using (var empCmd = new SqlCommand("SELECT TOP 1 Id FROM Employees WHERE NationalCode = @nc", conn, tx))
                    {
                        empCmd.Parameters.AddWithValue("@nc", nationalCode);
                        var result = await empCmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value) employeeId = Convert.ToInt32(result);
                    }

                    if (employeeId.HasValue)
                    {
                        // ✅ همگام‌سازی افزایشی — فقط اضافه/به‌روزرسانی، هرگز حذف
                        foreach (var acc in accounts)
                        {
                            bool exists = false;
                            using (var checkCmd = new SqlCommand(
                                "SELECT COUNT(1) FROM EmployeeBankAccounts WHERE EmployeeId = @empId AND ShebaNumber = @sheba",
                                conn, tx))
                            {
                                checkCmd.Parameters.AddWithValue("@empId", employeeId.Value);
                                checkCmd.Parameters.AddWithValue("@sheba", acc.ShebaNumber);
                                exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
                            }

                            if (exists)
                            {
                                // اگر تفصیلی می‌گوید این حساب main است، آن را main کن
                                if (acc.IsMainAccount)
                                {
                                    using (var reset = new SqlCommand(
                                        "UPDATE EmployeeBankAccounts SET IsMainAccount = 0 WHERE EmployeeId = @empId",
                                        conn, tx))
                                    {
                                        reset.Parameters.AddWithValue("@empId", employeeId.Value);
                                        await reset.ExecuteNonQueryAsync();
                                    }

                                    using (var setMain = new SqlCommand(
                                        "UPDATE EmployeeBankAccounts SET IsMainAccount = 1 WHERE EmployeeId = @empId AND ShebaNumber = @sheba",
                                        conn, tx))
                                    {
                                        setMain.Parameters.AddWithValue("@empId", employeeId.Value);
                                        setMain.Parameters.AddWithValue("@sheba", acc.ShebaNumber);
                                        await setMain.ExecuteNonQueryAsync();
                                    }
                                }
                            }
                            else
                            {
                                // حساب جدید — اضافه کن
                                using var insEmp = new SqlCommand(@"
                                    INSERT INTO EmployeeBankAccounts (EmployeeId, ShebaNumber, BankCode, AccountNumber, IsMainAccount)
                                    VALUES (@empId, @sheba, @bankCode, '', @isMain)", conn, tx);
                                insEmp.Parameters.AddWithValue("@empId", employeeId.Value);
                                insEmp.Parameters.AddWithValue("@sheba", acc.ShebaNumber);
                                insEmp.Parameters.AddWithValue("@bankCode", acc.ShebaNumber.Length >= 7 ? acc.ShebaNumber.Substring(4, 3) : "");
                                insEmp.Parameters.AddWithValue("@isMain", acc.IsMainAccount ? 1 : 0);
                                await insEmp.ExecuteNonQueryAsync();
                            }
                        }

                        // اطمینان از وجود حداقل یک حساب اصلی
                        using (var checkMain = new SqlCommand(
                            "SELECT COUNT(1) FROM EmployeeBankAccounts WHERE EmployeeId = @empId AND IsMainAccount = 1",
                            conn, tx))
                        {
                            checkMain.Parameters.AddWithValue("@empId", employeeId.Value);
                            int mainCount = Convert.ToInt32(await checkMain.ExecuteScalarAsync());

                            if (mainCount == 0)
                            {
                                using var promote = new SqlCommand(@"
                                    UPDATE EmployeeBankAccounts SET IsMainAccount = 1 
                                    WHERE Id = (SELECT TOP 1 Id FROM EmployeeBankAccounts WHERE EmployeeId = @empId ORDER BY Id)",
                                    conn, tx);
                                promote.Parameters.AddWithValue("@empId", employeeId.Value);
                                await promote.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task AddAsync(Tafsili t)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                INSERT INTO Tafsilis (Code, PersonType, FirstName, LastName, CompanyName, Name, Type, NationalCode, NationalId, EconomicCode, IsActive)
                VALUES (@Code, @PersonType, @FirstName, @LastName, @CompanyName, @Name, @Type, @NCode, @NId, @ECode, @IsActive);
                SELECT SCOPE_IDENTITY();";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Code", t.Code);
            cmd.Parameters.AddWithValue("@PersonType", t.PersonType);
            cmd.Parameters.AddWithValue("@FirstName", t.FirstName ?? "");
            cmd.Parameters.AddWithValue("@LastName", t.LastName ?? "");
            cmd.Parameters.AddWithValue("@CompanyName", t.CompanyName ?? "");
            cmd.Parameters.AddWithValue("@Name", t.Name);
            cmd.Parameters.AddWithValue("@Type", t.Type);
            cmd.Parameters.AddWithValue("@NCode", (object?)t.NationalCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NId", (object?)t.NationalId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ECode", (object?)t.EconomicCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IsActive", t.IsActive ? 1 : 0);

            t.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task UpdateAsync(Tafsili t)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                UPDATE Tafsilis SET 
                    Code = @Code, PersonType = @PersonType, FirstName = @FirstName, LastName = @LastName, 
                    CompanyName = @CompanyName, Name = @Name, Type = @Type, NationalCode = @NCode, 
                    NationalId = @NId, EconomicCode = @ECode, IsActive = @IsActive
                WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Code", t.Code);
            cmd.Parameters.AddWithValue("@PersonType", t.PersonType);
            cmd.Parameters.AddWithValue("@FirstName", t.FirstName ?? "");
            cmd.Parameters.AddWithValue("@LastName", t.LastName ?? "");
            cmd.Parameters.AddWithValue("@CompanyName", t.CompanyName ?? "");
            cmd.Parameters.AddWithValue("@Name", t.Name);
            cmd.Parameters.AddWithValue("@Type", t.Type);
            cmd.Parameters.AddWithValue("@NCode", (object?)t.NationalCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NId", (object?)t.NationalId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ECode", (object?)t.EconomicCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IsActive", t.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@Id", t.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = "DELETE FROM Tafsilis WHERE Id = @Id";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        // ImportFromExcelAsync — بدون تغییر
        public async Task<(int inserted, int updated, List<string> errors)> ImportFromExcelAsync(string filePath)
        {
            int inserted = 0;
            int updated = 0;
            var errors = new List<string>();

            using var workbook = new XLWorkbook(filePath);
            var ws = workbook.Worksheet(1);
            var rows = ws.RowsUsed().Skip(1);

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();

            try
            {
                int rowNum = 1;
                foreach (var row in rows)
                {
                    rowNum++;
                    string savePoint = $"Row_{rowNum}";
                    tx.Save(savePoint);

                    try
                    {
                        string code = row.Cell(1).GetString().Trim();
                        string personTypeRaw = row.Cell(2).GetString().Trim();
                        string firstName = row.Cell(3).GetString().Trim();
                        string lastName = row.Cell(4).GetString().Trim();
                        string companyName = row.Cell(5).GetString().Trim();
                        string nationalCode = StringHelper.ConvertToEnglishDigits(row.Cell(6).GetString().Trim());
                        string nationalId = StringHelper.ConvertToEnglishDigits(row.Cell(7).GetString().Trim());
                        string role = row.Cell(8).GetString().Trim();
                        string sheba = row.Cell(9).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(code))
                        {
                            errors.Add($"ردیف {rowNum}: کد تفصیلی خالی است.");
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(companyName))
                        {
                            errors.Add($"ردیف {rowNum}: باید یا نام یا نام شرکت پر شده باشد.");
                            continue;
                        }

                        string personType = "شخص حقیقی";
                        if (!string.IsNullOrWhiteSpace(personTypeRaw) && personTypeRaw.Contains("حقوقی"))
                            personType = "شخص حقوقی";

                        if (string.IsNullOrWhiteSpace(role)) role = "سایر نقش‌ها";

                        string name = personType == "شخص حقوقی"
                            ? companyName
                            : $"{firstName} {lastName}".Trim();

                        int? existingId = null;
                        using (var cmdCheck = new SqlCommand("SELECT TOP 1 Id FROM Tafsilis WHERE Code = @code", conn, tx))
                        {
                            cmdCheck.Parameters.AddWithValue("@code", code);
                            var res = await cmdCheck.ExecuteScalarAsync();
                            if (res != null && res != DBNull.Value)
                                existingId = Convert.ToInt32(res);
                        }

                        if (existingId.HasValue)
                        {
                            using var cmdUpd = new SqlCommand(@"
                                UPDATE Tafsilis SET 
                                    PersonType = @personType, FirstName = @firstName, LastName = @lastName,
                                    CompanyName = @companyName, Name = @name, Type = @role,
                                    NationalCode = @ncode, NationalId = @nid, IsActive = 1
                                WHERE Id = @id", conn, tx);
                            cmdUpd.Parameters.AddWithValue("@personType", personType);
                            cmdUpd.Parameters.AddWithValue("@firstName", firstName);
                            cmdUpd.Parameters.AddWithValue("@lastName", lastName);
                            cmdUpd.Parameters.AddWithValue("@companyName", companyName);
                            cmdUpd.Parameters.AddWithValue("@name", name);
                            cmdUpd.Parameters.AddWithValue("@role", role);
                            cmdUpd.Parameters.AddWithValue("@ncode", (object?)nationalCode ?? DBNull.Value);
                            cmdUpd.Parameters.AddWithValue("@nid", (object?)nationalId ?? DBNull.Value);
                            cmdUpd.Parameters.AddWithValue("@id", existingId.Value);
                            await cmdUpd.ExecuteNonQueryAsync();
                            updated++;
                        }
                        else
                        {
                            using var cmdIns = new SqlCommand(@"
                                INSERT INTO Tafsilis (Code, PersonType, FirstName, LastName, CompanyName, Name, Type, NationalCode, NationalId, IsActive)
                                VALUES (@code, @personType, @firstName, @lastName, @companyName, @name, @role, @ncode, @nid, 1);
                                SELECT SCOPE_IDENTITY();", conn, tx);
                            cmdIns.Parameters.AddWithValue("@code", code);
                            cmdIns.Parameters.AddWithValue("@personType", personType);
                            cmdIns.Parameters.AddWithValue("@firstName", firstName);
                            cmdIns.Parameters.AddWithValue("@lastName", lastName);
                            cmdIns.Parameters.AddWithValue("@companyName", companyName);
                            cmdIns.Parameters.AddWithValue("@name", name);
                            cmdIns.Parameters.AddWithValue("@role", role);
                            cmdIns.Parameters.AddWithValue("@ncode", (object?)nationalCode ?? DBNull.Value);
                            cmdIns.Parameters.AddWithValue("@nid", (object?)nationalId ?? DBNull.Value);
                            existingId = Convert.ToInt32(await cmdIns.ExecuteScalarAsync());
                            inserted++;
                        }

                        if (!string.IsNullOrWhiteSpace(sheba))
                        {
                            string normalized = BankHelper.NormalizeSheba(sheba);
                            if (normalized != null && normalized.Length == 24)
                            {
                                string fullSheba = "IR" + normalized;

                                using var cmdCheckSheba = new SqlCommand(
                                    "SELECT COUNT(1) FROM TafsiliBankAccounts WHERE TafsiliId = @id AND ShebaNumber = @sheba",
                                    conn, tx);
                                cmdCheckSheba.Parameters.AddWithValue("@id", existingId.Value);
                                cmdCheckSheba.Parameters.AddWithValue("@sheba", fullSheba);

                                if (Convert.ToInt32(await cmdCheckSheba.ExecuteScalarAsync()) == 0)
                                {
                                    using var cmdInsSheba = new SqlCommand(@"
                                        INSERT INTO TafsiliBankAccounts (TafsiliId, ShebaNumber, BankName, IsMainAccount)
                                        VALUES (@id, @sheba, @bankName, 
                                            CASE WHEN NOT EXISTS (SELECT 1 FROM TafsiliBankAccounts WHERE TafsiliId = @id) THEN 1 ELSE 0 END)",
                                        conn, tx);
                                    cmdInsSheba.Parameters.AddWithValue("@id", existingId.Value);
                                    cmdInsSheba.Parameters.AddWithValue("@sheba", fullSheba);
                                    cmdInsSheba.Parameters.AddWithValue("@bankName", BankHelper.GetBankNameFromSheba(normalized));
                                    await cmdInsSheba.ExecuteNonQueryAsync();
                                }
                            }
                            else
                            {
                                errors.Add($"ردیف {rowNum}: شماره شبا «{sheba}» نامعتبر است.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        try { tx.Rollback(savePoint); } catch { }
                        errors.Add($"ردیف {rowNum}: {ex.Message}");
                    }
                }

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            return (inserted, updated, errors);
        }
    }
}