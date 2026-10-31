using Microsoft.Data.SqlClient;
using PersonnelApp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PersonnelApp.Data
{
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

            string sql = @"
                SELECT e.Id as EmployeeId, e.PersonnelCode, e.FirstName + ' ' + e.LastName as EmployeeName, ISNULL(e.PersonnelType, 'ماهانه') as PersonnelType,
                       ISNULL(a.Id, 0) as Id, 
                       ISNULL(a.WorkingDays, 30) as WorkingDays, ISNULL(a.HolidayDays, 0) as HolidayDays,
                       ISNULL(a.HoursWorked, 0) as HoursWorked, ISNULL(a.OvertimeHours, 0) as OvertimeHours, ISNULL(a.AbsenceDays, 0) as AbsenceDays,
                       ISNULL(a.HourlyLeave, 0) as HourlyLeave, ISNULL(a.DailyLeave, 0) as DailyLeave,
                       ISNULL(a.TotalPresence, 0) as TotalPresence, ISNULL(a.GrossOvertime, 0) as GrossOvertime,
                       ISNULL(a.AbsenceHours, 0) as AbsenceHours, ISNULL(a.NetOvertime, 0) as NetOvertime,
                       ISNULL(a.PayableOvertime, 0) as PayableOvertime, ISNULL(a.LunchDays, 0) as LunchDays,
                       ISNULL(a.OtherOvertime, 0) as OtherOvertime, ISNULL(a.EarlyOvertime, 0) as EarlyOvertime
                FROM Employees e
                LEFT JOIN Attendance a ON e.Id = a.EmployeeId AND a.Year = @Year AND a.Month = @Month
                WHERE ISNULL(e.IsActive, 1) = 1
                ORDER BY e.LastName, e.FirstName";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Year", year);
            cmd.Parameters.AddWithValue("@Month", month);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new Attendance
                {
                    EmployeeId = reader.GetInt32(0),
                    PersonnelCode = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    EmployeeName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    PersonnelType = reader.IsDBNull(3) ? "ماهانه" : reader.GetString(3).Trim(),
                    Id = reader.GetInt32(4),
                    WorkingDays = reader.GetInt32(5),
                    HolidayDays = reader.GetInt32(6),
                    HoursWorked = reader.GetDecimal(7),
                    OvertimeHours = reader.GetDecimal(8),
                    AbsenceDays = reader.GetInt32(9),
                    HourlyLeave = reader.GetDecimal(10),
                    DailyLeave = reader.GetInt32(11),
                    TotalPresence = reader.GetDecimal(12),
                    GrossOvertime = reader.GetDecimal(13),
                    AbsenceHours = reader.GetDecimal(14),
                    NetOvertime = reader.GetDecimal(15),
                    PayableOvertime = reader.GetDecimal(16),
                    LunchDays = reader.GetInt32(17),
                    OtherOvertime = reader.GetDecimal(18),
                    EarlyOvertime = reader.GetDecimal(19),
                    Year = year,
                    Month = month
                });
            }
            return list;
        }

        public async Task SaveAttendanceBatchAsync(List<Attendance> attendances)
        {
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
}