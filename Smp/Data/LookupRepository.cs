using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Smp.Models;
using System.Threading.Tasks;

namespace Smp.Data;

public class LookupRepository
{
    private readonly string _connectionString;

    // ✅ FIX STEP 2: EnsureTableCreated حذف شد
    // (جدول LookupItems و seed آن در DatabaseInitializer ساخته می‌شود)
    public LookupRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public List<LookupItem> GetByCategory(string category)
    {
        var list = new List<LookupItem>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT Id, Category, Value FROM LookupItems WHERE Category = @Category ORDER BY Id", conn);
        cmd.Parameters.AddWithValue("@Category", category);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new LookupItem
            {
                Id = reader.GetInt32(0),
                Category = reader.GetString(1),
                Value = reader.GetString(2)
            });
        }
        return list;
    }

    public async Task<List<LookupItem>> GetByCategoryAsync(string category)
    {
        var list = new List<LookupItem>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT Id, Category, Value FROM LookupItems WHERE Category = @Category ORDER BY Id", conn);
        cmd.Parameters.AddWithValue("@Category", category);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new LookupItem
            {
                Id = reader.GetInt32(0),
                Category = reader.GetString(1),
                Value = reader.GetString(2)
            });
        }
        return list;
    }

    public void Add(LookupItem lookupItem)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("INSERT INTO LookupItems (Category, Value) VALUES (@Category, @Value); SELECT SCOPE_IDENTITY();", conn);
        cmd.Parameters.AddWithValue("@Category", lookupItem.Category);
        cmd.Parameters.AddWithValue("@Value", lookupItem.Value);
        lookupItem.Id = Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void Delete(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("DELETE FROM LookupItems WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.ExecuteNonQuery();
    }
}
