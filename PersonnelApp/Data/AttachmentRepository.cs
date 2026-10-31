using Microsoft.Data.SqlClient;
using PersonnelApp.Models;
using System;
using System.Collections.Generic;
using System.Data;

namespace PersonnelApp.Data
{
    public class AttachmentRepository
    {
        private readonly string _connectionString;

        public AttachmentRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<Attachment> GetByEntity(string entityType, int entityId)
        {
            var list = new List<Attachment>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            // ✅ FIX P0: جدول Attachments ستون Description و CreatedAt ندارد
            // فقط UploadedAt دارد
            string sql = @"
                SELECT Id, EntityType, EntityId, FileName, FileSize, UploadedAt
                FROM Attachments
                WHERE EntityType = @Type AND EntityId = @Id
                ORDER BY UploadedAt DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Type", entityType);
            cmd.Parameters.AddWithValue("@Id", entityId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Attachment
                {
                    Id = reader.GetInt32(0),
                    EntityType = reader.GetString(1),
                    EntityId = reader.GetInt32(2),
                    FileName = reader.GetString(3),
                    FileSize = reader.GetInt64(4),
                    Description = "",
                    CreatedAt = reader.IsDBNull(5) ? DateTime.Now : reader.GetDateTime(5)
                });
            }
            return list;
        }

        public List<Attachment> GetByEmployeeId(int employeeId)
        {
            return GetByEntity("Employee", employeeId);
        }

        public byte[]? GetFileData(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string sql = "SELECT FileData FROM Attachments WHERE Id = @Id";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);

            var result = cmd.ExecuteScalar();
            return result == DBNull.Value || result == null ? null : (byte[])result;
        }

        public void Add(Attachment attachment)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            // ✅ FIX P0: فقط ستون‌های موجود در جدول
            string sql = @"
                INSERT INTO Attachments (EntityType, EntityId, FileName, FileData, FileSize)
                VALUES (@Type, @Id, @FileName, @FileData, @FileSize);
                SELECT SCOPE_IDENTITY();";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Type", attachment.EntityType);
            cmd.Parameters.AddWithValue("@Id", attachment.EntityId);
            cmd.Parameters.AddWithValue("@FileName", attachment.FileName);
            cmd.Parameters.Add("@FileData", SqlDbType.VarBinary, -1).Value = attachment.FileData;
            cmd.Parameters.AddWithValue("@FileSize", attachment.FileSize);

            attachment.Id = Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void Delete(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string sql = "DELETE FROM Attachments WHERE Id = @Id";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }
    }
}