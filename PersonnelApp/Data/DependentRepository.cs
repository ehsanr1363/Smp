using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using PersonnelApp.Models;

namespace PersonnelApp.Data
{
    public class DependentRepository
    {
        private readonly string _connectionString;

        public DependentRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<Dependent>> GetByEmployeeIdAsync(int employeeId)
        {
            var list = new List<Dependent>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT Id, EmployeeId, FirstName, LastName, NationalCode, BirthCertificateNumber, 
                       FatherName, BirthDate, Gender, BirthPlace, Relation, HasSupplementaryInsurance 
                FROM Dependents WHERE EmployeeId = @empId", conn);
            cmd.Parameters.AddWithValue("@empId", employeeId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new Dependent
                {
                    Id = reader.GetInt32(0),
                    EmployeeId = reader.GetInt32(1),
                    FirstName = reader.GetString(2),
                    LastName = reader.GetString(3),
                    NationalCode = reader.IsDBNull(4) ? null : reader.GetString(4),
                    BirthCertificateNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                    FatherName = reader.IsDBNull(6) ? null : reader.GetString(6),

                    // ✅ استفاده از GetDateTime
                    BirthDate = reader.IsDBNull(7) ? null : reader.GetDateTime(7),

                    Gender = reader.IsDBNull(8) ? null : reader.GetString(8),
                    BirthPlace = reader.IsDBNull(9) ? null : reader.GetString(9),
                    Relation = reader.IsDBNull(10) ? null : reader.GetString(10),
                    HasSupplementaryInsurance = !reader.IsDBNull(11) && Convert.ToInt32(reader[11]) == 1
                });
            }
            return list;
        }

        public async Task<List<Dependent>> GetAllAsync()
        {
            var list = new List<Dependent>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT Id, EmployeeId, FirstName, LastName, NationalCode, BirthCertificateNumber, 
                       FatherName, BirthDate, Gender, BirthPlace, Relation, HasSupplementaryInsurance 
                FROM Dependents", conn);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new Dependent
                {
                    Id = reader.GetInt32(0),
                    EmployeeId = reader.GetInt32(1),
                    FirstName = reader.GetString(2),
                    LastName = reader.GetString(3),
                    NationalCode = reader.IsDBNull(4) ? null : reader.GetString(4),
                    BirthCertificateNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                    FatherName = reader.IsDBNull(6) ? null : reader.GetString(6),

                    // ✅ استفاده از GetDateTime
                    BirthDate = reader.IsDBNull(7) ? null : reader.GetDateTime(7),

                    Gender = reader.IsDBNull(8) ? null : reader.GetString(8),
                    BirthPlace = reader.IsDBNull(9) ? null : reader.GetString(9),
                    Relation = reader.IsDBNull(10) ? null : reader.GetString(10),
                    HasSupplementaryInsurance = !reader.IsDBNull(11) && Convert.ToInt32(reader[11]) == 1
                });
            }
            return list;
        }

        public async Task<int?> GetEmployeeIdByNationalCodeAsync(string nationalCode)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT TOP 1 Id FROM Employees WHERE NationalCode = @nc", conn);
            cmd.Parameters.AddWithValue("@nc", nationalCode);
            var result = await cmd.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : (int?)null;
        }

        public async Task<string?> GetEmployeeFullNameByNationalCodeAsync(string nationalCode)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT TOP 1 FirstName, LastName FROM Employees WHERE NationalCode = @nc", conn);
            cmd.Parameters.AddWithValue("@nc", nationalCode);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                string fName = reader.IsDBNull(0) ? "" : reader.GetString(0);
                string lName = reader.IsDBNull(1) ? "" : reader.GetString(1);
                return $"{fName} {lName}".Trim();
            }
            return null;
        }

        public async Task<bool> IsDependentExistsAsync(string nationalCode)
        {
            if (string.IsNullOrWhiteSpace(nationalCode)) return false;
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT COUNT(1) FROM Dependents WHERE NationalCode = @nc", conn);
            cmd.Parameters.AddWithValue("@nc", nationalCode);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        public async Task AddAsync(Dependent dep)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                INSERT INTO Dependents (EmployeeId, FirstName, LastName, NationalCode, BirthCertificateNumber, FatherName, BirthDate, Gender, BirthPlace, Relation, HasSupplementaryInsurance) 
                VALUES (@empId, @fname, @lname, @nc, @idNum, @father, @birth, @gender, @birthPlace, @rel, @hasIns);", conn);

            cmd.Parameters.AddWithValue("@empId", dep.EmployeeId);
            cmd.Parameters.AddWithValue("@fname", dep.FirstName);
            cmd.Parameters.AddWithValue("@lname", dep.LastName);
            cmd.Parameters.AddWithValue("@nc", (object?)dep.NationalCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@idNum", (object?)dep.BirthCertificateNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@father", (object?)dep.FatherName ?? DBNull.Value);

            // ✅ تاریخ به صورت DateTime
            cmd.Parameters.AddWithValue("@birth", (object?)dep.BirthDate?.Date ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@gender", (object?)dep.Gender ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@birthPlace", (object?)dep.BirthPlace ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@rel", (object?)dep.Relation ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@hasIns", dep.HasSupplementaryInsurance ? 1 : 0);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateAsync(Dependent dep)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                UPDATE Dependents SET 
                    EmployeeId = @empId, FirstName = @fname, LastName = @lname, NationalCode = @nc, 
                    BirthCertificateNumber = @idNum, FatherName = @father, BirthDate = @birth, 
                    Gender = @gender, BirthPlace = @birthPlace, Relation = @rel, HasSupplementaryInsurance = @hasIns 
                WHERE Id = @id", conn);

            cmd.Parameters.AddWithValue("@empId", dep.EmployeeId);
            cmd.Parameters.AddWithValue("@fname", dep.FirstName);
            cmd.Parameters.AddWithValue("@lname", dep.LastName);
            cmd.Parameters.AddWithValue("@nc", (object?)dep.NationalCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@idNum", (object?)dep.BirthCertificateNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@father", (object?)dep.FatherName ?? DBNull.Value);

            // ✅ تاریخ به صورت DateTime
            cmd.Parameters.AddWithValue("@birth", (object?)dep.BirthDate?.Date ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@gender", (object?)dep.Gender ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@birthPlace", (object?)dep.BirthPlace ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@rel", (object?)dep.Relation ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@hasIns", dep.HasSupplementaryInsurance ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", dep.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("DELETE FROM Dependents WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}