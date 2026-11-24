using Microsoft.Data.SqlClient;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Smp.Data;

public class AttendanceRepository
{
    private readonly string _connectionString;

    public AttendanceRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<Attendance>> GetAttendanceForMonthAsync(int year, int month)
    {
        var list = new List<Attendance>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // محاسبه تاریخ شروع و پایان ماه شمسی به میلادی و تعداد روزهای ماه
        var pc = new PersianCalendar();
        DateTime startDate = pc.ToDateTime(year, month, 1, 0, 0, 0, 0);
        int daysInMonth = pc.GetDaysInMonth(year, month);
        DateTime endDate = pc.ToDateTime(year, month, daysInMonth, 23, 59, 59, 0);

        // ماهی که برای پرسنل کارکردی ثبت نشده باشد، روزهای کارکرد = ۰ (نه تعداد روزهای ماه)
        string sql = @"
                SELECT e.Id as EmployeeId, e.PersonnelCode, e.FirstName + ' ' + e.LastName as EmployeeName, ISNULL(e.PersonnelType, 'ماهانه') as PersonnelType,
                       ISNULL(a.Id, 0) as Id, 
                       ISNULL(a.WorkingDays, 0) as WorkingDays, ISNULL(a.HolidayDays, 0) as HolidayDays,
                       ISNULL(a.HoursWorked, 0) as HoursWorked, ISNULL(a.OvertimeHours, 0) as OvertimeHours, ISNULL(a.AbsenceDays, 0) as AbsenceDays,
                       ISNULL(a.HourlyLeave, 0) as HourlyLeave, ISNULL(a.DailyLeave, 0) as DailyLeave,
                       ISNULL(a.TotalPresence, 0) as TotalPresence, ISNULL(a.GrossOvertime, 0) as GrossOvertime,
                       ISNULL(a.AbsenceHours, 0) as AbsenceHours, ISNULL(a.NetOvertime, 0) as NetOvertime,
                       ISNULL(a.PayableOvertime, 0) as PayableOvertime, ISNULL(a.LunchDays, 0) as LunchDays,
                       ISNULL(a.OtherOvertime, 0) as OtherOvertime, ISNULL(a.EarlyOvertime, 0) as EarlyOvertime
                FROM Employees e
                LEFT JOIN Attendance a ON e.Id = a.EmployeeId AND a.Year = @Year AND a.Month = @Month
                WHERE ISNULL(e.IsActive, 1) = 1
                  AND EXISTS (
                      SELECT 1 FROM EmployeeContracts ec 
                      WHERE ec.EmployeeId = e.Id 
                        AND ec.StartDate <= @EndDate 
                        AND (ec.EndDate IS NULL OR ec.EndDate >= @StartDate)
                  )
                ORDER BY e.LastName, e.FirstName";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Year", year);
        cmd.Parameters.AddWithValue("@Month", month);
        cmd.Parameters.AddWithValue("@StartDate", startDate);
        cmd.Parameters.AddWithValue("@EndDate", endDate);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Attendance
            {
                EmployeeId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                PersonnelCode = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                EmployeeName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                PersonnelType = reader.IsDBNull(3) ? "ماهانه" : reader.GetString(3).Trim(),
                Id = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                WorkingDays = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                HolidayDays = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                HoursWorked = reader.IsDBNull(7) ? 0m : reader.GetDecimal(7),
                OvertimeHours = reader.IsDBNull(8) ? 0m : reader.GetDecimal(8),
                AbsenceDays = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                HourlyLeave = reader.IsDBNull(10) ? 0m : reader.GetDecimal(10),
                DailyLeave = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                TotalPresence = reader.IsDBNull(12) ? 0m : reader.GetDecimal(12),
                GrossOvertime = reader.IsDBNull(13) ? 0m : reader.GetDecimal(13),
                AbsenceHours = reader.IsDBNull(14) ? 0m : reader.GetDecimal(14),
                NetOvertime = reader.IsDBNull(15) ? 0m : reader.GetDecimal(15),
                PayableOvertime = reader.IsDBNull(16) ? 0m : reader.GetDecimal(16),
                LunchDays = reader.IsDBNull(17) ? 0 : reader.GetInt32(17),
                OtherOvertime = reader.IsDBNull(18) ? 0m : reader.GetDecimal(18),
                EarlyOvertime = reader.IsDBNull(19) ? 0m : reader.GetDecimal(19),
                Year = year,
                Month = month
            });
        }
        return list;
    }

    /// <summary>
    /// جمع روزهای کارکرد ثبت‌شده در حضور و غیاب، به تفکیک پرسنل و سال شمسی: [پرسنل][سال] = جمع روز.
    /// روز کارکرد هر ماه = روزهای عادی + روزهای تعطیلِ کارکرده، حداکثر به اندازه‌ی روزهای همان ماه.
    /// employeeId / year خالی = همه.
    /// </summary>
    public async Task<Dictionary<int, Dictionary<int, int>>> GetWorkedDaysAsync(int? employeeId = null, int? year = null)
    {
        var map = new Dictionary<int, Dictionary<int, int>>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
                SELECT EmployeeId, Year, Month, ISNULL(WorkingDays, 0), ISNULL(HolidayDays, 0)
                FROM Attendance
                WHERE (@Emp IS NULL OR EmployeeId = @Emp) AND (@Year IS NULL OR Year = @Year)";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@Emp", System.Data.SqlDbType.Int) { Value = (object?)employeeId ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Year", System.Data.SqlDbType.Int) { Value = (object?)year ?? DBNull.Value });

        var pc = new PersianCalendar();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            int empId = reader.GetInt32(0);
            int y = reader.GetInt32(1);
            int m = reader.GetInt32(2);
            int worked = Math.Max(0, reader.GetInt32(3)) + Math.Max(0, reader.GetInt32(4));
            if (m >= 1 && m <= 12 && y >= 1 && y <= 9378)
                worked = Math.Min(worked, pc.GetDaysInMonth(y, m));

            if (!map.TryGetValue(empId, out var years)) map[empId] = years = new Dictionary<int, int>();
            years[y] = (years.TryGetValue(y, out var cur) ? cur : 0) + worked;
        }
        return map;
    }

    public async Task SaveAttendanceBatchAsync(List<Attendance> attendances)
    {
        if (attendances is null || attendances.Count == 0) return;

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        try
        {
            foreach (var att in attendances)
            {
                string sql = @"
                        IF EXISTS (SELECT 1 FROM Attendance WHERE EmployeeId = @EmpId AND Year = @Year AND Month = @Month)
                        BEGIN
                            UPDATE Attendance SET 
                                WorkingDays = @WorkingDays, HolidayDays = @HolidayDays,
                                HoursWorked = @HoursWorked, OvertimeHours = @OvertimeHours, AbsenceDays = @AbsenceDays,
                                HourlyLeave = @HourlyLeave, DailyLeave = @DailyLeave,
                                TotalPresence = @TotalPresence, GrossOvertime = @GrossOvertime,
                                AbsenceHours = @AbsenceHours, NetOvertime = @NetOvertime,
                                PayableOvertime = @PayableOvertime, LunchDays = @LunchDays,
                                OtherOvertime = @OtherOvertime, EarlyOvertime = @EarlyOvertime
                            WHERE EmployeeId = @EmpId AND Year = @Year AND Month = @Month
                        END
                        ELSE
                        BEGIN
                            INSERT INTO Attendance (EmployeeId, Year, Month, WorkingDays, HolidayDays, HoursWorked, OvertimeHours, AbsenceDays,
                                                    HourlyLeave, DailyLeave, TotalPresence, GrossOvertime, AbsenceHours, NetOvertime, PayableOvertime, LunchDays, OtherOvertime, EarlyOvertime)
                            VALUES (@EmpId, @Year, @Month, @WorkingDays, @HolidayDays, @HoursWorked, @OvertimeHours, @AbsenceDays,
                                    @HourlyLeave, @DailyLeave, @TotalPresence, @GrossOvertime, @AbsenceHours, @NetOvertime, @PayableOvertime, @LunchDays, @OtherOvertime, @EarlyOvertime)
                        END";

                using var cmd = new SqlCommand(sql, conn, transaction);
                cmd.Parameters.AddWithValue("@EmpId", att.EmployeeId);
                cmd.Parameters.AddWithValue("@Year", att.Year);
                cmd.Parameters.AddWithValue("@Month", att.Month);
                cmd.Parameters.AddWithValue("@WorkingDays", att.WorkingDays);
                cmd.Parameters.AddWithValue("@HolidayDays", att.HolidayDays);
                cmd.Parameters.AddWithValue("@HoursWorked", att.HoursWorked);
                cmd.Parameters.AddWithValue("@OvertimeHours", att.OvertimeHours);
                cmd.Parameters.AddWithValue("@AbsenceDays", att.AbsenceDays);
                cmd.Parameters.AddWithValue("@HourlyLeave", att.HourlyLeave);
                cmd.Parameters.AddWithValue("@DailyLeave", att.DailyLeave);
                cmd.Parameters.AddWithValue("@TotalPresence", att.TotalPresence);
                cmd.Parameters.AddWithValue("@GrossOvertime", att.GrossOvertime);
                cmd.Parameters.AddWithValue("@AbsenceHours", att.AbsenceHours);
                cmd.Parameters.AddWithValue("@NetOvertime", att.NetOvertime);
                cmd.Parameters.AddWithValue("@PayableOvertime", att.PayableOvertime);
                cmd.Parameters.AddWithValue("@LunchDays", att.LunchDays);
                cmd.Parameters.AddWithValue("@OtherOvertime", att.OtherOvertime);
                cmd.Parameters.AddWithValue("@EarlyOvertime", att.EarlyOvertime);

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
