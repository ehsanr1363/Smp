using Microsoft.Data.SqlClient;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PersonnelApp.Data
{
    public class EmployeeRepository
    {
        private readonly string _connectionString;

        public EmployeeRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<Employee>> GetEmployeesWithDetailsAsync(string search = "", bool showInactive = true)
        {
            var list = new List<Employee>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"SELECT e.Id, e.PersonnelCode, e.FirstName, e.LastName, e.FatherName, e.NationalCode, 
                               e.BirthCertificateNumber, e.BirthDate, e.BirthPlace, e.IssuancePlace, e.MaritalStatus, 
                               e.MilitaryStatus, e.Education, e.SpecializedField, e.ChildrenCount, e.InsuranceNumber, 
                               e.Photo, e.ContractStartDate, e.ContractYear, e.ContractNumber, 
                               e.DepartmentId, d.Name AS DepartmentName, e.Position, e.PersonnelType, 
                               e.WorkLocation, e.Status, e.PhoneNumber, e.BaseSalary, e.YearsBonus, e.AttractionAllowance, 
                               e.ResponsibilityAllowance, e.GroceryAllowance, e.HousingAllowance, e.ChildAllowance, 
                               e.MarriageAllowance, e.ManagementAllowance, e.TechnicalAllowance, e.TransportationAllowance, e.Username,
                               e.IsActive, e.Address, e.ContractEndDate, e.HireDate, d.Address AS DepartmentAddress,
                               e.CompanyId, c.Name AS CompanyTitle, c.Address AS CompanyAddress, e.OtherAllowances, e.Gender,
                               e.DailyWage, e.HourlyWage, e.HasInsurance, e.DailyWageCategory
                        FROM Employees e
                        LEFT JOIN Departments d ON e.DepartmentId = d.Id
                        LEFT JOIN Companies c ON e.CompanyId = c.Id
                        WHERE 1=1";

            if (!string.IsNullOrWhiteSpace(search))
                sql += " AND (e.FirstName LIKE @search OR e.LastName LIKE @search OR e.NationalCode LIKE @search OR e.PersonnelCode LIKE @search)";

            if (!showInactive)
                sql += " AND ISNULL(e.IsActive, 1) = 1";

            sql += " ORDER BY ISNULL(e.IsActive, 1) DESC, e.LastName, e.FirstName";

            using var cmd = new SqlCommand(sql, conn);
            if (!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("@search", $"%{search}%");

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new Employee
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    PersonnelCode = reader.IsDBNull(reader.GetOrdinal("PersonnelCode")) ? null : reader.GetString(reader.GetOrdinal("PersonnelCode")),
                    FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                    LastName = reader.GetString(reader.GetOrdinal("LastName")),
                    FatherName = reader.IsDBNull(reader.GetOrdinal("FatherName")) ? null : reader.GetString(reader.GetOrdinal("FatherName")),
                    NationalCode = reader.IsDBNull(reader.GetOrdinal("NationalCode")) ? null : reader.GetString(reader.GetOrdinal("NationalCode")),
                    BirthCertificateNumber = reader.IsDBNull(reader.GetOrdinal("BirthCertificateNumber")) ? null : reader.GetString(reader.GetOrdinal("BirthCertificateNumber")),
                    BirthDate = reader.IsDBNull(reader.GetOrdinal("BirthDate")) ? null : reader.GetDateTime(reader.GetOrdinal("BirthDate")),
                    BirthPlace = reader.IsDBNull(reader.GetOrdinal("BirthPlace")) ? null : reader.GetString(reader.GetOrdinal("BirthPlace")),
                    IssuancePlace = reader.IsDBNull(reader.GetOrdinal("IssuancePlace")) ? null : reader.GetString(reader.GetOrdinal("IssuancePlace")),
                    MaritalStatus = reader.IsDBNull(reader.GetOrdinal("MaritalStatus")) ? null : reader.GetString(reader.GetOrdinal("MaritalStatus")),
                    MilitaryStatus = reader.IsDBNull(reader.GetOrdinal("MilitaryStatus")) ? null : reader.GetString(reader.GetOrdinal("MilitaryStatus")),
                    Education = reader.IsDBNull(reader.GetOrdinal("Education")) ? null : reader.GetString(reader.GetOrdinal("Education")),
                    SpecializedField = reader.IsDBNull(reader.GetOrdinal("SpecializedField")) ? null : reader.GetString(reader.GetOrdinal("SpecializedField")),
                    ChildrenCount = reader.IsDBNull(reader.GetOrdinal("ChildrenCount")) ? 0 : Convert.ToInt32(reader["ChildrenCount"]),
                    InsuranceNumber = reader.IsDBNull(reader.GetOrdinal("InsuranceNumber")) ? null : reader.GetString(reader.GetOrdinal("InsuranceNumber")),
                    Photo = reader.IsDBNull(reader.GetOrdinal("Photo")) ? null : (byte[])reader["Photo"],
                    ContractStartDate = reader.IsDBNull(reader.GetOrdinal("ContractStartDate")) ? null : reader.GetDateTime(reader.GetOrdinal("ContractStartDate")),
                    ContractYear = reader.IsDBNull(reader.GetOrdinal("ContractYear")) ? null : Convert.ToInt32(reader["ContractYear"]),
                    ContractNumber = reader.IsDBNull(reader.GetOrdinal("ContractNumber")) ? null : reader.GetString(reader.GetOrdinal("ContractNumber")),
                    DepartmentId = reader.IsDBNull(reader.GetOrdinal("DepartmentId")) ? null : Convert.ToInt32(reader["DepartmentId"]),
                    DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? null : reader.GetString(reader.GetOrdinal("DepartmentName")),
                    Position = reader.IsDBNull(reader.GetOrdinal("Position")) ? null : reader.GetString(reader.GetOrdinal("Position")),
                    PersonnelType = reader.IsDBNull(reader.GetOrdinal("PersonnelType")) ? null : reader.GetString(reader.GetOrdinal("PersonnelType")),
                    WorkLocation = reader.IsDBNull(reader.GetOrdinal("WorkLocation")) ? null : reader.GetString(reader.GetOrdinal("WorkLocation")),
                    Status = reader.IsDBNull(reader.GetOrdinal("Status")) ? null : reader.GetString(reader.GetOrdinal("Status")),
                    PhoneNumber = reader.IsDBNull(reader.GetOrdinal("PhoneNumber")) ? null : reader.GetString(reader.GetOrdinal("PhoneNumber")),
                    BaseSalary = reader.IsDBNull(reader.GetOrdinal("BaseSalary")) ? 0 : Convert.ToDecimal(reader["BaseSalary"]),
                    YearsBonus = reader.IsDBNull(reader.GetOrdinal("YearsBonus")) ? 0 : Convert.ToDecimal(reader["YearsBonus"]),
                    AttractionAllowance = reader.IsDBNull(reader.GetOrdinal("AttractionAllowance")) ? 0 : Convert.ToDecimal(reader["AttractionAllowance"]),
                    ResponsibilityAllowance = reader.IsDBNull(reader.GetOrdinal("ResponsibilityAllowance")) ? 0 : Convert.ToDecimal(reader["ResponsibilityAllowance"]),
                    GroceryAllowance = reader.IsDBNull(reader.GetOrdinal("GroceryAllowance")) ? 0 : Convert.ToDecimal(reader["GroceryAllowance"]),
                    HousingAllowance = reader.IsDBNull(reader.GetOrdinal("HousingAllowance")) ? 0 : Convert.ToDecimal(reader["HousingAllowance"]),
                    ChildAllowance = reader.IsDBNull(reader.GetOrdinal("ChildAllowance")) ? 0 : Convert.ToDecimal(reader["ChildAllowance"]),
                    MarriageAllowance = reader.IsDBNull(reader.GetOrdinal("MarriageAllowance")) ? 0 : Convert.ToDecimal(reader["MarriageAllowance"]),
                    ManagementAllowance = reader.IsDBNull(reader.GetOrdinal("ManagementAllowance")) ? 0 : Convert.ToDecimal(reader["ManagementAllowance"]),
                    TechnicalAllowance = reader.IsDBNull(reader.GetOrdinal("TechnicalAllowance")) ? 0 : Convert.ToDecimal(reader["TechnicalAllowance"]),
                    TransportationAllowance = reader.IsDBNull(reader.GetOrdinal("TransportationAllowance")) ? 0 : Convert.ToDecimal(reader["TransportationAllowance"]),
                    Username = reader.IsDBNull(reader.GetOrdinal("Username")) ? null : reader.GetString(reader.GetOrdinal("Username")),
                    IsActive = reader.IsDBNull(reader.GetOrdinal("IsActive")) ? true : Convert.ToInt32(reader["IsActive"]) == 1,
                    Address = reader.IsDBNull(reader.GetOrdinal("Address")) ? null : reader.GetString(reader.GetOrdinal("Address")),
                    ContractEndDate = reader.IsDBNull(reader.GetOrdinal("ContractEndDate")) ? null : reader.GetDateTime(reader.GetOrdinal("ContractEndDate")),
                    HireDate = reader.IsDBNull(reader.GetOrdinal("HireDate")) ? null : reader.GetDateTime(reader.GetOrdinal("HireDate")),
                    DepartmentAddress = reader.IsDBNull(reader.GetOrdinal("DepartmentAddress")) ? null : reader.GetString(reader.GetOrdinal("DepartmentAddress")),
                    CompanyId = reader.IsDBNull(reader.GetOrdinal("CompanyId")) ? null : Convert.ToInt32(reader["CompanyId"]),
                    CompanyTitle = reader.IsDBNull(reader.GetOrdinal("CompanyTitle")) ? null : reader.GetString(reader.GetOrdinal("CompanyTitle")),
                    CompanyAddress = reader.IsDBNull(reader.GetOrdinal("CompanyAddress")) ? null : reader.GetString(reader.GetOrdinal("CompanyAddress")),
                    OtherAllowances = reader.IsDBNull(reader.GetOrdinal("OtherAllowances")) ? 0 : Convert.ToDecimal(reader["OtherAllowances"]),
                    Gender = reader.IsDBNull(reader.GetOrdinal("Gender")) ? null : reader.GetString(reader.GetOrdinal("Gender")),
                    DailyWage = reader.IsDBNull(reader.GetOrdinal("DailyWage")) ? 0 : Convert.ToDecimal(reader["DailyWage"]),
                    HourlyWage = reader.IsDBNull(reader.GetOrdinal("HourlyWage")) ? 0 : Convert.ToDecimal(reader["HourlyWage"]),
                    HasInsurance = reader.IsDBNull(reader.GetOrdinal("HasInsurance")) ? true : Convert.ToInt32(reader["HasInsurance"]) == 1,
                    DailyWageCategory = reader.IsDBNull(reader.GetOrdinal("DailyWageCategory")) ? null : reader.GetString(reader.GetOrdinal("DailyWageCategory"))
                });
            }
            return list;
        }

        public Employee? GetById(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            var sql = @"SELECT e.*, d.Name AS DepartmentName, c.Name AS CompanyTitle FROM Employees e LEFT JOIN Departments d ON e.DepartmentId = d.Id LEFT JOIN Companies c ON e.CompanyId = c.Id WHERE e.Id = @Id";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new Employee
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    PersonnelCode = reader["PersonnelCode"]?.ToString(),
                    FirstName = reader["FirstName"]?.ToString() ?? "",
                    LastName = reader["LastName"]?.ToString() ?? "",
                    FatherName = reader["FatherName"]?.ToString(),
                    NationalCode = reader["NationalCode"]?.ToString(),
                    BirthCertificateNumber = reader["BirthCertificateNumber"]?.ToString(),
                    BirthDate = reader["BirthDate"] as DateTime?,
                    BirthPlace = reader["BirthPlace"]?.ToString(),
                    IssuancePlace = reader["IssuancePlace"]?.ToString(),
                    MaritalStatus = reader["MaritalStatus"]?.ToString(),
                    MilitaryStatus = reader["MilitaryStatus"]?.ToString(),
                    Education = reader["Education"]?.ToString(),
                    SpecializedField = reader["SpecializedField"]?.ToString(),
                    ChildrenCount = reader["ChildrenCount"] != DBNull.Value ? Convert.ToInt32(reader["ChildrenCount"]) : 0,
                    InsuranceNumber = reader["InsuranceNumber"]?.ToString(),
                    Photo = reader["Photo"] != DBNull.Value ? (byte[])reader["Photo"] : null,
                    ContractStartDate = reader["ContractStartDate"] as DateTime?,
                    ContractEndDate = reader["ContractEndDate"] as DateTime?,
                    ContractYear = reader["ContractYear"] != DBNull.Value ? Convert.ToInt32(reader["ContractYear"]) : null,
                    ContractNumber = reader["ContractNumber"]?.ToString(),
                    CompanyId = reader["CompanyId"] != DBNull.Value ? Convert.ToInt32(reader["CompanyId"]) : null,
                    DepartmentId = reader["DepartmentId"] != DBNull.Value ? Convert.ToInt32(reader["DepartmentId"]) : null,
                    Position = reader["Position"]?.ToString(),
                    PersonnelType = reader["PersonnelType"]?.ToString(),
                    WorkLocation = reader["WorkLocation"]?.ToString(),
                    Status = reader["Status"]?.ToString(),
                    PhoneNumber = reader["PhoneNumber"]?.ToString(),
                    IsActive = reader["IsActive"] != DBNull.Value && Convert.ToInt32(reader["IsActive"]) == 1,
                    BaseSalary = reader["BaseSalary"] != DBNull.Value ? Convert.ToDecimal(reader["BaseSalary"]) : 0,
                    YearsBonus = reader["YearsBonus"] != DBNull.Value ? Convert.ToDecimal(reader["YearsBonus"]) : 0,
                    AttractionAllowance = reader["AttractionAllowance"] != DBNull.Value ? Convert.ToDecimal(reader["AttractionAllowance"]) : 0,
                    ResponsibilityAllowance = reader["ResponsibilityAllowance"] != DBNull.Value ? Convert.ToDecimal(reader["ResponsibilityAllowance"]) : 0,
                    GroceryAllowance = reader["GroceryAllowance"] != DBNull.Value ? Convert.ToDecimal(reader["GroceryAllowance"]) : 0,
                    HousingAllowance = reader["HousingAllowance"] != DBNull.Value ? Convert.ToDecimal(reader["HousingAllowance"]) : 0,
                    ChildAllowance = reader["ChildAllowance"] != DBNull.Value ? Convert.ToDecimal(reader["ChildAllowance"]) : 0,
                    MarriageAllowance = reader["MarriageAllowance"] != DBNull.Value ? Convert.ToDecimal(reader["MarriageAllowance"]) : 0,
                    ManagementAllowance = reader["ManagementAllowance"] != DBNull.Value ? Convert.ToDecimal(reader["ManagementAllowance"]) : 0,
                    TechnicalAllowance = reader["TechnicalAllowance"] != DBNull.Value ? Convert.ToDecimal(reader["TechnicalAllowance"]) : 0,
                    TransportationAllowance = reader["TransportationAllowance"] != DBNull.Value ? Convert.ToDecimal(reader["TransportationAllowance"]) : 0,
                    OtherAllowances = reader["OtherAllowances"] != DBNull.Value ? Convert.ToDecimal(reader["OtherAllowances"]) : 0,
                    Username = reader["Username"]?.ToString(),
                    Address = reader["Address"]?.ToString(),
                    HireDate = reader["HireDate"] as DateTime?,
                    Gender = reader["Gender"]?.ToString(),
                    DailyWage = reader["DailyWage"] != DBNull.Value ? Convert.ToDecimal(reader["DailyWage"]) : 0,
                    HourlyWage = reader["HourlyWage"] != DBNull.Value ? Convert.ToDecimal(reader["HourlyWage"]) : 0,
                    HasInsurance = reader["HasInsurance"] != DBNull.Value && Convert.ToInt32(reader["HasInsurance"]) == 1,
                    DailyWageCategory = reader["DailyWageCategory"]?.ToString(),
                    DepartmentName = reader["DepartmentName"]?.ToString(),
                    CompanyTitle = reader["CompanyTitle"]?.ToString()
                };
            }
            return null;
        }

        public bool ExistsByPersonnelCode(string personnelCode, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(personnelCode)) return false;
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            var sql = "SELECT COUNT(1) FROM Employees WHERE PersonnelCode = @code";
            if (excludeId.HasValue) sql += " AND Id <> @excludeId";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@code", personnelCode.Trim());
            if (excludeId.HasValue) cmd.Parameters.AddWithValue("@excludeId", excludeId.Value);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public bool ExistsByNationalCode(string nationalCode, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(nationalCode)) return false;
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            var sql = "SELECT COUNT(1) FROM Employees WHERE NationalCode = @code";
            if (excludeId.HasValue) sql += " AND Id <> @excludeId";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@code", nationalCode.Trim());
            if (excludeId.HasValue) cmd.Parameters.AddWithValue("@excludeId", excludeId.Value);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public async Task AddAsync(Employee e)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            var sql = @"INSERT INTO Employees (
                            PersonnelCode, FirstName, LastName, FatherName, NationalCode, BirthCertificateNumber, 
                            BirthDate, BirthPlace, IssuancePlace, MaritalStatus, MilitaryStatus, Education, 
                            SpecializedField, ChildrenCount, InsuranceNumber, Photo, ContractStartDate, 
                            ContractYear, ContractNumber, DepartmentId, Position, PersonnelType, WorkLocation, 
                            Status, PhoneNumber, BaseSalary, YearsBonus, AttractionAllowance, ResponsibilityAllowance, 
                            GroceryAllowance, HousingAllowance, ChildAllowance, MarriageAllowance, ManagementAllowance, 
                            TechnicalAllowance, TransportationAllowance, OtherAllowances, Username, IsActive, Address, ContractEndDate, HireDate, CompanyId, Gender,
                            DailyWage, HourlyWage, HasInsurance, DailyWageCategory)
                        VALUES (
                            @PersonnelCode, @FirstName, @LastName, @FatherName, @NationalCode, @BirthCertificateNumber, 
                            @BirthDate, @BirthPlace, @IssuancePlace, @MaritalStatus, @MilitaryStatus, @Education, 
                            @SpecializedField, @ChildrenCount, @InsuranceNumber, @Photo, @ContractStartDate, 
                            @ContractYear, @ContractNumber, @DepartmentId, @Position, @PersonnelType, @WorkLocation, 
                            @Status, @PhoneNumber, @BaseSalary, @YearsBonus, @AttractionAllowance, @ResponsibilityAllowance, 
                            @GroceryAllowance, @HousingAllowance, @ChildAllowance, @MarriageAllowance, @ManagementAllowance, 
                            @TechnicalAllowance, @TransportationAllowance, @OtherAllowances, @Username, @IsActive, @Address, @ContractEndDate, @HireDate, @CompanyId, @Gender,
                            @DailyWage, @HourlyWage, @HasInsurance, @DailyWageCategory);
                        SELECT SCOPE_IDENTITY();";
            using var cmd = new SqlCommand(sql, conn);
            AddParameters(cmd, e);
            e.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task UpdateAsync(Employee e, DateTime? terminationDate = null)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                if (!e.IsActive)
                {
                    var endDate = (terminationDate ?? DateTime.Now);
                    e.ContractEndDate = endDate;

                    if (terminationDate.HasValue)
                    {
                        string termSql = @"UPDATE EmployeeContracts SET Status = N'خاتمه یافته', EndDate = @EndDate WHERE EmployeeId = @Id AND Status = N'فعال'";
                        using var termCmd = new SqlCommand(termSql, conn, transaction);
                        termCmd.Parameters.AddWithValue("@EndDate", endDate.Date);
                        termCmd.Parameters.AddWithValue("@Id", e.Id);
                        await termCmd.ExecuteNonQueryAsync();
                    }
                }

                var sql = @"UPDATE Employees SET
                                PersonnelCode = @PersonnelCode, FirstName = @FirstName, LastName = @LastName, FatherName = @FatherName, 
                                NationalCode = @NationalCode, BirthCertificateNumber = @BirthCertificateNumber, BirthDate = @BirthDate, 
                                BirthPlace = @BirthPlace, IssuancePlace = @IssuancePlace, MaritalStatus = @MaritalStatus, 
                                MilitaryStatus = @MilitaryStatus, Education = @Education, SpecializedField = @SpecializedField, 
                                ChildrenCount = @ChildrenCount, InsuranceNumber = @InsuranceNumber, 
                                Photo = @Photo, ContractStartDate = @ContractStartDate, ContractYear = @ContractYear, 
                                ContractNumber = @ContractNumber, DepartmentId = @DepartmentId, 
                                Position = @Position, PersonnelType = @PersonnelType, WorkLocation = @WorkLocation, Status = @Status, 
                                PhoneNumber = @PhoneNumber, BaseSalary = @BaseSalary, YearsBonus = @YearsBonus, 
                                AttractionAllowance = @AttractionAllowance, ResponsibilityAllowance = @ResponsibilityAllowance, 
                                GroceryAllowance = @GroceryAllowance, HousingAllowance = @HousingAllowance, ChildAllowance = @ChildAllowance, 
                                MarriageAllowance = @MarriageAllowance, ManagementAllowance = @ManagementAllowance, 
                                TechnicalAllowance = @TechnicalAllowance, TransportationAllowance = @TransportationAllowance, OtherAllowances = @OtherAllowances, Username = @Username,
                                IsActive = @IsActive, Address = @Address, ContractEndDate = @ContractEndDate, HireDate = @HireDate,
                                CompanyId = @CompanyId, Gender = @Gender,
                                DailyWage = @DailyWage, HourlyWage = @HourlyWage, HasInsurance = @HasInsurance,
                                DailyWageCategory = @DailyWageCategory
                            WHERE Id = @Id";
                using var cmd = new SqlCommand(sql, conn, transaction);
                cmd.Parameters.AddWithValue("@Id", e.Id);
                AddParameters(cmd, e);
                await cmd.ExecuteNonQueryAsync();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public void Delete(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using (var checkPayroll = new SqlCommand("SELECT COUNT(1) FROM Payrolls WHERE EmployeeId = @Id", conn))
            {
                checkPayroll.Parameters.AddWithValue("@Id", id);
                if (Convert.ToInt32(checkPayroll.ExecuteScalar()) > 0)
                    throw new InvalidOperationException("این کارمند دارای سابقه فیش حقوقی است و قابل حذف نیست.\nلطفاً به جای حذف، وضعیت او را «غیرفعال» کنید.");
            }

            using (var checkAtt = new SqlCommand("SELECT COUNT(1) FROM Attendance WHERE EmployeeId = @Id", conn))
            {
                checkAtt.Parameters.AddWithValue("@Id", id);
                if (Convert.ToInt32(checkAtt.ExecuteScalar()) > 0)
                    throw new InvalidOperationException("این کارمند دارای سابقه کارکرد است و قابل حذف نیست.\nلطفاً به جای حذف، وضعیت او را «غیرفعال» کنید.");
            }

            using var cmd = new SqlCommand("DELETE FROM Employees WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

        private void AddParameters(SqlCommand cmd, Employee e)
        {
            cmd.Parameters.AddWithValue("@PersonnelCode", (object?)e.PersonnelCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FirstName", e.FirstName);
            cmd.Parameters.AddWithValue("@LastName", e.LastName);
            cmd.Parameters.AddWithValue("@FatherName", (object?)e.FatherName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NationalCode", (object?)e.NationalCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BirthCertificateNumber", (object?)e.BirthCertificateNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BirthDate", (object?)e.BirthDate?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BirthPlace", (object?)e.BirthPlace ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IssuancePlace", (object?)e.IssuancePlace ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MaritalStatus", (object?)e.MaritalStatus ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MilitaryStatus", (object?)e.MilitaryStatus ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Education", (object?)e.Education ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SpecializedField", (object?)e.SpecializedField ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ChildrenCount", e.ChildrenCount);
            cmd.Parameters.AddWithValue("@InsuranceNumber", (object?)e.InsuranceNumber ?? DBNull.Value);

            cmd.Parameters.Add("@Photo", System.Data.SqlDbType.VarBinary, -1).Value = (object?)e.Photo ?? DBNull.Value;

            cmd.Parameters.AddWithValue("@ContractStartDate", (object?)e.ContractStartDate?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ContractEndDate", (object?)e.ContractEndDate?.Date ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@ContractYear", (object?)e.ContractYear ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ContractNumber", (object?)e.ContractNumber ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@DepartmentId", (object?)e.DepartmentId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Position", (object?)e.Position ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PersonnelType", (object?)e.PersonnelType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@WorkLocation", (object?)e.WorkLocation ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", (object?)e.Status ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PhoneNumber", (object?)e.PhoneNumber ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@BaseSalary", e.BaseSalary);
            cmd.Parameters.AddWithValue("@YearsBonus", e.YearsBonus);
            cmd.Parameters.AddWithValue("@AttractionAllowance", e.AttractionAllowance);
            cmd.Parameters.AddWithValue("@ResponsibilityAllowance", e.ResponsibilityAllowance);
            cmd.Parameters.AddWithValue("@GroceryAllowance", e.GroceryAllowance);
            cmd.Parameters.AddWithValue("@HousingAllowance", e.HousingAllowance);
            cmd.Parameters.AddWithValue("@ChildAllowance", e.ChildAllowance);
            cmd.Parameters.AddWithValue("@MarriageAllowance", e.MarriageAllowance);
            cmd.Parameters.AddWithValue("@ManagementAllowance", e.ManagementAllowance);
            cmd.Parameters.AddWithValue("@TechnicalAllowance", e.TechnicalAllowance);
            cmd.Parameters.AddWithValue("@TransportationAllowance", e.TransportationAllowance);
            cmd.Parameters.AddWithValue("@OtherAllowances", e.OtherAllowances);

            cmd.Parameters.AddWithValue("@DailyWage", e.DailyWage);
            cmd.Parameters.AddWithValue("@HourlyWage", e.HourlyWage);
            cmd.Parameters.AddWithValue("@HasInsurance", e.HasInsurance ? 1 : 0);
            cmd.Parameters.AddWithValue("@DailyWageCategory", (object?)e.DailyWageCategory ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@Username", (object?)e.Username ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IsActive", e.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@Address", (object?)e.Address ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@HireDate", (object?)e.HireDate?.Date ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@CompanyId", (object?)e.CompanyId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Gender", (object?)e.Gender ?? DBNull.Value);
        }
    }
}