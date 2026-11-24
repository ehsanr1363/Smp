using Microsoft.Data.SqlClient;
using Smp.Models;
using System;
using System.Collections.Generic;

namespace Smp.Data;

public class ContractArchiveRepository
{
    private readonly string _connectionString;

    public ContractArchiveRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    // ۱. دریافت لیست بر اساس شناسه کارمند (کدی که قبلاً نوشتید)
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
            list.Add(MapReaderToContractArchive(reader));
        }
        return list;
    }

    // ۲. متد افزودن (Add)
    public void Add(ContractArchive archive)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        var sql = @"INSERT INTO ContractArchives 
                        (EmployeeId, ArchiveDate, ContractNumber, ContractStartDate, ContractEndDate, 
                         BaseSalary, YearsBonus, AttractionAllowance, ResponsibilityAllowance, GroceryAllowance, 
                         HousingAllowance, ChildAllowance, MarriageAllowance, ManagementAllowance, TechnicalAllowance, 
                         OtherAllowances, Position, DepartmentId, DepartmentName, CompanyId, CompanyTitle) 
                        VALUES 
                        (@EmployeeId, @ArchiveDate, @ContractNumber, @ContractStartDate, @ContractEndDate, 
                         @BaseSalary, @YearsBonus, @AttractionAllowance, @ResponsibilityAllowance, @GroceryAllowance, 
                         @HousingAllowance, @ChildAllowance, @MarriageAllowance, @ManagementAllowance, @TechnicalAllowance, 
                         @OtherAllowances, @Position, @DepartmentId, @DepartmentName, @CompanyId, @CompanyTitle)";

        using var cmd = new SqlCommand(sql, conn);
        AddParametersToCommand(cmd, archive);
        cmd.ExecuteNonQuery();
    }

    // ۳. متد ویرایش (Update)
    public void Update(ContractArchive archive)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        var sql = @"UPDATE ContractArchives SET 
                        EmployeeId = @EmployeeId, ArchiveDate = @ArchiveDate, ContractNumber = @ContractNumber, 
                        ContractStartDate = @ContractStartDate, ContractEndDate = @ContractEndDate, 
                        BaseSalary = @BaseSalary, YearsBonus = @YearsBonus, AttractionAllowance = @AttractionAllowance, 
                        ResponsibilityAllowance = @ResponsibilityAllowance, GroceryAllowance = @GroceryAllowance, 
                        HousingAllowance = @HousingAllowance, ChildAllowance = @ChildAllowance, MarriageAllowance = @MarriageAllowance, 
                        ManagementAllowance = @ManagementAllowance, TechnicalAllowance = @TechnicalAllowance, OtherAllowances = @OtherAllowances, 
                        Position = @Position, DepartmentId = @DepartmentId, DepartmentName = @DepartmentName, 
                        CompanyId = @CompanyId, CompanyTitle = @CompanyTitle
                        WHERE Id = @Id";

        using var cmd = new SqlCommand(sql, conn);
        AddParametersToCommand(cmd, archive);
        cmd.Parameters.AddWithValue("@Id", archive.Id); // شرط آپدیت
        cmd.ExecuteNonQuery();
    }

    // ۴. متد حذف (Delete)
    public void Delete(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        var sql = "DELETE FROM ContractArchives WHERE Id = @Id";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.ExecuteNonQuery();
    }

    // ۵. متد جستجو بر اساس شماره قرارداد و کارمند
    public ContractArchive GetByContractNumberAndEmployeeId(string contractNumber, int employeeId)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        var sql = "SELECT * FROM ContractArchives WHERE ContractNumber = @ContractNumber AND EmployeeId = @EmployeeId";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ContractNumber", contractNumber);
        cmd.Parameters.AddWithValue("@EmployeeId", employeeId);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return MapReaderToContractArchive(reader);
        }
        return null;
    }

    // --- متدهای کمکی برای جلوگیری از تکرار کد ---

    // تبدیل ردیف دیتابیس به آبجکت
    private ContractArchive MapReaderToContractArchive(SqlDataReader reader)
    {
        return new ContractArchive
        {
            Id = Convert.ToInt32(reader["Id"]),
            EmployeeId = Convert.ToInt32(reader["EmployeeId"]),
            ArchiveDate = Convert.ToDateTime(reader["ArchiveDate"]),
            ContractNumber = reader["ContractNumber"] != DBNull.Value ? reader["ContractNumber"].ToString() : null,
            ContractStartDate = reader["ContractStartDate"] != DBNull.Value ? Convert.ToDateTime(reader["ContractStartDate"]) : (DateTime?)null,
            ContractEndDate = reader["ContractEndDate"] != DBNull.Value ? Convert.ToDateTime(reader["ContractEndDate"]) : (DateTime?)null,
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
            OtherAllowances = reader["OtherAllowances"] != DBNull.Value ? Convert.ToDecimal(reader["OtherAllowances"]) : 0,
            Position = reader["Position"] != DBNull.Value ? reader["Position"].ToString() : null,
            DepartmentId = reader["DepartmentId"] != DBNull.Value ? Convert.ToInt32(reader["DepartmentId"]) : 0,
            DepartmentName = reader["DepartmentName"] != DBNull.Value ? reader["DepartmentName"].ToString() : null,
            CompanyId = reader["CompanyId"] != DBNull.Value ? Convert.ToInt32(reader["CompanyId"]) : 0,
            CompanyTitle = reader["CompanyTitle"] != DBNull.Value ? reader["CompanyTitle"].ToString() : null
        };
    }

    // اضافه کردن پارامترها به دستور SQL
    private void AddParametersToCommand(SqlCommand cmd, ContractArchive a)
    {
        cmd.Parameters.AddWithValue("@EmployeeId", a.EmployeeId);
        cmd.Parameters.AddWithValue("@ArchiveDate", a.ArchiveDate);
        cmd.Parameters.AddWithValue("@ContractNumber", (object)a.ContractNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ContractStartDate", (object)a.ContractStartDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ContractEndDate", (object)a.ContractEndDate ?? DBNull.Value);
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
        cmd.Parameters.AddWithValue("@Position", (object)a.Position ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DepartmentId", a.DepartmentId);
        cmd.Parameters.AddWithValue("@DepartmentName", (object)a.DepartmentName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CompanyId", a.CompanyId);
        cmd.Parameters.AddWithValue("@CompanyTitle", (object)a.CompanyTitle ?? DBNull.Value);
    }
}
