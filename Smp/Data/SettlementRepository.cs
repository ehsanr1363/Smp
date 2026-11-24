using Microsoft.Data.SqlClient;
using Smp.Models;
using System;
using System.Threading.Tasks;

namespace Smp.Data;

public class SettlementRepository
{
    private readonly string _connectionString;

    public SettlementRepository(string connectionString)
    {
        _connectionString = connectionString;
        // 👈 EnsureTableExists حذف شد
    }

    public async Task SaveSettlementAsync(SettlementRecord record)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
                INSERT INTO Settlements (EmployeeId, SettlementDate, YearsOfWork, ServiceBonus, LeaveBonus, SeverancePay, RemainingLoan, NetSettlement)
                VALUES (@EmpId, @Date, @Years, @Service, @Leave, @Severance, @Loan, @Net)";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@EmpId", record.EmployeeId);
        cmd.Parameters.AddWithValue("@Date", record.SettlementDate);
        cmd.Parameters.AddWithValue("@Years", record.YearsOfWork);
        cmd.Parameters.AddWithValue("@Service", record.ServiceBonus);
        cmd.Parameters.AddWithValue("@Leave", record.LeaveBonus);
        cmd.Parameters.AddWithValue("@Severance", record.SeverancePay);
        cmd.Parameters.AddWithValue("@Loan", record.RemainingLoan);
        cmd.Parameters.AddWithValue("@Net", record.NetSettlement);

        await cmd.ExecuteNonQueryAsync();
        Smp.Helpers.AuditHelper.Log("ثبت", "تسویه حساب", $"پرسنل با شناسه {record.EmployeeId}");
    }
}
