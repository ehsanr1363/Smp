using Microsoft.Data.SqlClient;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Smp.Data;

public class MissionRepository
{
    private readonly string _connectionString;
    private static bool _tableEnsured;

    public MissionRepository(string connectionString) => _connectionString = connectionString;

    /// <summary>اگر جدول ماموریت‌ها نبود ساخته می‌شود (مثل الگوی EnsureColumns در تنظیمات حقوق).</summary>
    private static async Task EnsureTableAsync(SqlConnection conn)
    {
        if (_tableEnsured) return;
        const string sql = @"
IF OBJECT_ID(N'dbo.MissionRecords', N'U') IS NULL
CREATE TABLE dbo.MissionRecords (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId INT NOT NULL,
    Province NVARCHAR(100) NULL, Location NVARCHAR(200) NULL, Subject NVARCHAR(500) NULL,
    DepartDate DATE NOT NULL, DepartTime NVARCHAR(5) NULL, ReturnDate DATE NOT NULL, ReturnTime NVARCHAR(5) NULL,
    DaysCount INT NOT NULL DEFAULT 0,
    SalaryBase DECIMAL(18,0) NOT NULL DEFAULT 0, MissionRate DECIMAL(9,4) NOT NULL DEFAULT 0,
    DailyAmount DECIMAL(18,0) NOT NULL DEFAULT 0, DaysTotal DECIMAL(18,0) NOT NULL DEFAULT 0,
    NightsCount INT NOT NULL DEFAULT 0, NightUnit DECIMAL(18,0) NOT NULL DEFAULT 0, NightsTotal DECIMAL(18,0) NOT NULL DEFAULT 0,
    StayCount INT NOT NULL DEFAULT 0, StayUnit DECIMAL(18,0) NOT NULL DEFAULT 0, StayTotal DECIMAL(18,0) NOT NULL DEFAULT 0,
    TravelCost DECIMAL(18,0) NOT NULL DEFAULT 0,
    LunchCount INT NOT NULL DEFAULT 0, LunchUnit DECIMAL(18,0) NOT NULL DEFAULT 0, LunchTotal DECIMAL(18,0) NOT NULL DEFAULT 0,
    GrandTotal DECIMAL(18,0) NOT NULL DEFAULT 0,
    Notes NVARCHAR(1000) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_MissionRecords_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id)
);";
        using (var cmd = new SqlCommand(sql, conn))
            await cmd.ExecuteNonQueryAsync();
        _tableEnsured = true;
    }

    private const string SelectSql = @"
SELECT m.*, e.FirstName + N' ' + e.LastName AS EmployeeName
FROM MissionRecords m
INNER JOIN Employees e ON e.Id = m.EmployeeId";

    public async Task<List<MissionRecord>> GetAllAsync(int? employeeId = null)
    {
        var list = new List<MissionRecord>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await EnsureTableAsync(conn);

        string sql = SelectSql + (employeeId.HasValue ? " WHERE m.EmployeeId = @Emp" : "") + " ORDER BY m.DepartDate DESC, m.Id DESC";
        using var cmd = new SqlCommand(sql, conn);
        if (employeeId.HasValue) cmd.Parameters.AddWithValue("@Emp", employeeId.Value);

        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(Map(r));
        return list;
    }

    public async Task<int> SaveAsync(MissionRecord m)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await EnsureTableAsync(conn);

        string sql = m.Id == 0
            ? @"INSERT INTO MissionRecords
                    (EmployeeId, Province, Location, Subject, DepartDate, DepartTime, ReturnDate, ReturnTime, DaysCount,
                     SalaryBase, MissionRate, DailyAmount, DaysTotal, NightsCount, NightUnit, NightsTotal,
                     StayCount, StayUnit, StayTotal, TravelCost, LunchCount, LunchUnit, LunchTotal, GrandTotal, Notes)
                    VALUES
                    (@EmployeeId, @Province, @Location, @Subject, @DepartDate, @DepartTime, @ReturnDate, @ReturnTime, @DaysCount,
                     @SalaryBase, @MissionRate, @DailyAmount, @DaysTotal, @NightsCount, @NightUnit, @NightsTotal,
                     @StayCount, @StayUnit, @StayTotal, @TravelCost, @LunchCount, @LunchUnit, @LunchTotal, @GrandTotal, @Notes);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);"
            : @"UPDATE MissionRecords SET
                    EmployeeId=@EmployeeId, Province=@Province, Location=@Location, Subject=@Subject,
                    DepartDate=@DepartDate, DepartTime=@DepartTime, ReturnDate=@ReturnDate, ReturnTime=@ReturnTime, DaysCount=@DaysCount,
                    SalaryBase=@SalaryBase, MissionRate=@MissionRate, DailyAmount=@DailyAmount, DaysTotal=@DaysTotal,
                    NightsCount=@NightsCount, NightUnit=@NightUnit, NightsTotal=@NightsTotal,
                    StayCount=@StayCount, StayUnit=@StayUnit, StayTotal=@StayTotal, TravelCost=@TravelCost,
                    LunchCount=@LunchCount, LunchUnit=@LunchUnit, LunchTotal=@LunchTotal, GrandTotal=@GrandTotal, Notes=@Notes
                    WHERE Id=@Id;
                    SELECT @Id;";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", m.Id);
        cmd.Parameters.AddWithValue("@EmployeeId", m.EmployeeId);
        cmd.Parameters.AddWithValue("@Province", m.Province ?? "");
        cmd.Parameters.AddWithValue("@Location", m.Location ?? "");
        cmd.Parameters.AddWithValue("@Subject", m.Subject ?? "");
        cmd.Parameters.AddWithValue("@DepartDate", m.DepartDate.Date);
        cmd.Parameters.AddWithValue("@DepartTime", m.DepartTime ?? "");
        cmd.Parameters.AddWithValue("@ReturnDate", m.ReturnDate.Date);
        cmd.Parameters.AddWithValue("@ReturnTime", m.ReturnTime ?? "");
        cmd.Parameters.AddWithValue("@DaysCount", m.DaysCount);
        cmd.Parameters.AddWithValue("@SalaryBase", m.SalaryBase);
        cmd.Parameters.AddWithValue("@MissionRate", m.MissionRate);
        cmd.Parameters.AddWithValue("@DailyAmount", m.DailyAmount);
        cmd.Parameters.AddWithValue("@DaysTotal", m.DaysTotal);
        cmd.Parameters.AddWithValue("@NightsCount", m.NightsCount);
        cmd.Parameters.AddWithValue("@NightUnit", m.NightUnit);
        cmd.Parameters.AddWithValue("@NightsTotal", m.NightsTotal);
        cmd.Parameters.AddWithValue("@StayCount", m.StayCount);
        cmd.Parameters.AddWithValue("@StayUnit", m.StayUnit);
        cmd.Parameters.AddWithValue("@StayTotal", m.StayTotal);
        cmd.Parameters.AddWithValue("@TravelCost", m.TravelCost);
        cmd.Parameters.AddWithValue("@LunchCount", m.LunchCount);
        cmd.Parameters.AddWithValue("@LunchUnit", m.LunchUnit);
        cmd.Parameters.AddWithValue("@LunchTotal", m.LunchTotal);
        cmd.Parameters.AddWithValue("@GrandTotal", m.GrandTotal);
        cmd.Parameters.AddWithValue("@Notes", (object?)m.Notes ?? DBNull.Value);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await EnsureTableAsync(conn);
        using var cmd = new SqlCommand("DELETE FROM MissionRecords WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string Str(SqlDataReader r, string col) => r[col] == DBNull.Value ? "" : r[col].ToString() ?? "";
    private static decimal Dec(SqlDataReader r, string col) => r[col] == DBNull.Value ? 0m : Convert.ToDecimal(r[col]);
    private static int Int(SqlDataReader r, string col) => r[col] == DBNull.Value ? 0 : Convert.ToInt32(r[col]);

    private static MissionRecord Map(SqlDataReader r) => new MissionRecord
    {
        Id = Int(r, "Id"),
        EmployeeId = Int(r, "EmployeeId"),
        EmployeeName = Str(r, "EmployeeName"),
        Province = Str(r, "Province"),
        Location = Str(r, "Location"),
        Subject = Str(r, "Subject"),
        DepartDate = Convert.ToDateTime(r["DepartDate"]),
        DepartTime = Str(r, "DepartTime"),
        ReturnDate = Convert.ToDateTime(r["ReturnDate"]),
        ReturnTime = Str(r, "ReturnTime"),
        DaysCount = Int(r, "DaysCount"),
        SalaryBase = Dec(r, "SalaryBase"),
        MissionRate = Dec(r, "MissionRate"),
        DailyAmount = Dec(r, "DailyAmount"),
        DaysTotal = Dec(r, "DaysTotal"),
        NightsCount = Int(r, "NightsCount"),
        NightUnit = Dec(r, "NightUnit"),
        NightsTotal = Dec(r, "NightsTotal"),
        StayCount = Int(r, "StayCount"),
        StayUnit = Dec(r, "StayUnit"),
        StayTotal = Dec(r, "StayTotal"),
        TravelCost = Dec(r, "TravelCost"),
        LunchCount = Int(r, "LunchCount"),
        LunchUnit = Dec(r, "LunchUnit"),
        LunchTotal = Dec(r, "LunchTotal"),
        GrandTotal = Dec(r, "GrandTotal"),
        Notes = r["Notes"] == DBNull.Value ? null : r["Notes"].ToString(),
        CreatedAt = r["CreatedAt"] == DBNull.Value ? null : Convert.ToDateTime(r["CreatedAt"])
    };
}
