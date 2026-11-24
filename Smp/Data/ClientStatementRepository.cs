using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Smp.Models;

namespace Smp.Data;

/// <summary>جمع لیست حقوق کارگران روزمزدِ یک محل خدمت در یک ماه</summary>
public record PayrollPeriodTotals(int WorkerCount, decimal TotalEarnings, decimal EmployerInsurance, decimal NetPay)
{
    /// <summary>کارکرد = حقوق و اضافه‌کار + بیمه سهم کارفرما (بدون ارزش افزوده)</summary>
    public decimal Gross => TotalEarnings + EmployerInsurance;
}

public class ClientStatementRepository
{
    private readonly string _connectionString;

    public ClientStatementRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public List<ClientStatement> GetByContractId(int contractId)
    {
        var list = new List<ClientStatement>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        string query = @"
                SELECT Id, ContractId, StatementNumber, StatementDate, GrossAmount, VAT, 
                       RetentionDeduction, InsuranceDeduction, AdvanceRecovery, OtherDeductions, NetReceivableAmount, CreatedAt, FeeAmount
                FROM ClientStatements 
                WHERE ContractId = @cid 
                ORDER BY Id ASC";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@cid", contractId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new ClientStatement
            {
                Id = reader.GetInt32(0),
                ContractId = reader.GetInt32(1),
                StatementNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                StatementDate = reader.IsDBNull(3) ? DateTime.Now : Convert.ToDateTime(reader[3]),
                GrossAmount = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader[4]),
                VAT = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader[5]),
                RetentionDeduction = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader[6]),
                InsuranceDeduction = reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader[7]),
                AdvanceRecovery = reader.IsDBNull(8) ? 0 : Convert.ToDecimal(reader[8]),
                OtherDeductions = reader.IsDBNull(9) ? 0 : Convert.ToDecimal(reader[9]),
                NetReceivableAmount = reader.IsDBNull(10) ? 0 : Convert.ToDecimal(reader[10]),
                CreatedAt = reader.IsDBNull(11) ? DateTime.Now : Convert.ToDateTime(reader[11]),
                FeeAmount = reader.IsDBNull(12) ? 0 : Convert.ToDecimal(reader[12])
            });
        }
        return list;
    }

    public ClientStatement? GetById(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        string query = @"
                SELECT Id, ContractId, StatementNumber, StatementDate, GrossAmount, VAT, 
                       RetentionDeduction, InsuranceDeduction, AdvanceRecovery, OtherDeductions, NetReceivableAmount, CreatedAt, FeeAmount
                FROM ClientStatements 
                WHERE Id = @id";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new ClientStatement
            {
                Id = reader.GetInt32(0),
                ContractId = reader.GetInt32(1),
                StatementNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                StatementDate = reader.IsDBNull(3) ? DateTime.Now : Convert.ToDateTime(reader[3]),
                GrossAmount = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader[4]),
                VAT = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader[5]),
                RetentionDeduction = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader[6]),
                InsuranceDeduction = reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader[7]),
                AdvanceRecovery = reader.IsDBNull(8) ? 0 : Convert.ToDecimal(reader[8]),
                OtherDeductions = reader.IsDBNull(9) ? 0 : Convert.ToDecimal(reader[9]),
                NetReceivableAmount = reader.IsDBNull(10) ? 0 : Convert.ToDecimal(reader[10]),
                CreatedAt = reader.IsDBNull(11) ? DateTime.Now : Convert.ToDateTime(reader[11]),
                FeeAmount = reader.IsDBNull(12) ? 0 : Convert.ToDecimal(reader[12])
            };
        }
        return null;
    }

    /// <summary>
    /// جمع لیست حقوق (سال/ماه شمسی) فقط برای کارگران «روزمزد» که «محل خدمت»شان برابر محل خدمت قرارداد است.
    /// TotalEarnings شامل اضافه‌کار است؛ InsuranceEmployer همان بیمه سهم کارفرمای ثبت‌شده در لیست حقوق.
    /// </summary>
    public PayrollPeriodTotals GetDailyWagePayrollTotals(int persianYear, int persianMonth, string workLocation)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        // مقایسه‌ی «نوع پرسنلی» و «محل خدمت» بدون حساسیت به فاصله/نیم‌فاصله، ی/ک عربی و ارقام فارسی/لاتین.
        // اگر ستون‌های Payrolls.WorkLocation/PersonnelType خالی بودند، از مشخصات فعلی پرسنل استفاده می‌شود.
        string typeKey = SqlKey("ISNULL(NULLIF(LTRIM(RTRIM(p.PersonnelType)), N''), e.PersonnelType)");
        string locKey = SqlKey("ISNULL(NULLIF(LTRIM(RTRIM(p.WorkLocation)), N''), e.WorkLocation)");
        string sql = $@"
                SELECT COUNT(1),
                       ISNULL(SUM(p.TotalEarnings), 0),
                       ISNULL(SUM(p.InsuranceEmployer), 0),
                       ISNULL(SUM(p.NetPay), 0)
                FROM Payrolls p
                LEFT JOIN Employees e ON e.Id = p.EmployeeId
                WHERE p.Year = @y AND p.Month = @m
                  AND {typeKey} = @type
                  AND {locKey} = @loc";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@y", persianYear);
        cmd.Parameters.AddWithValue("@m", persianMonth);
        cmd.Parameters.AddWithValue("@type", Smp.Helpers.PersianText.NormalizeForSearch("روزمزد"));
        cmd.Parameters.AddWithValue("@loc", Smp.Helpers.PersianText.NormalizeForSearch(workLocation ?? ""));

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return new PayrollPeriodTotals(0, 0, 0, 0);
        return new PayrollPeriodTotals(Convert.ToInt32(r[0]), Convert.ToDecimal(r[1]), Convert.ToDecimal(r[2]), Convert.ToDecimal(r[3]));
    }

    /// <summary>
    /// برای پیام راهنما وقتی لیستی پیدا نشد: محل‌های خدمتی که در آن ماه برایشان حقوق «روزمزد» ثبت شده (به همراه تعداد).
    /// </summary>
    public List<(string Location, int Count)> GetDailyWageLocationsInPeriod(int persianYear, int persianMonth)
    {
        var list = new List<(string, int)>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        string typeKey = SqlKey("ISNULL(NULLIF(LTRIM(RTRIM(p.PersonnelType)), N''), e.PersonnelType)");
        string locKey = SqlKey("ISNULL(NULLIF(LTRIM(RTRIM(p.WorkLocation)), N''), e.WorkLocation)");
        string sql = $@"
                SELECT ISNULL(NULLIF(LTRIM(RTRIM(p.WorkLocation)), N''), LTRIM(RTRIM(e.WorkLocation))) AS Loc, COUNT(1)
                FROM Payrolls p
                LEFT JOIN Employees e ON e.Id = p.EmployeeId
                WHERE p.Year = @y AND p.Month = @m
                  AND {typeKey} = @type
                GROUP BY ISNULL(NULLIF(LTRIM(RTRIM(p.WorkLocation)), N''), LTRIM(RTRIM(e.WorkLocation)))";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@y", persianYear);
        cmd.Parameters.AddWithValue("@m", persianMonth);
        cmd.Parameters.AddWithValue("@type", Smp.Helpers.PersianText.NormalizeForSearch("روزمزد"));

        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add((r.IsDBNull(0) ? "(بدون محل خدمت)" : r.GetString(0), Convert.ToInt32(r[1])));
        return list;
    }

    /// <summary>
    /// عبارت SQL هم‌ارز PersianText.NormalizeForSearch روی یک ستون:
    /// حذف فاصله و نیم‌فاصله، تبدیل ي/ك عربی به فارسی، و ارقام فارسی/عربی به لاتین.
    /// </summary>
    private static string SqlKey(string column)
    {
        string expr = column;
        void Rep(string from, string to) => expr = $"REPLACE({expr}, {from}, {to})";

        Rep("N' '", "N''");
        Rep("NCHAR(8204)", "N''");   // نیم‌فاصله
        Rep("NCHAR(8206)", "N''");
        Rep("NCHAR(8207)", "N''");
        Rep("NCHAR(1600)", "N''");   // کشیده
        Rep("NCHAR(1610)", "NCHAR(1740)");  // ي → ی
        Rep("NCHAR(1609)", "NCHAR(1740)");  // ى → ی
        Rep("NCHAR(1603)", "NCHAR(1705)");  // ك → ک
        for (int i = 0; i < 10; i++)
        {
            Rep($"NCHAR({1776 + i})", $"N'{i}'");   // ۰-۹
            Rep($"NCHAR({1632 + i})", $"N'{i}'");   // ٠-٩
        }
        return expr;
    }

    /// <summary>شماره صورت‌وضعیت در هر قرارداد باید یکتا باشد.</summary>
    private static void EnsureUniqueNumber(SqlConnection conn, ClientStatement s)
    {
        if (string.IsNullOrWhiteSpace(s.StatementNumber)) return;

        using var cmd = new SqlCommand(
            "SELECT COUNT(1) FROM ClientStatements WHERE ContractId = @cid AND LTRIM(RTRIM(StatementNumber)) = @num AND Id <> @id", conn);
        cmd.Parameters.AddWithValue("@cid", s.ContractId);
        cmd.Parameters.AddWithValue("@num", s.StatementNumber.Trim());
        cmd.Parameters.AddWithValue("@id", s.Id);
        if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
            throw new InvalidOperationException($"شماره صورت‌وضعیت «{s.StatementNumber.Trim()}» برای این قرارداد قبلاً ثبت شده است.");
    }

    public void Add(ClientStatement s)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        EnsureUniqueNumber(conn, s);
        s.RecalculateNet();

        using var cmd = new SqlCommand(@"
                INSERT INTO ClientStatements 
                (ContractId, StatementNumber, StatementDate, GrossAmount, FeeAmount, VAT, RetentionDeduction, InsuranceDeduction, AdvanceRecovery, OtherDeductions, NetReceivableAmount, CreatedAt)
                VALUES 
                (@cid, @num, @date, @gross, @fee, @vat, @retention, @insurance, @advance, @other, @net, @created);
                SELECT SCOPE_IDENTITY();", conn);

        cmd.Parameters.AddWithValue("@cid", s.ContractId);
        cmd.Parameters.AddWithValue("@num", (s.StatementNumber ?? "").Trim());
        cmd.Parameters.AddWithValue("@date", s.StatementDate);
        cmd.Parameters.AddWithValue("@gross", s.GrossAmount);
        cmd.Parameters.AddWithValue("@fee", s.FeeAmount);
        cmd.Parameters.AddWithValue("@vat", s.VAT);
        cmd.Parameters.AddWithValue("@retention", s.RetentionDeduction);
        cmd.Parameters.AddWithValue("@insurance", s.InsuranceDeduction);
        cmd.Parameters.AddWithValue("@advance", s.AdvanceRecovery);
        cmd.Parameters.AddWithValue("@other", s.OtherDeductions);
        cmd.Parameters.AddWithValue("@net", s.NetReceivableAmount);
        cmd.Parameters.AddWithValue("@created", DateTime.Now);

        var result = cmd.ExecuteScalar();
        if (result is not null && result != DBNull.Value)
            s.Id = Convert.ToInt32(result);
        Smp.Helpers.AuditHelper.Log("ثبت", "صورت وضعیت کارفرما", $"شناسه {s.Id}، مبلغ {s.TotalAmount:N0}");
    }

    public void Update(ClientStatement s)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        EnsureUniqueNumber(conn, s);
        s.RecalculateNet();

        using var cmd = new SqlCommand(@"
                UPDATE ClientStatements SET
                    StatementNumber = @num, StatementDate = @date,
                    GrossAmount = @gross, FeeAmount = @fee, VAT = @vat, 
                    RetentionDeduction = @retention, InsuranceDeduction = @insurance, 
                    AdvanceRecovery = @advance, OtherDeductions = @other,
                    NetReceivableAmount = @net
                WHERE Id = @id", conn);

        cmd.Parameters.AddWithValue("@num", (s.StatementNumber ?? "").Trim());
        cmd.Parameters.AddWithValue("@date", s.StatementDate);
        cmd.Parameters.AddWithValue("@gross", s.GrossAmount);
        cmd.Parameters.AddWithValue("@fee", s.FeeAmount);
        cmd.Parameters.AddWithValue("@vat", s.VAT);
        cmd.Parameters.AddWithValue("@retention", s.RetentionDeduction);
        cmd.Parameters.AddWithValue("@insurance", s.InsuranceDeduction);
        cmd.Parameters.AddWithValue("@advance", s.AdvanceRecovery);
        cmd.Parameters.AddWithValue("@other", s.OtherDeductions);
        cmd.Parameters.AddWithValue("@net", s.NetReceivableAmount);
        cmd.Parameters.AddWithValue("@id", s.Id);

        cmd.ExecuteNonQuery();
        Smp.Helpers.AuditHelper.Log("ویرایش", "صورت وضعیت کارفرما", $"شناسه {s.Id}، مبلغ {s.TotalAmount:N0}");
    }

    // ✅ متد به‌روزرسانی شده: پشتیبانی از چک‌ها (checkIds)
    public void LinkExpensesToStatement(int statementId, List<int> transactionIds, List<int> projectExpenseIds, List<int> checkIds = null)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            // ۱. آزادسازی هزینه‌هایی که قبلاً به این صورت وضعیت متصل بودند
            using (var cmdUnlink1 = new SqlCommand("UPDATE CashTransactions SET ClientStatementId = NULL WHERE ClientStatementId = @stId", conn, tx))
            {
                cmdUnlink1.Parameters.AddWithValue("@stId", statementId);
                cmdUnlink1.ExecuteNonQuery();
            }

            using (var cmdUnlink2 = new SqlCommand("UPDATE ProjectExpenses SET ClientStatementId = NULL WHERE ClientStatementId = @stId", conn, tx))
            {
                cmdUnlink2.Parameters.AddWithValue("@stId", statementId);
                cmdUnlink2.ExecuteNonQuery();
            }

            using (var cmdUnlink3 = new SqlCommand("UPDATE CashChecks SET ClientStatementId = NULL WHERE ClientStatementId = @stId", conn, tx))
            {
                cmdUnlink3.Parameters.AddWithValue("@stId", statementId);
                cmdUnlink3.ExecuteNonQuery();
            }

            // ۲. اتصال هزینه‌های جدید (تراکنش‌ها)
            if (transactionIds is not null && transactionIds.Count > 0)
            {
                foreach (var id in transactionIds)
                {
                    using var cmd = new SqlCommand("UPDATE CashTransactions SET ClientStatementId = @stId WHERE Id = @id", conn, tx);
                    cmd.Parameters.AddWithValue("@stId", statementId);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }

            // ۳. اتصال هزینه‌های جدید (فاکتورها)
            if (projectExpenseIds is not null && projectExpenseIds.Count > 0)
            {
                foreach (var id in projectExpenseIds)
                {
                    using var cmd = new SqlCommand("UPDATE ProjectExpenses SET ClientStatementId = @stId WHERE Id = @id", conn, tx);
                    cmd.Parameters.AddWithValue("@stId", statementId);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }

            // ۴. اتصال هزینه‌های جدید (چک‌ها)
            if (checkIds is not null && checkIds.Count > 0)
            {
                foreach (var id in checkIds)
                {
                    using var cmd = new SqlCommand("UPDATE CashChecks SET ClientStatementId = @stId WHERE Id = @id", conn, tx);
                    cmd.Parameters.AddWithValue("@stId", statementId);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// جمع حسن انجام کار و پیش‌دریافت مستهلک‌شده در سایر صورت‌وضعیت‌های همین قرارداد
    /// (برای کنترل سقف کسورات).
    /// </summary>
    /// <summary>جمع مبلغ کارکرد (ناخالص) صورت‌وضعیت‌های یک قرارداد؛ excludeStatementId = صورت‌وضعیتِ در حال ویرایش.</summary>
    public decimal GetGrossTotal(int contractId, int? excludeStatementId = null)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        string query = "SELECT ISNULL(SUM(GrossAmount + ISNULL(FeeAmount, 0)), 0) FROM ClientStatements WHERE ContractId = @cid";
        if (excludeStatementId.HasValue) query += " AND Id <> @excludeId";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@cid", contractId);
        if (excludeStatementId.HasValue) cmd.Parameters.AddWithValue("@excludeId", excludeStatementId.Value);
        return Convert.ToDecimal(cmd.ExecuteScalar());
    }

    public (decimal TotalRetention, decimal TotalAdvance) GetDeductionsTotal(int contractId, int? excludeStatementId = null)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        string query = @"
                SELECT ISNULL(SUM(RetentionDeduction), 0), ISNULL(SUM(AdvanceRecovery), 0)
                FROM ClientStatements
                WHERE ContractId = @cid";
        if (excludeStatementId.HasValue) query += " AND Id <> @excludeId";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@cid", contractId);
        if (excludeStatementId.HasValue) cmd.Parameters.AddWithValue("@excludeId", excludeStatementId.Value);

        using var reader = cmd.ExecuteReader();
        if (reader.Read()) return (Convert.ToDecimal(reader[0]), Convert.ToDecimal(reader[1]));
        return (0m, 0m);
    }

    public void Delete(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            // آزادسازی ارتباط تراکنش‌ها
            using (var cmd1 = new SqlCommand("UPDATE CashTransactions SET ClientStatementId = NULL WHERE ClientStatementId = @id", conn, tx))
            {
                cmd1.Parameters.AddWithValue("@id", id);
                cmd1.ExecuteNonQuery();
            }

            // آزادسازی ارتباط فاکتورها
            using (var cmd2 = new SqlCommand("UPDATE ProjectExpenses SET ClientStatementId = NULL WHERE ClientStatementId = @id", conn, tx))
            {
                cmd2.Parameters.AddWithValue("@id", id);
                cmd2.ExecuteNonQuery();
            }

            // آزادسازی ارتباط چک‌ها
            using (var cmd3 = new SqlCommand("UPDATE CashChecks SET ClientStatementId = NULL WHERE ClientStatementId = @id", conn, tx))
            {
                cmd3.Parameters.AddWithValue("@id", id);
                cmd3.ExecuteNonQuery();
            }

            // حذف صورت وضعیت
            using (var cmdDel = new SqlCommand("DELETE FROM ClientStatements WHERE Id = @id", conn, tx))
            {
                cmdDel.Parameters.AddWithValue("@id", id);
                cmdDel.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
        Smp.Helpers.AuditHelper.Log("حذف", "صورت وضعیت کارفرما", $"شناسه {id}");
    }
}
