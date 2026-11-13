using Microsoft.Data.SqlClient;
using System;
using System.Threading.Tasks;

namespace SMP.Data
{
    public class ContractTemplateRepository
    {
        private readonly string _connectionString;

        public ContractTemplateRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<string?> GetActiveTemplateContentAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new SqlCommand("SELECT TOP 1 Content FROM ContractTemplates WHERE IsActive = 1 ORDER BY Id DESC", conn);
            var result = await cmd.ExecuteScalarAsync();
            return result != null && result != DBNull.Value ? result.ToString() : null;
        }

        public async Task SaveTemplateAsync(string content)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // غیرفعال کردن قالب‌های قبلی
            using (var cmd = new SqlCommand("UPDATE ContractTemplates SET IsActive = 0", conn))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // درج قالب جدید
            using (var cmd = new SqlCommand("INSERT INTO ContractTemplates (Title, Content, IsActive) VALUES (N'قالب اصلی', @Content, 1)", conn))
            {
                cmd.Parameters.AddWithValue("@Content", content);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}