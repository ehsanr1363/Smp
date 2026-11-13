using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SMP.Data
{
    public class AlertRepository
    {
        private readonly string _connectionString;

        public AlertRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        // ✅ FIX 1: تبدیل به Async برای جلوگیری از فریز شدن UI
        // ✅ FIX 2: اصلاح شرط زمان (استفاده از < روز بعد) برای جلوگیری از جا افتادن روز سی‌ام
        public async Task<(int ExpiringGuarantees, int ExpiringContracts)> GetActiveAlertsAsync()
        {
            DateTime today = DateTime.Today;
            DateTime nextDayThreshold = today.AddDays(31); // روز بعد از مهلت ۳۰ روزه

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            int expiringGuarantees = 0;
            using (var cmd = new SqlCommand(@"
                SELECT COUNT(*) 
                FROM ContractorGuarantees 
                WHERE Status = N'فعال' 
                  AND MaturityDate IS NOT NULL
                  AND CAST(MaturityDate AS DATE) >= @Today 
                  AND CAST(MaturityDate AS DATE) <= @Threshold", conn))
            {
                cmd.Parameters.AddWithValue("@Today", today);
                cmd.Parameters.AddWithValue("@Threshold", today.AddDays(30));
                var result = await cmd.ExecuteScalarAsync();
                expiringGuarantees = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            }

            int expiringContracts = 0;
            using (var cmd = new SqlCommand(@"
                SELECT COUNT(*) 
                FROM ContractorContracts 
                WHERE Status = N'فعال' 
                  AND EndDate IS NOT NULL
                  AND CAST(EndDate AS DATE) >= @Today 
                  AND CAST(EndDate AS DATE) <= @Threshold", conn))
            {
                cmd.Parameters.AddWithValue("@Today", today);
                cmd.Parameters.AddWithValue("@Threshold", today.AddDays(30));
                var result = await cmd.ExecuteScalarAsync();
                expiringContracts = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            }

            return (expiringGuarantees, expiringContracts);
        }
    }
}