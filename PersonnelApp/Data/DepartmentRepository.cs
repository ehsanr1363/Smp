using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using PersonnelApp.Models;

namespace PersonnelApp.Data
{
    public class DepartmentRepository
    {
        private readonly string _connectionString;

        public DepartmentRepository(string connectionString)
        {
            _connectionString = connectionString;
            // 👈 EnsureTableCreated حذف شد
        }

        public List<Department> GetAll()
        {
            var list = new List<Department>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand("SELECT Id, Code, Name, Address FROM Departments ORDER BY Name", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Department
                {
                    Id = reader.GetInt32(0),
                    Code = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Name = reader.GetString(2),
                    Address = reader.IsDBNull(3) ? null : reader.GetString(3)
                });
            }
            return list;
        }

        public void Add(Department dept)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(
                "INSERT INTO Departments (Code, Name, Address) VALUES (@Code, @Name, @Address); SELECT SCOPE_IDENTITY();", conn);
            cmd.Parameters.AddWithValue("@Code", (object?)dept.Code ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Name", dept.Name);
            cmd.Parameters.AddWithValue("@Address", (object?)dept.Address ?? DBNull.Value);
            dept.Id = Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void Update(Department dept)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(
                "UPDATE Departments SET Code = @Code, Name = @Name, Address = @Address WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Code", (object?)dept.Code ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Name", dept.Name);
            cmd.Parameters.AddWithValue("@Address", (object?)dept.Address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Id", dept.Id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM Departments WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }
    }
}