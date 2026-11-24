using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Smp.Helpers;

namespace Smp.Data;

/// <summary>اطلاعات یک شخص (طرف قرارداد) برای ثبت/ویرایش. Id=0 یعنی شخص جدید.</summary>
public class PartyRecord
{
    public int Id { get; set; }
    public string PartyType { get; set; } = "";
    public bool IsForeign { get; set; }
    public bool IsContractor { get; set; }
    public bool IsClient { get; set; }
    public string? NationalCode { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? CompanyName { get; set; }
    public string? NationalId { get; set; }
    public string? EconomicCode { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}

/// <summary>دسترسی به داده‌ی اشخاص (طرفین قرارداد). فرم‌ها نباید مستقیم SQL بنویسند.</summary>
public class PartyRepository
{
    private readonly string _connectionString;

    // جدول‌هایی که ارجاع به شخص دارند؛ مقدار ثابت‌اند (از ورودی کاربر نمی‌آیند)
    private static readonly (string Table, string Title)[] ReferencingTables =
    {
        ("ContractorContracts", "قرارداد پیمانکار"),
        ("ClientContracts", "قرارداد کارفرما"),
        ("ProjectExpenses", "فاکتور هزینه"),
        ("CashChecks", "چک بانکی")
    };

    public PartyRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>لیست اشخاص با ستون‌های محاسبه‌شده (DisplayName, IdentityNo, RoleText). search خالی = همه</summary>
    public async Task<DataTable> SearchAsync(string? search)
    {
        string query = @"
                SELECT Id, PartyType, IsForeign, IsContractor, IsClient, NationalCode,
                       FirstName, LastName, CompanyName, NationalId, EconomicCode, Phone, Address,
                       CASE
                           WHEN PartyType = N'حقیقی' THEN ISNULL(FirstName, N'') + N' ' + ISNULL(LastName, N'')
                           ELSE ISNULL(CompanyName, N'')
                       END AS DisplayName,
                       CASE WHEN PartyType = N'حقیقی' THEN NationalCode ELSE NationalId END AS IdentityNo,
                       CASE
                           WHEN IsContractor = 1 AND IsClient = 1 THEN N'پیمانکار و کارفرما'
                           WHEN IsContractor = 1 THEN N'پیمانکار'
                           WHEN IsClient = 1 THEN N'کارفرما'
                           ELSE N''
                       END AS RoleText
                FROM Parties
                WHERE 1=1";

        bool hasSearch = !string.IsNullOrWhiteSpace(search);
        if (hasSearch)
            query += " AND (FirstName LIKE @s OR LastName LIKE @s OR CompanyName LIKE @s OR NationalCode LIKE @s OR NationalId LIKE @s)";
        query += " ORDER BY Id DESC";

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(query, conn);
        if (hasSearch) cmd.Parameters.AddWithValue("@s", $"%{PersianText.NormalizeText(PersianText.ToLatinDigits(search))}%");

        using var reader = await cmd.ExecuteReaderAsync();
        var dt = new DataTable();
        dt.Load(reader);
        return dt;
    }

    /// <summary>
    /// شخص و حساب‌های بانکی‌اش را در یک تراکنش حذف می‌کند.
    /// اگر در قرارداد/فاکتور/چک استفاده شده باشد InvalidOperationException با فهرست موارد می‌اندازد.
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();
        try
        {
            var references = new List<string>();
            foreach (var (table, title) in ReferencingTables)
            {
                using var cmd = new SqlCommand($"SELECT COUNT(1) FROM {table} WHERE PartyId = @id", conn, tx);
                cmd.Parameters.AddWithValue("@id", id);
                if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0) references.Add(title);
            }

            if (references.Count > 0)
                throw new InvalidOperationException(
                    "این شخص در موارد زیر ارجاع داده شده و قابل حذف نیست:\n\n• " + string.Join("\n• ", references));

            using (var cmd = new SqlCommand("DELETE FROM PartyBankAccounts WHERE PartyId = @id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
            }
            using (var cmd = new SqlCommand("DELETE FROM Parties WHERE Id = @id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ───────────────────────── ثبت و ویرایش شخص ─────────────────────────

    public async Task<PartyRecord?> GetByIdAsync(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT * FROM Parties WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var rd = await cmd.ExecuteReaderAsync();
        if (!await rd.ReadAsync()) return null;

        static string S(SqlDataReader r, string col) => r[col] == DBNull.Value ? "" : Convert.ToString(r[col]) ?? "";
        return new PartyRecord
        {
            Id = id,
            PartyType = S(rd, "PartyType"),
            IsForeign = Convert.ToInt32(rd["IsForeign"]) == 1,
            IsContractor = Convert.ToInt32(rd["IsContractor"]) == 1,
            IsClient = Convert.ToInt32(rd["IsClient"]) == 1,
            NationalCode = S(rd, "NationalCode"),
            FirstName = S(rd, "FirstName"),
            LastName = S(rd, "LastName"),
            CompanyName = S(rd, "CompanyName"),
            NationalId = S(rd, "NationalId"),
            EconomicCode = S(rd, "EconomicCode"),
            Phone = S(rd, "Phone"),
            Address = S(rd, "Address")
        };
    }

    /// <summary>شخص را درج (Id=0) یا ویرایش می‌کند. کد ملی/شناسه ملی تکراری InvalidOperationException می‌اندازد.</summary>
    public async Task SaveAsync(PartyRecord p)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        if (!string.IsNullOrWhiteSpace(p.NationalCode) &&
            await ExistsAsync(conn, "NationalCode", PersianText.NormalizeIdentifier(p.NationalCode), p.Id))
            throw new InvalidOperationException("این کد ملی قبلاً ثبت شده است.");

        if (!string.IsNullOrWhiteSpace(p.NationalId) &&
            await ExistsAsync(conn, "NationalId", PersianText.NormalizeIdentifier(p.NationalId), p.Id))
            throw new InvalidOperationException("این شناسه ملی قبلاً ثبت شده است.");

        string sql = p.Id > 0
            ? @"UPDATE Parties SET PartyType=@type, IsForeign=@isForeign, IsContractor=@isContractor, IsClient=@isClient, NationalCode=@nc, FirstName=@fn, LastName=@ln, CompanyName=@cn, NationalId=@nid, EconomicCode=@ec, Phone=@phone, Address=@addr WHERE Id=@id;"
            : @"INSERT INTO Parties (PartyType, IsForeign, IsContractor, IsClient, NationalCode, FirstName, LastName, CompanyName, NationalId, EconomicCode, Phone, Address) VALUES (@type, @isForeign, @isContractor, @isClient, @nc, @fn, @ln, @cn, @nid, @ec, @phone, @addr);";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@type", p.PartyType);
        cmd.Parameters.AddWithValue("@isForeign", p.IsForeign ? 1 : 0);
        cmd.Parameters.AddWithValue("@isContractor", p.IsContractor ? 1 : 0);
        cmd.Parameters.AddWithValue("@isClient", p.IsClient ? 1 : 0);
        cmd.Parameters.AddWithValue("@nc", OrNullId(p.NationalCode));
        cmd.Parameters.AddWithValue("@fn", OrNullText(p.FirstName));
        cmd.Parameters.AddWithValue("@ln", OrNullText(p.LastName));
        cmd.Parameters.AddWithValue("@cn", OrNullText(p.CompanyName));
        cmd.Parameters.AddWithValue("@nid", OrNullId(p.NationalId));
        cmd.Parameters.AddWithValue("@ec", OrNullId(p.EconomicCode));
        cmd.Parameters.AddWithValue("@phone", OrNullId(p.Phone));
        cmd.Parameters.AddWithValue("@addr", OrNullText(p.Address));
        if (p.Id > 0) cmd.Parameters.AddWithValue("@id", p.Id);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<bool> ExistsAsync(SqlConnection conn, string column, string value, int excludeId)
    {
        // column فقط یکی از دو مقدار ثابتِ داخلی است
        using var cmd = new SqlCommand($"SELECT COUNT(1) FROM Parties WHERE {column} = @v AND Id != @id", conn);
        cmd.Parameters.AddWithValue("@v", value);
        cmd.Parameters.AddWithValue("@id", excludeId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    /// <summary>متن فارسی (نام، آدرس): ي/ك عربی → ی/ک، حذف اعراب و Trim.</summary>
    private static object OrNullText(string? s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : PersianText.NormalizeText(s);

    /// <summary>شناسه‌ها و شماره‌ها (کد ملی، شناسه ملی، کد اقتصادی، تلفن): ارقام لاتین و بدون فاصله/خط‌تیره.</summary>
    private static object OrNullId(string? s) { var v = PersianText.NormalizeIdentifier(s); return v.Length == 0 ? DBNull.Value : v; }

    // ───────────────────────── حساب‌های بانکی (شبا) ─────────────────────────

    public async Task<DataTable> GetBankAccountsAsync(int partyId)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(
            "SELECT Id, ShebaNumber, BankName, IsMainAccount FROM PartyBankAccounts WHERE PartyId = @pid ORDER BY IsMainAccount DESC, Id DESC;", conn);
        cmd.Parameters.AddWithValue("@pid", partyId);
        using var rd = await cmd.ExecuteReaderAsync();
        var dt = new DataTable();
        dt.Load(rd);
        return dt;
    }

    /// <summary>شبا را ثبت می‌کند؛ اولین حساب شخص به‌طور خودکار «اصلی» می‌شود. شبای تکراری استثنا می‌دهد.</summary>
    public async Task AddBankAccountAsync(int partyId, string fullSheba, string bankCode, string bankName)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();
        try
        {
            using (var cmd = new SqlCommand("SELECT COUNT(1) FROM PartyBankAccounts WHERE ShebaNumber = @sheba;", conn, tx))
            {
                cmd.Parameters.AddWithValue("@sheba", fullSheba);
                if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0)
                    throw new InvalidOperationException("این شماره شبا قبلاً در سیستم ثبت شده است.");
            }

            int count;
            using (var cmd = new SqlCommand("SELECT COUNT(1) FROM PartyBankAccounts WHERE PartyId = @pid;", conn, tx))
            {
                cmd.Parameters.AddWithValue("@pid", partyId);
                count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            using (var cmd = new SqlCommand(@"
                    INSERT INTO PartyBankAccounts (PartyId, ShebaNumber, BankCode, BankName, IsMainAccount)
                    VALUES (@pid, @sheba, @bcode, @bname, @isMain);", conn, tx))
            {
                cmd.Parameters.AddWithValue("@pid", partyId);
                cmd.Parameters.AddWithValue("@sheba", fullSheba);
                cmd.Parameters.AddWithValue("@bcode", bankCode);
                cmd.Parameters.AddWithValue("@bname", bankName);
                cmd.Parameters.AddWithValue("@isMain", count == 0 ? 1 : 0);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task SetMainBankAccountAsync(int partyId, int accountId)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();
        try
        {
            using (var cmd = new SqlCommand("UPDATE PartyBankAccounts SET IsMainAccount = 0 WHERE PartyId = @pid;", conn, tx))
            {
                cmd.Parameters.AddWithValue("@pid", partyId);
                await cmd.ExecuteNonQueryAsync();
            }
            using (var cmd = new SqlCommand("UPDATE PartyBankAccounts SET IsMainAccount = 1 WHERE Id = @id;", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", accountId);
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteBankAccountAsync(int accountId)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand("DELETE FROM PartyBankAccounts WHERE Id = @id;", conn);
        cmd.Parameters.AddWithValue("@id", accountId);
        await cmd.ExecuteNonQueryAsync();
    }
}
