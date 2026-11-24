using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Smp.Models;
using Smp.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Smp.Data;

public class EmployeeContractRepository
{
    private readonly string _connectionString;

    public EmployeeContractRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<EmployeeContract>> GetContractsByEmployeeIdAsync(int employeeId)
    {
        var list = new List<EmployeeContract>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
                SELECT c.*, e.FirstName + ' ' + e.LastName AS EmployeeName
                FROM EmployeeContracts c
                INNER JOIN Employees e ON c.EmployeeId = e.Id
                WHERE c.EmployeeId = @EmpId
                ORDER BY c.StartDate DESC";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@EmpId", employeeId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapReader(reader));
        }
        return list;
    }

    /// <summary>بازه‌ی همه‌ی قراردادها (فقط شناسه‌ی پرسنل، شروع و پایان) به تفکیک پرسنل؛ برای محاسبه‌ی روزهای کارکرد</summary>
    public async Task<Dictionary<int, List<EmployeeContract>>> GetAllContractPeriodsAsync()
    {
        var map = new Dictionary<int, List<EmployeeContract>>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand("SELECT EmployeeId, StartDate, EndDate FROM EmployeeContracts", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            int empId = reader.GetInt32(0);
            if (!map.TryGetValue(empId, out var list)) map[empId] = list = new List<EmployeeContract>();
            list.Add(new EmployeeContract
            {
                EmployeeId = empId,
                StartDate = reader.GetDateTime(1),
                EndDate = reader.IsDBNull(2) ? null : reader.GetDateTime(2)
            });
        }
        return map;
    }

    /// <summary>
    /// شناسه‌ی کارمندانی که در بازه‌ی [from, to] حکم/قرارداد دارند
    /// (شروع حکم ≤ پایان بازه و پایان حکم خالی یا ≥ ابتدای بازه).
    /// </summary>
    public async Task<HashSet<int>> GetEmployeeIdsWithContractInRangeAsync(DateTime from, DateTime to)
    {
        var ids = new HashSet<int>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
                SELECT DISTINCT EmployeeId
                FROM EmployeeContracts
                WHERE StartDate <= @To
                  AND (EndDate IS NULL OR EndDate >= @From)";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@From", from);
        cmd.Parameters.AddWithValue("@To", to);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) ids.Add(reader.GetInt32(0));
        return ids;
    }

    public async Task<EmployeeContract?> GetActiveContractByEmployeeIdAsync(int employeeId)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
                SELECT TOP 1 c.*, e.FirstName + ' ' + e.LastName AS EmployeeName
                FROM EmployeeContracts c
                INNER JOIN Employees e ON c.EmployeeId = e.Id
                WHERE c.EmployeeId = @EmpId AND c.Status = N'فعال'
                ORDER BY c.StartDate DESC";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@EmpId", employeeId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return MapReader(reader);
    }

    // ✅ متد جدید که باعث خطای CS1061 شده بود
    public async Task SetActiveContractEndDateAsync(int contractId, DateTime? endDate)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        try
        {
            int employeeId = 0;

            // ۱. پیدا کردن شناسه کارمند مربوط به این قرارداد فعال
            using (var cmdGet = new SqlCommand("SELECT EmployeeId FROM EmployeeContracts WHERE Id = @Id AND Status = N'فعال'", conn, transaction))
            {
                cmdGet.Parameters.AddWithValue("@Id", contractId);
                var result = await cmdGet.ExecuteScalarAsync();
                if (result is null || result == DBNull.Value)
                    throw new InvalidOperationException("قرارداد فعال مورد نظر یافت نشد.");

                employeeId = Convert.ToInt32(result);
            }

            // ۲. بروزرسانی تاریخ پایان در جدول قراردادها (EmployeeContracts)
            using (var cmd = new SqlCommand("UPDATE EmployeeContracts SET EndDate = @EndDate WHERE Id = @Id", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@EndDate", (object?)endDate?.Date ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Id", contractId);
                await cmd.ExecuteNonQueryAsync();
            }

            // ۳. بروزرسانی تاریخ پایان در جدول اصلی کارمندان (Employees)
            using (var cmdEmp = new SqlCommand("UPDATE Employees SET ContractEndDate = @EndDate WHERE Id = @EmployeeId", conn, transaction))
            {
                cmdEmp.Parameters.AddWithValue("@EndDate", (object?)endDate?.Date ?? DBNull.Value);
                cmdEmp.Parameters.AddWithValue("@EmployeeId", employeeId);
                await cmdEmp.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task AddNewContractAsync(EmployeeContract c)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        try
        {
            // ۰. کنترل تاریخ: شروع قرارداد جدید باید بعد از شروع قرارداد فعال فعلی باشد
            using (var cmdCheck = new SqlCommand(
                "SELECT MAX(StartDate) FROM EmployeeContracts WHERE EmployeeId = @EmpId AND Status = N'فعال'", conn, transaction))
            {
                cmdCheck.Parameters.AddWithValue("@EmpId", c.EmployeeId);
                var maxStart = await cmdCheck.ExecuteScalarAsync();
                if (maxStart is not null && maxStart != DBNull.Value && c.StartDate.Date <= Convert.ToDateTime(maxStart).Date)
                    throw new InvalidOperationException("تاریخ شروع قرارداد جدید باید بعد از تاریخ شروع قرارداد فعال فعلی باشد.");
            }

            // ۱. بایگانی قرارداد فعال قبلی (اگر وجود داشته باشد)
            string archiveSql = @"
                    INSERT INTO ContractArchives (
                        EmployeeId, ArchiveDate, ContractNumber, ContractStartDate, ContractEndDate, 
                        BaseSalary, YearsBonus, AttractionAllowance, ResponsibilityAllowance, GroceryAllowance, 
                        HousingAllowance, ChildAllowance, MarriageAllowance, ManagementAllowance, TechnicalAllowance, 
                        OtherAllowances, Position, DepartmentId, DepartmentName, CompanyId, CompanyTitle
                    )
                    SELECT 
                        c.EmployeeId, GETDATE(), c.ContractNumber, c.StartDate, c.EndDate, 
                        c.BaseSalary, c.YearsBonus, c.AttractionAllowance, c.ResponsibilityAllowance, c.GroceryAllowance, 
                        c.HousingAllowance, c.ChildAllowance, c.MarriageAllowance, c.ManagementAllowance, c.TechnicalAllowance, 
                        c.OtherAllowances, c.Position, e.DepartmentId, d.Name, e.CompanyId, comp.Name
                    FROM EmployeeContracts c
                    LEFT JOIN Employees e ON c.EmployeeId = e.Id
                    LEFT JOIN Departments d ON e.DepartmentId = d.Id
                    LEFT JOIN Companies comp ON e.CompanyId = comp.Id
                    WHERE c.EmployeeId = @EmpId AND c.Status = N'فعال'";

            using (var cmdArchive = new SqlCommand(archiveSql, conn, transaction))
            {
                cmdArchive.Parameters.AddWithValue("@EmpId", c.EmployeeId);
                await cmdArchive.ExecuteNonQueryAsync();
            }

            // ۲. خاتمه دادن به قرارداد فعال قبلی
            // 🔴 قرارداد قبلی دقیقاً یک روز قبل از شروع قرارداد جدید بسته می‌شود
            string terminateSql = @"
                    UPDATE EmployeeContracts 
                    SET Status = N'خاتمه یافته',
                        EndDate = CASE
                                    WHEN EndDate IS NOT NULL AND EndDate < @NewStartDateMinusOne THEN EndDate  -- قبلاً زودتر تمام شده
                                    WHEN @NewStartDateMinusOne < StartDate THEN StartDate                      -- هرگز پایان قبل از شروع نشود
                                    ELSE @NewStartDateMinusOne
                                  END
                    WHERE EmployeeId = @EmpId AND Status = N'فعال'";
            using (var cmdTerminate = new SqlCommand(terminateSql, conn, transaction))
            {
                cmdTerminate.Parameters.AddWithValue("@EmpId", c.EmployeeId);
                cmdTerminate.Parameters.AddWithValue("@NewStartDateMinusOne", c.StartDate.Date.AddDays(-1));
                await cmdTerminate.ExecuteNonQueryAsync();
            }

            // ۳. ثبت قرارداد جدید
            string insertSql = @"
                    INSERT INTO EmployeeContracts 
                    (EmployeeId, ContractNumber, StartDate, EndDate, Position, PersonnelType, DailyWageCategory, 
                     BaseSalary, YearsBonus, AttractionAllowance, ResponsibilityAllowance, 
                     ManagementAllowance, TechnicalAllowance, HousingAllowance, GroceryAllowance, 
                     ChildAllowance, MarriageAllowance, OtherAllowances, TransportationAllowance, DailyWage, HourlyWage, 
                     HasInsurance, Status)
                    VALUES 
                    (@EmpId, @Num, @Start, @End, @Pos, @Type, @DailyCat, 
                     @Base, @Years, @Attr, @Resp, 
                     @Mgmt, @Tech, @House, @Groc, 
                     @Child, @Marr, @Other, @Transport, @Daily, @Hourly, 
                     @Ins, N'فعال');
                    SELECT SCOPE_IDENTITY();";

            int newId;
            using (var cmdInsert = new SqlCommand(insertSql, conn, transaction))
            {
                cmdInsert.Parameters.AddWithValue("@EmpId", c.EmployeeId);
                cmdInsert.Parameters.AddWithValue("@Num", c.ContractNumber);
                cmdInsert.Parameters.AddWithValue("@Start", c.StartDate.Date);
                cmdInsert.Parameters.AddWithValue("@End", (object?)c.EndDate?.Date ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("@Pos", (object?)c.Position ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("@Type", (object?)c.PersonnelType ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("@DailyCat", (object?)c.DailyWageCategory ?? DBNull.Value);
                cmdInsert.Parameters.AddWithValue("@Base", c.BaseSalary);
                cmdInsert.Parameters.AddWithValue("@Years", c.YearsBonus);
                cmdInsert.Parameters.AddWithValue("@Attr", c.AttractionAllowance);
                cmdInsert.Parameters.AddWithValue("@Resp", c.ResponsibilityAllowance);
                cmdInsert.Parameters.AddWithValue("@Mgmt", c.ManagementAllowance);
                cmdInsert.Parameters.AddWithValue("@Tech", c.TechnicalAllowance);
                cmdInsert.Parameters.AddWithValue("@House", c.HousingAllowance);
                cmdInsert.Parameters.AddWithValue("@Groc", c.GroceryAllowance);
                cmdInsert.Parameters.AddWithValue("@Child", c.ChildAllowance);
                cmdInsert.Parameters.AddWithValue("@Marr", c.MarriageAllowance);
                cmdInsert.Parameters.AddWithValue("@Other", c.OtherAllowances);
                cmdInsert.Parameters.AddWithValue("@Transport", c.TransportationAllowance);
                cmdInsert.Parameters.AddWithValue("@Daily", c.DailyWage);
                cmdInsert.Parameters.AddWithValue("@Hourly", c.HourlyWage);
                cmdInsert.Parameters.AddWithValue("@Ins", c.HasInsurance ? 1 : 0);
                newId = Convert.ToInt32(await cmdInsert.ExecuteScalarAsync());
            }

            // ۴. بروزرسانی اطلاعات کارمند در جدول اصلی
            string updateEmpSql = @"
                    UPDATE Employees SET 
                        ContractNumber = @Num, ContractStartDate = @Start, ContractEndDate = @End,
                        PersonnelType = @Type, 
                        BaseSalary = @Base, YearsBonus = @Years, AttractionAllowance = @Attr,
                        ResponsibilityAllowance = @Resp, ManagementAllowance = @Mgmt, TechnicalAllowance = @Tech,
                        OtherAllowances = @Other, TransportationAllowance = @Transport,
                        DailyWage = @Daily, HourlyWage = @Hourly, HasInsurance = @Ins
                    WHERE Id = @EmpId";
            using (var cmdUpdate = new SqlCommand(updateEmpSql, conn, transaction))
            {
                cmdUpdate.Parameters.AddWithValue("@EmpId", c.EmployeeId);
                cmdUpdate.Parameters.AddWithValue("@Num", c.ContractNumber);
                cmdUpdate.Parameters.AddWithValue("@Start", c.StartDate.Date);
                cmdUpdate.Parameters.AddWithValue("@End", (object?)c.EndDate?.Date ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("@Type", (object?)c.PersonnelType ?? DBNull.Value);
                cmdUpdate.Parameters.AddWithValue("@Base", c.BaseSalary);
                cmdUpdate.Parameters.AddWithValue("@Years", c.YearsBonus);
                cmdUpdate.Parameters.AddWithValue("@Attr", c.AttractionAllowance);
                cmdUpdate.Parameters.AddWithValue("@Resp", c.ResponsibilityAllowance);
                cmdUpdate.Parameters.AddWithValue("@Mgmt", c.ManagementAllowance);
                cmdUpdate.Parameters.AddWithValue("@Tech", c.TechnicalAllowance);
                cmdUpdate.Parameters.AddWithValue("@Other", c.OtherAllowances);
                cmdUpdate.Parameters.AddWithValue("@Transport", c.TransportationAllowance);
                cmdUpdate.Parameters.AddWithValue("@Daily", c.DailyWage);
                cmdUpdate.Parameters.AddWithValue("@Hourly", c.HourlyWage);
                cmdUpdate.Parameters.AddWithValue("@Ins", c.HasInsurance ? 1 : 0);
                await cmdUpdate.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            c.Id = newId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        Smp.Helpers.AuditHelper.Log("ثبت", "قرارداد پرسنل", $"شماره {c.ContractNumber}، پرسنل {c.EmployeeId}");
    }

    /// <summary>
    /// اصلاح مبالغ قرارداد فعالی که تاریخ شروعش دقیقاً برابر c.StartDate است (بدون ساخت قرارداد جدید).
    /// قبل از تغییر، یک نسخه از مقادیر قبلی در ContractArchives بایگانی می‌شود.
    /// </summary>
    public async Task ReviseActiveContractAsync(EmployeeContract c)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        try
        {
            int activeId;
            using (var cmdGet = new SqlCommand(
                "SELECT TOP 1 Id FROM EmployeeContracts WHERE EmployeeId = @EmpId AND Status = N'فعال' AND StartDate = @Start ORDER BY Id DESC", conn, transaction))
            {
                cmdGet.Parameters.AddWithValue("@EmpId", c.EmployeeId);
                cmdGet.Parameters.AddWithValue("@Start", c.StartDate.Date);
                var r = await cmdGet.ExecuteScalarAsync();
                if (r is null || r == DBNull.Value)
                    throw new InvalidOperationException("قرارداد فعال با این تاریخ شروع یافت نشد.");
                activeId = Convert.ToInt32(r);
            }

            // ۱. بایگانی مقادیر قبلی
            const string archiveSql = @"
                    INSERT INTO ContractArchives (
                        EmployeeId, ArchiveDate, ContractNumber, ContractStartDate, ContractEndDate,
                        BaseSalary, YearsBonus, AttractionAllowance, ResponsibilityAllowance, GroceryAllowance,
                        HousingAllowance, ChildAllowance, MarriageAllowance, ManagementAllowance, TechnicalAllowance,
                        OtherAllowances, Position, DepartmentId, DepartmentName, CompanyId, CompanyTitle
                    )
                    SELECT
                        c.EmployeeId, GETDATE(), c.ContractNumber, c.StartDate, c.EndDate,
                        c.BaseSalary, c.YearsBonus, c.AttractionAllowance, c.ResponsibilityAllowance, c.GroceryAllowance,
                        c.HousingAllowance, c.ChildAllowance, c.MarriageAllowance, c.ManagementAllowance, c.TechnicalAllowance,
                        c.OtherAllowances, c.Position, e.DepartmentId, d.Name, e.CompanyId, comp.Name
                    FROM EmployeeContracts c
                    LEFT JOIN Employees e ON c.EmployeeId = e.Id
                    LEFT JOIN Departments d ON e.DepartmentId = d.Id
                    LEFT JOIN Companies comp ON e.CompanyId = comp.Id
                    WHERE c.Id = @Id";
            using (var cmdArchive = new SqlCommand(archiveSql, conn, transaction))
            {
                cmdArchive.Parameters.AddWithValue("@Id", activeId);
                await cmdArchive.ExecuteNonQueryAsync();
            }

            // ۲. اعمال مقادیر جدید روی همان قرارداد فعال
            const string updContract = @"
                    UPDATE EmployeeContracts SET
                        EndDate = @End, PersonnelType = @Type, DailyWageCategory = @DailyCat,
                        BaseSalary = @Base, YearsBonus = @Years, AttractionAllowance = @Attr,
                        ResponsibilityAllowance = @Resp, ManagementAllowance = @Mgmt, TechnicalAllowance = @Tech,
                        OtherAllowances = @Other, TransportationAllowance = @Transport,
                        DailyWage = @Daily, HourlyWage = @Hourly, HasInsurance = @Ins
                    WHERE Id = @Id";
            using (var cmd = new SqlCommand(updContract, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", activeId);
                cmd.Parameters.AddWithValue("@End", (object?)c.EndDate?.Date ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Type", (object?)c.PersonnelType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DailyCat", (object?)c.DailyWageCategory ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Base", c.BaseSalary);
                cmd.Parameters.AddWithValue("@Years", c.YearsBonus);
                cmd.Parameters.AddWithValue("@Attr", c.AttractionAllowance);
                cmd.Parameters.AddWithValue("@Resp", c.ResponsibilityAllowance);
                cmd.Parameters.AddWithValue("@Mgmt", c.ManagementAllowance);
                cmd.Parameters.AddWithValue("@Tech", c.TechnicalAllowance);
                cmd.Parameters.AddWithValue("@Other", c.OtherAllowances);
                cmd.Parameters.AddWithValue("@Transport", c.TransportationAllowance);
                cmd.Parameters.AddWithValue("@Daily", c.DailyWage);
                cmd.Parameters.AddWithValue("@Hourly", c.HourlyWage);
                cmd.Parameters.AddWithValue("@Ins", c.HasInsurance ? 1 : 0);
                await cmd.ExecuteNonQueryAsync();
            }

            // ۳. همگام‌سازی جدول اصلی پرسنل
            const string updEmp = @"
                    UPDATE Employees SET
                        ContractEndDate = @End, PersonnelType = @Type,
                        BaseSalary = @Base, YearsBonus = @Years, AttractionAllowance = @Attr,
                        ResponsibilityAllowance = @Resp, ManagementAllowance = @Mgmt, TechnicalAllowance = @Tech,
                        OtherAllowances = @Other, TransportationAllowance = @Transport,
                        DailyWage = @Daily, HourlyWage = @Hourly, HasInsurance = @Ins
                    WHERE Id = @EmpId";
            using (var cmd = new SqlCommand(updEmp, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@EmpId", c.EmployeeId);
                cmd.Parameters.AddWithValue("@End", (object?)c.EndDate?.Date ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Type", (object?)c.PersonnelType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Base", c.BaseSalary);
                cmd.Parameters.AddWithValue("@Years", c.YearsBonus);
                cmd.Parameters.AddWithValue("@Attr", c.AttractionAllowance);
                cmd.Parameters.AddWithValue("@Resp", c.ResponsibilityAllowance);
                cmd.Parameters.AddWithValue("@Mgmt", c.ManagementAllowance);
                cmd.Parameters.AddWithValue("@Tech", c.TechnicalAllowance);
                cmd.Parameters.AddWithValue("@Other", c.OtherAllowances);
                cmd.Parameters.AddWithValue("@Transport", c.TransportationAllowance);
                cmd.Parameters.AddWithValue("@Daily", c.DailyWage);
                cmd.Parameters.AddWithValue("@Hourly", c.HourlyWage);
                cmd.Parameters.AddWithValue("@Ins", c.HasInsurance ? 1 : 0);
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            c.Id = activeId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        Smp.Helpers.AuditHelper.Log("ویرایش", "قرارداد پرسنل", $"اصلاح مبالغ قرارداد فعال، پرسنل {c.EmployeeId}، شروع {c.StartDate:yyyy-MM-dd}");
    }

    public async Task UpdateContractAsync(EmployeeContract c)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        try
        {
            string currentStatus = "";
            DateTime currentStart = DateTime.MinValue;
            DateTime? currentEnd = null;
            decimal currentBase = 0, currentYears = 0, currentAttr = 0, currentResp = 0;
            decimal currentMgmt = 0, currentTech = 0, currentHouse = 0, currentGroc = 0;
            decimal currentChild = 0, currentMarr = 0, currentOther = 0, currentTransport = 0;
            decimal currentDaily = 0, currentHourly = 0;

            using (var cmdRead = new SqlCommand(@"
                    SELECT Status, StartDate, EndDate, BaseSalary, YearsBonus, AttractionAllowance, ResponsibilityAllowance,
                           ManagementAllowance, TechnicalAllowance, HousingAllowance, GroceryAllowance,
                           ChildAllowance, MarriageAllowance, OtherAllowances, TransportationAllowance,
                           DailyWage, HourlyWage
                    FROM EmployeeContracts WHERE Id = @Id", conn, transaction))
            {
                cmdRead.Parameters.AddWithValue("@Id", c.Id);
                using var reader = await cmdRead.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new InvalidOperationException("قرارداد مورد نظر یافت نشد.");

                currentStatus = reader.IsDBNull(0) ? "" : reader.GetString(0);
                currentStart = reader.IsDBNull(1) ? DateTime.MinValue : reader.GetDateTime(1);
                currentEnd = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
                currentBase = reader.GetDecimal(3);
                currentYears = reader.GetDecimal(4);
                currentAttr = reader.GetDecimal(5);
                currentResp = reader.GetDecimal(6);
                currentMgmt = reader.GetDecimal(7);
                currentTech = reader.GetDecimal(8);
                currentHouse = reader.GetDecimal(9);
                currentGroc = reader.GetDecimal(10);
                currentChild = reader.GetDecimal(11);
                currentMarr = reader.GetDecimal(12);
                currentOther = reader.GetDecimal(13);
                currentTransport = reader.GetDecimal(14);
                currentDaily = reader.GetDecimal(15);
                currentHourly = reader.GetDecimal(16);
            }

            var financialChanges = new List<string>();
            if (c.BaseSalary != currentBase) financialChanges.Add("حقوق پایه");
            if (c.YearsBonus != currentYears) financialChanges.Add("پایه سنوات");
            if (c.AttractionAllowance != currentAttr) financialChanges.Add("حق جذب");
            if (c.ResponsibilityAllowance != currentResp) financialChanges.Add("حق مسئولیت");
            if (c.ManagementAllowance != currentMgmt) financialChanges.Add("حق مدیریت");
            if (c.TechnicalAllowance != currentTech) financialChanges.Add("حق فنی");
            if (c.HousingAllowance != currentHouse) financialChanges.Add("حق مسکن");
            if (c.GroceryAllowance != currentGroc) financialChanges.Add("حق خواربار");
            if (c.ChildAllowance != currentChild) financialChanges.Add("حق اولاد");
            if (c.MarriageAllowance != currentMarr) financialChanges.Add("حق تاهل");
            if (c.OtherAllowances != currentOther) financialChanges.Add("سایر مزایا");
            if (c.TransportationAllowance != currentTransport) financialChanges.Add("حق ایاب و ذهاب");
            if (c.DailyWage != currentDaily) financialChanges.Add("دستمزد روزانه");
            if (c.HourlyWage != currentHourly) financialChanges.Add("دستمزد ساعتی");
            if (c.StartDate.Date != currentStart.Date) financialChanges.Add("تاریخ شروع");
            if ((c.EndDate?.Date) != (currentEnd?.Date)) financialChanges.Add("تاریخ پایان");

            // 🔴 بررسی وجود فیش حقوقی
            bool hasPayrolls = HasPayrollsForContract(c.EmployeeId, currentStart, currentEnd, conn, transaction);

            if (hasPayrolls && financialChanges.Count > 0)
            {
                throw new InvalidOperationException(
                    "⛔ امکان ویرایش قرارداد وجود ندارد!\n\n" +
                    $"شما در حال تغییر این موارد هستید: {string.Join("، ", financialChanges)}\n\n" +
                    "🔒 چون برای این قرارداد در سیستم حقوق و دستمزد ثبت شده است، تغییر تاریخ‌ها یا مبالغ مالی آن باعث به‌هم‌ریختن سوابق فیش‌های حقوقی می‌شود.\n\n" +
                    "✅ فقط اطلاعات غیرمالی (مثل سمت، نوع پرسنلی) قابل ویرایش است.");
            }
            else if (currentStatus != "فعال" && financialChanges.Count > 0)
            {
                throw new InvalidOperationException(
                    "⛔ ویرایش قرارداد خاتمه‌یافته مجاز نیست!\n\n" +
                    $"شما در حال تغییر این موارد هستید: {string.Join("، ", financialChanges)}\n\n" +
                    "🔒 چرا محدودیت وجود دارد؟\n" +
                    "این قرارداد بسته شده و تغییر آن، سوابق سیستم را خراب می‌کند.\n\n" +
                    "✅ برای اصلاح تاریخ پایان چه کنیم؟\n" +
                    "۱. قرارداد فعال (جدید) را حذف کنید.\n" +
                    "۲. تاریخ پایان این قرارداد را به درستی اصلاح کنید.\n" +
                    "۳. دوباره قرارداد جدید را ثبت کنید.");
            }

            string updateSql = @"
                    UPDATE EmployeeContracts SET 
                        ContractNumber = @Num, StartDate = @Start, EndDate = @End,
                        Position = @Pos, PersonnelType = @Type, DailyWageCategory = @DailyCat,
                        BaseSalary = @Base, YearsBonus = @Years, AttractionAllowance = @Attr,
                        ResponsibilityAllowance = @Resp, ManagementAllowance = @Mgmt, TechnicalAllowance = @Tech,
                        HousingAllowance = @House, GroceryAllowance = @Groc, ChildAllowance = @Child,
                        MarriageAllowance = @Marr, OtherAllowances = @Other, TransportationAllowance = @Transport,
                        DailyWage = @Daily, HourlyWage = @Hourly, HasInsurance = @Ins
                    WHERE Id = @Id";

            using (var cmd = new SqlCommand(updateSql, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", c.Id);
                cmd.Parameters.AddWithValue("@Num", c.ContractNumber);
                cmd.Parameters.AddWithValue("@Start", c.StartDate.Date);
                cmd.Parameters.AddWithValue("@End", (object?)c.EndDate?.Date ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Pos", (object?)c.Position ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Type", (object?)c.PersonnelType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DailyCat", (object?)c.DailyWageCategory ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Base", c.BaseSalary);
                cmd.Parameters.AddWithValue("@Years", c.YearsBonus);
                cmd.Parameters.AddWithValue("@Attr", c.AttractionAllowance);
                cmd.Parameters.AddWithValue("@Resp", c.ResponsibilityAllowance);
                cmd.Parameters.AddWithValue("@Mgmt", c.ManagementAllowance);
                cmd.Parameters.AddWithValue("@Tech", c.TechnicalAllowance);
                cmd.Parameters.AddWithValue("@House", c.HousingAllowance);
                cmd.Parameters.AddWithValue("@Groc", c.GroceryAllowance);
                cmd.Parameters.AddWithValue("@Child", c.ChildAllowance);
                cmd.Parameters.AddWithValue("@Marr", c.MarriageAllowance);
                cmd.Parameters.AddWithValue("@Other", c.OtherAllowances);
                cmd.Parameters.AddWithValue("@Transport", c.TransportationAllowance);
                cmd.Parameters.AddWithValue("@Daily", c.DailyWage);
                cmd.Parameters.AddWithValue("@Hourly", c.HourlyWage);
                cmd.Parameters.AddWithValue("@Ins", c.HasInsurance ? 1 : 0);
                await cmd.ExecuteNonQueryAsync();
            }

            if (currentStatus == "فعال")
            {
                string updateEmpSql = @"
                        UPDATE Employees SET 
                            ContractNumber = @Num, ContractStartDate = @Start, ContractEndDate = @End,
                            PersonnelType = @Type, 
                            BaseSalary = @Base, YearsBonus = @Years, AttractionAllowance = @Attr,
                            ResponsibilityAllowance = @Resp, ManagementAllowance = @Mgmt, TechnicalAllowance = @Tech,
                            OtherAllowances = @Other, TransportationAllowance = @Transport,
                            DailyWage = @Daily, HourlyWage = @Hourly, HasInsurance = @Ins
                        WHERE Id = @EmpId";
                using (var cmdUpdateEmp = new SqlCommand(updateEmpSql, conn, transaction))
                {
                    cmdUpdateEmp.Parameters.AddWithValue("@EmpId", c.EmployeeId);
                    cmdUpdateEmp.Parameters.AddWithValue("@Num", c.ContractNumber);
                    cmdUpdateEmp.Parameters.AddWithValue("@Start", c.StartDate.Date);
                    cmdUpdateEmp.Parameters.AddWithValue("@End", (object?)c.EndDate?.Date ?? DBNull.Value);
                    cmdUpdateEmp.Parameters.AddWithValue("@Type", (object?)c.PersonnelType ?? DBNull.Value);
                    cmdUpdateEmp.Parameters.AddWithValue("@Base", c.BaseSalary);
                    cmdUpdateEmp.Parameters.AddWithValue("@Years", c.YearsBonus);
                    cmdUpdateEmp.Parameters.AddWithValue("@Attr", c.AttractionAllowance);
                    cmdUpdateEmp.Parameters.AddWithValue("@Resp", c.ResponsibilityAllowance);
                    cmdUpdateEmp.Parameters.AddWithValue("@Mgmt", c.ManagementAllowance);
                    cmdUpdateEmp.Parameters.AddWithValue("@Tech", c.TechnicalAllowance);
                    cmdUpdateEmp.Parameters.AddWithValue("@Other", c.OtherAllowances);
                    cmdUpdateEmp.Parameters.AddWithValue("@Transport", c.TransportationAllowance);
                    cmdUpdateEmp.Parameters.AddWithValue("@Daily", c.DailyWage);
                    cmdUpdateEmp.Parameters.AddWithValue("@Hourly", c.HourlyWage);
                    cmdUpdateEmp.Parameters.AddWithValue("@Ins", c.HasInsurance ? 1 : 0);
                    await cmdUpdateEmp.ExecuteNonQueryAsync();
                }
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        Smp.Helpers.AuditHelper.Log("ویرایش", "قرارداد پرسنل", $"شماره {c.ContractNumber}");
    }

    // ✅ متد جدید عمومی برای بررسی وجود فیش حقوقی (برای استفاده در UI)
    public bool HasPayrolls(int contractId)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        int employeeId = 0;
        DateTime startDate = DateTime.MinValue;
        DateTime? endDate = null;

        using (var cmd = new SqlCommand("SELECT EmployeeId, StartDate, EndDate FROM EmployeeContracts WHERE Id = @Id", conn))
        {
            cmd.Parameters.AddWithValue("@Id", contractId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                employeeId = reader.GetInt32(0);
                startDate = reader.IsDBNull(1) ? DateTime.MinValue : reader.GetDateTime(1);
                endDate = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
            }
        }

        if (employeeId == 0 || startDate <= new DateTime(1900, 1, 1)) return false;

        using var transaction = conn.BeginTransaction();
        return HasPayrollsForContract(employeeId, startDate, endDate, conn, transaction);
    }

    public void Delete(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var transaction = conn.BeginTransaction();

        try
        {
            int employeeId = 0;
            string status = "";
            DateTime startDate = DateTime.MinValue;
            DateTime? endDate = null;

            using (var cmd = new SqlCommand("SELECT EmployeeId, Status, StartDate, EndDate FROM EmployeeContracts WHERE Id = @Id", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    employeeId = reader.GetInt32(0);
                    status = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    startDate = reader.IsDBNull(2) ? DateTime.MinValue : reader.GetDateTime(2);
                    endDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3);
                }
            }

            if (employeeId == 0) return; // قرارداد یافت نشد

            // 🔴 لایه امنیتی دوم: چک کردن وجود حقوق برای جلوگیری از حذف
            if (HasPayrollsForContract(employeeId, startDate, endDate, conn, transaction))
            {
                throw new InvalidOperationException(
                    "⛔ امکان حذف قرارداد وجود ندارد!\n\n" +
                    "برای این قرارداد در سیستم حقوق و دستمزد ثبت شده است. برای حفظ سوابق مالی، حذف قراردادهای دارای فیش حقوقی مجاز نیست.");
            }

            // حذف قرارداد
            using (var cmdDel = new SqlCommand("DELETE FROM EmployeeContracts WHERE Id = @Id", conn, transaction))
            {
                cmdDel.Parameters.AddWithValue("@Id", id);
                cmdDel.ExecuteNonQuery();
            }

            // ✅ اگر قرارداد حذف شده «فعال» بود، به صورت خودکار قرارداد قبلی را فعال می‌کنیم
            if (status == "فعال")
            {
                int? newActiveId = null;
                string newContractNumber = null;
                DateTime? newStart = null, newEnd = null;
                decimal baseSal = 0, years = 0, attr = 0, resp = 0, mgmt = 0, tech = 0, other = 0, trans = 0, daily = 0, hourly = 0;
                string pType = null;

                using (var cmdNext = new SqlCommand(@"
                        SELECT TOP 1 Id, ContractNumber, StartDate, EndDate, BaseSalary, YearsBonus, AttractionAllowance, ResponsibilityAllowance, ManagementAllowance, TechnicalAllowance, OtherAllowances, TransportationAllowance, DailyWage, HourlyWage, PersonnelType
                        FROM EmployeeContracts 
                        WHERE EmployeeId = @EmpId 
                        ORDER BY StartDate DESC", conn, transaction))
                {
                    cmdNext.Parameters.AddWithValue("@EmpId", employeeId);
                    using var reader = cmdNext.ExecuteReader();
                    if (reader.Read())
                    {
                        newActiveId = reader.GetInt32(0);
                        newContractNumber = reader.IsDBNull(1) ? null : reader.GetString(1);
                        newStart = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
                        newEnd = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3);
                        baseSal = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4);
                        years = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5);
                        attr = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6);
                        resp = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7);
                        mgmt = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8);
                        tech = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9);
                        other = reader.IsDBNull(10) ? 0 : reader.GetDecimal(10);
                        trans = reader.IsDBNull(11) ? 0 : reader.GetDecimal(11);
                        daily = reader.IsDBNull(12) ? 0 : reader.GetDecimal(12);
                        hourly = reader.IsDBNull(13) ? 0 : reader.GetDecimal(13);
                        pType = reader.IsDBNull(14) ? null : reader.GetString(14);
                    }
                }

                if (newActiveId.HasValue)
                {
                    using (var cmdReactivate = new SqlCommand("UPDATE EmployeeContracts SET Status = N'فعال' WHERE Id = @Id", conn, transaction))
                    {
                        cmdReactivate.Parameters.AddWithValue("@Id", newActiveId.Value);
                        cmdReactivate.ExecuteNonQuery();
                    }

                    using (var cmdUpdEmp = new SqlCommand(@"
                            UPDATE Employees SET 
                                ContractNumber = @Num, ContractStartDate = @Start, ContractEndDate = @End,
                                PersonnelType = @Type, 
                                BaseSalary = @Base, YearsBonus = @Years, AttractionAllowance = @Attr,
                                ResponsibilityAllowance = @Resp, ManagementAllowance = @Mgmt, TechnicalAllowance = @Tech,
                                OtherAllowances = @Other, TransportationAllowance = @Transport,
                                DailyWage = @Daily, HourlyWage = @Hourly
                            WHERE Id = @EmpId", conn, transaction))
                    {
                        cmdUpdEmp.Parameters.AddWithValue("@EmpId", employeeId);
                        cmdUpdEmp.Parameters.AddWithValue("@Num", (object?)newContractNumber ?? DBNull.Value);
                        cmdUpdEmp.Parameters.AddWithValue("@Start", (object?)newStart ?? DBNull.Value);
                        cmdUpdEmp.Parameters.AddWithValue("@End", (object?)newEnd ?? DBNull.Value);
                        cmdUpdEmp.Parameters.AddWithValue("@Type", (object?)pType ?? DBNull.Value);
                        cmdUpdEmp.Parameters.AddWithValue("@Base", baseSal);
                        cmdUpdEmp.Parameters.AddWithValue("@Years", years);
                        cmdUpdEmp.Parameters.AddWithValue("@Attr", attr);
                        cmdUpdEmp.Parameters.AddWithValue("@Resp", resp);
                        cmdUpdEmp.Parameters.AddWithValue("@Mgmt", mgmt);
                        cmdUpdEmp.Parameters.AddWithValue("@Tech", tech);
                        cmdUpdEmp.Parameters.AddWithValue("@Other", other);
                        cmdUpdEmp.Parameters.AddWithValue("@Transport", trans);
                        cmdUpdEmp.Parameters.AddWithValue("@Daily", daily);
                        cmdUpdEmp.Parameters.AddWithValue("@Hourly", hourly);
                        cmdUpdEmp.ExecuteNonQuery();
                    }
                }
                else
                {
                    using (var cmdClearEmp = new SqlCommand(@"
                            UPDATE Employees SET 
                                ContractNumber = NULL, ContractStartDate = NULL, ContractEndDate = NULL,
                                PersonnelType = NULL, 
                                BaseSalary = 0, YearsBonus = 0, AttractionAllowance = 0,
                                ResponsibilityAllowance = 0, ManagementAllowance = 0, TechnicalAllowance = 0,
                                OtherAllowances = 0, TransportationAllowance = 0,
                                DailyWage = 0, HourlyWage = 0
                            WHERE Id = @EmpId", conn, transaction))
                    {
                        cmdClearEmp.Parameters.AddWithValue("@EmpId", employeeId);
                        cmdClearEmp.ExecuteNonQuery();
                    }
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
        Smp.Helpers.AuditHelper.Log("حذف", "قرارداد پرسنل", $"شناسه {id}");
    }

    // ✅ متد کمکی ایمن برای بررسی اینکه آیا در بازه زمانی این قرارداد حقوقی صادر شده است یا خیر
    private bool HasPayrollsForContract(int employeeId, DateTime startDate, DateTime? endDate, SqlConnection conn, SqlTransaction transaction)
    {
        if (employeeId <= 0) return false;

        // جلوگیری از خطای تاریخ نامعتبر (مثل 0001/01/01)
        if (startDate <= new DateTime(1900, 1, 1)) return false;
        if (endDate.HasValue && endDate.Value <= new DateTime(1900, 1, 1)) endDate = null;

        var pc = new System.Globalization.PersianCalendar();
        int startYear = pc.GetYear(startDate);
        int startMonth = pc.GetMonth(startDate);

        int endYear = endDate.HasValue ? pc.GetYear(endDate.Value) : 9999;
        int endMonth = endDate.HasValue ? pc.GetMonth(endDate.Value) : 12;

        string sql = @"
                SELECT COUNT(1) FROM Payrolls 
                WHERE EmployeeId = @EmpId
                  AND (
                      (Year > @StartYear OR (Year = @StartYear AND Month >= @StartMonth))
                      AND
                      (Year < @EndYear OR (Year = @EndYear AND Month <= @EndMonth))
                  )";

        using var cmd = new SqlCommand(sql, conn, transaction);
        cmd.Parameters.AddWithValue("@EmpId", employeeId);
        cmd.Parameters.AddWithValue("@StartYear", startYear);
        cmd.Parameters.AddWithValue("@StartMonth", startMonth);
        cmd.Parameters.AddWithValue("@EndYear", endYear);
        cmd.Parameters.AddWithValue("@EndMonth", endMonth);

        var result = cmd.ExecuteScalar();
        return Convert.ToInt32(result) > 0;
    }

    public async Task<(int inserted, int updated, List<string> errors)> ImportFromExcelAsync(string filePath)
    {
        int inserted = 0;
        int updated = 0;
        var errors = new List<string>();

        using var workbook = new XLWorkbook(filePath);
        var ws = workbook.Worksheet(1);
        var rows = ws.RowsUsed().Skip(1);

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        try
        {
            int rowNum = 1;
            foreach (var row in rows)
            {
                rowNum++;
                string savePointName = $"Row_{rowNum}";
                transaction.Save(savePointName);

                try
                {
                    string personnelCode = row.Cell(1).GetString().Trim();
                    string contractNumber = row.Cell(2).GetString().Trim();
                    string startDateStr = row.Cell(3).GetString().Trim();
                    string endDateStr = row.Cell(4).GetString().Trim();

                    string baseSalaryStr = row.Cell(5).GetString().Trim();
                    string yearsBonusStr = row.Cell(6).GetString().Trim();
                    string attractionStr = row.Cell(7).GetString().Trim();
                    string responsibilityStr = row.Cell(8).GetString().Trim();
                    string managementStr = row.Cell(9).GetString().Trim();
                    string technicalStr = row.Cell(10).GetString().Trim();
                    string housingStr = row.Cell(11).GetString().Trim();
                    string groceryStr = row.Cell(12).GetString().Trim();
                    string childStr = row.Cell(13).GetString().Trim();
                    string marriageStr = row.Cell(14).GetString().Trim();
                    string otherStr = row.Cell(15).GetString().Trim();
                    string transportationStr = row.Cell(16).GetString().Trim();
                    string dailyWageStr = row.Cell(17).GetString().Trim();
                    string hourlyWageStr = row.Cell(18).GetString().Trim();
                    string insuranceStr = row.Cell(19).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(personnelCode) || string.IsNullOrWhiteSpace(contractNumber))
                    {
                        errors.Add($"ردیف {rowNum}: کد پرسنلی و شماره قرارداد الزامی است.");
                        continue;
                    }

                    int? employeeId = null;
                    string personnelType = "ماهانه";
                    string dailyWageCategory = "";
                    string position = "";

                    string findEmpSql = @"
                            SELECT Id, ISNULL(PersonnelType, N'ماهانه'), ISNULL(DailyWageCategory, ''), ISNULL(Position, '') 
                            FROM Employees WHERE PersonnelCode = @Code";

                    using (var findCmd = new SqlCommand(findEmpSql, conn, transaction))
                    {
                        findCmd.Parameters.AddWithValue("@Code", personnelCode);
                        using var reader = await findCmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            employeeId = reader.GetInt32(0);
                            personnelType = reader.GetString(1);
                            dailyWageCategory = reader.GetString(2);
                            position = reader.GetString(3);
                        }
                    }

                    if (!employeeId.HasValue)
                    {
                        errors.Add($"ردیف {rowNum}: کارمندی با کد «{personnelCode}» یافت نشد.");
                        continue;
                    }

                    DateTime startDate = ParseDate(startDateStr);
                    if (startDate == DateTime.MinValue)
                    {
                        errors.Add($"ردیف {rowNum}: تاریخ شروع نامعتبر است.");
                        continue;
                    }

                    DateTime? endDate = null;
                    if (!string.IsNullOrWhiteSpace(endDateStr))
                    {
                        DateTime tempEnd = ParseDate(endDateStr);
                        if (tempEnd != DateTime.MinValue) endDate = tempEnd;
                    }

                    decimal baseSalary = 0, yearsBonus = 0, attraction = 0, responsibility = 0;
                    decimal management = 0, technical = 0, housing = 0, grocery = 0;
                    decimal child = 0, marriage = 0, other = 0, transportation = 0;
                    decimal dailyWage = 0, hourlyWage = 0;

                    string typeNormalized = personnelType.Trim();

                    if (typeNormalized.Contains("روزمزد") || typeNormalized.Contains("روز مزد"))
                    {
                        int contractYear = startDate.Year;
                        string dailyRateQuery = "SELECT Amount FROM DailyWageRates WHERE Year = @Year AND Category = @Cat";
                        using var cmdRate = new SqlCommand(dailyRateQuery, conn, transaction);
                        cmdRate.Parameters.AddWithValue("@Year", contractYear);
                        cmdRate.Parameters.AddWithValue("@Cat", dailyWageCategory);

                        var rateResult = await cmdRate.ExecuteScalarAsync();
                        if (rateResult is not null && rateResult != DBNull.Value)
                        {
                            dailyWage = Convert.ToDecimal(rateResult);
                        }
                        else
                        {
                            decimal.TryParse(dailyWageStr.Replace(",", "").Trim(), out dailyWage);
                        }
                    }
                    else if (typeNormalized.Contains("ساعتی") || typeNormalized.Contains("ساعت"))
                    {
                        decimal.TryParse(hourlyWageStr.Replace(",", "").Trim(), out hourlyWage);
                    }
                    else
                    {
                        decimal.TryParse(baseSalaryStr.Replace(",", "").Trim(), out baseSalary);
                        decimal.TryParse(yearsBonusStr.Replace(",", "").Trim(), out yearsBonus);
                        decimal.TryParse(attractionStr.Replace(",", "").Trim(), out attraction);
                        decimal.TryParse(responsibilityStr.Replace(",", "").Trim(), out responsibility);
                        decimal.TryParse(managementStr.Replace(",", "").Trim(), out management);
                        decimal.TryParse(technicalStr.Replace(",", "").Trim(), out technical);
                        decimal.TryParse(housingStr.Replace(",", "").Trim(), out housing);
                        decimal.TryParse(groceryStr.Replace(",", "").Trim(), out grocery);
                        decimal.TryParse(childStr.Replace(",", "").Trim(), out child);
                        decimal.TryParse(marriageStr.Replace(",", "").Trim(), out marriage);
                        decimal.TryParse(otherStr.Replace(",", "").Trim(), out other);
                        decimal.TryParse(transportationStr.Replace(",", "").Trim(), out transportation);
                    }

                    bool hasInsurance = insuranceStr == "دارد" || insuranceStr == "بله" || insuranceStr == "1";

                    int? existingId = null;
                    string checkSql = "SELECT Id FROM EmployeeContracts WHERE EmployeeId = @EmpId AND ContractNumber = @Num";
                    using (var checkCmd = new SqlCommand(checkSql, conn, transaction))
                    {
                        checkCmd.Parameters.AddWithValue("@EmpId", employeeId.Value);
                        checkCmd.Parameters.AddWithValue("@Num", contractNumber);
                        var result = await checkCmd.ExecuteScalarAsync();
                        if (result is not null && result != DBNull.Value)
                            existingId = Convert.ToInt32(result);
                    }

                    if (existingId.HasValue)
                    {
                        string updateSql = @"
                                UPDATE EmployeeContracts SET 
                                    StartDate = @Start, EndDate = @End,
                                    BaseSalary = @Base, YearsBonus = @Years, AttractionAllowance = @Attr,
                                    ResponsibilityAllowance = @Resp, ManagementAllowance = @Mgmt, TechnicalAllowance = @Tech,
                                    HousingAllowance = @House, GroceryAllowance = @Groc, ChildAllowance = @Child,
                                    MarriageAllowance = @Marr, OtherAllowances = @Other, TransportationAllowance = @Transport,
                                    DailyWage = @Daily, HourlyWage = @Hourly, HasInsurance = @Ins,
                                    DailyWageCategory = @DailyCat
                                WHERE Id = @Id";
                        using var updateCmd = new SqlCommand(updateSql, conn, transaction);
                        updateCmd.Parameters.AddWithValue("@Start", startDate.Date);
                        updateCmd.Parameters.AddWithValue("@End", (object?)endDate?.Date ?? DBNull.Value);
                        updateCmd.Parameters.AddWithValue("@Base", baseSalary);
                        updateCmd.Parameters.AddWithValue("@Years", yearsBonus);
                        updateCmd.Parameters.AddWithValue("@Attr", attraction);
                        updateCmd.Parameters.AddWithValue("@Resp", responsibility);
                        updateCmd.Parameters.AddWithValue("@Mgmt", management);
                        updateCmd.Parameters.AddWithValue("@Tech", technical);
                        updateCmd.Parameters.AddWithValue("@House", housing);
                        updateCmd.Parameters.AddWithValue("@Groc", grocery);
                        updateCmd.Parameters.AddWithValue("@Child", child);
                        updateCmd.Parameters.AddWithValue("@Marr", marriage);
                        updateCmd.Parameters.AddWithValue("@Other", other);
                        updateCmd.Parameters.AddWithValue("@Transport", transportation);
                        updateCmd.Parameters.AddWithValue("@Daily", dailyWage);
                        updateCmd.Parameters.AddWithValue("@Hourly", hourlyWage);
                        updateCmd.Parameters.AddWithValue("@Ins", hasInsurance ? 1 : 0);
                        updateCmd.Parameters.AddWithValue("@DailyCat", (object?)dailyWageCategory ?? DBNull.Value);
                        updateCmd.Parameters.AddWithValue("@Id", existingId.Value);
                        await updateCmd.ExecuteNonQueryAsync();
                        updated++;
                    }
                    else
                    {
                        string insertSql = @"
                                INSERT INTO EmployeeContracts 
                                (EmployeeId, ContractNumber, StartDate, EndDate, Position, PersonnelType, DailyWageCategory, 
                                 BaseSalary, YearsBonus, AttractionAllowance, ResponsibilityAllowance, 
                                 ManagementAllowance, TechnicalAllowance, HousingAllowance, GroceryAllowance, 
                                 ChildAllowance, MarriageAllowance, OtherAllowances, TransportationAllowance, DailyWage, HourlyWage, 
                                 HasInsurance, Status)
                                VALUES 
                                (@EmpId, @Num, @Start, @End, @Pos, @Type, @DailyCat, 
                                 @Base, @Years, @Attr, @Resp, 
                                 @Mgmt, @Tech, @House, @Groc, 
                                 @Child, @Marr, @Other, @Transport, @Daily, @Hourly, 
                                 @Ins, N'فعال')";
                        using var insertCmd = new SqlCommand(insertSql, conn, transaction);
                        insertCmd.Parameters.AddWithValue("@EmpId", employeeId.Value);
                        insertCmd.Parameters.AddWithValue("@Num", contractNumber);
                        insertCmd.Parameters.AddWithValue("@Start", startDate.Date);
                        insertCmd.Parameters.AddWithValue("@End", (object?)endDate?.Date ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@Pos", (object?)position ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@Type", personnelType);
                        insertCmd.Parameters.AddWithValue("@DailyCat", (object?)dailyWageCategory ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@Base", baseSalary);
                        insertCmd.Parameters.AddWithValue("@Years", yearsBonus);
                        insertCmd.Parameters.AddWithValue("@Attr", attraction);
                        insertCmd.Parameters.AddWithValue("@Resp", responsibility);
                        insertCmd.Parameters.AddWithValue("@Mgmt", management);
                        insertCmd.Parameters.AddWithValue("@Tech", technical);
                        insertCmd.Parameters.AddWithValue("@House", housing);
                        insertCmd.Parameters.AddWithValue("@Groc", grocery);
                        insertCmd.Parameters.AddWithValue("@Child", child);
                        insertCmd.Parameters.AddWithValue("@Marr", marriage);
                        insertCmd.Parameters.AddWithValue("@Other", other);
                        insertCmd.Parameters.AddWithValue("@Transport", transportation);
                        insertCmd.Parameters.AddWithValue("@Daily", dailyWage);
                        insertCmd.Parameters.AddWithValue("@Hourly", hourlyWage);
                        insertCmd.Parameters.AddWithValue("@Ins", hasInsurance ? 1 : 0);
                        await insertCmd.ExecuteNonQueryAsync();
                        inserted++;
                    }

                    string updateEmpSql = @"
                            UPDATE Employees SET 
                                ContractNumber = @Num, ContractStartDate = @Start, ContractEndDate = @End,
                                BaseSalary = @Base, YearsBonus = @Years, AttractionAllowance = @Attr,
                                ResponsibilityAllowance = @Resp, ManagementAllowance = @Mgmt, TechnicalAllowance = @Tech,
                                OtherAllowances = @Other, TransportationAllowance = @Transport,
                                DailyWage = @Daily, HourlyWage = @Hourly, HasInsurance = @Ins
                            WHERE Id = @EmpId";
                    using var updateEmpCmd = new SqlCommand(updateEmpSql, conn, transaction);
                    updateEmpCmd.Parameters.AddWithValue("@EmpId", employeeId.Value);
                    updateEmpCmd.Parameters.AddWithValue("@Num", contractNumber);
                    updateEmpCmd.Parameters.AddWithValue("@Start", startDate.Date);
                    updateEmpCmd.Parameters.AddWithValue("@End", (object?)endDate?.Date ?? DBNull.Value);
                    updateEmpCmd.Parameters.AddWithValue("@Base", baseSalary);
                    updateEmpCmd.Parameters.AddWithValue("@Years", yearsBonus);
                    updateEmpCmd.Parameters.AddWithValue("@Attr", attraction);
                    updateEmpCmd.Parameters.AddWithValue("@Resp", responsibility);
                    updateEmpCmd.Parameters.AddWithValue("@Mgmt", management);
                    updateEmpCmd.Parameters.AddWithValue("@Tech", technical);
                    updateEmpCmd.Parameters.AddWithValue("@Other", other);
                    updateEmpCmd.Parameters.AddWithValue("@Transport", transportation);
                    updateEmpCmd.Parameters.AddWithValue("@Daily", dailyWage);
                    updateEmpCmd.Parameters.AddWithValue("@Hourly", hourlyWage);
                    updateEmpCmd.Parameters.AddWithValue("@Ins", hasInsurance ? 1 : 0);
                    await updateEmpCmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    try { transaction.Rollback(savePointName); } catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "EmployeeContractRepository"); }
                    errors.Add($"ردیف {rowNum}: {ex.Message}");
                }
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return (inserted, updated, errors);
    }

    private DateTime ParseDate(string dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return DateTime.MinValue;
        try
        {
            if (DateTime.TryParse(dateStr, out DateTime result)) return result;
        }
        catch (Exception __ex) { Smp.Helpers.LogHelper.LogException(__ex, "EmployeeContractRepository"); }

        try
        {
            var parts = dateStr.Split('/', '-');
            if (parts.Length != 3) return DateTime.MinValue;
            int year = Convert.ToInt32(parts[0]);
            int month = Convert.ToInt32(parts[1]);
            int day = Convert.ToInt32(parts[2]);
            var pc = new System.Globalization.PersianCalendar();
            return pc.ToDateTime(year, month, day, 0, 0, 0, 0);
        }
        catch { return DateTime.MinValue; }
    }

    private static EmployeeContract MapReader(SqlDataReader reader)
    {
        return new EmployeeContract
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            EmployeeId = reader.GetInt32(reader.GetOrdinal("EmployeeId")),
            ContractNumber = reader.IsDBNull(reader.GetOrdinal("ContractNumber")) ? "" : reader.GetString(reader.GetOrdinal("ContractNumber")),
            StartDate = DateHelper.ParseDbDate(reader["StartDate"]) ?? DateTime.Now,
            EndDate = DateHelper.ParseDbDate(reader["EndDate"]),
            Position = reader.IsDBNull(reader.GetOrdinal("Position")) ? null : reader.GetString(reader.GetOrdinal("Position")),
            PersonnelType = reader.IsDBNull(reader.GetOrdinal("PersonnelType")) ? null : reader.GetString(reader.GetOrdinal("PersonnelType")),
            DailyWageCategory = reader.IsDBNull(reader.GetOrdinal("DailyWageCategory")) ? null : reader.GetString(reader.GetOrdinal("DailyWageCategory")),
            BaseSalary = reader.IsDBNull(reader.GetOrdinal("BaseSalary")) ? 0 : Convert.ToDecimal(reader["BaseSalary"]),
            YearsBonus = reader.IsDBNull(reader.GetOrdinal("YearsBonus")) ? 0 : Convert.ToDecimal(reader["YearsBonus"]),
            AttractionAllowance = reader.IsDBNull(reader.GetOrdinal("AttractionAllowance")) ? 0 : Convert.ToDecimal(reader["AttractionAllowance"]),
            ResponsibilityAllowance = reader.IsDBNull(reader.GetOrdinal("ResponsibilityAllowance")) ? 0 : Convert.ToDecimal(reader["ResponsibilityAllowance"]),
            ManagementAllowance = reader.IsDBNull(reader.GetOrdinal("ManagementAllowance")) ? 0 : Convert.ToDecimal(reader["ManagementAllowance"]),
            TechnicalAllowance = reader.IsDBNull(reader.GetOrdinal("TechnicalAllowance")) ? 0 : Convert.ToDecimal(reader["TechnicalAllowance"]),
            HousingAllowance = reader.IsDBNull(reader.GetOrdinal("HousingAllowance")) ? 0 : Convert.ToDecimal(reader["HousingAllowance"]),
            GroceryAllowance = reader.IsDBNull(reader.GetOrdinal("GroceryAllowance")) ? 0 : Convert.ToDecimal(reader["GroceryAllowance"]),
            ChildAllowance = reader.IsDBNull(reader.GetOrdinal("ChildAllowance")) ? 0 : Convert.ToDecimal(reader["ChildAllowance"]),
            MarriageAllowance = reader.IsDBNull(reader.GetOrdinal("MarriageAllowance")) ? 0 : Convert.ToDecimal(reader["MarriageAllowance"]),
            OtherAllowances = reader.IsDBNull(reader.GetOrdinal("OtherAllowances")) ? 0 : Convert.ToDecimal(reader["OtherAllowances"]),
            TransportationAllowance = reader.IsDBNull(reader.GetOrdinal("TransportationAllowance")) ? 0 : Convert.ToDecimal(reader["TransportationAllowance"]),
            DailyWage = reader.IsDBNull(reader.GetOrdinal("DailyWage")) ? 0 : Convert.ToDecimal(reader["DailyWage"]),
            HourlyWage = reader.IsDBNull(reader.GetOrdinal("HourlyWage")) ? 0 : Convert.ToDecimal(reader["HourlyWage"]),
            HasInsurance = reader.IsDBNull(reader.GetOrdinal("HasInsurance")) || reader.GetBoolean(reader.GetOrdinal("HasInsurance")),
            Status = reader.IsDBNull(reader.GetOrdinal("Status")) ? "فعال" : reader.GetString(reader.GetOrdinal("Status")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            EmployeeName = reader.GetString(reader.GetOrdinal("EmployeeName"))
        };
    }
}
