using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.SqlClient;
using SMP.Models;

namespace SMP.Data
{
    public class ContractArchiveRepository
    {
        private readonly string _connectionString;

        // ✅ FIX STEP 2: EnsureColumnsExist حذف شد
        // (جدول ContractArchives و ستون OtherAllowances در DatabaseInitializer ساخته می‌شوند)
        public ContractArchiveRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<ContractArchive> GetByEmployeeId(int employeeId)
        {
            var list = new List<ContractArchive>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var sql = @"SELECT Id, EmployeeId, ArchiveDate, ContractNumber, ContractStartDate, ContractEndDate, 
                               BaseSalary, YearsBonus, AttractionAllowance, ResponsibilityAllowance, GroceryAllowance, 
                               HousingAllowance, ChildAllowance, MarriageAllowance, ManagementAllowance, TechnicalAllowance, 
                               OtherAllowances, Position, DepartmentId, DepartmentName, CompanyId, CompanyTitle 
                        FROM ContractArchives WHERE EmployeeId = @EmployeeId ORDER BY ArchiveDate DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EmployeeId", employeeId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ContractArchive
                {
                    Id = reader.GetInt32(0),
                    EmployeeId = reader.GetInt32(1),
                    ArchiveDate = Convert.ToDateTime(reader[2]),
                    ContractNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                    ContractStartDate = reader.IsDBNull(4) ? null : ParseDate(reader.GetString(4)),
                    ContractEndDate = reader.IsDBNull(5) ? null : ParseDate(reader.GetString(5)),

                    BaseSalary = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader[6]),
                    YearsBonus = reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader[7]),
                    AttractionAllowance = reader.IsDBNull(8) ? 0 : Convert.ToDecimal(reader[8]),
                    ResponsibilityAllowance = reader.IsDBNull(9) ? 0 : Convert.ToDecimal(reader[9]),
                    GroceryAllowance = reader.IsDBNull(10) ? 0 : Convert.ToDecimal(reader[10]),
                    HousingAllowance = reader.IsDBNull(11) ? 0 : Convert.ToDecimal(reader[11]),
                    ChildAllowance = reader.IsDBNull(12) ? 0 : Convert.ToDecimal(reader[12]),
                    MarriageAllowance = reader.IsDBNull(13) ? 0 : Convert.ToDecimal(reader[13]),
                    ManagementAllowance = reader.IsDBNull(14) ? 0 : Convert.ToDecimal(reader[14]),
                    TechnicalAllowance = reader.IsDBNull(15) ? 0 : Convert.ToDecimal(reader[15]),
                    OtherAllowances = reader.IsDBNull(16) ? 0 : Convert.ToDecimal(reader[16]),

                    Position = reader.IsDBNull(17) ? null : reader.GetString(17),
                    DepartmentId = reader.IsDBNull(18) ? null : reader.GetInt32(18),
                    DepartmentName = reader.IsDBNull(19) ? null : reader.GetString(19),
                    CompanyId = reader.IsDBNull(20) ? null : reader.GetInt32(20),
                    CompanyTitle = reader.IsDBNull(21) ? null : reader.GetString(21)
                });
            }
            return list;
        }

        public ContractArchive? GetByContractNumberAndEmployeeId(string contractNumber, int employeeId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            var sql = "SELECT Id, EmployeeId FROM ContractArchives WHERE ContractNumber = @code AND EmployeeId = @empId";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@code", contractNumber);
            cmd.Parameters.AddWithValue("@empId", employeeId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new ContractArchive
                {
                    Id = reader.GetInt32(0),
                    EmployeeId = reader.GetInt32(1)
                };
            }
            return null;
        }

        public void Add(ContractArchive a)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            var sql = @"INSERT INTO ContractArchives (
                            EmployeeId, ArchiveDate, ContractNumber, ContractStartDate, ContractEndDate, 
                            BaseSalary, YearsBonus, AttractionAllowance, ResponsibilityAllowance, GroceryAllowance, 
                            HousingAllowance, ChildAllowance, MarriageAllowance, ManagementAllowance, TechnicalAllowance, 
                            OtherAllowances, Position, DepartmentId, DepartmentName, CompanyId, CompanyTitle)
                        VALUES (
                            @EmployeeId, @ArchiveDate, @ContractNumber, @ContractStartDate, @ContractEndDate, 
                            @BaseSalary, @YearsBonus, @AttractionAllowance, @ResponsibilityAllowance, @GroceryAllowance, 
                            @HousingAllowance, @ChildAllowance, @MarriageAllowance, @ManagementAllowance, @TechnicalAllowance, 
                            @OtherAllowances, @Position, @DepartmentId, @DepartmentName, @CompanyId, @CompanyTitle);
                        SELECT SCOPE_IDENTITY();";

            using var cmd = new SqlCommand(sql, conn);
            AddParameters(cmd, a);
            a.Id = Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void Update(ContractArchive a)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            var sql = @"UPDATE ContractArchives SET 
                            ArchiveDate = @ArchiveDate, ContractNumber = @ContractNumber, ContractStartDate = @ContractStartDate, 
                            ContractEndDate = @ContractEndDate, BaseSalary = @BaseSalary, YearsBonus = @YearsBonus, 
                            AttractionAllowance = @AttractionAllowance, ResponsibilityAllowance = @ResponsibilityAllowance, 
                            GroceryAllowance = @GroceryAllowance, HousingAllowance = @HousingAllowance, ChildAllowance = @ChildAllowance, 
                            MarriageAllowance = @MarriageAllowance, ManagementAllowance = @ManagementAllowance, 
                            TechnicalAllowance = @TechnicalAllowance, OtherAllowances = @OtherAllowances, Position = @Position, 
                            DepartmentId = @DepartmentId, DepartmentName = @DepartmentName, CompanyId = @CompanyId, CompanyTitle = @CompanyTitle
                        WHERE Id = @Id";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", a.Id);
            AddParameters(cmd, a);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM ContractArchives WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

        private void AddParameters(SqlCommand cmd, ContractArchive a)
        {
            cmd.Parameters.AddWithValue("@EmployeeId", a.EmployeeId);
            cmd.Parameters.AddWithValue("@ArchiveDate", a.ArchiveDate.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@ContractNumber", (object?)a.ContractNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ContractStartDate", (object?)a.ContractStartDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ContractEndDate", (object?)a.ContractEndDate?.ToString("yyyy-MM-dd") ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@BaseSalary", a.BaseSalary);
            cmd.Parameters.AddWithValue("@YearsBonus", a.YearsBonus);
            cmd.Parameters.AddWithValue("@AttractionAllowance", a.AttractionAllowance);
            cmd.Parameters.AddWithValue("@ResponsibilityAllowance", a.ResponsibilityAllowance);
            cmd.Parameters.AddWithValue("@GroceryAllowance", a.GroceryAllowance);
            cmd.Parameters.AddWithValue("@HousingAllowance", a.HousingAllowance);
            cmd.Parameters.AddWithValue("@ChildAllowance", a.ChildAllowance);
            cmd.Parameters.AddWithValue("@MarriageAllowance", a.MarriageAllowance);
            cmd.Parameters.AddWithValue("@ManagementAllowance", a.ManagementAllowance);
            cmd.Parameters.AddWithValue("@TechnicalAllowance", a.TechnicalAllowance);
            cmd.Parameters.AddWithValue("@OtherAllowances", a.OtherAllowances);

            cmd.Parameters.AddWithValue("@Position", (object?)a.Position ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DepartmentId", (object?)a.DepartmentId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DepartmentName", (object?)a.DepartmentName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CompanyId", (object?)a.CompanyId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CompanyTitle", (object?)a.CompanyTitle ?? DBNull.Value);
        }

        private static DateTime? ParseDate(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTime.TryParse(s, out DateTime dt)) return dt;
            return null;
        }
    }
}