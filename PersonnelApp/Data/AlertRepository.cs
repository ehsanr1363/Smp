using System;
using Microsoft.Data.SqlClient;

namespace PersonnelApp.Data
{
    public class AlertRepository
    {
        private readonly string _connectionString;

        public AlertRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public (int ExpiringGuarantees, int ExpiringContracts) GetActiveAlerts()
        {
            DateTime today = DateTime.Today;
            DateTime warningThreshold = today.AddDays(30); // ۳۰ روز آینده

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            // ✅ FIX P1: فیلتر تاریخ در SQL Server — نه خواندن کل جدول و فیلتر در C#
            int expiringGuarantees;
            using (var cmd = new SqlCommand(@"
                SELECT COUNT(*) 
                FROM ContractorGuarantees 
                WHERE Status = N'فعال' 
                  AND MaturityDate IS NOT NULL
                  AND MaturityDate >= @Today 
                  AND MaturityDate <= @Threshold", conn))
            {
                cmd.Parameters.AddWithValue("@Today", today);
                cmd.Parameters.AddWithValue("@Threshold", warningThreshold);
                expiringGuarantees = Convert.ToInt32(cmd.ExecuteScalar());
            }

            int expiringContracts;
            using (var cmd = new SqlCommand(@"
                SELECT COUNT(*) 
                FROM ContractorContracts 
                WHERE Status = N'فعال' 
                  AND EndDate IS NOT NULL
                  AND EndDate >= @Today 
                  AND EndDate <= @Threshold", conn))
            {
                cmd.Parameters.AddWithValue("@Today", today);
                cmd.Parameters.AddWithValue("@Threshold", warningThreshold);
                expiringContracts = Convert.ToInt32(cmd.ExecuteScalar());
            }

            return (expiringGuarantees, expiringContracts);
        }
    }
}