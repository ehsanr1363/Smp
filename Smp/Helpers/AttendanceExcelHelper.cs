using ClosedXML.Excel;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Smp.Helpers;

/// <summary>
/// خروجی قالب اکسل کارکرد (با اطلاعات فعلی ماه) و ورود کارکرد از اکسل.
/// ورود، مقادیر را فقط روی لیست بارگذاری‌شده‌ی فرم می‌نشاند؛ ذخیره در دیتابیس با دکمه‌ی «ذخیره کارکردها» انجام می‌شود.
/// </summary>
public static class AttendanceExcelHelper
{
    private enum Kind { Int, Decimal, Time }

    private sealed class Col
    {
        public string Header = "";
        public string[] Aliases = Array.Empty<string>();
        public Kind Kind;
        public Action<Attendance, decimal> Set = (_, _) => { };
        public Func<Attendance, string> Get = _ => "";
    }

    public sealed class ImportResult
    {
        public int SheetsRead { get; set; }
        public int RowsRead { get; set; }
        public List<Attendance> Updated { get; } = new();
        public List<string> NotFound { get; } = new();
        public List<string> Warnings { get; } = new();
    }

    // ───────────────────────── تعریف ستون‌ها ─────────────────────────
    private static Col Int(string h, Func<Attendance, int> get, Action<Attendance, int> set, params string[] aliases)
        => new() { Header = h, Aliases = aliases, Kind = Kind.Int, Set = (a, v) => set(a, (int)Math.Round(v)), Get = a => get(a).ToString(CultureInfo.InvariantCulture) };

    private static Col Dec(string h, Func<Attendance, decimal> get, Action<Attendance, decimal> set, params string[] aliases)
        => new() { Header = h, Aliases = aliases, Kind = Kind.Decimal, Set = set, Get = a => get(a).ToString("0.##", CultureInfo.InvariantCulture) };

    private static Col Tm(string h, Func<Attendance, decimal> get, Action<Attendance, decimal> set, params string[] aliases)
        => new() { Header = h, Aliases = aliases, Kind = Kind.Time, Set = set, Get = a => TimeHelper.FormatToTimeString(get(a)) };

    private static readonly Col cWorking = Int("کل کارکرد (روز)", a => a.WorkingDays, (a, v) => a.WorkingDays = Math.Max(0, v), "روزهای کارکرد", "کارکرد", "کارکرد روز");
    private static readonly Col cWorkingDaily = Int("روزهای عادی", a => a.WorkingDays, (a, v) => a.WorkingDays = Math.Max(0, v), "روزهای کارکرد", "کل کارکرد", "کارکرد");
    private static readonly Col cHoliday = Int("روزهای تعطیل", a => a.HolidayDays, (a, v) => a.HolidayDays = Math.Max(0, v), "روزهای تعطیل کارکرده", "تعطیلی");
    private static readonly Col cHours = Dec("تعداد ساعات کارکرد", a => a.HoursWorked, (a, v) => a.HoursWorked = Math.Max(0, v), "ساعات کارکرد", "ساعت کارکرد");
    private static readonly Col cPresence = Tm("حضور کل (ساعت)", a => a.TotalPresence, (a, v) => a.TotalPresence = v, "حضور کل", "جمع حضور");
    private static readonly Col cGross = Tm("اضافه کار ناخالص", a => a.GrossOvertime, (a, v) => a.GrossOvertime = v, "اضافه کاری ناخالص");
    private static readonly Col cAbsence = Tm("کسر کار (ساعت)", a => a.AbsenceHours, (a, v) => a.AbsenceHours = v, "کسر کار", "کسرکار");
    private static readonly Col cEarly = Tm("اضافه کار اول وقت", a => a.EarlyOvertime, (a, v) => a.EarlyOvertime = v, "اضافه کاری اول وقت");
    private static readonly Col cOther = Tm("سایر اضافه کار", a => a.OtherOvertime, (a, v) => a.OtherOvertime = v, "سایر اضافه کاری");
    private static readonly Col cPayable = Tm("اضافه کار قابل پرداخت", a => a.PayableOvertime, (a, v) => a.PayableOvertime = v, "اضافه کاری قابل پرداخت");
    private static readonly Col cHourlyLeave = Dec("مرخصی ساعتی", a => a.HourlyLeave, (a, v) => a.HourlyLeave = v);
    private static readonly Col cDailyLeave = Int("مرخصی روزانه", a => a.DailyLeave, (a, v) => a.DailyLeave = Math.Max(0, v));
    private static readonly Col cLunch = Int("روزهای نهاری", a => a.LunchDays, (a, v) => a.LunchDays = Math.Max(0, v), "نهاری");

    // ستون‌هایی که هر برگه دارد (همان ستون‌های قابل‌ویرایش هر تب در فرم)
    private static readonly Col[] ContractualCols = { cWorking, cPresence, cGross, cAbsence, cEarly, cOther, cPayable, cHourlyLeave, cDailyLeave, cLunch };
    private static readonly Col[] DailyCols = { cWorkingDaily, cHoliday, cPresence, cEarly };
    private static readonly Col[] HourlyCols = { cHours };
    private static readonly Col[] AllCols = { cWorking, cWorkingDaily, cHoliday, cHours, cPresence, cGross, cAbsence, cEarly, cOther, cPayable, cHourlyLeave, cDailyLeave, cLunch };

    private const string CodeHeader = "کد پرسنلی";
    private const string NameHeader = "نام و نام خانوادگی";

    // ───────────────────────── خروجی قالب ─────────────────────────
    private static readonly string[] MonthNames =
        { "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };

    public static void ExportTemplate(string path, int year, int month,
        IEnumerable<Attendance> contractual, IEnumerable<Attendance> daily, IEnumerable<Attendance> hourly,
        decimal normalDayHours = 8.5m, decimal holidayDayHours = 7m)
    {
        using var wb = new XLWorkbook();
        AddDailySheet(wb, year, month, daily, normalDayHours, holidayDayHours);
        AddSheet(wb, "قراردادی-ماهانه", ContractualCols, contractual);
        AddSheet(wb, "ساعتی", HourlyCols, hourly);
        wb.SaveAs(path);
    }

    private static string MonthName(int m) => (m >= 1 && m <= 12) ? MonthNames[m - 1] : m.ToString();

    /// <summary>
    /// برگه‌ی کارگران روزمزد دقیقاً به شکل فرم کارکرد کارگری: عنوان، نام، روزهای عادی/تعطیل، جمع کارکرد (فرمول)،
    /// موظفی (فرمول)، حضور کل، اضافه کار اول وقت، اضافه کار (کسر کار) (فرمول) و ساعت روز عادی/تعطیل در I3 و J3.
    /// </summary>
    private static void AddDailySheet(XLWorkbook wb, int year, int month, IEnumerable<Attendance> rows, decimal normalHours, decimal holidayHours)
    {
        const string TimeFmt = "[hh]:mm";
        var ws = wb.Worksheets.Add("روزمزد");
        ws.RightToLeft = true;
        ws.Style.Font.FontName = "Tahoma";

        ws.Range("A1:H1").Merge();
        var title = ws.Cell(1, 1);
        title.Value = $"کارکرد {MonthName(month)} ماه {year} کارگران روزمزد";
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 13;
        title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Row(1).Height = 28;

        string[] headers = { "نام و نام خانوادگی", "روزهای عادی", "روزهای تعطیل", "جمع کارکرد", "موظفی", "حضور کل", "اضافه کار اول وقت", "اضافه کار ( کسر کار )", "روز عادی", "روز تعطیل" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(2, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        ws.Row(2).Height = 26;

        // ساعت روز عادی و تعطیل (از تنظیمات حقوق همان سال) — فرمول موظفی به این دو سلول ارجاع می‌دهد
        ws.Cell(3, 9).SetValue((double)(normalHours / 24m));
        ws.Cell(3, 10).SetValue((double)(holidayHours / 24m));
        ws.Cell(3, 9).Style.NumberFormat.Format = TimeFmt;
        ws.Cell(3, 10).Style.NumberFormat.Format = TimeFmt;

        int r = 3;
        foreach (var a in rows)
        {
            ws.Cell(r, 1).SetValue(a.EmployeeName ?? "");
            ws.Cell(r, 2).SetValue(a.WorkingDays);
            ws.Cell(r, 3).SetValue(a.HolidayDays);
            ws.Cell(r, 4).FormulaA1 = $"B{r}+C{r}";
            ws.Cell(r, 5).FormulaA1 = $"(B{r}*$I$3)+(C{r}*$J$3)";
            ws.Cell(r, 6).SetValue((double)(a.TotalPresence / 24m));
            ws.Cell(r, 7).SetValue((double)(a.EarlyOvertime / 24m));
            ws.Cell(r, 8).FormulaA1 = $"(F{r}-G{r})-E{r}";
            foreach (int c in new[] { 5, 6, 7, 8 }) ws.Cell(r, c).Style.NumberFormat.Format = TimeFmt;
            r++;
        }

        ws.Range(2, 1, Math.Max(r - 1, 3), 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Column(1).Width = 26;
        for (int c = 2; c <= 10; c++) ws.Column(c).Width = 14;
        ws.SheetView.FreezeRows(2);
    }

    private static void AddSheet(XLWorkbook wb, string name, Col[] cols, IEnumerable<Attendance> rows)
    {
        var ws = wb.Worksheets.Add(name);
        ws.RightToLeft = true;

        var headers = new List<string> { CodeHeader, NameHeader };
        headers.AddRange(cols.Select(c => c.Header));
        for (int i = 0; i < headers.Count; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontName = "Tahoma";
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        ws.Row(1).Height = 30;

        // کد پرسنلی و ستون‌های ساعت:دقیقه به‌صورت متن، تا اکسل «209:38» را به زمان تبدیل نکند
        ws.Column(1).Style.NumberFormat.Format = "@";
        for (int c = 0; c < cols.Length; c++)
            if (cols[c].Kind == Kind.Time) ws.Column(c + 3).Style.NumberFormat.Format = "@";

        int r = 2;
        foreach (var a in rows)
        {
            ws.Cell(r, 1).SetValue(a.PersonnelCode ?? "");
            ws.Cell(r, 2).SetValue(a.EmployeeName ?? "");
            for (int c = 0; c < cols.Length; c++)
            {
                var cell = ws.Cell(r, c + 3);
                string txt = cols[c].Get(a);
                if (cols[c].Kind == Kind.Time) cell.SetValue(txt);
                else if (decimal.TryParse(txt, NumberStyles.Any, CultureInfo.InvariantCulture, out var num)) cell.SetValue(num);
            }
            r++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    // ───────────────────────── ورود از اکسل ─────────────────────────
    public static ImportResult ImportInto(string path, IList<Attendance> target)
    {
        var result = new ImportResult();

        // کد پرسنلی → ردیف کارکرد (با و بدون صفرهای ابتدایی)
        var byCode = new Dictionary<string, Attendance>(StringComparer.Ordinal);
        // نام → ردیف‌ها (فرم کارگری کد پرسنلی ندارد و با نام تطبیق داده می‌شود)
        var byName = new Dictionary<string, List<Attendance>>(StringComparer.Ordinal);
        foreach (var a in target)
        {
            string k = NormCode(a.PersonnelCode);
            if (k.Length > 0 && !byCode.ContainsKey(k)) byCode[k] = a;

            string n = NormName(a.EmployeeName);
            if (n.Length > 0)
            {
                if (!byName.TryGetValue(n, out var list)) byName[n] = list = new List<Attendance>();
                list.Add(a);
            }
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var wb = new XLWorkbook(stream);

        var touched = new HashSet<Attendance>();
        var notFound = new HashSet<string>(StringComparer.Ordinal);

        foreach (var ws in wb.Worksheets)
        {
            var used = ws.RangeUsed();
            if (used is null) continue;

            int firstRow = used.FirstRow().RowNumber();
            int lastRow = used.LastRow().RowNumber();
            int firstCol = used.FirstColumn().ColumnNumber();
            int lastCol = used.LastColumn().ColumnNumber();

            // ردیف عنوان ستون‌ها: اولین ردیفی (در ۱۵ ردیف اول) که ستون «کد پرسنلی» یا «نام و نام خانوادگی» دارد.
            // ردیف‌های بالاتر (مثلاً عنوان «کارکرد آبان ماه کارگران روزمزد») نادیده گرفته می‌شوند.
            int headerRow = -1, codeCol = -1, nameCol = -1;
            for (int r = firstRow; r <= Math.Min(lastRow, firstRow + 14) && headerRow < 0; r++)
            {
                for (int c = firstCol; c <= lastCol; c++)
                {
                    string h = NormHeader(Text(ws.Cell(r, c)));
                    if (h.Length == 0) continue;
                    if (codeCol < 0 && (h == NormHeader(CodeHeader) || h == "کدپرسنلی" || h == "شمارهپرسنلی")) codeCol = c;
                    else if (nameCol < 0 && (h == NormHeader(NameHeader) || h == "نامونامخانوادگی" || h == "نام")) nameCol = c;
                }
                if (codeCol >= 0 || nameCol >= 0) headerRow = r;
            }

            if (headerRow < 0) { result.Warnings.Add($"برگه «{ws.Name}»: ستون «{NameHeader}» یا «{CodeHeader}» پیدا نشد و نادیده گرفته شد."); continue; }

            // ستون‌ها از روی عنوان شناسایی می‌شوند، نه ترتیب. ستون‌های محاسباتی (جمع کارکرد، موظفی، اضافه کار (کسر کار)) و
            // ستون‌های نامرتبط خوانده نمی‌شوند؛ مقدارشان بعد از ورود از روی بقیه دوباره محاسبه می‌شود.
            var colMap = new Dictionary<int, Col>();
            for (int c = firstCol; c <= lastCol; c++)
            {
                if (c == codeCol || c == nameCol) continue;
                string h = NormHeader(Text(ws.Cell(headerRow, c)));
                if (h.Length == 0) continue;
                var def = AllCols.FirstOrDefault(d => NormHeader(d.Header) == h || d.Aliases.Any(al => NormHeader(al) == h));
                if (def is not null && !colMap.Values.Any(v => v.Header == def.Header)) colMap[c] = def;
            }
            if (colMap.Count == 0) { result.Warnings.Add($"برگه «{ws.Name}»: هیچ ستون کارکردی شناخته نشد."); continue; }

            result.SheetsRead++;
            for (int r = headerRow + 1; r <= lastRow; r++)
            {
                Attendance? att = null;
                string label = "";

                if (codeCol >= 0)
                {
                    string rawCode = Text(ws.Cell(r, codeCol));
                    string code = NormCode(rawCode);
                    if (code.Length > 0)
                    {
                        label = rawCode;
                        byCode.TryGetValue(code, out att);
                    }
                }

                if (att is null && nameCol >= 0)
                {
                    string rawName = Text(ws.Cell(r, nameCol));
                    string n = NormName(rawName);
                    if (n.Length > 0)
                    {
                        label = rawName;
                        if (byName.TryGetValue(n, out var list))
                        {
                            if (list.Count == 1) att = list[0];
                            else
                            {
                                result.RowsRead++;
                                result.Warnings.Add($"برگه «{ws.Name}» ردیف {r}: چند نفر با نام «{rawName}» وجود دارد؛ برای این ردیف از ستون «{CodeHeader}» استفاده کنید.");
                                continue;
                            }
                        }
                    }
                }

                if (label.Length == 0) continue;   // ردیف خالی
                result.RowsRead++;

                if (att is null) { notFound.Add(label); continue; }

                foreach (var kv in colMap)
                {
                    var cell = ws.Cell(r, kv.Key);
                    if (cell.IsEmpty()) continue;   // سلول خالی = بدون تغییر
                    if (!TryRead(cell, kv.Value.Kind, out decimal v))
                    {
                        result.Warnings.Add($"برگه «{ws.Name}» ردیف {r} ستون «{kv.Value.Header}»: مقدار «{Text(cell)}» قابل‌خواندن نیست.");
                        continue;
                    }
                    kv.Value.Set(att, v);
                    touched.Add(att);
                }
            }
        }

        result.Updated.AddRange(touched);
        result.NotFound.AddRange(notFound);
        return result;
    }

    // ───────────────────────── کمکی‌ها ─────────────────────────
    private static string Text(IXLCell cell)
    {
        if (cell is null || cell.IsEmpty()) return "";
        try
        {
            return cell.DataType == XLDataType.Number
                ? cell.GetDouble().ToString("0.####", CultureInfo.InvariantCulture)
                : cell.GetString().Trim();
        }
        catch { try { return cell.GetString().Trim(); } catch { return ""; } }
    }

    private static string NormCode(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        string t = StringHelper.ConvertToEnglishDigits(s).Trim();
        if (t.EndsWith(".0")) t = t.Substring(0, t.Length - 2);   // کدی که اکسل عددی ذخیره کرده
        return t.TrimStart('0');
    }

    private static string NormName(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char ch in s)
        {
            if (char.IsWhiteSpace(ch) || ch == '\u200c' || ch == '\u200f' || ch == '\u200e') continue;
            sb.Append(ch == 'ي' ? 'ی' : ch == 'ك' ? 'ک' : char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    private static string NormHeader(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        return s.Replace('ي', 'ی').Replace('ك', 'ک')
                .Replace(" ", "").Replace("\u200c", "").Replace("(", "").Replace(")", "").Replace("_", "")
                .ToLowerInvariant();
    }

    /// <summary>
    /// مقدار سلول را به عدد تبدیل می‌کند. ستون‌های «ساعت:دقیقه» هم متن (209:38)، هم زمان واقعی اکسل
    /// (مثلاً قالب [h]:mm که اکسل آن را کسری از شبانه‌روز ذخیره می‌کند) و هم عدد اعشاری را می‌پذیرند.
    /// </summary>
    private static bool TryRead(IXLCell cell, Kind kind, out decimal value)
    {
        value = 0;
        try
        {
            if (kind == Kind.Time)
            {
                // قالب [hh]:mm اکسل، زمان را «کسری از شبانه‌روز» ذخیره می‌کند (۲۵۵:۰۷ ساعت = ۱۰٫۶۳ روز)
                if (cell.DataType == XLDataType.TimeSpan) { value = (decimal)cell.GetTimeSpan().TotalHours; return true; }

                if (cell.DataType == XLDataType.Number || cell.DataType == XLDataType.DateTime)
                {
                    string fmt = (cell.Style.NumberFormat.Format ?? "").ToLowerInvariant();
                    bool isTimeFmt = fmt.Contains('h') || fmt.Contains(':');
                    try
                    {
                        double d = cell.GetDouble();   // عدد خام اکسل (روز)
                        value = Math.Round((decimal)(isTimeFmt ? d * 24d : d), 4);
                        return true;
                    }
                    catch when (cell.DataType == XLDataType.DateTime)
                    {
                        var dt = cell.GetDateTime();
                        value = (decimal)(dt - new DateTime(1899, 12, 30)).TotalHours;
                        return true;
                    }
                }

                string txt = StringHelper.ConvertToEnglishDigits(cell.GetString().Trim());
                if (txt.Length == 0) return false;
                value = TimeHelper.ParseTimeString(txt);
                return !(value == 0 && !IsZeroText(txt));
            }

            if (cell.DataType == XLDataType.Number) { value = (decimal)cell.GetDouble(); return true; }

            string s = StringHelper.ConvertToEnglishDigits(cell.GetString().Trim()).Replace(",", "").Replace("٬", "").Replace("٫", ".");
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }
        catch { return false; }
    }

    private static bool IsZeroText(string t)
    {
        t = t.Trim();
        return t == "0" || t == "0:00" || t == "00:00" || t == "0:0" || t == "0.0" || t == "-";
    }
}
