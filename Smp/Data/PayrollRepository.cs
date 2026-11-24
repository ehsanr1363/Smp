using Microsoft.Data.SqlClient;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Smp.Data;

public class PayrollRepository
{
    private readonly string _connectionString;

    // نام جدول‌ها فقط از این whitelist می‌آید (جلوگیری از SQL Injection در نام جدول)
    private const string KindLoan = "Loan";
    private const string KindAdvance = "Advance";   // مساعده: همان جدول EmployeeLoans ولی با LoanType = "مساعده"
    private const string KindSuppIns = "SuppIns";
    private const string KindCarIns = "CarIns";

    private static readonly Dictionary<string, string> KindTables = new()
    {
        [KindLoan] = "EmployeeLoans",
        [KindAdvance] = "EmployeeLoans",
        [KindSuppIns] = "EmployeeSupplementaryInsurances",
        [KindCarIns] = "EmployeeCarInsurances"
    };

    // فیلتر نوع ردیف: «وام» = هر چیزی جز مساعده، «مساعده» = فقط LoanType = مساعده
    private static readonly Dictionary<string, string> KindFilters = new()
    {
        [KindLoan] = " AND ISNULL(LoanType, N'وام') <> N'مساعده'",
        // مساعده فقط در ماه شروع خودش کسر می‌شود (ماه شروع نامشخص = هر ماه)
        [KindAdvance] = " AND LoanType = N'مساعده' AND (ISNULL(StartYear, 0) = 0 OR ISNULL(StartMonth, 0) = 0 OR (StartYear = @Y AND StartMonth = @M))"
    };

    private static bool _logTableEnsured;
    private static readonly object _logLock = new();

    public PayrollRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    // ==========================================================
    // متدهای ذخیره‌سازی با قابلیت دریافت ورودی‌های متغیر
    // ==========================================================

    public async Task SavePayrollsAndDeductInstallmentsAsync(params object[] args)
    {
        var (payrolls, year, month) = ExtractPayrollArgs(args);
        await SavePayrollsAndDeductInstallmentsCoreAsync(payrolls, year, month);
    }

    public async Task ReSavePayrollsForMonthWithInstallmentRestoreAsync(params object[] args)
    {
        var (payrolls, year, month) = ExtractPayrollArgs(args);
        await ReSavePayrollsForMonthWithInstallmentRestoreCoreAsync(payrolls, year, month);
    }

    public async Task SavePayrollBatchAsync(List<PayrollResult> payrolls, int year, int month)
    {
        await SavePayrollsAndDeductInstallmentsCoreAsync(payrolls, year, month);
        Smp.Helpers.AuditHelper.Log("ثبت", "پردازش حقوق", $"{payrolls.Count} نفر، سال {year} ماه {month}");
    }

    // ==========================================================
    // جدول لاگ اقساط: دقیقاً ثبت می‌کند هر فیش چقدر از کدام ردیف وام/بیمه کم کرده
    // تا برگشت (ذخیره مجدد / حذف فیش) دقیق و بدون دوبار-کسر انجام شود.
    // ==========================================================

    private async Task EnsureLogTableAsync()
    {
        if (_logTableEnsured) return;

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
                IF OBJECT_ID(N'dbo.PayrollInstallmentLog', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.PayrollInstallmentLog (
                        Id         INT IDENTITY(1,1) PRIMARY KEY,
                        [Year]     INT           NOT NULL,
                        [Month]    INT           NOT NULL,
                        EmployeeId INT           NOT NULL,
                        Kind       NVARCHAR(20)  NOT NULL,
                        RowId      INT           NOT NULL,
                        Amount     DECIMAL(18,2) NOT NULL
                    );
                    CREATE INDEX IX_PayrollInstallmentLog_Period
                        ON dbo.PayrollInstallmentLog ([Year], [Month], EmployeeId);
                END";
        using (var cmd = new SqlCommand(sql, conn))
            await cmd.ExecuteNonQueryAsync();

        // ستون جدید «کسر مساعده» (جدا از وام) + انتقال داده‌های قدیمی
        bool advanceColAdded;
        using (var chk = new SqlCommand(
            "SELECT CASE WHEN COL_LENGTH(N'dbo.Payrolls', N'AdvanceDeduction') IS NULL THEN 1 ELSE 0 END", conn))
            advanceColAdded = Convert.ToInt32(await chk.ExecuteScalarAsync()) == 1;

        if (advanceColAdded)
        {
            using (var add = new SqlCommand(
                "ALTER TABLE dbo.Payrolls ADD AdvanceDeduction DECIMAL(18,2) NOT NULL " +
                "CONSTRAINT DF_Payrolls_AdvanceDeduction DEFAULT 0;", conn))
                await add.ExecuteNonQueryAsync();

            // فیش‌های قبلی: بخشی از «قسط وام» که در واقع مساعده بوده از روی لاگ جدا می‌شود
            const string migrate = @"
                UPDATE l SET l.Kind = N'Advance'
                FROM dbo.PayrollInstallmentLog l
                INNER JOIN dbo.EmployeeLoans el ON el.Id = l.RowId
                WHERE l.Kind = N'Loan' AND el.LoanType = N'مساعده';

                UPDATE p SET
                    p.AdvanceDeduction = x.Amt,
                    p.LoanDeduction = CASE WHEN p.LoanDeduction - x.Amt < 0 THEN 0 ELSE p.LoanDeduction - x.Amt END
                FROM dbo.Payrolls p
                INNER JOIN (SELECT [Year], [Month], EmployeeId, SUM(Amount) AS Amt
                            FROM dbo.PayrollInstallmentLog
                            WHERE Kind = N'Advance'
                            GROUP BY [Year], [Month], EmployeeId) x
                    ON x.[Year] = p.[Year] AND x.[Month] = p.[Month] AND x.EmployeeId = p.EmployeeId;";
            using (var mig = new SqlCommand(migrate, conn))
                await mig.ExecuteNonQueryAsync();
        }

        // ستون‌های Payrolls (اگر نبود اضافه می‌شود)
        string[] columns =
        {
            "SupplementaryInsuranceDeduction", "CarInsuranceDeduction", "OtherDeductions",
            "OvertimePay", "AbsenceDeduction",
            "YearsBonus", "HousingAllowance", "GroceryAllowance", "ChildAllowance",
            "AttractionAllowance", "ResponsibilityAllowance", "ManagementAllowance",
            "TechnicalAllowance", "MarriageAllowance", "TransportationAllowance",
            "OtherAllowances", "InsuranceBase", "TaxableBase",
            "WorkingDays", "OvertimeHours", "AbsenceHours"
        };

        var sb = new System.Text.StringBuilder();
        foreach (var c in columns)
        {
            sb.AppendLine(
                $"IF COL_LENGTH(N'dbo.Payrolls', N'{c}') IS NULL " +
                $"ALTER TABLE dbo.Payrolls ADD {c} DECIMAL(18,2) NOT NULL " +
                $"CONSTRAINT DF_Payrolls_{c} DEFAULT 0;");
        }

        using (var cmd2 = new SqlCommand(sb.ToString(), conn))
            await cmd2.ExecuteNonQueryAsync();

        lock (_logLock) { _logTableEnsured = true; }
    }

    // ==========================================================
    // منطق اصلی (Private Core Methods)
    // ==========================================================

    private async Task SavePayrollsAndDeductInstallmentsCoreAsync(List<PayrollResult> payrolls, int year, int month)
    {
        if (payrolls is null || payrolls.Count == 0) return;

        await EnsureLogTableAsync();

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        try
        {
            foreach (var p in payrolls)
            {
                // ✅ FIX: اگر برای این کارمند/ماه قبلاً قسط کم شده، اول برمی‌گردد (ذخیره مجدد = بدون دوبار-کسر)
                await RestoreLoggedInstallmentsAsync(conn, transaction, year, month, p.EmployeeId);

                await InsertOrUpdatePayrollAsync(conn, transaction, p, year, month);
                await DeductAndLogInstallmentsAsync(conn, transaction, p, year, month);
            }
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new Exception("خطا در ذخیره حقوق و کسر اقساط: " + ex.Message, ex);
        }
    }

    private async Task ReSavePayrollsForMonthWithInstallmentRestoreCoreAsync(List<PayrollResult> payrolls, int year, int month)
    {
        await EnsureLogTableAsync();

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        try
        {
            // ۱. برگرداندن همه‌ی اقساطِ کسرشده‌ی این ماه (طبق لاگ)
            await RestoreLoggedInstallmentsAsync(conn, transaction, year, month, null);

            // ۲. حذف حقوق‌های قبلی این ماه
            using (var cmdDel = new SqlCommand("DELETE FROM Payrolls WHERE Year = @Year AND Month = @Month", conn, transaction))
            {
                cmdDel.Parameters.AddWithValue("@Year", year);
                cmdDel.Parameters.AddWithValue("@Month", month);
                await cmdDel.ExecuteNonQueryAsync();
            }

            // ۳. درج مجدد و کسر اقساط جدید
            if (payrolls is not null)
            {
                foreach (var p in payrolls)
                {
                    await InsertOrUpdatePayrollAsync(conn, transaction, p, year, month);
                    await DeductAndLogInstallmentsAsync(conn, transaction, p, year, month);
                }
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new Exception("خطا در بازسازی حقوق: " + ex.Message, ex);
        }
    }

    // ==========================================================
    // متدهای کمکی
    // ==========================================================

    private (List<PayrollResult>, int, int) ExtractPayrollArgs(object[] args)
    {
        int year = 0, month = 0;
        List<PayrollResult> payrolls = null;

        foreach (var arg in args)
        {
            if (arg is int i)
            {
                if (year == 0) year = i;
                else if (month == 0) month = i;
            }
            else if (arg is List<PayrollResult> list)
            {
                payrolls = list;
            }
        }

        if (payrolls is null || year == 0 || month == 0)
            throw new ArgumentException("پارامترهای ارسالی به متد نامعتبر هستند (سال، ماه یا لیست حقوق یافت نشد).");

        return (payrolls, year, month);
    }

    private async Task InsertOrUpdatePayrollAsync(SqlConnection conn, SqlTransaction transaction, PayrollResult p, int year, int month)
    {
        string sql = @"
                IF EXISTS (SELECT 1 FROM Payrolls WHERE EmployeeId = @EmpId AND Year = @Year AND Month = @Month)
                BEGIN
                    UPDATE Payrolls SET 
                        BaseSalary = @BaseSalary,
                        TotalEarnings = @TotalEarnings,
                        TotalDeductions = @TotalDeductions,
                        NetPay = @NetPay,
                        InsuranceEmployee = @InsEmp,
                        InsuranceEmployer = @InsEmpr,
                        IncomeTax = @Tax,
                        LoanDeduction = @Loan,
                        AdvanceDeduction = @Advance,
                        SupplementaryInsuranceDeduction = @SupIns,
                        CarInsuranceDeduction = @CarIns,
                        OtherDeductions = @Other,
                        OvertimePay = @Overtime,
                        AbsenceDeduction = @Absence,
                        YearsBonus = @YearsBonus,
                        HousingAllowance = @HousingAllowance,
                        GroceryAllowance = @GroceryAllowance,
                        ChildAllowance = @ChildAllowance,
                        AttractionAllowance = @AttractionAllowance,
                        ResponsibilityAllowance = @ResponsibilityAllowance,
                        ManagementAllowance = @ManagementAllowance,
                        TechnicalAllowance = @TechnicalAllowance,
                        MarriageAllowance = @MarriageAllowance,
                        TransportationAllowance = @TransportationAllowance,
                        OtherAllowances = @OtherAllowances,
                        InsuranceBase = @InsuranceBase,
                        TaxableBase = @TaxableBase,
                        WorkingDays = @WorkingDays,
                        OvertimeHours = @OvertimeHours,
                        AbsenceHours = @AbsenceHours,
                        WorkLocation = (SELECT TOP 1 LTRIM(RTRIM(WorkLocation)) FROM Employees WHERE Id = @EmpId),
                        PersonnelType = (SELECT TOP 1 LTRIM(RTRIM(PersonnelType)) FROM Employees WHERE Id = @EmpId)
                    WHERE EmployeeId = @EmpId AND Year = @Year AND Month = @Month
                END
                ELSE
                BEGIN
                    INSERT INTO Payrolls (
                        EmployeeId, Year, Month, BaseSalary, TotalEarnings, TotalDeductions, NetPay,
                        InsuranceEmployee, InsuranceEmployer, IncomeTax, LoanDeduction, AdvanceDeduction,
                        SupplementaryInsuranceDeduction, CarInsuranceDeduction, OtherDeductions,
                        OvertimePay, AbsenceDeduction,
                        YearsBonus, HousingAllowance, GroceryAllowance, ChildAllowance, AttractionAllowance, ResponsibilityAllowance, ManagementAllowance, TechnicalAllowance, MarriageAllowance, TransportationAllowance, OtherAllowances, InsuranceBase, TaxableBase, WorkingDays, OvertimeHours, AbsenceHours,
                        WorkLocation, PersonnelType
                    ) VALUES (
                        @EmpId, @Year, @Month, @BaseSalary, @TotalEarnings, @TotalDeductions, @NetPay,
                        @InsEmp, @InsEmpr, @Tax, @Loan, @Advance,
                        @SupIns, @CarIns, @Other,
                        @Overtime, @Absence,
                        @YearsBonus, @HousingAllowance, @GroceryAllowance, @ChildAllowance, @AttractionAllowance, @ResponsibilityAllowance, @ManagementAllowance, @TechnicalAllowance, @MarriageAllowance, @TransportationAllowance, @OtherAllowances, @InsuranceBase, @TaxableBase, @WorkingDays, @OvertimeHours, @AbsenceHours,
                        (SELECT TOP 1 LTRIM(RTRIM(WorkLocation)) FROM Employees WHERE Id = @EmpId),
                        (SELECT TOP 1 LTRIM(RTRIM(PersonnelType)) FROM Employees WHERE Id = @EmpId)
                    )
                END";

        using var cmd = new SqlCommand(sql, conn, transaction);
        cmd.Parameters.AddWithValue("@EmpId", p.EmployeeId);
        cmd.Parameters.AddWithValue("@Year", year);
        cmd.Parameters.AddWithValue("@Month", month);
        cmd.Parameters.AddWithValue("@BaseSalary", p.BaseSalary);
        cmd.Parameters.AddWithValue("@TotalEarnings", p.TotalEarnings);
        cmd.Parameters.AddWithValue("@TotalDeductions", p.TotalDeductions);
        cmd.Parameters.AddWithValue("@NetPay", p.NetPay);
        cmd.Parameters.AddWithValue("@InsEmp", p.InsuranceEmployee);
        cmd.Parameters.AddWithValue("@InsEmpr", p.InsuranceEmployer);
        cmd.Parameters.AddWithValue("@Tax", p.IncomeTax);
        cmd.Parameters.AddWithValue("@Loan", p.LoanDeduction);
        cmd.Parameters.AddWithValue("@Advance", p.AdvanceDeduction);
        cmd.Parameters.AddWithValue("@SupIns", p.SupplementaryInsuranceDeduction);
        cmd.Parameters.AddWithValue("@CarIns", p.CarInsuranceDeduction);
        cmd.Parameters.AddWithValue("@Other", p.OtherDeductions);
        cmd.Parameters.AddWithValue("@Overtime", p.OvertimePay);
        cmd.Parameters.AddWithValue("@Absence", p.AbsenceDeduction);
        cmd.Parameters.AddWithValue("@YearsBonus", p.YearsBonus);
        cmd.Parameters.AddWithValue("@HousingAllowance", p.HousingAllowance);
        cmd.Parameters.AddWithValue("@GroceryAllowance", p.GroceryAllowance);
        cmd.Parameters.AddWithValue("@ChildAllowance", p.ChildAllowance);
        cmd.Parameters.AddWithValue("@AttractionAllowance", p.AttractionAllowance);
        cmd.Parameters.AddWithValue("@ResponsibilityAllowance", p.ResponsibilityAllowance);
        cmd.Parameters.AddWithValue("@ManagementAllowance", p.ManagementAllowance);
        cmd.Parameters.AddWithValue("@TechnicalAllowance", p.TechnicalAllowance);
        cmd.Parameters.AddWithValue("@MarriageAllowance", p.MarriageAllowance);
        cmd.Parameters.AddWithValue("@TransportationAllowance", p.TransportationAllowance);
        cmd.Parameters.AddWithValue("@OtherAllowances", p.OtherAllowances);
        cmd.Parameters.AddWithValue("@InsuranceBase", p.InsuranceBase);
        cmd.Parameters.AddWithValue("@TaxableBase", p.TaxableBase);
        cmd.Parameters.AddWithValue("@WorkingDays", p.WorkingDays);
        cmd.Parameters.AddWithValue("@OvertimeHours", p.OvertimeHours);
        cmd.Parameters.AddWithValue("@AbsenceHours", p.AbsenceHours);

        await cmd.ExecuteNonQueryAsync();
    }

    // ----------------------------------------------------------
    // کسر اقساط (ردیف‌به‌ردیف) + ثبت در لاگ
    // ----------------------------------------------------------

    private async Task DeductAndLogInstallmentsAsync(SqlConnection conn, SqlTransaction transaction, PayrollResult p, int year, int month)
    {
        if (p.LoanDeduction > 0)
            await DeductKindAsync(conn, transaction, year, month, p.EmployeeId, KindLoan, p.LoanDeduction);

        if (p.AdvanceDeduction > 0)
            await DeductKindAsync(conn, transaction, year, month, p.EmployeeId, KindAdvance, p.AdvanceDeduction);

        if (p.SupplementaryInsuranceDeduction > 0)
            await DeductKindAsync(conn, transaction, year, month, p.EmployeeId, KindSuppIns, p.SupplementaryInsuranceDeduction);

        if (p.CarInsuranceDeduction > 0)
            await DeductKindAsync(conn, transaction, year, month, p.EmployeeId, KindCarIns, p.CarInsuranceDeduction);
    }

    private async Task DeductKindAsync(SqlConnection conn, SqlTransaction transaction,
        int year, int month, int empId, string kind, decimal amount)
    {
        string table = KindTables[kind];

        // ۱. اول همه‌ی ردیف‌ها را بخوان و Reader را ببند (مشکل «open DataReader» بدون MARS)
        var rows = new List<(int Id, decimal Inst, decimal Rem)>();
        using (var sel = new SqlCommand(
            $"SELECT Id, MonthlyInstallment, RemainingAmount FROM {table} " +
            "WHERE EmployeeId = @EmpId AND IsActive = 1 AND RemainingAmount > 0" +
            (KindFilters.TryGetValue(kind, out var filter) ? filter : "") + " ORDER BY Id",
            conn, transaction))
        {
            sel.Parameters.AddWithValue("@EmpId", empId);
            if (kind == KindAdvance)
            {
                sel.Parameters.AddWithValue("@Y", year);
                sel.Parameters.AddWithValue("@M", month);
            }
            using var reader = await sel.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add((
                    reader.GetInt32(0),
                    reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
                    reader.IsDBNull(2) ? 0m : reader.GetDecimal(2)));
            }
        }

        // ۲. به ترتیب Id، از هر ردیف حداکثر «قسط خودش» (و نه بیشتر از مانده) کم کن
        decimal left = amount;
        foreach (var row in rows)
        {
            if (left <= 0) break;

            decimal perRow = row.Inst > 0 ? Math.Min(row.Inst, row.Rem) : row.Rem;
            decimal take = Math.Min(perRow, left);
            if (take <= 0) continue;

            using (var upd = new SqlCommand(
                $"UPDATE {table} SET " +
                "RemainingAmount = RemainingAmount - @Take, " +
                "IsActive = CASE WHEN RemainingAmount - @Take <= 0 THEN 0 ELSE 1 END " +
                "WHERE Id = @Id", conn, transaction))
            {
                upd.Parameters.AddWithValue("@Take", take);
                upd.Parameters.AddWithValue("@Id", row.Id);
                await upd.ExecuteNonQueryAsync();
            }

            using (var log = new SqlCommand(
                "INSERT INTO PayrollInstallmentLog ([Year], [Month], EmployeeId, Kind, RowId, Amount) " +
                "VALUES (@Y, @M, @E, @K, @R, @A)", conn, transaction))
            {
                log.Parameters.AddWithValue("@Y", year);
                log.Parameters.AddWithValue("@M", month);
                log.Parameters.AddWithValue("@E", empId);
                log.Parameters.AddWithValue("@K", kind);
                log.Parameters.AddWithValue("@R", row.Id);
                log.Parameters.AddWithValue("@A", take);
                await log.ExecuteNonQueryAsync();
            }

            left -= take;
        }

        // اگر قسط محاسبه‌شده با وام‌های فعال همخوانی نداشت (مثلاً بین محاسبه و ذخیره وام ویرایش شده)
        // کل تراکنش Rollback شود تا داده ناسازگار ذخیره نشود.
        if (left > 0.5m)
            throw new InvalidOperationException(
                $"قسط محاسبه‌شده برای کارمند (Id={empId}) با اقساط فعال او همخوانی ندارد. لطفاً دوباره «محاسبه» را بزنید.");
    }

    // ----------------------------------------------------------
    // برگشت اقساط طبق لاگ (empId = null → همه‌ی کارمندان آن ماه)
    // ----------------------------------------------------------

    private async Task RestoreLoggedInstallmentsAsync(SqlConnection conn, SqlTransaction transaction,
        int year, int month, int? empId)
    {
        // فیش‌هایی که لاگ اقساطشان ثبت نشده (ذخیره‌شده با نسخه‌های قدیمی) از روی مبلغ خودِ فیش برگردانده می‌شوند
        await RestoreFromPayrollAmountsWhenNoLogAsync(conn, transaction, year, month, empId);

        var logs = new List<(string Kind, int RowId, decimal Amount)>();

        string selectSql = "SELECT Kind, RowId, Amount FROM PayrollInstallmentLog WHERE [Year] = @Y AND [Month] = @M"
                           + (empId.HasValue ? " AND EmployeeId = @E" : "");

        using (var sel = new SqlCommand(selectSql, conn, transaction))
        {
            sel.Parameters.AddWithValue("@Y", year);
            sel.Parameters.AddWithValue("@M", month);
            if (empId.HasValue) sel.Parameters.AddWithValue("@E", empId.Value);

            using var reader = await sel.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                logs.Add((reader.GetString(0), reader.GetInt32(1), reader.GetDecimal(2)));
        }

        foreach (var l in logs)
        {
            if (!KindTables.TryGetValue(l.Kind, out var table)) continue;

            // مانده هیچ‌وقت از TotalAmount بیشتر نمی‌شود
            using var upd = new SqlCommand(
                $"UPDATE {table} SET " +
                "RemainingAmount = CASE WHEN RemainingAmount + @A > TotalAmount THEN TotalAmount ELSE RemainingAmount + @A END, " +
                "IsActive = 1 WHERE Id = @Id", conn, transaction);
            upd.Parameters.AddWithValue("@A", l.Amount);
            upd.Parameters.AddWithValue("@Id", l.RowId);
            await upd.ExecuteNonQueryAsync();
        }

        string delSql = "DELETE FROM PayrollInstallmentLog WHERE [Year] = @Y AND [Month] = @M"
                        + (empId.HasValue ? " AND EmployeeId = @E" : "");
        using (var del = new SqlCommand(delSql, conn, transaction))
        {
            del.Parameters.AddWithValue("@Y", year);
            del.Parameters.AddWithValue("@M", month);
            if (empId.HasValue) del.Parameters.AddWithValue("@E", empId.Value);
            await del.ExecuteNonQueryAsync();
        }
    }

    private async Task RestoreFromPayrollAmountsWhenNoLogAsync(SqlConnection conn, SqlTransaction transaction,
        int year, int month, int? empId)
    {
        var slips = new List<(int EmpId, decimal Loan, decimal Adv)>();
        using (var sel = new SqlCommand(
            "SELECT EmployeeId, LoanDeduction, AdvanceDeduction FROM Payrolls " +
            "WHERE [Year] = @Y AND [Month] = @M" + (empId.HasValue ? " AND EmployeeId = @E" : ""),
            conn, transaction))
        {
            sel.Parameters.AddWithValue("@Y", year);
            sel.Parameters.AddWithValue("@M", month);
            if (empId.HasValue) sel.Parameters.AddWithValue("@E", empId.Value);
            using var reader = await sel.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                slips.Add((reader.GetInt32(0),
                           reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
                           reader.IsDBNull(2) ? 0m : reader.GetDecimal(2)));
        }

        foreach (var s in slips)
        {
            if (s.Loan > 0 && !await HasLogAsync(conn, transaction, year, month, s.EmpId, KindLoan))
                await AddBackWithoutLogAsync(conn, transaction, year, month, s.EmpId, KindLoan, s.Loan);
            if (s.Adv > 0 && !await HasLogAsync(conn, transaction, year, month, s.EmpId, KindAdvance))
                await AddBackWithoutLogAsync(conn, transaction, year, month, s.EmpId, KindAdvance, s.Adv);
        }
    }

    private static async Task<bool> HasLogAsync(SqlConnection conn, SqlTransaction transaction,
        int year, int month, int empId, string kind)
    {
        using var cmd = new SqlCommand(
            "SELECT COUNT(1) FROM PayrollInstallmentLog WHERE [Year] = @Y AND [Month] = @M AND EmployeeId = @E AND Kind = @K",
            conn, transaction);
        cmd.Parameters.AddWithValue("@Y", year);
        cmd.Parameters.AddWithValue("@M", month);
        cmd.Parameters.AddWithValue("@E", empId);
        cmd.Parameters.AddWithValue("@K", kind);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    // فقط ردیف‌هایی که تا آن ماه شروع شده‌اند و مبلغی از آن‌ها کسر شده است، به ترتیب Id برگردانده می‌شوند
    private async Task AddBackWithoutLogAsync(SqlConnection conn, SqlTransaction transaction,
        int year, int month, int empId, string kind, decimal amount)
    {
        string table = KindTables[kind];
        var rows = new List<(int Id, decimal Paid)>();
        using (var sel = new SqlCommand(
            $"SELECT Id, TotalAmount - RemainingAmount FROM {table} " +
            "WHERE EmployeeId = @E AND TotalAmount - RemainingAmount > 0 " +
            "AND (ISNULL(StartYear, 0) = 0 OR StartYear < @Y OR (StartYear = @Y AND StartMonth <= @M))" +
            (KindFilters.TryGetValue(kind, out var f) ? f : "") + " ORDER BY Id",
            conn, transaction))
        {
            sel.Parameters.AddWithValue("@E", empId);
            sel.Parameters.AddWithValue("@Y", year);
            sel.Parameters.AddWithValue("@M", month);
            using var reader = await sel.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                rows.Add((reader.GetInt32(0), reader.GetDecimal(1)));
        }

        decimal left = amount;
        foreach (var row in rows)
        {
            if (left <= 0) break;
            decimal give = Math.Min(row.Paid, left);
            using var upd = new SqlCommand(
                $"UPDATE {table} SET RemainingAmount = RemainingAmount + @A, IsActive = 1 WHERE Id = @Id",
                conn, transaction);
            upd.Parameters.AddWithValue("@A", give);
            upd.Parameters.AddWithValue("@Id", row.Id);
            await upd.ExecuteNonQueryAsync();
            left -= give;
        }
    }

    // ==========================================================
    // متدهای خواندن اطلاعات
    // ==========================================================

    public async Task<bool> HasPayrollsForMonthAsync(int year, int month)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        string sql = "SELECT COUNT(1) FROM Payrolls WHERE Year = @Year AND Month = @Month";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Year", year);
        cmd.Parameters.AddWithValue("@Month", month);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    public async Task<DataTable> GetSavedPayrollsAsync(int year, int month)
    {
        await EnsureLogTableAsync(); // اطمینان از وجود ستون‌های جدید
        var dt = new DataTable();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
                SELECT p.Id, p.EmployeeId, p.Year, p.Month, p.BaseSalary, p.TotalEarnings, p.TotalDeductions, p.NetPay,
                       p.InsuranceEmployee, p.InsuranceEmployer, p.IncomeTax, p.LoanDeduction, p.AdvanceDeduction, p.SupplementaryInsuranceDeduction,
                       p.CarInsuranceDeduction, p.OtherDeductions, p.OvertimePay, p.AbsenceDeduction,
                       p.YearsBonus, p.HousingAllowance, p.GroceryAllowance, p.ChildAllowance, p.AttractionAllowance,
                       p.ResponsibilityAllowance, p.ManagementAllowance, p.TechnicalAllowance, p.MarriageAllowance,
                       p.TransportationAllowance, p.OtherAllowances, p.InsuranceBase, p.TaxableBase,
                       p.WorkingDays, p.OvertimeHours, p.AbsenceHours,
                       e.PersonnelType,
                       LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(p.WorkLocation)), N''), ISNULL(e.WorkLocation, N'')))) AS WorkLocation,
                       e.FirstName + ' ' + e.LastName AS EmployeeName, e.PersonnelCode, e.NationalCode, e.InsuranceNumber, e.Position,
                       ISNULL((SELECT TOP 1 ShebaNumber FROM EmployeeBankAccounts WHERE EmployeeId = e.Id AND IsMainAccount = 1), '---') AS ShebaNumber
                FROM Payrolls p
                INNER JOIN Employees e ON p.EmployeeId = e.Id
                WHERE p.Year = @Year AND p.Month = @Month
                ORDER BY e.LastName, e.FirstName";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Year", year);
        cmd.Parameters.AddWithValue("@Month", month);
        using var adapter = new SqlDataAdapter(cmd);
        await Task.Run(() => adapter.Fill(dt));
        return dt;
    }

    public async Task<DataTable> GetBankPaymentListAsync(int year, int month)
    {
        var dt = new DataTable();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        string sql = @"
                SELECT p.EmployeeId, e.FirstName, e.LastName, e.NationalCode, p.NetPay, 
                       ISNULL((SELECT TOP 1 ShebaNumber FROM EmployeeBankAccounts WHERE EmployeeId = e.Id AND IsMainAccount = 1 AND ISNULL(IsActive, 1) = 1), '') AS ShebaNumber,
                       ISNULL((SELECT TOP 1 BankCode FROM EmployeeBankAccounts WHERE EmployeeId = e.Id AND IsMainAccount = 1 AND ISNULL(IsActive, 1) = 1), '') AS BankCode
                FROM Payrolls p
                INNER JOIN Employees e ON p.EmployeeId = e.Id
                WHERE p.Year = @Year AND p.Month = @Month AND p.NetPay > 0
                ORDER BY e.LastName, e.FirstName";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Year", year);
        cmd.Parameters.AddWithValue("@Month", month);
        using var adapter = new SqlDataAdapter(cmd);
        await Task.Run(() => adapter.Fill(dt));
        return dt;
    }

    public async Task<DataTable> GetEmployeePayrollHistoryAsync(int employeeId)
    {
        var dt = new DataTable();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        string sql = @"
                SELECT p.Year, p.Month, p.BaseSalary, p.TotalEarnings, p.TotalDeductions, p.NetPay,
                       p.InsuranceEmployee, p.IncomeTax, p.LoanDeduction, p.AdvanceDeduction
                FROM Payrolls p
                WHERE p.EmployeeId = @EmpId
                ORDER BY p.Year DESC, p.Month DESC";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@EmpId", employeeId);
        using var adapter = new SqlDataAdapter(cmd);
        await Task.Run(() => adapter.Fill(dt));
        return dt;
    }

    public async Task<DataTable> GetOfficialInsuranceReportAsync(int year, int month)
    {
        await EnsureLogTableAsync(); // اطمینان از وجود ستون‌های جدید
        var dt = new DataTable();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        string sql = @"
                SELECT ISNULL(e.InsuranceNumber, '') AS [شماره بیمه], e.FirstName AS [نام], e.LastName AS [نام خانوادگی], e.NationalCode AS [کد ملی],
                       CASE WHEN p.InsuranceBase > 0 THEN p.InsuranceBase ELSE p.TotalEarnings END AS [مبلغ مشمول بیمه],
                       p.InsuranceEmployee AS [سهم کارمند], p.InsuranceEmployer AS [سهم کارفرما],
                       (p.InsuranceEmployee + p.InsuranceEmployer) AS [جمع بیمه]
                FROM Payrolls p
                INNER JOIN Employees e ON p.EmployeeId = e.Id
                WHERE p.Year = @Year AND p.Month = @Month AND e.HasInsurance = 1
                ORDER BY e.LastName, e.FirstName";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Year", year);
        cmd.Parameters.AddWithValue("@Month", month);
        using var adapter = new SqlDataAdapter(cmd);
        await Task.Run(() => adapter.Fill(dt));
        return dt;
    }

    public async Task<DataTable> GetOfficialTaxReportAsync(int year, int month)
    {
        await EnsureLogTableAsync(); // اطمینان از وجود ستون‌های جدید
        var dt = new DataTable();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        string sql = @"
                SELECT e.NationalCode AS [کد ملی], e.FirstName AS [نام], e.LastName AS [نام خانوادگی],
                       p.TotalEarnings AS [جمع حقوق و مزایا],
                       p.InsuranceEmployee AS [بیمه سهم کارمند],
                       CASE WHEN p.TaxableBase > 0 THEN p.TaxableBase
                            ELSE CASE WHEN p.TotalEarnings - p.InsuranceEmployee > 0 THEN p.TotalEarnings - p.InsuranceEmployee ELSE 0 END END AS [درآمد مشمول مالیات],
                       p.IncomeTax AS [مالیات محاسبه شده]
                FROM Payrolls p
                INNER JOIN Employees e ON p.EmployeeId = e.Id
                WHERE p.Year = @Year AND p.Month = @Month
                ORDER BY e.LastName, e.FirstName";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Year", year);
        cmd.Parameters.AddWithValue("@Month", month);
        using var adapter = new SqlDataAdapter(cmd);
        await Task.Run(() => adapter.Fill(dt));
        return dt;
    }

    // ==========================================================
    // حذف (با برگشت اقساط)
    // ==========================================================

    public async Task DeletePayrollsForMonthAsync(int year, int month)
    {
        await EnsureLogTableAsync();

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        try
        {
            // ✅ FIX: قبل از حذف، اقساط کسرشده‌ی این ماه به وام/بیمه‌ها برمی‌گردد
            await RestoreLoggedInstallmentsAsync(conn, transaction, year, month, null);

            using var cmd = new SqlCommand("DELETE FROM Payrolls WHERE Year = @Year AND Month = @Month", conn, transaction);
            cmd.Parameters.AddWithValue("@Year", year);
            cmd.Parameters.AddWithValue("@Month", month);
            await cmd.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        Smp.Helpers.AuditHelper.Log("حذف", "پردازش حقوق", $"کل حقوق سال {year} ماه {month}");
    }

    public async Task DeletePayrollAsync(int id)
    {
        await EnsureLogTableAsync();

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        try
        {
            int empId = 0, year = 0, month = 0;
            bool found = false;

            using (var sel = new SqlCommand("SELECT EmployeeId, Year, Month FROM Payrolls WHERE Id = @Id", conn, transaction))
            {
                sel.Parameters.AddWithValue("@Id", id);
                using var reader = await sel.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    empId = reader.GetInt32(0);
                    year = reader.GetInt32(1);
                    month = reader.GetInt32(2);
                    found = true;
                }
            }

            if (found)
                await RestoreLoggedInstallmentsAsync(conn, transaction, year, month, empId);

            using (var cmd = new SqlCommand("DELETE FROM Payrolls WHERE Id = @Id", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        Smp.Helpers.AuditHelper.Log("حذف", "پردازش حقوق", $"فیش حقوقی شناسه {id}");
    }

    // ✅ متد جدید: حذف فیش حقوقی یک کارمند خاص در یک ماه خاص
    public async Task DeletePayrollForEmployeeAsync(int employeeId, int year, int month)
    {
        await EnsureLogTableAsync();

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        try
        {
            // ۱. برگرداندن اقساط کسرشده (وام، بیمه تکمیلی و...) برای این کارمند در این ماه
            await RestoreLoggedInstallmentsAsync(conn, transaction, year, month, employeeId);

            // ۲. حذف رکورد حقوق از جدول Payrolls
            using (var cmd = new SqlCommand("DELETE FROM Payrolls WHERE EmployeeId = @EmpId AND Year = @Year AND Month = @Month", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@EmpId", employeeId);
                cmd.Parameters.AddWithValue("@Year", year);
                cmd.Parameters.AddWithValue("@Month", month);
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        Smp.Helpers.AuditHelper.Log("حذف", "پردازش حقوق", $"پرسنل {employeeId}، سال {year} ماه {month}");
    }
}
