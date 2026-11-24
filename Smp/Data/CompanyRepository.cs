using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Smp.Models;

namespace Smp.Data;

public class CompanyRepository
{
    private readonly string _connectionString;

    // ✅ FIX STEP 2: EnsureColumnsExist حذف شد
    // (جدول Companies و ستون‌های آن در DatabaseInitializer ساخته می‌شوند)
    public CompanyRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public List<Company> GetAll()
    {
        var list = new List<Company>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT * FROM Companies ORDER BY Name;", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Company
            {
                Id = Convert.ToInt32(reader["Id"]),
                Name = reader["Name"].ToString() ?? "",
                NationalId = reader["NationalId"] != DBNull.Value ? reader["NationalId"].ToString() : null,
                EconomicCode = reader["EconomicCode"] != DBNull.Value ? reader["EconomicCode"].ToString() : null,
                Address = reader["Address"] != DBNull.Value ? reader["Address"].ToString() : null,
                PostalCode = reader["PostalCode"] != DBNull.Value ? reader["PostalCode"].ToString() : null,
                Phone = reader["Phone"] != DBNull.Value ? reader["Phone"].ToString() : null,
                WorkshopCode = reader["WorkshopCode"] != DBNull.Value ? reader["WorkshopCode"].ToString() : null,
                Logo = reader["Logo"] != DBNull.Value ? (byte[])reader["Logo"] : null
            });
        }
        return list;
    }

    public Company? GetById(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT * FROM Companies WHERE Id = @Id;", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Company
            {
                Id = Convert.ToInt32(reader["Id"]),
                Name = reader["Name"].ToString() ?? "",
                NationalId = reader["NationalId"] != DBNull.Value ? reader["NationalId"].ToString() : null,
                EconomicCode = reader["EconomicCode"] != DBNull.Value ? reader["EconomicCode"].ToString() : null,
                Address = reader["Address"] != DBNull.Value ? reader["Address"].ToString() : null,
                PostalCode = reader["PostalCode"] != DBNull.Value ? reader["PostalCode"].ToString() : null,
                Phone = reader["Phone"] != DBNull.Value ? reader["Phone"].ToString() : null,
                WorkshopCode = reader["WorkshopCode"] != DBNull.Value ? reader["WorkshopCode"].ToString() : null,
                Logo = reader["Logo"] != DBNull.Value ? (byte[])reader["Logo"] : null
            };
        }
        return null;
    }

    public void Add(Company company)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand(@"
                INSERT INTO Companies (Name, NationalId, EconomicCode, Address, PostalCode, Phone, WorkshopCode, Logo)
                VALUES (@Name, @NationalId, @EconomicCode, @Address, @PostalCode, @Phone, @WorkshopCode, @Logo);
                SELECT SCOPE_IDENTITY();", conn);

        AddParameters(cmd, company);
        company.Id = Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void Update(Company company)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand(@"
                UPDATE Companies SET 
                    Name = @Name, NationalId = @NationalId, EconomicCode = @EconomicCode, 
                    Address = @Address, PostalCode = @PostalCode, Phone = @Phone, 
                    WorkshopCode = @WorkshopCode, Logo = @Logo
                WHERE Id = @Id;", conn);

        cmd.Parameters.AddWithValue("@Id", company.Id);
        AddParameters(cmd, company);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("DELETE FROM Companies WHERE Id = @Id;", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.ExecuteNonQuery();
    }

    private void AddParameters(SqlCommand cmd, Company company)
    {
        cmd.Parameters.AddWithValue("@Name", company.Name);
        cmd.Parameters.AddWithValue("@NationalId", (object?)company.NationalId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EconomicCode", (object?)company.EconomicCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Address", (object?)company.Address ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PostalCode", (object?)company.PostalCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Phone", (object?)company.Phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@WorkshopCode", (object?)company.WorkshopCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Logo", (object?)company.Logo ?? DBNull.Value);
    }
}
