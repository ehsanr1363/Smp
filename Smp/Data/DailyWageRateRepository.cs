using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;

namespace Smp.Data;

public class DailyWageRateItem
{
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
}

/// <summary>نرخ روزانه‌ی طبقه‌های کارگری و تنظیمات سالانه‌ی روزمزدها (سال شمسی).</summary>
public class DailyWageRateRepository
{
    private readonly string _connectionString;
    public DailyWageRateRepository(string connectionString) { _connectionString = connectionString; }

    public List<DailyWageRateItem> GetRates(int year)
    {
        var list = new List<DailyWageRateItem>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT Category, Amount FROM DailyWageRates WHERE Year = @Year ORDER BY Category", conn);
        cmd.Parameters.AddWithValue("@Year", year);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new DailyWageRateItem { Category = r.GetString(0), Amount = r.GetDecimal(1) });
        return list;
    }

    /// <summary>
    /// نرخ طبقه‌ها برای نمایش در فرم پرسنل: نرخ همان سال؛ اگر هنوز چیزی ثبت نشده، نرخ آخرین سالِ ثبت‌شده.
    /// UsedYear سالی است که نرخ‌ها واقعاً از آن خوانده شده‌اند.
    /// </summary>
    public (List<DailyWageRateItem> Rates, int UsedYear) GetRatesWithFallback(int year)
    {
        var list = GetRates(year);
        if (list.Count > 0) return (list, year);

        int? latest = null;
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            using var cmd = new SqlCommand("SELECT MAX(Year) FROM DailyWageRates", conn);
            var v = cmd.ExecuteScalar();
            if (v is not null && v != DBNull.Value) latest = Convert.ToInt32(v);
        }
        if (latest.HasValue) return (GetRates(latest.Value), latest.Value);
        return (list, year);
    }

    /// <summary>
    /// نام طبقه‌های کارگری برای انتخاب در فرم پرسنل: طبقه‌های همان سال؛
    /// اگر برای آن سال هنوز چیزی تعریف نشده، طبقه‌های آخرین سالِ ثبت‌شده.
    /// </summary>
    public List<string> GetCategoryNames(int year)
    {
        var list = new List<string>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand(@"
                DECLARE @Y INT = CASE WHEN EXISTS (SELECT 1 FROM DailyWageRates WHERE Year = @Year)
                                      THEN @Year ELSE (SELECT MAX(Year) FROM DailyWageRates) END;
                SELECT DISTINCT Category FROM DailyWageRates WHERE Year = @Y ORDER BY Category", conn);
        cmd.Parameters.AddWithValue("@Year", year);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            if (!r.IsDBNull(0)) list.Add(r.GetString(0));
        return list;
    }

    /// <summary>ضریب اضافه‌کار روزمزد در آن سال؛ اگر ثبت نشده باشد null.</summary>
    public decimal? GetOvertimeMultiplier(int year)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT OvertimeMultiplier FROM DailyWageYearSettings WHERE Year = @Year", conn);
        cmd.Parameters.AddWithValue("@Year", year);
        var v = cmd.ExecuteScalar();
        return (v is null || v == DBNull.Value) ? (decimal?)null : Convert.ToDecimal(v);
    }

    /// <summary>نرخ طبقه‌ها و ضریب اضافه‌کار یک سال را یک‌جا و در یک تراکنش ذخیره می‌کند (جایگزین مقادیر قبلی همان سال).</summary>
    public void SaveYear(int year, IEnumerable<DailyWageRateItem> rates, decimal overtimeMultiplier)
    {
        if (overtimeMultiplier <= 0) throw new InvalidOperationException("ضریب اضافه‌کار باید بیشتر از صفر باشد.");

        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            using (var del = new SqlCommand("DELETE FROM DailyWageRates WHERE Year = @Year", conn, tx))
            {
                del.Parameters.AddWithValue("@Year", year);
                del.ExecuteNonQuery();
            }

            var seen = new HashSet<string>();
            foreach (var item in rates)
            {
                string cat = (item.Category ?? "").Trim();
                if (cat.Length == 0) continue;
                if (!seen.Add(cat)) throw new InvalidOperationException($"طبقه‌ی «{cat}» تکراری است.");
                if (item.Amount <= 0) throw new InvalidOperationException($"دستمزد طبقه‌ی «{cat}» باید بیشتر از صفر باشد.");

                using var ins = new SqlCommand("INSERT INTO DailyWageRates (Year, Category, Amount) VALUES (@Year, @Cat, @Amt)", conn, tx);
                ins.Parameters.AddWithValue("@Year", year);
                ins.Parameters.AddWithValue("@Cat", cat);
                ins.Parameters.AddWithValue("@Amt", item.Amount);
                ins.ExecuteNonQuery();
            }

            using (var up = new SqlCommand(@"
                    IF EXISTS (SELECT 1 FROM DailyWageYearSettings WHERE Year = @Year)
                        UPDATE DailyWageYearSettings SET OvertimeMultiplier = @Mult WHERE Year = @Year
                    ELSE
                        INSERT INTO DailyWageYearSettings (Year, OvertimeMultiplier) VALUES (@Year, @Mult)", conn, tx))
            {
                up.Parameters.AddWithValue("@Year", year);
                up.Parameters.AddWithValue("@Mult", overtimeMultiplier);
                up.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
