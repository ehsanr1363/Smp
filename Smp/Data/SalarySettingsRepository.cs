using Microsoft.Data.SqlClient;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Smp.Data;

public class SalarySettingsRepository
{
    private readonly string _connectionString;

    public SalarySettingsRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private static bool _columnsEnsured;

    /// <summary>ستون‌های مشمولین عیدی/سنوات اگر در جدول نبود اضافه می‌شود (بدون نیاز به اسکریپت دستی).</summary>
    private static async Task EnsureColumnsAsync(SqlConnection conn)
    {
        if (_columnsEnsured) return;
        const string sql = @"
                IF COL_LENGTH(N'dbo.SalarySettings', N'EidiIncludeComponents') IS NULL ALTER TABLE dbo.SalarySettings ADD EidiIncludeComponents NVARCHAR(400) NULL;
                IF COL_LENGTH(N'dbo.SalarySettings', N'SanavatIncludeComponents') IS NULL ALTER TABLE dbo.SalarySettings ADD SanavatIncludeComponents NVARCHAR(400) NULL;
                IF COL_LENGTH(N'dbo.SalarySettings', N'MissionIncludeComponents') IS NULL ALTER TABLE dbo.SalarySettings ADD MissionIncludeComponents NVARCHAR(400) NULL;
                IF COL_LENGTH(N'dbo.SalarySettings', N'MissionRate') IS NULL ALTER TABLE dbo.SalarySettings ADD MissionRate DECIMAL(9,4) NULL;";
        using (var cmd = new SqlCommand(sql, conn))
            await cmd.ExecuteNonQueryAsync();
        _columnsEnsured = true;
    }

    public async Task<SalarySettings?> GetByYearAsync(int year)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await EnsureColumnsAsync(conn);

        var sql = "SELECT * FROM SalarySettings WHERE Year = @Year";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Year", year);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SalarySettings
            {
                Id = Convert.ToInt32(reader["Id"]),
                Year = Convert.ToInt32(reader["Year"]),
                MinimumWage = reader["MinimumWage"] != DBNull.Value ? Convert.ToDecimal(reader["MinimumWage"]) : 0,
                HousingAllowance = reader["HousingAllowance"] != DBNull.Value ? Convert.ToDecimal(reader["HousingAllowance"]) : 0,
                FoodAllowance = reader["FoodAllowance"] != DBNull.Value ? Convert.ToDecimal(reader["FoodAllowance"]) : 0,
                MarriageAllowance = reader["MarriageAllowance"] != DBNull.Value ? Convert.ToDecimal(reader["MarriageAllowance"]) : 0,
                ChildAllowanceRate = reader["ChildAllowanceRate"] != DBNull.Value ? Convert.ToDecimal(reader["ChildAllowanceRate"]) : 0,
                InsuranceEmployeeRate = reader["InsuranceEmployeeRate"] != DBNull.Value ? Convert.ToDecimal(reader["InsuranceEmployeeRate"]) : 0,
                InsuranceEmployerRate = reader["InsuranceEmployerRate"] != DBNull.Value ? Convert.ToDecimal(reader["InsuranceEmployerRate"]) : 0,

                // 🔴 TaxExemptionLimit حذف شد

                AbsenceMultiplier = reader["AbsenceMultiplier"] != DBNull.Value ? Convert.ToDecimal(reader["AbsenceMultiplier"]) : 1.0m,

                UpdatedAt = reader["UpdatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["UpdatedAt"]) : null,
                NormalDayHours = reader["NormalDayHours"] != DBNull.Value ? Convert.ToDecimal(reader["NormalDayHours"]) : 8.50m,
                HolidayDayHours = reader["HolidayDayHours"] != DBNull.Value ? Convert.ToDecimal(reader["HolidayDayHours"]) : 7.00m,
                MonthlyWorkingHours = reader["MonthlyWorkingHours"] != DBNull.Value ? Convert.ToDecimal(reader["MonthlyWorkingHours"]) : 220.00m,

                TaxIncludeBaseSalary = reader["TaxIncludeBaseSalary"] != DBNull.Value && Convert.ToBoolean(reader["TaxIncludeBaseSalary"]),
                TaxIncludeYearsBonus = reader["TaxIncludeYearsBonus"] != DBNull.Value && Convert.ToBoolean(reader["TaxIncludeYearsBonus"]),
                TaxIncludeAttraction = reader["TaxIncludeAttraction"] != DBNull.Value && Convert.ToBoolean(reader["TaxIncludeAttraction"]),
                TaxIncludeResponsibility = reader["TaxIncludeResponsibility"] != DBNull.Value && Convert.ToBoolean(reader["TaxIncludeResponsibility"]),
                TaxIncludeManagement = reader["TaxIncludeManagement"] != DBNull.Value && Convert.ToBoolean(reader["TaxIncludeManagement"]),
                TaxIncludeTechnical = reader["TaxIncludeTechnical"] != DBNull.Value && Convert.ToBoolean(reader["TaxIncludeTechnical"]),
                TaxIncludeHousing = reader["TaxIncludeHousing"] != DBNull.Value && Convert.ToBoolean(reader["TaxIncludeHousing"]),
                TaxIncludeGrocery = reader["TaxIncludeGrocery"] != DBNull.Value && Convert.ToBoolean(reader["TaxIncludeGrocery"]),
                TaxIncludeChild = reader["TaxIncludeChild"] != DBNull.Value && Convert.ToBoolean(reader["TaxIncludeChild"]),
                TaxIncludeMarriage = reader["TaxIncludeMarriage"] != DBNull.Value && Convert.ToBoolean(reader["TaxIncludeMarriage"]),
                TaxIncludeTransportation = reader["TaxIncludeTransportation"] != DBNull.Value && Convert.ToBoolean(reader["TaxIncludeTransportation"]),
                TaxIncludeOtherAllowances = reader["TaxIncludeOtherAllowances"] != DBNull.Value && Convert.ToBoolean(reader["TaxIncludeOtherAllowances"]),

                InsuranceIncludeBaseSalary = reader["InsuranceIncludeBaseSalary"] != DBNull.Value && Convert.ToBoolean(reader["InsuranceIncludeBaseSalary"]),
                InsuranceIncludeYearsBonus = reader["InsuranceIncludeYearsBonus"] != DBNull.Value && Convert.ToBoolean(reader["InsuranceIncludeYearsBonus"]),
                InsuranceIncludeAttraction = reader["InsuranceIncludeAttraction"] != DBNull.Value && Convert.ToBoolean(reader["InsuranceIncludeAttraction"]),
                InsuranceIncludeResponsibility = reader["InsuranceIncludeResponsibility"] != DBNull.Value && Convert.ToBoolean(reader["InsuranceIncludeResponsibility"]),
                InsuranceIncludeManagement = reader["InsuranceIncludeManagement"] != DBNull.Value && Convert.ToBoolean(reader["InsuranceIncludeManagement"]),
                InsuranceIncludeTechnical = reader["InsuranceIncludeTechnical"] != DBNull.Value && Convert.ToBoolean(reader["InsuranceIncludeTechnical"]),
                InsuranceIncludeHousing = reader["InsuranceIncludeHousing"] != DBNull.Value && Convert.ToBoolean(reader["InsuranceIncludeHousing"]),
                InsuranceIncludeGrocery = reader["InsuranceIncludeGrocery"] != DBNull.Value && Convert.ToBoolean(reader["InsuranceIncludeGrocery"]),
                InsuranceIncludeChild = reader["InsuranceIncludeChild"] != DBNull.Value && Convert.ToBoolean(reader["InsuranceIncludeChild"]),
                InsuranceIncludeMarriage = reader["InsuranceIncludeMarriage"] != DBNull.Value && Convert.ToBoolean(reader["InsuranceIncludeMarriage"]),
                InsuranceIncludeTransportation = reader["InsuranceIncludeTransportation"] != DBNull.Value && Convert.ToBoolean(reader["InsuranceIncludeTransportation"]),
                InsuranceIncludeOther = reader["InsuranceIncludeOther"] != DBNull.Value && Convert.ToBoolean(reader["InsuranceIncludeOther"]),

                OvertimeIncludeBaseSalary = reader["OvertimeIncludeBaseSalary"] != DBNull.Value && Convert.ToBoolean(reader["OvertimeIncludeBaseSalary"]),
                OvertimeIncludeYearsBonus = reader["OvertimeIncludeYearsBonus"] != DBNull.Value && Convert.ToBoolean(reader["OvertimeIncludeYearsBonus"]),
                OvertimeIncludeAttraction = reader["OvertimeIncludeAttraction"] != DBNull.Value && Convert.ToBoolean(reader["OvertimeIncludeAttraction"]),
                OvertimeIncludeResponsibility = reader["OvertimeIncludeResponsibility"] != DBNull.Value && Convert.ToBoolean(reader["OvertimeIncludeResponsibility"]),
                OvertimeIncludeManagement = reader["OvertimeIncludeManagement"] != DBNull.Value && Convert.ToBoolean(reader["OvertimeIncludeManagement"]),
                OvertimeIncludeTechnical = reader["OvertimeIncludeTechnical"] != DBNull.Value && Convert.ToBoolean(reader["OvertimeIncludeTechnical"]),
                OvertimeIncludeHousing = reader["OvertimeIncludeHousing"] != DBNull.Value && Convert.ToBoolean(reader["OvertimeIncludeHousing"]),
                OvertimeIncludeGrocery = reader["OvertimeIncludeGrocery"] != DBNull.Value && Convert.ToBoolean(reader["OvertimeIncludeGrocery"]),
                OvertimeIncludeChild = reader["OvertimeIncludeChild"] != DBNull.Value && Convert.ToBoolean(reader["OvertimeIncludeChild"]),
                OvertimeIncludeMarriage = reader["OvertimeIncludeMarriage"] != DBNull.Value && Convert.ToBoolean(reader["OvertimeIncludeMarriage"]),
                OvertimeIncludeTransportation = reader["OvertimeIncludeTransportation"] != DBNull.Value && Convert.ToBoolean(reader["OvertimeIncludeTransportation"]),
                OvertimeIncludeOther = reader["OvertimeIncludeOther"] != DBNull.Value && Convert.ToBoolean(reader["OvertimeIncludeOther"]),

                // NULL (تنظیم‌نشده) = رفتار قبلی: فقط حقوق پایه
                EidiIncludeComponents = reader["EidiIncludeComponents"] != DBNull.Value ? (reader["EidiIncludeComponents"].ToString() ?? "") : "Base",
                SanavatIncludeComponents = reader["SanavatIncludeComponents"] != DBNull.Value ? (reader["SanavatIncludeComponents"].ToString() ?? "") : "Base",

                // NULL (تنظیم‌نشده) = فقط حقوق پایه و نرخ پیش‌فرض ۳٪
                MissionIncludeComponents = reader["MissionIncludeComponents"] != DBNull.Value ? (reader["MissionIncludeComponents"].ToString() ?? "") : "Base",
                MissionRate = reader["MissionRate"] != DBNull.Value ? Convert.ToDecimal(reader["MissionRate"]) : 3m
            };
        }
        return null;
    }

    public async Task<List<TaxBracketDto>> GetBracketsAsync(int year)
    {
        var list = new List<TaxBracketDto>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = "SELECT FromAmount, ToAmount, TaxRate FROM TaxBrackets WHERE Year = @Year ORDER BY FromAmount ASC";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Year", year);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new TaxBracketDto
            {
                FromAmount = reader.GetDecimal(0),
                ToAmount = reader.IsDBNull(1) ? (decimal?)null : reader.GetDecimal(1),
                TaxRate = reader.GetDecimal(2)
            });
        }
        return list;
    }

    public async Task SaveWithBracketsAsync(SalarySettings settings, List<TaxBracketDto> brackets)
    {
        ValidateTaxBrackets(brackets);

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await EnsureColumnsAsync(conn);
        using var tx = conn.BeginTransaction();

        try
        {
            const string upsertSql = @"
                    IF EXISTS (SELECT 1 FROM SalarySettings WHERE Year = @Year)
                    BEGIN
                        UPDATE SalarySettings SET 
                            MinimumWage = @MinimumWage, HousingAllowance = @HousingAllowance, FoodAllowance = @FoodAllowance, MarriageAllowance = @MarriageAllowance,
                            ChildAllowanceRate = @ChildAllowanceRate, InsuranceEmployeeRate = @InsuranceEmployeeRate, InsuranceEmployerRate = @InsuranceEmployerRate,
                            AbsenceMultiplier = @AbsenceMultiplier,  
                            NormalDayHours = @NormalDayHours, HolidayDayHours = @HolidayDayHours, MonthlyWorkingHours = @MonthlyWorkingHours,
                            TaxIncludeBaseSalary = @TaxIncludeBaseSalary, TaxIncludeYearsBonus = @TaxIncludeYearsBonus, TaxIncludeAttraction = @TaxIncludeAttraction,
                            TaxIncludeResponsibility = @TaxIncludeResponsibility, TaxIncludeManagement = @TaxIncludeManagement, TaxIncludeTechnical = @TaxIncludeTechnical,
                            TaxIncludeHousing = @TaxIncludeHousing, TaxIncludeGrocery = @TaxIncludeGrocery, TaxIncludeChild = @TaxIncludeChild,
                            TaxIncludeMarriage = @TaxIncludeMarriage, TaxIncludeTransportation = @TaxIncludeTransportation, TaxIncludeOtherAllowances = @TaxIncludeOtherAllowances,
                            InsuranceIncludeBaseSalary = @InsuranceIncludeBaseSalary, InsuranceIncludeYearsBonus = @InsuranceIncludeYearsBonus, InsuranceIncludeAttraction = @InsuranceIncludeAttraction,
                            InsuranceIncludeResponsibility = @InsuranceIncludeResponsibility, InsuranceIncludeManagement = @InsuranceIncludeManagement, InsuranceIncludeTechnical = @InsuranceIncludeTechnical,
                            InsuranceIncludeHousing = @InsuranceIncludeHousing, InsuranceIncludeGrocery = @InsuranceIncludeGrocery, InsuranceIncludeChild = @InsuranceIncludeChild,
                            InsuranceIncludeMarriage = @InsuranceIncludeMarriage, InsuranceIncludeTransportation = @InsuranceIncludeTransportation, InsuranceIncludeOther = @InsuranceIncludeOther,
                            OvertimeIncludeBaseSalary = @OvertimeIncludeBaseSalary, OvertimeIncludeYearsBonus = @OvertimeIncludeYearsBonus, OvertimeIncludeAttraction = @OvertimeIncludeAttraction,
                            OvertimeIncludeResponsibility = @OvertimeIncludeResponsibility, OvertimeIncludeManagement = @OvertimeIncludeManagement, OvertimeIncludeTechnical = @OvertimeIncludeTechnical,
                            OvertimeIncludeHousing = @OvertimeIncludeHousing, OvertimeIncludeGrocery = @OvertimeIncludeGrocery, OvertimeIncludeChild = @OvertimeIncludeChild,
                            OvertimeIncludeMarriage = @OvertimeIncludeMarriage, OvertimeIncludeTransportation = @OvertimeIncludeTransportation, OvertimeIncludeOther = @OvertimeIncludeOther,
                            EidiIncludeComponents = @EidiIncludeComponents, SanavatIncludeComponents = @SanavatIncludeComponents,
                            MissionIncludeComponents = @MissionIncludeComponents, MissionRate = @MissionRate,
                            UpdatedAt = GETDATE()
                        WHERE Year = @Year
                    END
                    ELSE
                    BEGIN
                        INSERT INTO SalarySettings 
                            (Year, MinimumWage, HousingAllowance, FoodAllowance, MarriageAllowance, ChildAllowanceRate, InsuranceEmployeeRate, InsuranceEmployerRate, 
                             AbsenceMultiplier, 
                             NormalDayHours, HolidayDayHours, MonthlyWorkingHours,
                             TaxIncludeBaseSalary, TaxIncludeYearsBonus, TaxIncludeAttraction, TaxIncludeResponsibility, TaxIncludeManagement, TaxIncludeTechnical, TaxIncludeHousing, TaxIncludeGrocery, TaxIncludeChild, TaxIncludeMarriage, TaxIncludeTransportation, TaxIncludeOtherAllowances,
                             InsuranceIncludeBaseSalary, InsuranceIncludeYearsBonus, InsuranceIncludeAttraction, InsuranceIncludeResponsibility, InsuranceIncludeManagement, InsuranceIncludeTechnical, InsuranceIncludeHousing, InsuranceIncludeGrocery, InsuranceIncludeChild, InsuranceIncludeMarriage, InsuranceIncludeTransportation, InsuranceIncludeOther,
                             OvertimeIncludeBaseSalary, OvertimeIncludeYearsBonus, OvertimeIncludeAttraction, OvertimeIncludeResponsibility, OvertimeIncludeManagement, OvertimeIncludeTechnical, OvertimeIncludeHousing, OvertimeIncludeGrocery, OvertimeIncludeChild, OvertimeIncludeMarriage, OvertimeIncludeTransportation, OvertimeIncludeOther, EidiIncludeComponents, SanavatIncludeComponents, MissionIncludeComponents, MissionRate)
                        VALUES 
                            (@Year, @MinimumWage, @HousingAllowance, @FoodAllowance, @MarriageAllowance, @ChildAllowanceRate, @InsuranceEmployeeRate, @InsuranceEmployerRate, 
                             @AbsenceMultiplier, 
                             @NormalDayHours, @HolidayDayHours, @MonthlyWorkingHours,
                             @TaxIncludeBaseSalary, @TaxIncludeYearsBonus, @TaxIncludeAttraction, @TaxIncludeResponsibility, @TaxIncludeManagement, @TaxIncludeTechnical, @TaxIncludeHousing, @TaxIncludeGrocery, @TaxIncludeChild, @TaxIncludeMarriage, @TaxIncludeTransportation, @TaxIncludeOtherAllowances,
                             @InsuranceIncludeBaseSalary, @InsuranceIncludeYearsBonus, @InsuranceIncludeAttraction, @InsuranceIncludeResponsibility, @InsuranceIncludeManagement, @InsuranceIncludeTechnical, @InsuranceIncludeHousing, @InsuranceIncludeGrocery, @InsuranceIncludeChild, @InsuranceIncludeMarriage, @InsuranceIncludeTransportation, @InsuranceIncludeOther,
                             @OvertimeIncludeBaseSalary, @OvertimeIncludeYearsBonus, @OvertimeIncludeAttraction, @OvertimeIncludeResponsibility, @OvertimeIncludeManagement, @OvertimeIncludeTechnical, @OvertimeIncludeHousing, @OvertimeIncludeGrocery, @OvertimeIncludeChild, @OvertimeIncludeMarriage, @OvertimeIncludeTransportation, @OvertimeIncludeOther, @EidiIncludeComponents, @SanavatIncludeComponents, @MissionIncludeComponents, @MissionRate)
                    END";

            using (var cmd = new SqlCommand(upsertSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@Year", settings.Year);
                cmd.Parameters.AddWithValue("@MinimumWage", settings.MinimumWage);
                cmd.Parameters.AddWithValue("@HousingAllowance", settings.HousingAllowance);
                cmd.Parameters.AddWithValue("@FoodAllowance", settings.FoodAllowance);
                cmd.Parameters.AddWithValue("@MarriageAllowance", settings.MarriageAllowance);
                cmd.Parameters.AddWithValue("@ChildAllowanceRate", settings.ChildAllowanceRate);
                cmd.Parameters.AddWithValue("@InsuranceEmployeeRate", settings.InsuranceEmployeeRate);
                cmd.Parameters.AddWithValue("@InsuranceEmployerRate", settings.InsuranceEmployerRate);

                // 🔴 پارامتر معافیت حذف شد

                cmd.Parameters.AddWithValue("@AbsenceMultiplier", settings.AbsenceMultiplier);

                cmd.Parameters.AddWithValue("@NormalDayHours", settings.NormalDayHours);
                cmd.Parameters.AddWithValue("@HolidayDayHours", settings.HolidayDayHours);
                cmd.Parameters.AddWithValue("@MonthlyWorkingHours", settings.MonthlyWorkingHours);

                cmd.Parameters.AddWithValue("@TaxIncludeBaseSalary", settings.TaxIncludeBaseSalary ? 1 : 0);
                cmd.Parameters.AddWithValue("@TaxIncludeYearsBonus", settings.TaxIncludeYearsBonus ? 1 : 0);
                cmd.Parameters.AddWithValue("@TaxIncludeAttraction", settings.TaxIncludeAttraction ? 1 : 0);
                cmd.Parameters.AddWithValue("@TaxIncludeResponsibility", settings.TaxIncludeResponsibility ? 1 : 0);
                cmd.Parameters.AddWithValue("@TaxIncludeManagement", settings.TaxIncludeManagement ? 1 : 0);
                cmd.Parameters.AddWithValue("@TaxIncludeTechnical", settings.TaxIncludeTechnical ? 1 : 0);
                cmd.Parameters.AddWithValue("@TaxIncludeHousing", settings.TaxIncludeHousing ? 1 : 0);
                cmd.Parameters.AddWithValue("@TaxIncludeGrocery", settings.TaxIncludeGrocery ? 1 : 0);
                cmd.Parameters.AddWithValue("@TaxIncludeChild", settings.TaxIncludeChild ? 1 : 0);
                cmd.Parameters.AddWithValue("@TaxIncludeMarriage", settings.TaxIncludeMarriage ? 1 : 0);
                cmd.Parameters.AddWithValue("@TaxIncludeTransportation", settings.TaxIncludeTransportation ? 1 : 0);
                cmd.Parameters.AddWithValue("@TaxIncludeOtherAllowances", settings.TaxIncludeOtherAllowances ? 1 : 0);

                cmd.Parameters.AddWithValue("@InsuranceIncludeBaseSalary", settings.InsuranceIncludeBaseSalary ? 1 : 0);
                cmd.Parameters.AddWithValue("@InsuranceIncludeYearsBonus", settings.InsuranceIncludeYearsBonus ? 1 : 0);
                cmd.Parameters.AddWithValue("@InsuranceIncludeAttraction", settings.InsuranceIncludeAttraction ? 1 : 0);
                cmd.Parameters.AddWithValue("@InsuranceIncludeResponsibility", settings.InsuranceIncludeResponsibility ? 1 : 0);
                cmd.Parameters.AddWithValue("@InsuranceIncludeManagement", settings.InsuranceIncludeManagement ? 1 : 0);
                cmd.Parameters.AddWithValue("@InsuranceIncludeTechnical", settings.InsuranceIncludeTechnical ? 1 : 0);
                cmd.Parameters.AddWithValue("@InsuranceIncludeHousing", settings.InsuranceIncludeHousing ? 1 : 0);
                cmd.Parameters.AddWithValue("@InsuranceIncludeGrocery", settings.InsuranceIncludeGrocery ? 1 : 0);
                cmd.Parameters.AddWithValue("@InsuranceIncludeChild", settings.InsuranceIncludeChild ? 1 : 0);
                cmd.Parameters.AddWithValue("@InsuranceIncludeMarriage", settings.InsuranceIncludeMarriage ? 1 : 0);
                cmd.Parameters.AddWithValue("@InsuranceIncludeTransportation", settings.InsuranceIncludeTransportation ? 1 : 0);
                cmd.Parameters.AddWithValue("@InsuranceIncludeOther", settings.InsuranceIncludeOther ? 1 : 0);

                cmd.Parameters.AddWithValue("@OvertimeIncludeBaseSalary", settings.OvertimeIncludeBaseSalary ? 1 : 0);
                cmd.Parameters.AddWithValue("@OvertimeIncludeYearsBonus", settings.OvertimeIncludeYearsBonus ? 1 : 0);
                cmd.Parameters.AddWithValue("@OvertimeIncludeAttraction", settings.OvertimeIncludeAttraction ? 1 : 0);
                cmd.Parameters.AddWithValue("@OvertimeIncludeResponsibility", settings.OvertimeIncludeResponsibility ? 1 : 0);
                cmd.Parameters.AddWithValue("@OvertimeIncludeManagement", settings.OvertimeIncludeManagement ? 1 : 0);
                cmd.Parameters.AddWithValue("@OvertimeIncludeTechnical", settings.OvertimeIncludeTechnical ? 1 : 0);
                cmd.Parameters.AddWithValue("@OvertimeIncludeHousing", settings.OvertimeIncludeHousing ? 1 : 0);
                cmd.Parameters.AddWithValue("@OvertimeIncludeGrocery", settings.OvertimeIncludeGrocery ? 1 : 0);
                cmd.Parameters.AddWithValue("@OvertimeIncludeChild", settings.OvertimeIncludeChild ? 1 : 0);
                cmd.Parameters.AddWithValue("@OvertimeIncludeMarriage", settings.OvertimeIncludeMarriage ? 1 : 0);
                cmd.Parameters.AddWithValue("@OvertimeIncludeTransportation", settings.OvertimeIncludeTransportation ? 1 : 0);
                cmd.Parameters.AddWithValue("@OvertimeIncludeOther", settings.OvertimeIncludeOther ? 1 : 0);

                cmd.Parameters.AddWithValue("@EidiIncludeComponents", settings.EidiIncludeComponents ?? "");
                cmd.Parameters.AddWithValue("@SanavatIncludeComponents", settings.SanavatIncludeComponents ?? "");
                cmd.Parameters.AddWithValue("@MissionIncludeComponents", settings.MissionIncludeComponents ?? "");
                cmd.Parameters.AddWithValue("@MissionRate", settings.MissionRate);

                await cmd.ExecuteNonQueryAsync();
            }

            using (var del = new SqlCommand("DELETE FROM TaxBrackets WHERE Year = @Year", conn, tx))
            {
                del.Parameters.AddWithValue("@Year", settings.Year);
                await del.ExecuteNonQueryAsync();
            }

            var sortedBrackets = brackets.OrderBy(b => b.FromAmount).ToList();
            foreach (var b in sortedBrackets)
            {
                using var ins = new SqlCommand(@"INSERT INTO TaxBrackets (Year, FromAmount, ToAmount, TaxRate) VALUES (@Year, @From, @To, @Rate)", conn, tx);
                ins.Parameters.AddWithValue("@Year", settings.Year);
                ins.Parameters.AddWithValue("@From", b.FromAmount);
                ins.Parameters.AddWithValue("@To", (object?)b.ToAmount ?? DBNull.Value);
                ins.Parameters.AddWithValue("@Rate", b.TaxRate);
                await ins.ExecuteNonQueryAsync();
            }
            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    private static void ValidateTaxBrackets(List<TaxBracketDto>? brackets)
    {
        if (brackets is null || brackets.Count == 0) return;
        var sorted = brackets.OrderBy(b => b.FromAmount).ToList();

        if (sorted[0].FromAmount != 0)
            throw new InvalidOperationException($"اولین پله مالیات باید از مبلغ ۰ شروع شود، اما از {sorted[0].FromAmount:N0} شروع شده است.");

        for (int i = 0; i < sorted.Count; i++)
        {
            if (sorted[i].TaxRate < 0 || sorted[i].TaxRate > 1)
                throw new InvalidOperationException($"نرخ مالیات پله {i + 1} (از {sorted[i].FromAmount:N0}) نامعتبر است.\nنرخ باید بین ۰ و ۱ باشد. مقدار فعلی: {sorted[i].TaxRate}");
        }

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (!sorted[i].ToAmount.HasValue)
                throw new InvalidOperationException($"پله {i + 1} (از {sorted[i].FromAmount:N0}) سقف ندارد، پس پله بعدی نمی‌تواند وجود داشته باشد.");

            if (sorted[i].ToAmount.Value != sorted[i + 1].FromAmount)
                throw new InvalidOperationException($"پله‌های {i + 1} و {i + 2} پیوسته نیستند.");

            if (sorted[i].ToAmount.Value <= sorted[i].FromAmount)
                throw new InvalidOperationException($"پله {i + 1}: سقف باید بزرگتر از شروع باشد.");
        }
    }
}
