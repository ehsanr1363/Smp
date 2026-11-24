using Microsoft.Data.SqlClient;
using Smp.Helpers;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Smp.Data;

public class TankhahRepository
{
    private readonly string _connectionString;
    public TankhahRepository(string connectionString) => _connectionString = connectionString;

    // ✅ خواندن صاحبان تنخواه از لوک‌آپ
    public async Task<List<Tankhah>> GetAllTankhahsAsync()
    {
        var list = new List<Tankhah>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand(@"
                SELECT Id, Value, 1 AS IsActive, GETDATE() AS CreatedAt 
                FROM LookupItems 
                WHERE Category = N'TankhahOwner' 
                ORDER BY Value", conn);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Tankhah
            {
                Id = reader.GetInt32(0),
                OwnerName = reader.GetString(1),
                IsActive = true,
                CreatedAt = reader.GetDateTime(3)
            });
        }
        return list;
    }

    public async Task<List<TankhahList>> GetListsByTankhahIdAsync(int tankhahId)
    {
        var list = new List<TankhahList>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT Id, TankhahId, Title, ChargeDate, ChargeAmount, CurrentBalance, IsActive, CreatedAt FROM TankhahLists WHERE TankhahId = @Id ORDER BY Id DESC", conn);
        cmd.Parameters.AddWithValue("@Id", tankhahId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new TankhahList
            {
                Id = reader.GetInt32(0),
                TankhahId = reader.GetInt32(1),
                Title = reader.GetString(2),
                ChargeDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                ChargeAmount = reader.GetDecimal(4),
                CurrentBalance = reader.GetDecimal(5),
                IsActive = reader.GetBoolean(6),
                CreatedAt = reader.GetDateTime(7)
            });
        }
        return list;
    }

    // ✅ اصلاح JOIN برای خواندن نام از لوک‌آپ
    public async Task<List<TankhahList>> GetAllListsAsync()
    {
        var list = new List<TankhahList>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
                SELECT l.Id, l.TankhahId, ISNULL(t.Value, N'نامشخص') + N' - ' + l.Title AS Title, l.ChargeDate, l.ChargeAmount, l.CurrentBalance, l.IsActive, l.CreatedAt 
                FROM TankhahLists l 
                LEFT JOIN LookupItems t ON l.TankhahId = t.Id AND t.Category = N'TankhahOwner'
                ORDER BY t.Value, l.Id DESC", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new TankhahList
            {
                Id = reader.GetInt32(0),
                TankhahId = reader.GetInt32(1),
                Title = reader.GetString(2),
                ChargeDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                ChargeAmount = reader.GetDecimal(4),
                CurrentBalance = reader.GetDecimal(5),
                IsActive = reader.GetBoolean(6),
                CreatedAt = reader.GetDateTime(7)
            });
        }
        return list;
    }

    public async Task AddListAsync(TankhahList list)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
                INSERT INTO TankhahLists (TankhahId, Title, ChargeDate, ChargeAmount, CurrentBalance, IsActive, CreatedAt) 
                VALUES (@TankhahId, @Title, @ChargeDate, @ChargeAmount, @ChargeAmount, 1, GETDATE());
                SELECT SCOPE_IDENTITY();", conn);

        cmd.Parameters.AddWithValue("@TankhahId", list.TankhahId);
        cmd.Parameters.AddWithValue("@Title", list.Title);
        cmd.Parameters.AddWithValue("@ChargeDate", (object?)list.ChargeDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ChargeAmount", list.ChargeAmount);

        list.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task UpdateListAsync(TankhahList list)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
                UPDATE TankhahLists 
                SET Title = @Title, ChargeDate = @ChargeDate, ChargeAmount = @ChargeAmount,
                    CurrentBalance = CurrentBalance + (@ChargeAmount - ChargeAmount)
                WHERE Id = @Id", conn);

        cmd.Parameters.AddWithValue("@Title", list.Title);
        cmd.Parameters.AddWithValue("@ChargeDate", (object?)list.ChargeDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ChargeAmount", list.ChargeAmount);
        cmd.Parameters.AddWithValue("@Id", list.Id);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteListAsync(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var checkCmd = new SqlCommand("SELECT COUNT(1) FROM CashTransactions WHERE TankhahListId = @Id", conn);
        checkCmd.Parameters.AddWithValue("@Id", id);
        if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0)
            throw new InvalidOperationException("این لیست دارای هزینه ثبت‌شده است و قابل حذف نیست.");

        using var cmd = new SqlCommand("DELETE FROM TankhahLists WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
        Smp.Helpers.AuditHelper.Log("حذف", "تنخواه", $"لیست تنخواه شناسه {id}");
    }

    // ✅ ثبت تنخواه‌دار جدید در لوک‌آپ
    public async Task AddTankhahAsync(Tankhah tankhah)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand("INSERT INTO LookupItems (Category, Value) VALUES (N'TankhahOwner', @Name); SELECT SCOPE_IDENTITY();", conn);
        cmd.Parameters.AddWithValue("@Name", tankhah.OwnerName);
        tankhah.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task ChargeTankhahAsync(int bankAccountId, int tankhahListId, decimal amount, DateTime date, string description)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            decimal bankBalance = 0;
            using (var cmd = new SqlCommand("SELECT CurrentBalance FROM CompanyBankAccounts WHERE Id = @Id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", bankAccountId);
                var res = await cmd.ExecuteScalarAsync();
                if (res is not null && res != DBNull.Value) bankBalance = Convert.ToDecimal(res);
            }

            if (bankBalance < amount)
                throw new InvalidOperationException("موجودی حساب بانکی برای این شارژ کافی نیست.");

            using (var cmd = new SqlCommand("UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance - @Amount WHERE Id = @Id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Amount", amount);
                cmd.Parameters.AddWithValue("@Id", bankAccountId);
                await cmd.ExecuteNonQueryAsync();
            }

            using (var cmd = new SqlCommand("UPDATE TankhahLists SET CurrentBalance = CurrentBalance + @Amount, ChargeAmount = ChargeAmount + @Amount, ChargeDate = @Date WHERE Id = @Id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Amount", amount);
                cmd.Parameters.AddWithValue("@Date", date);
                cmd.Parameters.AddWithValue("@Id", tankhahListId);
                await cmd.ExecuteNonQueryAsync();
            }

            string title = "";
            using (var cmd = new SqlCommand("SELECT Title FROM TankhahLists WHERE Id = @Id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", tankhahListId);
                title = (await cmd.ExecuteScalarAsync())?.ToString() ?? "تنخواه";
            }

            using (var cmd = new SqlCommand(@"
                    INSERT INTO CashTransactions 
                    (AccountId, TransactionDate, Amount, Direction, TransactionType, Category, Description, TankhahListId, PaymentScope) 
                    VALUES (@AccountId, @Date, @Amount, N'پرداختی', N'حواله', N'انتقال بین حساب‌ها', @Desc, @ListId, N'Administrative')", conn, tx))
            {
                cmd.Parameters.AddWithValue("@AccountId", bankAccountId);
                cmd.Parameters.AddWithValue("@Date", date);
                cmd.Parameters.AddWithValue("@Amount", amount);
                cmd.Parameters.AddWithValue("@Desc", $"شارژ تنخواه {title} - {description}");
                cmd.Parameters.AddWithValue("@ListId", tankhahListId);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
        Smp.Helpers.AuditHelper.Log("شارژ", "تنخواه", $"لیست تنخواه {tankhahListId}، مبلغ {amount:N0}");
    }

    // ═══════════════════════════════════════════════════════════
    //  تسویه تنخواه به بانک (تنخواه‌دار پول را به حساب بانکی شرکت برمی‌گرداند)
    //  سند مجزاست و به هیچ لیست تنخواهی وصل نمی‌شود (TankhahListId = NULL)؛
    //  موجودی/شارژ لیست‌ها تغییر نمی‌کند. فقط موجودی بانک زیاد می‌شود و
    //  در گزارش عملکرد از مانده‌ی کلِ تنخواه‌دار کم می‌شود (تنخواه‌دار در RecipientName ثبت می‌شود).
    // ═══════════════════════════════════════════════════════════
    public const string SettlementCategory = "تسویه تنخواه";

    public async Task SettleToBankAsync(int tankhahOwnerId, int bankAccountId, decimal amount, DateTime date, string description)
    {
        if (amount <= 0) throw new InvalidOperationException("مبلغ باید بزرگتر از صفر باشد.");

        string? ownerName; string? bankTitle;
        using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            using (var cmd = new SqlCommand("SELECT Value FROM LookupItems WHERE Id = @Id AND Category = N'TankhahOwner'", conn))
            {
                cmd.Parameters.AddWithValue("@Id", tankhahOwnerId);
                ownerName = (await cmd.ExecuteScalarAsync())?.ToString();
            }
            using (var cmd = new SqlCommand("SELECT Title FROM CompanyBankAccounts WHERE Id = @Id", conn))
            {
                cmd.Parameters.AddWithValue("@Id", bankAccountId);
                bankTitle = (await cmd.ExecuteScalarAsync())?.ToString();
            }
        }
        if (string.IsNullOrWhiteSpace(ownerName)) throw new InvalidOperationException("تنخواه‌دار یافت نشد.");
        if (bankTitle is null) throw new InvalidOperationException("حساب بانکی یافت نشد.");

        string d = string.IsNullOrWhiteSpace(description) ? "" : " - " + description.Trim();
        var t = new CashTransaction
        {
            AccountId = bankAccountId,
            AccountTitle = bankTitle,
            TransactionDate = date,
            Amount = amount,
            Direction = DirectionTypes.Incoming,
            TransactionType = "حواله",
            PaymentScope = PaymentScopes.Administrative,
            Category = SettlementCategory,
            RecipientName = ownerName,
            Description = $"تسویه تنخواه {ownerName}{d}"
        };
        await new CashTransactionRepository(_connectionString).AddAsync(t);
        Smp.Helpers.AuditHelper.Log("تسویه", "تنخواه", $"{ownerName}، مبلغ {amount:N0}");
    }

    /// <summary>تسویه‌های ثبت‌شده (برگشت وجه از تنخواه‌دار به بانک)؛ ownerName = null یعنی همه</summary>
    public async Task<List<TankhahCharge>> GetSettlementsAsync(string? ownerName = null)
    {
        var result = new List<TankhahCharge>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
                SELECT TOP 300 t.Id, t.TransactionDate, t.Amount,
                       ISNULL(b.Title, ISNULL(t.AccountTitle, N'---')), ISNULL(t.Description, N''), ISNULL(t.RecipientName, N'')
                FROM CashTransactions t
                LEFT JOIN CompanyBankAccounts b ON t.AccountId = b.Id
                WHERE t.Category = @Cat AND t.Direction = @Dir
                  AND (@Owner IS NULL OR t.RecipientName = @Owner)
                ORDER BY t.TransactionDate DESC, t.Id DESC", conn);
        cmd.Parameters.AddWithValue("@Cat", SettlementCategory);
        cmd.Parameters.AddWithValue("@Dir", DirectionTypes.Incoming);
        cmd.Parameters.AddWithValue("@Owner", (object?)ownerName ?? DBNull.Value);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            result.Add(new TankhahCharge
            {
                Id = r.GetInt32(0),
                Date = r.GetDateTime(1),
                Amount = r.GetDecimal(2),
                BankTitle = r.GetString(3),
                Description = r.GetString(4),
                OwnerName = r.GetString(5)
            });
        }
        return result;
    }

    /// <summary>جمع تسویه‌های یک تنخواه‌دار (برای کم شدن از مانده‌ی او در گزارش)</summary>
    public async Task<decimal> GetSettledTotalAsync(string ownerName)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT ISNULL(SUM(Amount), 0) FROM CashTransactions WHERE Category = @Cat AND Direction = @Dir AND RecipientName = @Owner", conn);
        cmd.Parameters.AddWithValue("@Cat", SettlementCategory);
        cmd.Parameters.AddWithValue("@Dir", DirectionTypes.Incoming);
        cmd.Parameters.AddWithValue("@Owner", ownerName);
        return Convert.ToDecimal(await cmd.ExecuteScalarAsync());
    }

    /// <summary>حذف یک تسویه؛ موجودی بانک اصلاح می‌شود</summary>
    public async Task DeleteSettlementAsync(int transactionId)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT COUNT(1) FROM CashTransactions WHERE Id = @Id AND Category = @Cat AND Direction = @Dir", conn);
            cmd.Parameters.AddWithValue("@Id", transactionId);
            cmd.Parameters.AddWithValue("@Cat", SettlementCategory);
            cmd.Parameters.AddWithValue("@Dir", DirectionTypes.Incoming);
            if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0)
                throw new InvalidOperationException("این تسویه یافت نشد (ممکن است قبلاً حذف شده باشد).");
        }
        await new CashTransactionRepository(_connectionString).DeleteBatchAsync(new List<int> { transactionId });
    }

    /// <summary>ویرایش یک شارژ ثبت‌شده (ردیف CashTransactions): موجودی بانک و لیست تنخواه با اختلاف اصلاح می‌شود</summary>
    public async Task UpdateChargeAsync(int transactionId, int bankAccountId, decimal amount, DateTime date, string description)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            int oldAccountId; decimal oldAmount; int listId;
            using (var cmd = new SqlCommand("SELECT AccountId, Amount, TankhahListId FROM CashTransactions WHERE Id = @Id AND TankhahListId IS NOT NULL", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", transactionId);
                using var r = await cmd.ExecuteReaderAsync();
                if (!await r.ReadAsync())
                    throw new InvalidOperationException("شارژ مورد نظر پیدا نشد.");
                oldAccountId = r.GetInt32(0);
                oldAmount = r.GetDecimal(1);
                listId = r.GetInt32(2);
            }

            // برگرداندن مبلغ قبلی به حساب بانکی قبلی
            using (var cmd = new SqlCommand("UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance + @Amount WHERE Id = @Id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Amount", oldAmount);
                cmd.Parameters.AddWithValue("@Id", oldAccountId);
                await cmd.ExecuteNonQueryAsync();
            }

            // بررسی موجودی حساب جدید
            decimal bankBalance = 0;
            using (var cmd = new SqlCommand("SELECT CurrentBalance FROM CompanyBankAccounts WHERE Id = @Id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", bankAccountId);
                var res = await cmd.ExecuteScalarAsync();
                if (res is not null && res != DBNull.Value) bankBalance = Convert.ToDecimal(res);
            }
            if (bankBalance < amount)
                throw new InvalidOperationException("موجودی حساب بانکی برای این شارژ کافی نیست.");

            using (var cmd = new SqlCommand("UPDATE CompanyBankAccounts SET CurrentBalance = CurrentBalance - @Amount WHERE Id = @Id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Amount", amount);
                cmd.Parameters.AddWithValue("@Id", bankAccountId);
                await cmd.ExecuteNonQueryAsync();
            }

            // اصلاح لیست تنخواه با اختلاف مبلغ
            using (var cmd = new SqlCommand("UPDATE TankhahLists SET CurrentBalance = CurrentBalance + (@New - @Old), ChargeAmount = ChargeAmount + (@New - @Old), ChargeDate = @Date WHERE Id = @Id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@New", amount);
                cmd.Parameters.AddWithValue("@Old", oldAmount);
                cmd.Parameters.AddWithValue("@Date", date);
                cmd.Parameters.AddWithValue("@Id", listId);
                await cmd.ExecuteNonQueryAsync();
            }

            string title = "";
            using (var cmd = new SqlCommand("SELECT Title FROM TankhahLists WHERE Id = @Id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", listId);
                title = (await cmd.ExecuteScalarAsync())?.ToString() ?? "تنخواه";
            }

            using (var cmd = new SqlCommand("UPDATE CashTransactions SET AccountId = @AccountId, TransactionDate = @Date, Amount = @Amount, Description = @Desc WHERE Id = @Id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@AccountId", bankAccountId);
                cmd.Parameters.AddWithValue("@Date", date);
                cmd.Parameters.AddWithValue("@Amount", amount);
                cmd.Parameters.AddWithValue("@Desc", $"شارژ تنخواه {title} - {description}");
                cmd.Parameters.AddWithValue("@Id", transactionId);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
        Smp.Helpers.AuditHelper.Log("ویرایش", "تنخواه", $"شارژ شناسه {transactionId}، مبلغ {amount:N0}");
    }

    // ═══════════════════════════════════════════════════════════
    //  شماره‌گذاری خودکار لیست‌ها: «لیست 1»، «لیست 2»، ...
    //  کوچک‌ترین شماره‌ی آزاد هر شخص گرفته می‌شود؛ پس اگر لیستی حذف شد،
    //  لیست بعدی دوباره با همان شماره ساخته می‌شود و توالی به‌هم نمی‌خورد.
    // ═══════════════════════════════════════════════════════════
    private static readonly Regex ListNumberRegex = new(@"^\s*لیست\s*(\d+)\s*$", RegexOptions.Compiled);

    private static string NormalizeDigits(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (c >= '\u06F0' && c <= '\u06F9') sb.Append((char)('0' + (c - '\u06F0')));      // ارقام فارسی
            else if (c >= '\u0660' && c <= '\u0669') sb.Append((char)('0' + (c - '\u0660'))); // ارقام عربی
            else sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>شماره‌ی لیست از روی عنوان («لیست 3» یا «لیست ۳» → 3)؛ برای عنوان‌های دلخواه null</summary>
    public static int? ParseListNumber(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var m = ListNumberRegex.Match(NormalizeDigits(title));
        return m.Success && int.TryParse(m.Groups[1].Value, out int n) && n > 0 ? n : (int?)null;
    }

    public static string BuildListTitle(int number) => $"لیست {number}";

    /// <summary>لیست بعدی شخص را با اولین شماره‌ی آزاد می‌سازد و برمی‌گرداند</summary>
    public async Task<TankhahList> AddNextListAsync(int tankhahId)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            var used = new HashSet<int>();
            using (var cmd = new SqlCommand("SELECT Title FROM TankhahLists WHERE TankhahId = @Id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", tankhahId);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    if (r.IsDBNull(0)) continue;
                    var n = ParseListNumber(r.GetString(0));
                    if (n.HasValue) used.Add(n.Value);
                }
            }

            int next = 1;
            while (used.Contains(next)) next++;

            var list = new TankhahList
            {
                TankhahId = tankhahId,
                Title = BuildListTitle(next),
                ChargeDate = DateTime.Now,
                ChargeAmount = 0,
                CurrentBalance = 0
            };

            using (var cmd = new SqlCommand(@"
                    INSERT INTO TankhahLists (TankhahId, Title, ChargeDate, ChargeAmount, CurrentBalance, IsActive, CreatedAt)
                    VALUES (@TankhahId, @Title, @ChargeDate, 0, 0, 1, GETDATE());
                    SELECT SCOPE_IDENTITY();", conn, tx))
            {
                cmd.Parameters.AddWithValue("@TankhahId", list.TankhahId);
                cmd.Parameters.AddWithValue("@Title", list.Title);
                cmd.Parameters.AddWithValue("@ChargeDate", (object?)list.ChargeDate ?? DBNull.Value);
                list.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            await tx.CommitAsync();
            Smp.Helpers.AuditHelper.Log("ثبت", "تنخواه", $"{list.Title} برای تنخواه‌دار شناسه {tankhahId}");
            return list;
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    /// <summary>
    /// فهرست شارژهای تنخواه (ردیف‌های «انتقال بین حساب‌ها» که به یک لیست تنخواه وصل‌اند).
    /// listId: فقط شارژهای همان لیست | tankhahId: همه‌ی شارژهای همه‌ی لیست‌های آن شخص | هر دو null: همه
    /// </summary>
    public async Task<List<TankhahCharge>> GetChargesAsync(int? listId = null, int? tankhahId = null)
    {
        var result = new List<TankhahCharge>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"
                SELECT t.Id, l.Id, l.Title, t.TransactionDate, t.Amount,
                       ISNULL(b.Title, ISNULL(t.AccountTitle, N'---')), ISNULL(t.Description, N''), ISNULL(o.Value, N'')
                FROM CashTransactions t
                INNER JOIN TankhahLists l ON t.TankhahListId = l.Id
                LEFT JOIN LookupItems o ON l.TankhahId = o.Id AND o.Category = N'TankhahOwner'
                LEFT JOIN CompanyBankAccounts b ON t.AccountId = b.Id
                WHERE t.TankhahListId IS NOT NULL
                  AND t.Category = N'انتقال بین حساب‌ها'
                  AND t.Direction = @Dir
                  AND (@ListId IS NULL OR l.Id = @ListId)
                  AND (@TankhahId IS NULL OR l.TankhahId = @TankhahId)
                ORDER BY t.TransactionDate DESC, t.Id DESC", conn);
        cmd.Parameters.AddWithValue("@Dir", DirectionTypes.Outgoing);
        cmd.Parameters.AddWithValue("@ListId", (object?)listId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TankhahId", (object?)tankhahId ?? DBNull.Value);

        using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                result.Add(new TankhahCharge
                {
                    Id = r.GetInt32(0),
                    ListId = r.GetInt32(1),
                    ListTitle = r.GetString(2),
                    Date = r.GetDateTime(3),
                    Amount = r.GetDecimal(4),
                    BankTitle = r.GetString(5),
                    Description = r.GetString(6),
                    OwnerName = r.GetString(7)
                });
            }
        }

        // ── شارژهای «قدیمی»: انتقال بانک → حساب تنخواه (AccountTransfers) که به لیست وصل نیستند ──
        // حساب تنخواه با نام شخص تطبیق داده می‌شود؛ پیشوندهایی مثل «آقای/خانم» نادیده گرفته می‌شود.
        if (tankhahId.HasValue && !listId.HasValue)
        {
            string? owner = null;
            using (var cmdO = new SqlCommand("SELECT Value FROM LookupItems WHERE Id = @Id AND Category = N'TankhahOwner'", conn))
            {
                cmdO.Parameters.AddWithValue("@Id", tankhahId.Value);
                owner = (await cmdO.ExecuteScalarAsync())?.ToString();
            }
            if (!string.IsNullOrWhiteSpace(owner))
            {
                var tokens = OwnerTokens(owner!);
                foreach (var c in await LoadLegacyTransfersAsync(conn))
                    if (tokens.Count > 0 && MatchesOwner(c.TankhahAccountTitle + " " + c.MatchText, tokens))
                        result.Add(c);
            }
        }

        return result.OrderByDescending(c => c.Date).ThenByDescending(c => c.Id).ToList();
    }


    // ═══ شارژهای قدیمی (AccountTransfers) ═══
    private static readonly string[] NameNoise = { "آقای", "اقای", "خانم", "دکتر", "مهندس", "حاج", "تنخواه", "حساب", "صندوق", "آقا" };

    private static string Norm(string? t)
        => PersianText.NormalizeText(t).Replace(" ", "").Replace("-", "");

    private static List<string> OwnerTokens(string owner)
    {
        var words = StringHelper.NormalizePersianText(owner).Replace("\u200c", " ")
            .Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
        return words.Select(w => Norm(w)).Where(w => w.Length >= 2 && !NameNoise.Contains(w)).ToList();
    }

    private static bool MatchesOwner(string accountTitle, List<string> tokens)
    {
        string a = Norm(accountTitle);
        return tokens.All(t => a.Contains(t));
    }

    /// <summary>همه‌ی انتقال‌های بانک→تنخواه (مثبت) و تنخواه→بانک (منفی)؛ TankhahAccountTitle = عنوان حساب تنخواه</summary>
    private static async Task<List<TankhahCharge>> LoadLegacyTransfersAsync(SqlConnection conn)
    {
        var list = new List<TankhahCharge>();
        try
        {
            using var cmd = new SqlCommand(@"
                    SELECT a.Id, a.TransferDate, a.Amount, ISNULL(a.Description, N''),
                           ISNULL(s.Title, N'---') AS BankTitle, ISNULL(d.Title, N'') AS TankhahTitle, 1 AS IsCharge
                    FROM AccountTransfers a
                    LEFT JOIN CompanyBankAccounts s ON a.SourceAccountId = s.Id
                    LEFT JOIN CompanyBankAccounts d ON a.DestinationAccountId = d.Id
                    WHERE a.TransferType = N'BankToTankhah' OR ISNULL(d.IsCashDesk, 0) = 1
                    UNION ALL
                    SELECT a.Id, a.TransferDate, a.Amount, ISNULL(a.Description, N''),
                           ISNULL(d.Title, N'---'), ISNULL(s.Title, N''), 0
                    FROM AccountTransfers a
                    LEFT JOIN CompanyBankAccounts s ON a.SourceAccountId = s.Id
                    LEFT JOIN CompanyBankAccounts d ON a.DestinationAccountId = d.Id
                    WHERE a.TransferType = N'TankhahToBank' OR (ISNULL(s.IsCashDesk, 0) = 1 AND ISNULL(d.IsCashDesk, 0) = 0)", conn);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                bool isCharge = Convert.ToInt32(r.GetValue(6)) == 1;
                decimal amt = r.GetDecimal(2);
                string acct = r.GetString(5);
                list.Add(new TankhahCharge
                {
                    Id = -r.GetInt32(0),   // منفی: با شناسه‌ی CashTransactions قاطی نشود
                    ListId = 0,
                    ListTitle = (isCharge ? "قدیمی" : "تسویه به بانک") + (string.IsNullOrWhiteSpace(acct) ? "" : " — " + acct),
                    TankhahAccountTitle = acct,
                    Date = r.GetDateTime(1),
                    Amount = isCharge ? amt : -amt,
                    BankTitle = r.GetString(4),
                    Description = r.GetString(3)
                });
            }
        }
        catch (Exception ex) { LogHelper.LogException(ex, "TankhahRepository.LoadLegacyTransfers"); }

        // ── شارژهای ثبت‌شده به‌صورت تراکنش نقدی: دسته‌ی «شارژ تنخواه» یا دریافتی در حساب صندوق/تنخواه ──
        try
        {
            using var cmd = new SqlCommand(@"
                    SELECT t.Id, t.TransactionDate, t.Amount, ISNULL(t.Description, N''), t.Direction,
                           ISNULL(a.Title, ISNULL(t.AccountTitle, N'')) AS AcctTitle,
                           ISNULL(t.Category, N'') AS Cat,
                           ISNULL(t.TafsiliTitle, N'') + N' ' + ISNULL(t.RecipientName, N'') AS Party
                    FROM CashTransactions t
                    LEFT JOIN CompanyBankAccounts a ON t.AccountId = a.Id
                    WHERE t.TankhahListId IS NULL
                      AND ( LTRIM(RTRIM(ISNULL(t.Category, N''))) = N'شارژ تنخواه'
                         OR (t.Direction = N'پرداختی' AND ISNULL(t.Description, N'') LIKE N'شارژ تنخواه%')   -- شارژهای ایمپورت‌شده‌ای که به لیست وصل نشده‌اند
                         OR (ISNULL(a.IsCashDesk, 0) = 1 AND t.Direction = N'دریافتی'
                             AND ISNULL(t.Category, N'') NOT IN (N'سود بانکی', N'سود')) )", conn);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                bool incoming = r.GetString(4) == DirectionTypes.Incoming;
                decimal amt = r.GetDecimal(2);
                string acct = r.GetString(5);
                string cat = r.GetString(6);
                list.Add(new TankhahCharge
                {
                    Id = 1_000_000_000 + r.GetInt32(0),   // فاصله‌ی شناسه از سایر منابع
                    ListId = 0,
                    ListTitle = (!incoming || cat == "شارژ تنخواه" ? "قدیمی (تراکنش نقدی)" : "دریافتی صندوق") + (string.IsNullOrWhiteSpace(acct) ? "" : " — " + acct),
                    TankhahAccountTitle = acct,
                    MatchText = r.GetString(7) + " " + r.GetString(3),
                    Date = r.GetDateTime(1),
                    Amount = incoming ? amt : amt,   // «شارژ تنخواه» پرداختی از بانک هم شارژ است (مثبت)
                    BankTitle = incoming ? "---" : acct,
                    Description = r.GetString(3)
                });
            }
        }
        catch (Exception ex) { LogHelper.LogException(ex, "TankhahRepository.LoadLegacyCashCharges"); }
        return list;
    }


    /// <summary>گزارش عیب‌یابی: نشان می‌دهد شارژها واقعاً در کدام جدول/دسته ثبت شده‌اند</summary>
    public async Task<string> GetChargeDiagnosticsAsync()
    {
        var sb = new System.Text.StringBuilder();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        async Task Q(string title, string sql)
        {
            try
            {
                using var cmd = new SqlCommand(sql, conn);
                using var r = await cmd.ExecuteReaderAsync();
                sb.AppendLine("■ " + title);
                int n = 0;
                while (await r.ReadAsync() && n < 12)
                {
                    var parts = new List<string>();
                    for (int i = 0; i < r.FieldCount; i++) parts.Add(r.IsDBNull(i) ? "-" : Convert.ToString(r.GetValue(i)) ?? "-");
                    sb.AppendLine("   " + string.Join(" | ", parts));
                    n++;
                }
                if (n == 0) sb.AppendLine("   (هیچ)");
            }
            catch (Exception ex) { sb.AppendLine("■ " + title + " → خطا: " + ex.Message); }
        }

        await Q("حساب‌های صندوق/تنخواه (IsCashDesk): نام | موجودی",
            "SELECT Title, CurrentBalance FROM CompanyBankAccounts WHERE IsCashDesk = 1");
        await Q("انتقال‌ها بر اساس نوع: نوع | تعداد | جمع",
            "SELECT ISNULL(TransferType, N'(خالی)'), COUNT(*), SUM(Amount) FROM AccountTransfers GROUP BY TransferType");
        await Q("تراکنش‌های نقدی دارای «تنخواه» در دسته: دسته | جهت | تعداد | جمع",
            "SELECT Category, Direction, COUNT(*), SUM(Amount) FROM CashTransactions WHERE Category LIKE N'%تنخواه%' GROUP BY Category, Direction");
        await Q("تراکنش‌های وصل‌شده به لیست تنخواه: دسته | جهت | تعداد | جمع",
            "SELECT ISNULL(Category, N'-'), Direction, COUNT(*), SUM(Amount) FROM CashTransactions WHERE TankhahListId IS NOT NULL GROUP BY Category, Direction");
        await Q("نمونه تراکنش‌هایی که در شرح‌شان «تنخواه» یا «شارژ» است: تاریخ | مبلغ | جهت | دسته | حساب | شرح",
            @"SELECT TOP 10 TransactionDate, Amount, Direction, Category, AccountTitle, LEFT(ISNULL(Description, N''), 60)
                  FROM CashTransactions
                  WHERE Description LIKE N'%تنخواه%' OR Description LIKE N'%شارژ%' ORDER BY TransactionDate DESC");
        await Q("نمونه چک‌های دارای «تنخواه» در شرح: سررسید | مبلغ | نوع | گیرنده | شرح",
            @"SELECT TOP 10 DueDate, Amount, CheckType, RecipientName, LEFT(ISNULL(Description, N''), 60)
                  FROM CashChecks WHERE Description LIKE N'%تنخواه%' OR Description LIKE N'%شارژ%' ORDER BY DueDate DESC");
        return sb.ToString();
    }

    /// <summary>همه‌ی شارژهای ثبت‌شده در سیستم (همه‌ی تنخواه‌دارها، هر دو روش ثبت) — برای عیب‌یابی و دیدن اینکه داده کجاست</summary>
    public async Task<List<TankhahCharge>> GetAllChargesEverywhereAsync()
    {
        var all = await GetChargesAsync();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        all.AddRange(await LoadLegacyTransfersAsync(conn));
        return all.OrderByDescending(c => c.Date).ThenByDescending(c => c.Id).ToList();
    }

    public async Task<TankhahList?> GetListByIdAsync(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT Id, TankhahId, Title, ChargeDate, ChargeAmount, CurrentBalance, IsActive, CreatedAt FROM TankhahLists WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new TankhahList
        {
            Id = reader.GetInt32(0),
            TankhahId = reader.GetInt32(1),
            Title = reader.GetString(2),
            ChargeDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
            ChargeAmount = reader.GetDecimal(4),
            CurrentBalance = reader.GetDecimal(5),
            IsActive = reader.GetBoolean(6),
            CreatedAt = reader.GetDateTime(7)
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  ردیف‌های هزینه‌ی هر لیست (ذخیره در CashTransactions با TankhahListId)
    //  پول هنگام «شارژ» از بانک خارج شده؛ پس ردیف هزینه AccountId ندارد و موجودی بانک را تغییر نمی‌دهد.
    //  فقط موجودی همان لیست (CurrentBalance) کم/زیاد می‌شود.
    // ═══════════════════════════════════════════════════════════
    private const string ExpenseColumns =
        "Id, PaymentScope, ContractId, ContractorContractId, ContractTitle, ClientStatementId, TransactionDate, Amount, " +
        "Discount, Shipping, VAT, TransactionType, Direction, AccountId, AccountTitle, Category, CostType, RecipientName, " +
        "RecipientSheba, TafsiliCode, TafsiliTitle, Description, CreatedAt";

    public async Task<List<CashTransaction>> GetListExpensesAsync(int listId)
    {
        var result = new List<CashTransaction>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand($@"
                SELECT {ExpenseColumns}
                FROM CashTransactions
                WHERE TankhahListId = @ListId
                  AND Direction = @Dir
                  AND ISNULL(Category, N'') <> N'انتقال بین حساب‌ها'
                ORDER BY TransactionDate, Id", conn);
        cmd.Parameters.AddWithValue("@ListId", listId);
        cmd.Parameters.AddWithValue("@Dir", DirectionTypes.Outgoing);

        using var r = await cmd.ExecuteReaderAsync();
        string? S(int i) => r.IsDBNull(i) ? null : r.GetValue(i)?.ToString();
        decimal D(int i) => r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i));
        int? I(int i) => r.IsDBNull(i) ? (int?)null : Convert.ToInt32(r.GetValue(i));

        while (await r.ReadAsync())
        {
            result.Add(new CashTransaction
            {
                Id = Convert.ToInt32(r.GetValue(0)),
                PaymentScope = S(1),
                ContractId = I(2),
                ContractorContractId = I(3),
                ContractTitle = S(4),
                ClientStatementId = I(5),
                TransactionDate = r.IsDBNull(6) ? DateTime.Now : Convert.ToDateTime(r.GetValue(6)),
                Amount = D(7),
                Discount = D(8),
                Shipping = D(9),
                VAT = D(10),
                TransactionType = S(11) ?? "نقدی",
                Direction = S(12) ?? DirectionTypes.Outgoing,
                AccountId = I(13),
                AccountTitle = S(14),
                Category = S(15),
                CostType = S(16),
                RecipientName = S(17),
                RecipientSheba = S(18),
                TafsiliCode = S(19),
                TafsiliTitle = S(20),
                Description = S(21),
                CreatedAt = r.IsDBNull(22) ? DateTime.Now : Convert.ToDateTime(r.GetValue(22))
            });
        }
        return result;
    }

    private static void AddExpenseParameters(SqlCommand cmd, CashTransaction t)
    {
        cmd.Parameters.AddWithValue("@PaymentScope", (object?)t.PaymentScope ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ContractId", (object?)t.ContractId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ContractorContractId", (object?)t.ContractorContractId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ContractTitle", (object?)t.ContractTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TransactionDate", t.TransactionDate);
        cmd.Parameters.AddWithValue("@Amount", t.Amount);
        cmd.Parameters.AddWithValue("@Discount", t.Discount);
        cmd.Parameters.AddWithValue("@Shipping", t.Shipping);
        cmd.Parameters.AddWithValue("@VAT", t.VAT);
        cmd.Parameters.AddWithValue("@TransactionType", (object?)t.TransactionType ?? "نقدی");
        cmd.Parameters.AddWithValue("@Direction", DirectionTypes.Outgoing);
        cmd.Parameters.AddWithValue("@AccountTitle", (object?)t.AccountTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Category", (object?)t.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CostType", (object?)t.CostType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RecipientName", (object?)t.RecipientName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RecipientSheba", (object?)t.RecipientSheba ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TafsiliCode", (object?)t.TafsiliCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TafsiliTitle", (object?)t.TafsiliTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Description", (object?)t.Description ?? DBNull.Value);
    }

    public async Task AddListExpenseAsync(int listId, CashTransaction t)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();
        try
        {
            using (var cmd = new SqlCommand(@"
                    INSERT INTO CashTransactions
                    (PaymentScope, ContractId, ContractorContractId, ContractTitle, TransactionDate, Amount, Discount, Shipping, VAT,
                     TransactionType, Direction, AccountId, AccountTitle, Category, CostType, RecipientName, RecipientSheba,
                     TafsiliCode, TafsiliTitle, Description, TankhahListId, CreatedAt)
                    VALUES
                    (@PaymentScope, @ContractId, @ContractorContractId, @ContractTitle, @TransactionDate, @Amount, @Discount, @Shipping, @VAT,
                     @TransactionType, @Direction, NULL, @AccountTitle, @Category, @CostType, @RecipientName, @RecipientSheba,
                     @TafsiliCode, @TafsiliTitle, @Description, @ListId, GETDATE());
                    SELECT SCOPE_IDENTITY();", conn, tx))
            {
                AddExpenseParameters(cmd, t);
                cmd.Parameters.AddWithValue("@ListId", listId);
                var res = await cmd.ExecuteScalarAsync();
                t.Id = res is not null && res != DBNull.Value ? Convert.ToInt32(res) : 0;
            }

            using (var cmd = new SqlCommand("UPDATE TankhahLists SET CurrentBalance = CurrentBalance - @Amount WHERE Id = @ListId", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Amount", t.Amount);
                cmd.Parameters.AddWithValue("@ListId", listId);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
        Smp.Helpers.AuditHelper.Log("ثبت", "هزینه تنخواه", $"لیست {listId}، شناسه {t.Id}، مبلغ {t.Amount:N0}");
    }

    /// <summary>ثبت دسته‌ای ردیف‌های هزینه (ایمپورت اکسل) در یک تراکنش: یا همه ثبت می‌شوند یا هیچ‌کدام؛ مانده‌ی لیست یک‌بار کم می‌شود.</summary>
    public async Task<int> AddListExpensesBatchAsync(int listId, IEnumerable<CashTransaction> items)
    {
        var rows = items.ToList();
        if (rows.Count == 0) return 0;

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();
        decimal total = 0;
        try
        {
            foreach (var t in rows)
            {
                using var cmd = new SqlCommand(@"
                        INSERT INTO CashTransactions
                        (PaymentScope, ContractId, ContractorContractId, ContractTitle, TransactionDate, Amount, Discount, Shipping, VAT,
                         TransactionType, Direction, AccountId, AccountTitle, Category, CostType, RecipientName, RecipientSheba,
                         TafsiliCode, TafsiliTitle, Description, TankhahListId, CreatedAt)
                        VALUES
                        (@PaymentScope, @ContractId, @ContractorContractId, @ContractTitle, @TransactionDate, @Amount, @Discount, @Shipping, @VAT,
                         @TransactionType, @Direction, NULL, @AccountTitle, @Category, @CostType, @RecipientName, @RecipientSheba,
                         @TafsiliCode, @TafsiliTitle, @Description, @ListId, GETDATE());
                        SELECT SCOPE_IDENTITY();", conn, tx);
                AddExpenseParameters(cmd, t);
                cmd.Parameters.AddWithValue("@ListId", listId);
                var res = await cmd.ExecuteScalarAsync();
                t.Id = res is not null && res != DBNull.Value ? Convert.ToInt32(res) : 0;
                total += t.Amount;
            }

            using (var cmd = new SqlCommand("UPDATE TankhahLists SET CurrentBalance = CurrentBalance - @Amount WHERE Id = @ListId", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Amount", total);
                cmd.Parameters.AddWithValue("@ListId", listId);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }

        Smp.Helpers.AuditHelper.Log("ایمپورت", "هزینه تنخواه", $"لیست {listId}، تعداد {rows.Count} ردیف، مجموع {total:N0}");
        return rows.Count;
    }

    public async Task UpdateListExpenseAsync(int listId, CashTransaction t)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();
        try
        {
            decimal oldAmount;
            int? statementId;
            using (var cmd = new SqlCommand("SELECT Amount, ClientStatementId FROM CashTransactions WITH (UPDLOCK, ROWLOCK) WHERE Id = @Id AND TankhahListId = @ListId", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", t.Id);
                cmd.Parameters.AddWithValue("@ListId", listId);
                using var r = await cmd.ExecuteReaderAsync();
                if (!await r.ReadAsync())
                    throw new InvalidOperationException("ردیف مورد نظر یافت نشد (ممکن است قبلاً حذف شده باشد).");
                oldAmount = r.IsDBNull(0) ? 0m : Convert.ToDecimal(r.GetValue(0));
                statementId = r.IsDBNull(1) ? (int?)null : Convert.ToInt32(r.GetValue(1));
            }

            if (statementId.HasValue && oldAmount != t.Amount)
                throw new InvalidOperationException("این ردیف در یک صورت‌حساب ثبت شده است و تغییر مبلغ آن مجاز نیست.");

            using (var cmd = new SqlCommand(@"
                    UPDATE CashTransactions SET
                        PaymentScope = @PaymentScope, ContractId = @ContractId, ContractorContractId = @ContractorContractId,
                        ContractTitle = @ContractTitle, TransactionDate = @TransactionDate, Amount = @Amount,
                        Discount = @Discount, Shipping = @Shipping, VAT = @VAT, TransactionType = @TransactionType,
                        Direction = @Direction, AccountTitle = @AccountTitle, Category = @Category, CostType = @CostType,
                        RecipientName = @RecipientName, RecipientSheba = @RecipientSheba, TafsiliCode = @TafsiliCode,
                        TafsiliTitle = @TafsiliTitle, Description = @Description
                    WHERE Id = @Id AND TankhahListId = @ListId", conn, tx))
            {
                AddExpenseParameters(cmd, t);
                cmd.Parameters.AddWithValue("@Id", t.Id);
                cmd.Parameters.AddWithValue("@ListId", listId);
                await cmd.ExecuteNonQueryAsync();
            }

            using (var cmd = new SqlCommand("UPDATE TankhahLists SET CurrentBalance = CurrentBalance + (@Old - @New) WHERE Id = @ListId", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Old", oldAmount);
                cmd.Parameters.AddWithValue("@New", t.Amount);
                cmd.Parameters.AddWithValue("@ListId", listId);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
        Smp.Helpers.AuditHelper.Log("ویرایش", "هزینه تنخواه", $"لیست {listId}، شناسه {t.Id}، مبلغ {t.Amount:N0}");
    }

    public async Task DeleteListExpenseAsync(int listId, int transactionId)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();
        try
        {
            decimal amount;
            int? statementId;
            using (var cmd = new SqlCommand("SELECT Amount, ClientStatementId FROM CashTransactions WITH (UPDLOCK, ROWLOCK) WHERE Id = @Id AND TankhahListId = @ListId", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", transactionId);
                cmd.Parameters.AddWithValue("@ListId", listId);
                using var r = await cmd.ExecuteReaderAsync();
                if (!await r.ReadAsync())
                    throw new InvalidOperationException("ردیف مورد نظر یافت نشد (ممکن است قبلاً حذف شده باشد).");
                amount = r.IsDBNull(0) ? 0m : Convert.ToDecimal(r.GetValue(0));
                statementId = r.IsDBNull(1) ? (int?)null : Convert.ToInt32(r.GetValue(1));
            }

            if (statementId.HasValue)
                throw new InvalidOperationException("این ردیف در یک صورت‌حساب ثبت شده است و قابل حذف نیست.");

            using (var cmd = new SqlCommand("DELETE FROM CashTransactions WHERE Id = @Id AND TankhahListId = @ListId", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", transactionId);
                cmd.Parameters.AddWithValue("@ListId", listId);
                await cmd.ExecuteNonQueryAsync();
            }

            using (var cmd = new SqlCommand("UPDATE TankhahLists SET CurrentBalance = CurrentBalance + @Amount WHERE Id = @ListId", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Amount", amount);
                cmd.Parameters.AddWithValue("@ListId", listId);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
        Smp.Helpers.AuditHelper.Log("حذف", "هزینه تنخواه", $"لیست {listId}، شناسه {transactionId}");
    }
}
