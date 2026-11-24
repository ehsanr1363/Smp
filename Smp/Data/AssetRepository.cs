using Microsoft.Data.SqlClient;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Smp.Data;

/// <summary>دسترسی به داده‌های اموال، اتصال به اسناد خرید و تاریخچه‌ی جابه‌جایی</summary>
public class AssetRepository
{
    private readonly string _connectionString;
    public AssetRepository(string connectionString) => _connectionString = connectionString;

    private static object Db(object? v) => v ?? DBNull.Value;
    private static object DbStr(string? s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();

    // ───────────── لیست ─────────────
    public async Task<List<Asset>> GetAllAsync(string? search = null, string? status = null)
    {
        const string sql = @"
SELECT a.Id, a.AssetCode, a.Name, a.Category, a.SerialNumber, a.PurchaseDate, a.PurchaseAmount, a.Status,
       a.DepartmentId, d.Name AS DepartmentName,
       a.EmployeeId, LTRIM(RTRIM(ISNULL(e.FirstName,'') + ' ' + ISNULL(e.LastName,''))) AS EmployeeName,
       a.Location, a.Description, a.CreatedAt,
       (SELECT COUNT(*) FROM AssetPurchaseLinks l WHERE l.AssetId = a.Id) AS DocCount
FROM Assets a
LEFT JOIN Departments d ON d.Id = a.DepartmentId
LEFT JOIN Employees e ON e.Id = a.EmployeeId
WHERE (@status IS NULL OR a.Status = @status)
  AND (@q IS NULL OR a.AssetCode LIKE @q OR a.Name LIKE @q OR a.Category LIKE @q OR a.SerialNumber LIKE @q
       OR a.Location LIKE @q OR d.Name LIKE @q
       OR (ISNULL(e.FirstName,'') + ' ' + ISNULL(e.LastName,'')) LIKE @q)
ORDER BY a.Id DESC";

        var list = new List<Asset>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(status) ? DBNull.Value : status);
        cmd.Parameters.AddWithValue("@q", string.IsNullOrWhiteSpace(search) ? DBNull.Value : "%" + search.Trim() + "%");
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(MapAsset(r));
        return list;
    }

    public async Task<Asset?> GetByIdAsync(int id)
    {
        const string sql = @"
SELECT a.Id, a.AssetCode, a.Name, a.Category, a.SerialNumber, a.PurchaseDate, a.PurchaseAmount, a.Status,
       a.DepartmentId, d.Name AS DepartmentName,
       a.EmployeeId, LTRIM(RTRIM(ISNULL(e.FirstName,'') + ' ' + ISNULL(e.LastName,''))) AS EmployeeName,
       a.Location, a.Description, a.CreatedAt,
       (SELECT COUNT(*) FROM AssetPurchaseLinks l WHERE l.AssetId = a.Id) AS DocCount
FROM Assets a
LEFT JOIN Departments d ON d.Id = a.DepartmentId
LEFT JOIN Employees e ON e.Id = a.EmployeeId
WHERE a.Id = @id";
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? MapAsset(r) : null;
    }

    private static Asset MapAsset(SqlDataReader r) => new()
    {
        Id = r.GetInt32(r.GetOrdinal("Id")),
        AssetCode = r["AssetCode"]?.ToString() ?? "",
        Name = r["Name"]?.ToString() ?? "",
        Category = r["Category"] as string,
        SerialNumber = r["SerialNumber"] as string,
        PurchaseDate = r["PurchaseDate"] as DateTime?,
        PurchaseAmount = r["PurchaseAmount"] is decimal m ? m : 0m,
        Status = r["Status"]?.ToString() ?? AssetStatuses.Active,
        DepartmentId = r["DepartmentId"] as int?,
        DepartmentName = r["DepartmentName"] as string,
        EmployeeId = r["EmployeeId"] as int?,
        EmployeeName = string.IsNullOrWhiteSpace(r["EmployeeName"] as string) ? null : r["EmployeeName"] as string,
        Location = r["Location"] as string,
        Description = r["Description"] as string,
        CreatedAt = r["CreatedAt"] is DateTime c ? c : DateTime.MinValue,
        DocCount = r["DocCount"] is int n ? n : 0
    };

    // ───────────── داده‌های کمکی فرم ─────────────
    public async Task<List<IdName>> GetDepartmentsAsync()
        => await QueryIdNameAsync("SELECT Id, Name FROM Departments ORDER BY Name");

    public async Task<List<IdName>> GetEmployeesAsync()
        => await QueryIdNameAsync(@"SELECT Id, LTRIM(RTRIM(ISNULL(FirstName,'') + ' ' + ISNULL(LastName,''))) AS Name
                                        FROM Employees WHERE ISNULL(IsActive, 1) = 1 ORDER BY LastName, FirstName");

    public async Task<List<string>> GetCategoriesAsync()
    {
        var list = new List<string>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        // دسته‌بندی‌ها از «مدیریت Lookups» (کلید AssetCategory) می‌آیند
        using var cmd = new SqlCommand("SELECT Value FROM LookupItems WHERE Category = N'AssetCategory' ORDER BY Value", conn);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetString(0));
        return list;
    }

    private async Task<List<IdName>> QueryIdNameAsync(string sql)
    {
        var list = new List<IdName>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(sql, conn);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new IdName { Id = r.GetInt32(0), Name = r.IsDBNull(1) ? "" : r.GetString(1) });
        return list;
    }

    public async Task<List<string>> GetAllCodesAsync()
    {
        var list = new List<string>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT AssetCode FROM Assets", conn);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetString(0));
        return list;
    }

    /// <summary>ثبت گروهی اموال در یک تراکنش (همه یا هیچ). کد خالی = تولید خودکار. تعداد ثبت‌شده را برمی‌گرداند.</summary>
    public async Task<int> ImportAsync(IEnumerable<Asset> assets)
    {
        var list = assets.ToList();
        if (list.Count == 0) return 0;
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
        try
        {
            foreach (var a in list)
            {
                string code = a.AssetCode?.Trim() ?? "";
                if (code.Length == 0) code = await NextCodeAsync(conn, tx);
                using var ins = new SqlCommand(@"
INSERT INTO Assets (AssetCode, Name, Category, SerialNumber, PurchaseDate, PurchaseAmount, Status, DepartmentId, EmployeeId, Location, Description)
VALUES (@code, @name, @cat, @serial, @pdate, @amount, @status, @dept, @emp, @loc, @desc)", conn, tx);
                FillParams(ins, a, code);
                await ins.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
            return list.Count;
        }
        catch
        {
            try { await tx.RollbackAsync(); } catch { /* ignore */ }
            throw;
        }
    }

    public async Task<bool> CodeExistsAsync(string code, int excludeId)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT COUNT(*) FROM Assets WHERE AssetCode = @c AND Id <> @id", conn);
        cmd.Parameters.AddWithValue("@c", code);
        cmd.Parameters.AddWithValue("@id", excludeId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    // ───────────── اسناد خرید متصل ─────────────
    public async Task<List<AssetPurchaseLink>> GetLinksAsync(int assetId)
    {
        var list = new List<AssetPurchaseLink>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT Id, AssetId, DocType, DocId, DocDisplay, Amount FROM AssetPurchaseLinks WHERE AssetId = @a ORDER BY Id", conn);
        cmd.Parameters.AddWithValue("@a", assetId);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new AssetPurchaseLink
            {
                Id = r.GetInt32(0),
                AssetId = r.GetInt32(1),
                DocType = r.GetString(2),
                DocId = r.GetInt32(3),
                DocDisplay = r.IsDBNull(4) ? "" : r.GetString(4),
                Amount = r.IsDBNull(5) ? null : r.GetDecimal(5)
            });
        return list;
    }

    /// <summary>جستجوی اسناد پرداختی قابل اتصال به مال. تاریخ‌ها، شماره و طرف‌حساب برای نمایش آماده می‌شوند.</summary>
    public async Task<List<PurchaseDocItem>> SearchPurchaseDocsAsync(string docType, string? search)
    {
        string sql = docType switch
        {
            AssetDocTypes.Check => @"
SELECT TOP 300 c.Id AS DocId, c.DueDate AS [Date], c.Amount, c.CheckNumber AS DocNumber,
       COALESCE(NULLIF(c.RecipientName, ''), NULLIF(LTRIM(RTRIM(ISNULL(p.FirstName,'') + ' ' + ISNULL(p.LastName,''))), ''), p.CompanyName, '') AS Party,
       ISNULL(c.Description, '') AS [Description]
FROM CashChecks c
LEFT JOIN Parties p ON p.Id = c.PartyId
WHERE c.CheckType = N'پرداختی'
  AND (@q IS NULL OR c.CheckNumber LIKE @q OR c.RecipientName LIKE @q OR c.Description LIKE @q
       OR p.FirstName LIKE @q OR p.LastName LIKE @q OR p.CompanyName LIKE @q)
ORDER BY c.DueDate DESC, c.Id DESC",

            AssetDocTypes.CashTransaction => @"
SELECT TOP 300 t.Id AS DocId, t.TransactionDate AS [Date], t.Amount, CAST(t.Id AS NVARCHAR(20)) AS DocNumber,
       ISNULL(t.RecipientName, '') AS Party, ISNULL(t.Description, '') AS [Description]
FROM CashTransactions t
WHERE t.Direction = N'پرداختی'
  AND (@q IS NULL OR t.RecipientName LIKE @q OR t.Description LIKE @q OR CAST(t.Id AS NVARCHAR(20)) LIKE @q)
ORDER BY t.TransactionDate DESC, t.Id DESC",

            AssetDocTypes.ProjectExpense => @"
SELECT TOP 300 e.Id AS DocId, e.InvoiceDate AS [Date], e.Amount, ISNULL(e.InvoiceNumber, '') AS DocNumber,
       COALESCE(NULLIF(LTRIM(RTRIM(ISNULL(p.FirstName,'') + ' ' + ISNULL(p.LastName,''))), ''), p.CompanyName, '') AS Party,
       ISNULL(e.Description, '') AS [Description]
FROM ProjectExpenses e
LEFT JOIN Parties p ON p.Id = e.PartyId
WHERE (@q IS NULL OR e.InvoiceNumber LIKE @q OR e.Description LIKE @q
       OR p.FirstName LIKE @q OR p.LastName LIKE @q OR p.CompanyName LIKE @q)
ORDER BY e.InvoiceDate DESC, e.Id DESC",

            _ => throw new ArgumentException("نوع سند نامعتبر است: " + docType)
        };

        var list = new List<PurchaseDocItem>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@q", string.IsNullOrWhiteSpace(search) ? DBNull.Value : "%" + search.Trim() + "%");
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new PurchaseDocItem
            {
                DocType = docType,
                DocId = r.GetInt32(r.GetOrdinal("DocId")),
                Date = r["Date"] as DateTime?,
                Amount = r["Amount"] is decimal m ? m : 0m,
                DocNumber = r["DocNumber"]?.ToString() ?? "",
                Party = r["Party"]?.ToString() ?? "",
                Description = r["Description"]?.ToString() ?? ""
            });
        return list;
    }

    // ───────────── ذخیره ─────────────
    /// <summary>ثبت/ویرایش مال و جایگزینی کامل لینک‌های اسناد خرید (در یک تراکنش). کد خالی = تولید خودکار.</summary>
    public async Task<int> SaveAsync(Asset a, List<AssetPurchaseLink> links)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
        try
        {
            string code = a.AssetCode?.Trim() ?? "";
            if (code.Length == 0) code = await NextCodeAsync(conn, tx);

            int id = a.Id;
            if (id == 0)
            {
                using var ins = new SqlCommand(@"
INSERT INTO Assets (AssetCode, Name, Category, SerialNumber, PurchaseDate, PurchaseAmount, Status, DepartmentId, EmployeeId, Location, Description)
OUTPUT INSERTED.Id
VALUES (@code, @name, @cat, @serial, @pdate, @amount, @status, @dept, @emp, @loc, @desc)", conn, tx);
                FillParams(ins, a, code);
                id = Convert.ToInt32(await ins.ExecuteScalarAsync());
            }
            else
            {
                // برای ثبت تاریخچه‌ی جابه‌جایی، وضعیت قبلی خوانده می‌شود
                var before = await GetHolderAsync(conn, tx, id);

                using var upd = new SqlCommand(@"
UPDATE Assets SET AssetCode=@code, Name=@name, Category=@cat, SerialNumber=@serial, PurchaseDate=@pdate,
       PurchaseAmount=@amount, Status=@status, DepartmentId=@dept, EmployeeId=@emp, Location=@loc, Description=@desc
WHERE Id=@id", conn, tx);
                FillParams(upd, a, code);
                upd.Parameters.AddWithValue("@id", id);
                if (await upd.ExecuteNonQueryAsync() == 0) throw new InvalidOperationException("مال مورد نظر یافت نشد (ممکن است حذف شده باشد).");

                if (before is not null && (before.Value.dept != a.DepartmentId || before.Value.emp != a.EmployeeId
                    || !string.Equals(before.Value.loc ?? "", a.Location?.Trim() ?? "", StringComparison.Ordinal)))
                {
                    using var tr = new SqlCommand(@"
INSERT INTO AssetTransfers (AssetId, TransferDate, FromDepartment, FromEmployee, FromLocation, ToDepartment, ToEmployee, ToLocation)
VALUES (@id, GETDATE(), @fd, @fe, @fl, @td, @te, @tl)", conn, tx);
                    tr.Parameters.AddWithValue("@id", id);
                    tr.Parameters.AddWithValue("@fd", DbStr(before.Value.deptName));
                    tr.Parameters.AddWithValue("@fe", DbStr(before.Value.empName));
                    tr.Parameters.AddWithValue("@fl", DbStr(before.Value.loc));
                    tr.Parameters.AddWithValue("@td", DbStr(a.DepartmentName));
                    tr.Parameters.AddWithValue("@te", DbStr(a.EmployeeName));
                    tr.Parameters.AddWithValue("@tl", DbStr(a.Location));
                    await tr.ExecuteNonQueryAsync();
                }
            }

            using (var del = new SqlCommand("DELETE FROM AssetPurchaseLinks WHERE AssetId = @id", conn, tx))
            {
                del.Parameters.AddWithValue("@id", id);
                await del.ExecuteNonQueryAsync();
            }
            foreach (var l in links.GroupBy(x => (x.DocType, x.DocId)).Select(g => g.First()))
            {
                using var il = new SqlCommand(@"
INSERT INTO AssetPurchaseLinks (AssetId, DocType, DocId, DocDisplay, Amount) VALUES (@a, @t, @d, @disp, @amt)", conn, tx);
                il.Parameters.AddWithValue("@a", id);
                il.Parameters.AddWithValue("@t", l.DocType);
                il.Parameters.AddWithValue("@d", l.DocId);
                il.Parameters.AddWithValue("@disp", DbStr(l.DocDisplay));
                il.Parameters.AddWithValue("@amt", Db(l.Amount));
                await il.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return id;
        }
        catch
        {
            try { await tx.RollbackAsync(); } catch { /* ignore */ }
            throw;
        }
    }

    private static void FillParams(SqlCommand cmd, Asset a, string code)
    {
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@name", a.Name.Trim());
        cmd.Parameters.AddWithValue("@cat", DbStr(a.Category));
        cmd.Parameters.AddWithValue("@serial", DbStr(a.SerialNumber));
        cmd.Parameters.AddWithValue("@pdate", Db(a.PurchaseDate));
        cmd.Parameters.AddWithValue("@amount", a.PurchaseAmount);
        cmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(a.Status) ? AssetStatuses.Active : a.Status);
        cmd.Parameters.AddWithValue("@dept", Db(a.DepartmentId));
        cmd.Parameters.AddWithValue("@emp", Db(a.EmployeeId));
        cmd.Parameters.AddWithValue("@loc", DbStr(a.Location));
        cmd.Parameters.AddWithValue("@desc", DbStr(a.Description));
    }

    private static async Task<(int? dept, string? deptName, int? emp, string? empName, string? loc)?> GetHolderAsync(SqlConnection conn, SqlTransaction tx, int id)
    {
        using var cmd = new SqlCommand(@"
SELECT a.DepartmentId, d.Name, a.EmployeeId, LTRIM(RTRIM(ISNULL(e.FirstName,'') + ' ' + ISNULL(e.LastName,''))), a.Location
FROM Assets a
LEFT JOIN Departments d ON d.Id = a.DepartmentId
LEFT JOIN Employees e ON e.Id = a.EmployeeId
WHERE a.Id = @id", conn, tx);
        cmd.Parameters.AddWithValue("@id", id);
        using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return (r.IsDBNull(0) ? null : r.GetInt32(0), r.IsDBNull(1) ? null : r.GetString(1),
                r.IsDBNull(2) ? null : r.GetInt32(2), r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4));
    }

    /// <summary>کد بعدی به شکل AST-0001 (با قفل برای جلوگیری از تکرار هم‌زمان)</summary>
    private static async Task<string> NextCodeAsync(SqlConnection conn, SqlTransaction tx)
    {
        using var cmd = new SqlCommand(@"
SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(AssetCode, 5, 20) AS INT)), 0)
FROM Assets WITH (UPDLOCK, HOLDLOCK)
WHERE AssetCode LIKE 'AST-%'", conn, tx);
        int max = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return $"AST-{max + 1:D4}";
    }

    // ───────────── تاریخچه و حذف ─────────────
    public async Task<List<AssetTransfer>> GetTransfersAsync(int assetId)
    {
        var list = new List<AssetTransfer>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"SELECT Id, AssetId, TransferDate, FromDepartment, FromEmployee, FromLocation,
                                                    ToDepartment, ToEmployee, ToLocation, Notes
                                             FROM AssetTransfers WHERE AssetId = @a ORDER BY TransferDate DESC, Id DESC", conn);
        cmd.Parameters.AddWithValue("@a", assetId);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new AssetTransfer
            {
                Id = r.GetInt32(0), AssetId = r.GetInt32(1), TransferDate = r.GetDateTime(2),
                FromDepartment = r[3] as string, FromEmployee = r[4] as string, FromLocation = r[5] as string,
                ToDepartment = r[6] as string, ToEmployee = r[7] as string, ToLocation = r[8] as string, Notes = r[9] as string
            });
        return list;
    }

    /// <summary>حذف مال همراه با لینک‌ها و تاریخچه و پیوست‌هایش</summary>
    public async Task DeleteAsync(IEnumerable<int> ids)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return;
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
        try
        {
            foreach (int id in idList)
            {
                foreach (var sql in new[]
                {
                    "DELETE FROM Attachments WHERE EntityType = 'Asset' AND EntityId = @id",
                    "DELETE FROM AssetTransfers WHERE AssetId = @id",
                    "DELETE FROM AssetPurchaseLinks WHERE AssetId = @id",
                    "DELETE FROM Assets WHERE Id = @id"
                })
                {
                    using var cmd = new SqlCommand(sql, conn, tx);
                    cmd.Parameters.AddWithValue("@id", id);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            await tx.CommitAsync();
        }
        catch
        {
            try { await tx.RollbackAsync(); } catch { /* ignore */ }
            throw;
        }
    }
}
