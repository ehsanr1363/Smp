using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using PersonnelApp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PersonnelApp.Data
{
    public class TafsiliRepository
    {
        private readonly string _connectionString;

        public TafsiliRepository(string connectionString)
        {
            _connectionString = connectionString;
            // 👈 EnsureTableExists حذف شد
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
                       Type, NationalCode, NationalId, EconomicCode, IsActive, CreatedAt
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
                    CreatedAt = reader.GetDateTime(11)
                });
            }
            return list;
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

        public async Task<int> RebuildNamesAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                UPDATE Tafsilis SET 
                    Name = CASE 
                        WHEN PersonType = N'شخص حقوقی' OR PersonType = N'حقوقی'
                            THEN ISNULL(CompanyName, '')
                        ELSE LTRIM(RTRIM(ISNULL(FirstName, '') + ' ' + ISNULL(LastName, '')))
                    END";

            using var cmd = new SqlCommand(sql, conn);
            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> ClearAllAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            string sql = "DELETE FROM Tafsilis";
            using var cmd = new SqlCommand(sql, conn);
            return await cmd.ExecuteNonQueryAsync();
        }

        // ... متد ImportFromExcelAsync دقیقا مثل قبل باقی می‌ماند ...
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
            using var transaction = conn.BeginTransaction();

            try
            {
                int rowNum = 1;
                foreach (var row in rows)
                {
                    rowNum++;
                    string savePointName = $"Row_{rowNum}";
                    transaction.Save(savePointName);

                    try
                    {
                        string code = row.Cell(1).GetString().Trim();
                        string personType = row.Cell(2).GetString().Trim();
                        string firstName = row.Cell(3).GetString().Trim();
                        string lastName = row.Cell(4).GetString().Trim();
                        string companyName = row.Cell(5).GetString().Trim();
                        string nationalCode = row.Cell(6).GetString().Trim();
                        string nationalId = row.Cell(7).GetString().Trim();
                        string role = row.Cell(8).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(code)) continue;

                        personType = personType.Replace("ي", "ی").Replace("ك", "ک");
                        role = role.Replace("ي", "ی").Replace("ك", "ک");

                        bool isLegal = personType == "شخص حقوقی" || personType == "حقوقی";
                        bool isReal = personType == "شخص حقیقی" || personType == "حقیقی";

                        if (!isLegal && !isReal)
                        {
                            errors.Add($"ردیف {rowNum}: نوع شخص باید «شخص حقیقی» یا «شخص حقوقی» باشد (کد: {code})");
                            continue;
                        }

                        string finalPersonType = isLegal ? "شخص حقوقی" : "شخص حقیقی";

                        if (string.IsNullOrWhiteSpace(role))
                        {
                            errors.Add($"ردیف {rowNum}: ستون «نقش» خالی است (کد: {code})");
                            continue;
                        }

                        string fullName;
                        if (isLegal)
                        {
                            if (string.IsNullOrWhiteSpace(companyName))
                            {
                                errors.Add($"ردیف {rowNum}: شخص حقوقی باید نام شرکت داشته باشد (کد: {code})");
                                continue;
                            }
                            fullName = companyName;
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
                            {
                                errors.Add($"ردیف {rowNum}: شخص حقیقی باید نام یا نام خانوادگی داشته باشد (کد: {code})");
                                continue;
                            }
                            fullName = $"{firstName} {lastName}".Trim();
                        }

                        int? existingId = null;
                        string checkSql = "SELECT Id FROM Tafsilis WHERE Code = @Code AND Type = @Type";
                        using (var checkCmd = new SqlCommand(checkSql, conn, transaction))
                        {
                            checkCmd.Parameters.AddWithValue("@Code", code);
                            checkCmd.Parameters.AddWithValue("@Type", role);
                            var result = await checkCmd.ExecuteScalarAsync();
                            if (result != null && result != DBNull.Value)
                                existingId = Convert.ToInt32(result);
                        }

                        if (existingId.HasValue)
                        {
                            string updateSql = @"
                                UPDATE Tafsilis SET 
                                    PersonType = @PersonType, FirstName = @FirstName, LastName = @LastName, 
                                    CompanyName = @CompanyName, Name = @Name, 
                                    NationalCode = @NCode, NationalId = @NId
                                WHERE Id = @Id";
                            using var updateCmd = new SqlCommand(updateSql, conn, transaction);
                            updateCmd.Parameters.AddWithValue("@PersonType", finalPersonType);
                            updateCmd.Parameters.AddWithValue("@FirstName", firstName);
                            updateCmd.Parameters.AddWithValue("@LastName", lastName);
                            updateCmd.Parameters.AddWithValue("@CompanyName", companyName);
                            updateCmd.Parameters.AddWithValue("@Name", fullName);
                            updateCmd.Parameters.AddWithValue("@NCode", string.IsNullOrWhiteSpace(nationalCode) ? DBNull.Value : nationalCode);
                            updateCmd.Parameters.AddWithValue("@NId", string.IsNullOrWhiteSpace(nationalId) ? DBNull.Value : nationalId);
                            updateCmd.Parameters.AddWithValue("@Id", existingId.Value);
                            await updateCmd.ExecuteNonQueryAsync();
                            updated++;
                        }
                        else
                        {
                            string insertSql = @"
                                INSERT INTO Tafsilis (Code, PersonType, FirstName, LastName, CompanyName, Name, Type, NationalCode, NationalId, IsActive)
                                VALUES (@Code, @PersonType, @FirstName, @LastName, @CompanyName, @Name, @Type, @NCode, @NId, 1)";
                            using var insertCmd = new SqlCommand(insertSql, conn, transaction);
                            insertCmd.Parameters.AddWithValue("@Code", code);
                            insertCmd.Parameters.AddWithValue("@PersonType", finalPersonType);
                            insertCmd.Parameters.AddWithValue("@FirstName", firstName);
                            insertCmd.Parameters.AddWithValue("@LastName", lastName);
                            insertCmd.Parameters.AddWithValue("@CompanyName", companyName);
                            insertCmd.Parameters.AddWithValue("@Name", fullName);
                            insertCmd.Parameters.AddWithValue("@Type", role);
                            insertCmd.Parameters.AddWithValue("@NCode", string.IsNullOrWhiteSpace(nationalCode) ? DBNull.Value : nationalCode);
                            insertCmd.Parameters.AddWithValue("@NId", string.IsNullOrWhiteSpace(nationalId) ? DBNull.Value : nationalId);
                            await insertCmd.ExecuteNonQueryAsync();
                            inserted++;
                        }
                    }
                    catch (Exception ex)
                    {
                        try { transaction.Rollback(savePointName); } catch { }
                        errors.Add($"ردیف {rowNum}: {ex.Message}");
                    }
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return (inserted, updated, errors);
        }
    }
}