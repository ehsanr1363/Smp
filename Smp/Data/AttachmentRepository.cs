using Microsoft.Data.SqlClient;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Smp.Data;

public class AttachmentRepository
{
    private readonly string _connectionString;

    public AttachmentRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<Attachment>> GetByEntityAsync(string entityType, int entityId)
    {
        var list = new List<Attachment>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
                SELECT Id, EntityType, EntityId, FileName, FileSize, UploadedAt
                FROM Attachments
                WHERE EntityType = @Type AND EntityId = @Id
                ORDER BY UploadedAt DESC";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Type", entityType);
        cmd.Parameters.AddWithValue("@Id", entityId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Attachment
            {
                Id = reader.IsDBNull(reader.GetOrdinal("Id")) ? 0 : reader.GetInt32(reader.GetOrdinal("Id")),
                EntityType = reader.IsDBNull(reader.GetOrdinal("EntityType")) ? string.Empty : reader.GetString(reader.GetOrdinal("EntityType")),
                EntityId = reader.IsDBNull(reader.GetOrdinal("EntityId")) ? 0 : reader.GetInt32(reader.GetOrdinal("EntityId")),
                FileName = reader.IsDBNull(reader.GetOrdinal("FileName")) ? string.Empty : reader.GetString(reader.GetOrdinal("FileName")),
                FileSize = reader.IsDBNull(reader.GetOrdinal("FileSize")) ? 0 : reader.GetInt64(reader.GetOrdinal("FileSize")),
                Description = string.Empty,
                CreatedAt = reader.IsDBNull(reader.GetOrdinal("UploadedAt")) ? DateTime.Now : reader.GetDateTime(reader.GetOrdinal("UploadedAt"))
            });
        }
        return list;
    }

    public async Task<List<Attachment>> GetByEmployeeIdAsync(int employeeId)
    {
        return await GetByEntityAsync("Employee", employeeId);
    }

    public async Task<HashSet<string>> GetAttachedEntityKeysAsync(List<string> entityTypes, List<int> entityIds)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (entityIds is null || entityIds.Count == 0 || entityTypes is null || entityTypes.Count == 0)
            return result;

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var typeParams = new List<string>();
        using var cmd = new SqlCommand { Connection = conn };

        for (int i = 0; i < entityTypes.Count; i++)
        {
            string pName = $"@t{i}";
            typeParams.Add(pName);
            cmd.Parameters.AddWithValue(pName, entityTypes[i]);
        }

        var idParams = new List<string>();
        for (int i = 0; i < entityIds.Count; i++)
        {
            string pName = $"@id{i}";
            idParams.Add(pName);
            cmd.Parameters.AddWithValue(pName, entityIds[i]);
        }

        cmd.CommandText = $"SELECT DISTINCT EntityType, EntityId FROM Attachments WHERE EntityType IN ({string.Join(",", typeParams)}) AND EntityId IN ({string.Join(",", idParams)})";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
            {
                result.Add($"{reader.GetString(0)}_{reader.GetInt32(1)}");
            }
        }
        return result;
    }

    public async Task<HashSet<int>> GetAttachedEntityIdsAsync(string entityType, List<int> entityIds)
    {
        var result = new HashSet<int>();
        if (entityIds is null || entityIds.Count == 0 || string.IsNullOrWhiteSpace(entityType))
            return result;

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand { Connection = conn };
        cmd.Parameters.AddWithValue("@Type", entityType);

        var idParams = new List<string>();
        for (int i = 0; i < entityIds.Count; i++)
        {
            string pName = $"@id{i}";
            idParams.Add(pName);
            cmd.Parameters.AddWithValue(pName, entityIds[i]);
        }

        cmd.CommandText = $"SELECT DISTINCT EntityId FROM Attachments WHERE EntityType = @Type AND EntityId IN ({string.Join(",", idParams)})";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(0))
            {
                result.Add(reader.GetInt32(0));
            }
        }
        return result;
    }

    public async Task<byte[]?> GetFileDataAsync(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = "SELECT FileData FROM Attachments WHERE Id = @Id";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);

        var result = await cmd.ExecuteScalarAsync();
        return result == DBNull.Value || result is null ? null : (byte[])result;
    }

    public async Task AddAsync(Attachment attachment)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
                INSERT INTO Attachments (EntityType, EntityId, FileName, FileData, FileSize)
                VALUES (@Type, @Id, @FileName, @FileData, @FileSize);
                SELECT SCOPE_IDENTITY();";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Type", attachment.EntityType);
        cmd.Parameters.AddWithValue("@Id", attachment.EntityId);
        cmd.Parameters.AddWithValue("@FileName", attachment.FileName);
        cmd.Parameters.Add("@FileData", SqlDbType.VarBinary, -1).Value = (object?)attachment.FileData ?? DBNull.Value;
        cmd.Parameters.AddWithValue("@FileSize", attachment.FileSize);

        var scalarResult = await cmd.ExecuteScalarAsync();
        attachment.Id = scalarResult is not null && scalarResult != DBNull.Value ? Convert.ToInt32(scalarResult) : 0;
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = "DELETE FROM Attachments WHERE Id = @Id";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
    }
}
