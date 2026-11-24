using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Threading.Tasks;

namespace Smp.Data;

public class DashboardRepository
{
    private readonly string _connectionString;

    public DashboardRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<DataTable> GetPayrollTrendAsync(int year, int currentMonth)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
                SELECT p.Month, SUM(p.TotalEarnings) AS TotalGross,
                       SUM(p.InsuranceEmployee + p.InsuranceEmployer) AS TotalInsurance,
                       SUM(p.IncomeTax) AS TotalTax
                FROM Payrolls p
                WHERE p.Year = @Year AND p.Month <= @CurrentMonth AND p.Month > (@CurrentMonth - 6)
                GROUP BY p.Month
                ORDER BY p.Month ASC";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Year", year);
        cmd.Parameters.AddWithValue("@CurrentMonth", currentMonth);

        using var adapter = new SqlDataAdapter(cmd);
        var dt = new DataTable();
        await Task.Run(() => adapter.Fill(dt));
        return dt;
    }

    public async Task<DataTable> GetDepartmentDistributionAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
                SELECT ISNULL(d.Name, 'بدون دپارتمان') AS DepartmentName, COUNT(e.Id) AS EmployeeCount
                FROM Employees e
                LEFT JOIN Departments d ON e.DepartmentId = d.Id
                WHERE ISNULL(e.IsActive, 1) = 1
                GROUP BY d.Name
                ORDER BY EmployeeCount DESC";

        using var cmd = new SqlCommand(sql, conn);
        using var adapter = new SqlDataAdapter(cmd);
        var dt = new DataTable();
        await Task.Run(() => adapter.Fill(dt));
        return dt;
    }

    public async Task<DataTable> GetEmployeeStatsAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
                SELECT 
                    COUNT(*) AS TotalCount,
                    SUM(CASE WHEN ISNULL(IsActive, 1) = 1 THEN 1 ELSE 0 END) AS ActiveCount,
                    SUM(CASE WHEN ISNULL(IsActive, 1) = 0 THEN 1 ELSE 0 END) AS InactiveCount,
                    ISNULL(AVG(CASE WHEN ISNULL(IsActive, 1) = 1 THEN BaseSalary END), 0) AS AvgSalary
                FROM Employees";

        using var cmd = new SqlCommand(sql, conn);
        using var adapter = new SqlDataAdapter(cmd);
        var dt = new DataTable();
        await Task.Run(() => adapter.Fill(dt));
        return dt;
    }

    public async Task<decimal> GetLastPayrollTotalAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
                SELECT ISNULL(SUM(TotalEarnings), 0) 
                FROM Payrolls 
                WHERE Year = (SELECT MAX(Year) FROM Payrolls) 
                  AND Month = (SELECT MAX(Month) FROM Payrolls WHERE Year = (SELECT MAX(Year) FROM Payrolls))";

        using var cmd = new SqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        return result == DBNull.Value ? 0 : Convert.ToDecimal(result);
    }

    // ✅ FIX: ContractEndDate از نوع DATETIME است → مستقیم DateTime پاس بده
    public async Task<int> GetExpiringContractsCountAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
                SELECT COUNT(*) FROM Employees
                WHERE ISNULL(IsActive, 1) = 1 
                  AND ContractEndDate IS NOT NULL 
                  AND ContractEndDate <= @Next30Days
                  AND ContractEndDate >= @Today";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Today", DateTime.Today);
        cmd.Parameters.AddWithValue("@Next30Days", DateTime.Today.AddDays(30));

        var result = await cmd.ExecuteScalarAsync();
        return result == DBNull.Value ? 0 : Convert.ToInt32(result);
    }

    public async Task<DataTable> GetEducationDistributionAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
                SELECT ISNULL(Education, 'نامشخص') AS Education, COUNT(Id) AS EmployeeCount
                FROM Employees
                WHERE ISNULL(IsActive, 1) = 1
                GROUP BY Education
                ORDER BY EmployeeCount DESC";

        using var cmd = new SqlCommand(sql, conn);
        using var adapter = new SqlDataAdapter(cmd);
        var dt = new DataTable();
        await Task.Run(() => adapter.Fill(dt));
        return dt;
    }

    public async Task<DataTable> GetPersonnelTypeDistributionAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
                SELECT ISNULL(PersonnelType, 'نامشخص') AS PersonnelType, COUNT(Id) AS EmployeeCount
                FROM Employees
                WHERE ISNULL(IsActive, 1) = 1
                GROUP BY PersonnelType
                ORDER BY EmployeeCount DESC";

        using var cmd = new SqlCommand(sql, conn);
        using var adapter = new SqlDataAdapter(cmd);
        var dt = new DataTable();
        await Task.Run(() => adapter.Fill(dt));
        return dt;
    }

    // ✅ FIX: datetime
    public async Task<DataTable> GetExpiringContractsListAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
                SELECT 
                    e.PersonnelCode AS [کد پرسنلی],
                    ISNULL(e.FirstName, '') + ' ' + ISNULL(e.LastName, '') AS [نام و نام خانوادگی],
                    ISNULL(d.Name, '---') AS [واحد],
                    e.ContractNumber AS [شماره قرارداد],
                    e.ContractEndDate AS [تاریخ پایان]
                FROM Employees e
                LEFT JOIN Departments d ON e.DepartmentId = d.Id
                WHERE ISNULL(e.IsActive, 1) = 1 
                  AND e.ContractEndDate IS NOT NULL 
                  AND e.ContractEndDate <= @Next30Days
                  AND e.ContractEndDate >= @Today
                ORDER BY e.ContractEndDate ASC";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Today", DateTime.Today);
        cmd.Parameters.AddWithValue("@Next30Days", DateTime.Today.AddDays(30));

        using var adapter = new SqlDataAdapter(cmd);
        var dt = new DataTable();
        await Task.Run(() => adapter.Fill(dt));
        return dt;
    }
}
