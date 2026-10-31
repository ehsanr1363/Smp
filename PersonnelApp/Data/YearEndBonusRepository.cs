using Microsoft.Data.SqlClient;
using PersonnelApp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PersonnelApp.Data
{
    public class YearEndBonusRepository
    {
        private readonly string _connectionString;

        public YearEndBonusRepository(string connectionString)
        {
            _connectionString = connectionString;
            // 👈 EnsureTableExists حذف شد
        }

        public async Task SaveBonusesBatchAsync(List<YearEndBonusRecord> bonuses)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                foreach (var b in bonuses)
                {
                    string sql = @"
                        IF EXISTS (SELECT 1 FROM YearEndBonuses WHERE EmployeeId = @EmpId AND Year = @Year)
                        BEGIN
                            UPDATE YearEndBonuses SET 
                                MonthsWorked = @Months, BaseSalary = @Base,
                                BonusAmount = @Bonus, Tax = @Tax, NetBonus = @Net, CreatedAt = GETDATE()
                            WHERE EmployeeId = @EmpId AND Year = @Year
                        END
                        ELSE
                        BEGIN
                            INSERT INTO YearEndBonuses (EmployeeId, Year, MonthsWorked, BaseSalary, BonusAmount, Tax, NetBonus)
                            VALUES (@EmpId, @Year, @Months, @Base, @Bonus, @Tax, @Net)
                        END";

                    using var cmd = new SqlCommand(sql, conn, transaction);
                    cmd.Parameters.AddWithValue("@EmpId", b.EmployeeId);
                    cmd.Parameters.AddWithValue("@Year", b.Year);
                    cmd.Parameters.AddWithValue("@Months", b.MonthsWorked);
                    cmd.Parameters.AddWithValue("@Base", b.BaseSalary);
                    cmd.Parameters.AddWithValue("@Bonus", b.BonusAmount);
                    cmd.Parameters.AddWithValue("@Tax", b.Tax);
                    cmd.Parameters.AddWithValue("@Net", b.NetBonus);

                    await cmd.ExecuteNonQueryAsync();
                }
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}