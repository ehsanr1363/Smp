using ClosedXML.Excel;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Smp.Helpers;

public enum TankhahImportStatus { Valid, Duplicate, Error }

/// <summary>یک ردیف خوانده‌شده از فایل اکسل (به‌همراه نتیجه‌ی اعتبارسنجی)</summary>
public class TankhahImportRow
{
    public int ExcelRow { get; set; }
    public string DateText { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string PaymentTypeText { get; set; } = "";
    public string CostTypeText { get; set; } = "";
    public string PartyText { get; set; } = "";
    public string ContractText { get; set; } = "";
    public TankhahImportStatus Status { get; set; } = TankhahImportStatus.Valid;
    public string Message { get; set; } = "";

    /// <summary>ردیف آماده‌ی ثبت (فقط وقتی وضعیت خطا نیست)</summary>
    public CashTransaction? Transaction { get; set; }

    public string StatusText => Status switch
    {
        TankhahImportStatus.Valid => "معتبر",
        TankhahImportStatus.Duplicate => "تکراری",
        _ => "خطا"
    };
}

/// <summary>
/// ایمپورت ردیف‌های هزینه‌ی لیست تنخواه از اکسل + ساخت فایل نمونه.
/// ستون‌ها: تاریخ | شرح | مبلغ | نوع پرداخت | ماهیت هزینه | طرف حساب (تفصیلی) | شماره قرارداد
/// قواعد اعتبارسنجی دقیقاً مطابق فرم «ثبت ردیف جدید» (TankhahRowEditForm) است.
/// </summary>
public static class TankhahExcelImporter
{
    // ───────────── نوع پرداخت‌های قابل‌قبول در اکسل ─────────────
    public const string TypeAdmin = "هزینه اداری";
    public const string TypeContractor = "پیمانکار قراردادی";
    public const string TypeInformal = "پیمانکار فاکتوری";
    public const string TypeSeller = "فروشنده کالا";
    public const string TypePayroll = "حقوق و مساعده";
    public const string TypeProject = "هزینه پروژه";

    private enum ImportKind { Admin, Contractor, Informal, Seller, Payroll, Project }

    private static readonly string[] Headers =
    {
        "تاریخ (شمسی 1403/01/01)",
        "شرح / توضیحات",
        "مبلغ (ریال)",
        "نوع پرداخت (هزینه اداری / پیمانکار قراردادی / پیمانکار فاکتوری / فروشنده کالا / حقوق و مساعده)",
        "ماهیت هزینه (مثلاً خرید کالا/خدمات/سایر)",
        "طرف حساب (تفصیلی)",
        "شماره قرارداد (اختیاری - برای اتصال به پروژه)"
    };

    // ═══════════════════════════════════════════════════════════
    //  فایل نمونه
    // ═══════════════════════════════════════════════════════════
    public static void GenerateTemplate()
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = "Tankhah_Import_Template.xlsx",
            Title = "ذخیره فایل نمونه اکسل هزینه‌های تنخواه"
        };
        if (sfd.ShowDialog() != DialogResult.OK) return;

        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("تنخواه");
            ws.RightToLeft = true;

            for (int i = 0; i < Headers.Length; i++)
            {
                var c = ws.Cell(1, i + 1);
                c.Value = Headers[i];
                c.Style.Font.Bold = true;
                c.Style.Font.FontName = "Tahoma";
                c.Style.Font.FontColor = XLColor.White;
                c.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
                c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                c.Style.Alignment.WrapText = true;
            }
            ws.Row(1).Height = 62;

            // ردیف‌های نمونه (باید قبل از ایمپورت واقعی پاک شوند)
            object[][] samples =
            {
                new object[] { "1404/10/23", "نمونه - پذیرایی", 86250000, TypeAdmin,   "لوازم مصرفی", "", "" },
                new object[] { "1404/10/25", "نمونه - مساعده", 150000000, TypePayroll, "",            "", "" },
                new object[] { "1404/11/07", "نمونه - خرید بتن", 377000000, TypeSeller, "خرید کالا",  "", "شماره قرارداد کارفرما" },
            };
            for (int r = 0; r < samples.Length; r++)
            {
                for (int c = 0; c < samples[r].Length; c++)
                {
                    var cell = ws.Cell(r + 2, c + 1);
                    var v = samples[r][c];
                    if (v is int n) cell.Value = n; else cell.Value = v?.ToString() ?? "";
                    cell.Style.Font.FontName = "Tahoma";
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(255, 249, 219);
                }
                ws.Cell(r + 2, 1).Style.NumberFormat.Format = "@";
            }

            ws.Column(1).Style.NumberFormat.Format = "@";            // تاریخ به‌صورت متن (جلوگیری از تبدیل خودکار به تاریخ میلادی)
            ws.Column(3).Style.NumberFormat.Format = "#,##0";
            ws.Column(1).Width = 16;
            ws.Column(2).Width = 42;
            ws.Column(3).Width = 18;
            ws.Column(4).Width = 30;
            ws.Column(5).Width = 28;
            ws.Column(6).Width = 28;
            ws.Column(7).Width = 30;
            ws.SheetView.FreezeRows(1);

            ws.Range("D2:D1000").CreateDataValidation().List(
                $"\"{TypeAdmin},{TypeContractor},{TypeInformal},{TypeSeller},{TypePayroll},{TypeProject}\"");

            // ───────────── برگه‌ی راهنما ─────────────
            var g = wb.Worksheets.Add("راهنما");
            g.RightToLeft = true;
            string[] guide =
            {
                "راهنمای ایمپورت هزینه‌های تنخواه",
                "",
                "• فقط برگه‌ی اول («تنخواه») خوانده می‌شود. ردیف زردرنگ نمونه است؛ قبل از ایمپورت پاک کنید.",
                "• تاریخ: شمسی به شکل 1404/10/23 (ستون به‌صورت متن تنظیم شده است).",
                "• مبلغ: به ریال، عدد؛ مبلغ نهایی ردیف است و از مانده‌ی لیست کم می‌شود.",
                "• نوع پرداخت (الزامی): یکی از گزینه‌های کشویی ستون D.",
                $"• «{TypeContractor}»: شماره قرارداد پیمانکار در ستون G الزامی است.",
                $"• «{TypeInformal}» و «{TypeSeller}»: شماره قرارداد کارفرما در ستون G الزامی است.",
                $"• «{TypeAdmin}» و «{TypeProject}»: ستون G نادیده گرفته می‌شود (مثل فرم ثبت ردیف).",
                $"• «{TypePayroll}»: ستون G اختیاری است؛ ماهیت هزینه خودکار «حقوق و دستمزد» می‌شود.",
                "• ماهیت هزینه: اختیاری؛ اگر خالی باشد مقدار پیش‌فرض نوع پرداخت گذاشته می‌شود.",
                "• طرف حساب: کد تفصیلی یا نام تفصیلی. اگر پیدا نشود، فقط به‌صورت نام ذخیره می‌شود.",
                "• ردیف‌هایی که با تاریخ، مبلغ و شرح یکسان قبلاً در همین لیست ثبت شده‌اند «تکراری» علامت می‌خورند.",
            };
            for (int i = 0; i < guide.Length; i++)
            {
                var c = g.Cell(i + 1, 1);
                c.Value = guide[i];
                c.Style.Font.FontName = "Tahoma";
                if (i == 0) { c.Style.Font.Bold = true; c.Style.Font.FontSize = 13; }
            }
            g.Column(1).Width = 120;

            wb.SaveAs(sfd.FileName);
            ModernMessageBox.ShowInfo("فایل نمونه اکسل ساخته شد.", "موفق");
        }
        catch (Exception ex)
        {
            LogHelper.LogException(ex, "TankhahExcelImporter.GenerateTemplate");
            ModernMessageBox.ShowError("خطا در ساخت فایل نمونه:\n" + ex.Message);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  خواندن و اعتبارسنجی
    // ═══════════════════════════════════════════════════════════
    public static List<TankhahImportRow> Parse(
        string filePath,
        string accountTitle,
        IReadOnlyList<CashTransaction> existingRows,
        IReadOnlyList<ClientContract> clientContracts,
        IReadOnlyList<ContractorContract> contractorContracts,
        IReadOnlyList<Tafsili> tafsilis)
    {
        var result = new List<TankhahImportRow>();

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);

        var used = ws.RangeUsed();
        if (used is null) return result;

        // ستون‌ها از روی عنوان هدر پیدا می‌شوند؛ اگر پیدا نشد جای پیش‌فرض (A..G)
        var headerText = new Dictionary<int, string>();
        foreach (var c in ws.Row(1).CellsUsed())
            headerText[c.Address.ColumnNumber] = StringHelper.NormalizePersianText(c.GetString());

        int colDate = FindCol(headerText, 1, "تاریخ");
        int colDesc = FindCol(headerText, 2, "شرح");
        int colAmount = FindCol(headerText, 3, "مبلغ");
        int colType = FindCol(headerText, 4, "نوع پرداخت");
        int colCost = FindCol(headerText, 5, "ماهیت");
        int colParty = FindCol(headerText, 6, "طرف حساب");
        int colContract = FindCol(headerText, 7, "شماره قرارداد");

        var existingKeys = new HashSet<string>(
            existingRows.Select(r => DupKey(r.TransactionDate, r.Amount, r.Description)));

        int lastRow = used.LastRow().RowNumber();
        for (int r = 2; r <= lastRow; r++)
        {
            var row = ws.Row(r);
            var cDate = row.Cell(colDate);
            var cDesc = row.Cell(colDesc);
            var cAmount = row.Cell(colAmount);
            var cType = row.Cell(colType);
            var cCost = row.Cell(colCost);
            var cParty = row.Cell(colParty);
            var cContract = row.Cell(colContract);

            // ردیف کاملاً خالی
            if (new[] { cDate, cDesc, cAmount, cType, cCost, cParty, cContract }.All(c => c.IsEmpty())) continue;

            var item = new TankhahImportRow
            {
                ExcelRow = r,
                Description = CellText(cDesc),
                PaymentTypeText = CellText(cType),
                CostTypeText = CellText(cCost),
                PartyText = CellText(cParty),
                ContractText = CellText(cContract)
            };

            var errors = new List<string>();
            var notes = new List<string>();

            // ── تاریخ ──
            DateTime? date = ParseDate(cDate);
            item.DateText = date.HasValue ? DateHelper.ToPersianDate(date) : CellText(cDate);
            if (date is null) errors.Add("تاریخ نامعتبر است");

            // ── مبلغ ──
            if (!TryParseAmount(cAmount, out decimal amount) || amount <= 0)
                errors.Add("مبلغ نامعتبر یا صفر است");
            item.Amount = amount;

            // ── شرح ──
            if (string.IsNullOrWhiteSpace(item.Description)) errors.Add("شرح خالی است");

            // ── نوع پرداخت ──
            ImportKind? kind = MapKind(item.PaymentTypeText);
            if (kind is null)
                errors.Add(string.IsNullOrWhiteSpace(item.PaymentTypeText) ? "نوع پرداخت مشخص نشده" : "نوع پرداخت ناشناخته است");

            // ── قرارداد ──
            ClientContract? clientContract = null;
            ContractorContract? contractorContract = null;
            string contractKey = NormalizeKey(item.ContractText);

            if (kind == ImportKind.Contractor)
            {
                if (contractKey.Length == 0) errors.Add("شماره قرارداد پیمانکار الزامی است");
                else
                {
                    contractorContract = contractorContracts.FirstOrDefault(c => NormalizeKey(c.ContractNumber) == contractKey);
                    if (contractorContract is null) errors.Add($"قرارداد پیمانکار «{item.ContractText}» یافت نشد");
                }
            }
            else if (kind == ImportKind.Informal || kind == ImportKind.Seller)
            {
                if (contractKey.Length == 0) errors.Add("شماره قرارداد کارفرما الزامی است");
                else
                {
                    clientContract = clientContracts.FirstOrDefault(c => NormalizeKey(c.ContractNumber) == contractKey);
                    if (clientContract is null) errors.Add($"قرارداد کارفرما «{item.ContractText}» یافت نشد");
                }
            }
            else if (kind == ImportKind.Payroll && contractKey.Length > 0)
            {
                clientContract = clientContracts.FirstOrDefault(c => NormalizeKey(c.ContractNumber) == contractKey);
                if (clientContract is null) notes.Add("قرارداد یافت نشد؛ بدون اتصال ثبت می‌شود");
            }
            else if ((kind == ImportKind.Admin || kind == ImportKind.Project) && contractKey.Length > 0)
            {
                notes.Add("شماره قرارداد برای این نوع نادیده گرفته شد");
            }

            // ── طرف حساب ──
            Tafsili? tafsili = null;
            if (!string.IsNullOrWhiteSpace(item.PartyText))
            {
                tafsili = FindTafsili(tafsilis, item.PartyText);
                if (tafsili is null) notes.Add("طرف حساب در تفصیلی‌ها یافت نشد؛ فقط به‌صورت نام ذخیره می‌شود");
            }

            if (errors.Count > 0)
            {
                item.Status = TankhahImportStatus.Error;
                item.Message = string.Join("؛ ", errors);
                result.Add(item);
                continue;
            }

            // ── ساخت ردیف مطابق منطق SaveSafeAsync فرم ثبت ردیف ──
            var t = BuildTransaction(kind!.Value, date!.Value, amount, item, accountTitle, clientContract, contractorContract, tafsili);
            item.Transaction = t;

            if (existingKeys.Contains(DupKey(t.TransactionDate, t.Amount, t.Description)))
            {
                item.Status = TankhahImportStatus.Duplicate;
                notes.Insert(0, "قبلاً با همین تاریخ، مبلغ و شرح در این لیست ثبت شده است");
            }

            item.Message = string.Join("؛ ", notes);
            result.Add(item);
        }

        return result;
    }

    private static CashTransaction BuildTransaction(
        ImportKind kind, DateTime date, decimal amount, TankhahImportRow item, string accountTitle,
        ClientContract? clientContract, ContractorContract? contractorContract, Tafsili? tafsili)
    {
        string scope;
        string category;
        string costType = item.CostTypeText.Trim();
        string contractTitle;
        int? clientContractId = null;
        int? contractorContractId = null;

        switch (kind)
        {
            case ImportKind.Admin:
                scope = PaymentScopes.Administrative;
                category = "اداری";
                contractTitle = "هزینه‌های اداری";
                if (costType.Length == 0) costType = "سایر";
                break;

            case ImportKind.Project:
                scope = PaymentScopes.ProjectExpense;
                category = "عملیاتی";
                contractTitle = "هزینه‌های پروژه";
                if (costType.Length == 0) costType = "سایر";
                break;

            case ImportKind.Payroll:
                scope = PaymentScopes.Administrative;
                category = "پرسنلی";
                costType = "حقوق و دستمزد";   // فرم ویرایش، حقوق را از روی همین مقدار تشخیص می‌دهد
                contractTitle = "حقوق و دستمزد پرسنل";
                if (clientContract is not null)
                {
                    clientContractId = clientContract.Id;
                    contractTitle = $"{clientContract.ContractNumber} - {clientContract.Subject}";
                }
                break;

            case ImportKind.Seller:
            case ImportKind.Informal:
                scope = kind == ImportKind.Seller ? PaymentScopes.DirectPurchase : PaymentScopes.InformalContractor;
                category = "عملیاتی";
                if (costType.Length == 0) costType = kind == ImportKind.Seller ? "خرید کالا" : "خرید خدمات";
                clientContractId = clientContract!.Id;
                contractTitle = $"{clientContract.ContractNumber} - {clientContract.Subject}";
                break;

            default: // Contractor
                scope = PaymentScopes.ContractorContract;
                category = "عملیاتی";
                if (costType.Length == 0) costType = "خدمات پیمانکاری";
                contractorContractId = contractorContract!.Id;
                clientContractId = contractorContract.ClientContractId;
                contractTitle = $"{contractorContract.DisplayName} - قرارداد {contractorContract.ContractNumber}";
                break;
        }

        string? partyName = tafsili is not null ? tafsili.Name
                          : !string.IsNullOrWhiteSpace(item.PartyText) ? item.PartyText.Trim()
                          : contractorContract is not null ? contractorContract.DisplayName
                          : null;

        return new CashTransaction
        {
            AccountId = null,
            AccountTitle = accountTitle,
            PaymentScope = scope,
            ContractId = clientContractId,
            ContractorContractId = contractorContractId,
            ContractTitle = contractTitle,
            TransactionDate = date,
            Amount = amount,
            Discount = 0,
            Shipping = 0,
            VAT = 0,
            TransactionType = "نقدی",
            Direction = DirectionTypes.Outgoing,
            Category = category,
            CostType = costType,
            TafsiliCode = tafsili?.Code,
            TafsiliTitle = tafsili?.Name,
            RecipientName = partyName,
            Description = item.Description.Trim()
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  ابزارهای داخلی
    // ═══════════════════════════════════════════════════════════
    private static int FindCol(Dictionary<int, string> headers, int fallback, string prefix)
    {
        foreach (var kv in headers.OrderBy(k => k.Key))
            if (kv.Value.StartsWith(prefix, StringComparison.Ordinal)) return kv.Key;
        return fallback;
    }

    private static string CellText(IXLCell cell)
    {
        if (cell is null || cell.IsEmpty()) return "";
        try
        {
            if (cell.DataType == XLDataType.Number)
                return cell.GetDouble().ToString("0.####", CultureInfo.InvariantCulture);
            return StringHelper.NormalizePersianText(cell.GetString());
        }
        catch { return ""; }
    }

    private static string NormalizeKey(string? s)
        => StringHelper.ConvertToEnglishDigits(StringHelper.NormalizePersianText(s)).Replace(" ", "").ToUpperInvariant();

    private static string DupKey(DateTime date, decimal amount, string? description)
        => $"{date:yyyyMMdd}|{amount:0.##}|{StringHelper.NormalizePersianText(description).Replace(" ", "")}";

    private static ImportKind? MapKind(string? raw)
    {
        string s = StringHelper.NormalizePersianText(raw).Replace(" ", "");
        if (s.Length == 0) return null;

        if (s.Contains("اداری")) return ImportKind.Admin;
        if (s.Contains("حقوق") || s.Contains("مساعده")) return ImportKind.Payroll;
        if (s.Contains("فاکتور") || s.Contains("بدونقرارداد")) return ImportKind.Informal;
        if (s.Contains("فروشنده") || s.Contains("کالا") || s.Contains("خریدمستقیم")) return ImportKind.Seller;
        if (s.Contains("پیمانکار")) return ImportKind.Contractor;
        if (s.Contains("پروژه")) return ImportKind.Project;
        return null;
    }

    private static bool TryParseAmount(IXLCell cell, out decimal value)
    {
        value = 0;
        if (cell is null || cell.IsEmpty()) return false;
        try
        {
            if (cell.DataType == XLDataType.Number)
            {
                value = Math.Round((decimal)cell.GetDouble(), 0, MidpointRounding.AwayFromZero);
                return true;
            }
            string s = StringHelper.ConvertToEnglishDigits(cell.GetString())
                .Replace(",", "").Replace("٬", "").Replace("،", "").Replace(" ", "").Replace("٫", ".").Trim();
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
            {
                value = Math.Round(v, 0, MidpointRounding.AwayFromZero);
                return true;
            }
        }
        catch (Exception ex) { LogHelper.LogException(ex, "TankhahExcelImporter.TryParseAmount"); }
        return false;
    }

    private static DateTime? ParseDate(IXLCell cell)
    {
        if (cell is null || cell.IsEmpty()) return null;
        try
        {
            if (cell.DataType == XLDataType.DateTime)
            {
                var d = cell.GetDateTime();
                // اکسل «1404/10/23» تایپ‌شده را گاهی به‌اشتباه تاریخ میلادی سال ۱۴۰۴ می‌خواند؛ همان اجزا را شمسی حساب می‌کنیم
                if (d.Year >= 1300 && d.Year <= 1600)
                    return DateHelper.ParsePersianDate($"{d.Year:0000}/{d.Month:00}/{d.Day:00}");
                return d.Date;
            }

            string s = StringHelper.ConvertToEnglishDigits(
                cell.DataType == XLDataType.Number
                    ? cell.GetDouble().ToString("0", CultureInfo.InvariantCulture)
                    : cell.GetString()).Trim();

            s = s.Replace("-", "/").Replace(".", "/").Replace("\\", "/");
            if (Regex.IsMatch(s, @"^\d{8}$")) s = $"{s.Substring(0, 4)}/{s.Substring(4, 2)}/{s.Substring(6, 2)}";

            return DateHelper.ParsePersianDate(s);
        }
        catch (Exception ex) { LogHelper.LogException(ex, "TankhahExcelImporter.ParseDate"); }
        return null;
    }

    private static Tafsili? FindTafsili(IReadOnlyList<Tafsili> all, string text)
    {
        string key = NormalizeKey(text);
        if (key.Length == 0) return null;

        var byCode = all.FirstOrDefault(t => NormalizeKey(t.Code) == key);
        if (byCode is not null) return byCode;

        string nameKey = StringHelper.NormalizePersianText(text).Replace(" ", "");
        var exact = all.FirstOrDefault(t => StringHelper.NormalizePersianText(t.Name).Replace(" ", "") == nameKey);
        if (exact is not null) return exact;

        // «کد - نام» مثل نمایش داخل فرم
        var byDisplay = all.FirstOrDefault(t => StringHelper.NormalizePersianText(t.DisplayText).Replace(" ", "") == nameKey);
        if (byDisplay is not null) return byDisplay;

        // تطبیق جزئی فقط وقتی یکتا باشد
        var partial = all.Where(t => StringHelper.NormalizePersianText(t.Name).Replace(" ", "").Contains(nameKey)).Take(2).ToList();
        return partial.Count == 1 ? partial[0] : null;
    }
}
