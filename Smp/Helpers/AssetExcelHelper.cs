using ClosedXML.Excel;
using Smp.Data;
using Smp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Smp.Helpers;

/// <summary>نتیجه‌ی خواندن فایل اکسل اموال (قبل از ذخیره)</summary>
public class AssetImportResult
{
    public List<Asset> Valid { get; } = new();
    public List<string> Errors { get; } = new();     // ردیف‌های ردشده
    public List<string> Warnings { get; } = new();   // ردیف‌های واردشده با هشدار (واحد/تحویل‌گیرنده‌ی نامعتبر)
}

/// <summary>قالب و ایمپورت اکسل اموال</summary>
public static class AssetExcelHelper
{
    private static readonly string[] Headers =
    {
        "کد اموال (خالی = خودکار)",     // 1
        "نام مال *",                      // 2
        "دسته‌بندی (از لیست دسته‌بندی اموال)", // 3
        "شماره سریال",                    // 4
        "وضعیت (فعال/در تعمیر/اسقاط/فروخته‌شده)", // 5
        "تاریخ خرید (شمسی 1403/01/01)",   // 6
        "مبلغ خرید (ریال)",               // 7
        "واحد / بخش (نام)",               // 8
        "تحویل‌گیرنده (نام و نام خانوادگی)", // 9
        "محل نگهداری",                    // 10
        "توضیحات"                         // 11
    };

    public static void SaveTemplate(string path)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("اموال");
        ws.RightToLeft = true;

        for (int i = 0; i < Headers.Length; i++)
        {
            var c = ws.Cell(1, i + 1);
            c.Value = Headers[i];
            c.Style.Font.Bold = true;
            c.Style.Fill.BackgroundColor = XLColor.FromHtml("#E9E3F8");
            c.Style.Alignment.WrapText = true;
            c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Column(i + 1).Width = i == 1 || i == 10 ? 30 : 22;
        }
        ws.Row(1).Height = 36;

        // یک ردیف نمونه
        ws.Cell(2, 2).Value = "لپ‌تاپ Lenovo ThinkPad";
        ws.Cell(2, 3).Value = "کامپیوتر";
        ws.Cell(2, 4).Value = "SN-12345";
        ws.Cell(2, 5).Value = AssetStatuses.Active;
        ws.Cell(2, 6).Style.NumberFormat.Format = "@";
        ws.Cell(2, 6).Value = "1403/05/20";
        ws.Cell(2, 7).Value = 450000000;
        ws.Cell(2, 8).Value = "دفتر مرکزی";
        ws.Cell(2, 10).Value = "طبقه ۲ - اتاق ۲۰۵";

        // ستون تاریخ و کد به صورت متن
        ws.Column(1).Style.NumberFormat.Format = "@";
        ws.Column(6).Style.NumberFormat.Format = "@";
        ws.SheetView.FreezeRows(1);
        wb.SaveAs(path);
    }

    private static string Norm(string? s) => PersianText.CollapseWords(s);

    private static string Text(IXLCell c)
    {
        if (c.IsEmpty()) return "";
        return c.DataType == XLDataType.Number
            ? c.GetDouble().ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            : c.GetFormattedString().Trim();
    }

    public static async Task<AssetImportResult> ParseAsync(string path, AssetRepository repo)
    {
        var result = new AssetImportResult();

        var depts = (await repo.GetDepartmentsAsync()).GroupBy(d => Norm(d.Name)).ToDictionary(g => g.Key, g => g.First());
        var emps = (await repo.GetEmployeesAsync()).GroupBy(e => Norm(e.Name)).ToDictionary(g => g.Key, g => g.First());
        var cats = (await repo.GetCategoriesAsync()).GroupBy(Norm).ToDictionary(g => g.Key, g => g.First());
        var existingCodes = new HashSet<string>(await repo.GetAllCodesAsync(), StringComparer.OrdinalIgnoreCase);
        var fileCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        int last = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (int r = 2; r <= last; r++)
        {
            var row = ws.Row(r);
            if (row.IsEmpty()) continue;

            string code = Text(row.Cell(1));
            string name = Text(row.Cell(2));
            if (name.Length == 0) { result.Errors.Add($"ردیف {r}: نام مال خالی است."); continue; }

            if (code.Length > 0)
            {
                if (existingCodes.Contains(code)) { result.Errors.Add($"ردیف {r}: کد «{code}» قبلاً در سیستم ثبت شده است."); continue; }
                if (!fileCodes.Add(code)) { result.Errors.Add($"ردیف {r}: کد «{code}» در همین فایل تکراری است."); continue; }
            }

            // وضعیت
            string status = Norm(Text(row.Cell(5)));
            if (status.Length == 0) status = AssetStatuses.Active;
            else
            {
                var match = AssetStatuses.All.FirstOrDefault(s => Norm(s) == status || Norm(s).Replace(" ", "") == status.Replace(" ", ""));
                if (match is null) { result.Errors.Add($"ردیف {r}: وضعیت «{status}» نامعتبر است."); continue; }
                status = match;
            }

            // تاریخ خرید
            DateTime? date = null;
            var dc = row.Cell(6);
            if (!dc.IsEmpty())
            {
                if (dc.DataType == XLDataType.DateTime) date = dc.GetDateTime();
                else
                {
                    date = DateHelper.ParsePersianDate(Text(dc));
                    if (date is null) { result.Errors.Add($"ردیف {r}: تاریخ خرید «{Text(dc)}» نامعتبر است."); continue; }
                }
            }

            // مبلغ
            decimal amount = 0;
            var ac = row.Cell(7);
            if (!ac.IsEmpty())
            {
                if (ac.DataType == XLDataType.Number) amount = (decimal)ac.GetDouble();
                else if (!NumberHelper.TryParseFormatted(Text(ac), out amount))
                { result.Errors.Add($"ردیف {r}: مبلغ «{Text(ac)}» نامعتبر است."); continue; }
                if (amount < 0) { result.Errors.Add($"ردیف {r}: مبلغ نمی‌تواند منفی باشد."); continue; }
            }

            var asset = new Asset
            {
                AssetCode = code,
                Name = name,
                SerialNumber = Text(row.Cell(4)),
                Status = status,
                PurchaseDate = date,
                PurchaseAmount = amount,
                Location = Text(row.Cell(10)),
                Description = Text(row.Cell(11))
            };

            string catText = Text(row.Cell(3));
            if (catText.Length > 0)
            {
                if (cats.TryGetValue(Norm(catText), out var cat)) asset.Category = cat;
                else result.Warnings.Add($"ردیف {r}: دسته‌بندی «{catText}» در لیست Lookup نیست (بدون دسته ثبت می‌شود). ابتدا آن را در «مدیریت Lookups ← دسته‌بندی اموال» اضافه کنید.");
            }

            string deptText = Text(row.Cell(8));
            if (deptText.Length > 0)
            {
                if (depts.TryGetValue(Norm(deptText), out var d)) { asset.DepartmentId = d.Id; asset.DepartmentName = d.Name; }
                else result.Warnings.Add($"ردیف {r}: واحد «{deptText}» پیدا نشد (بدون واحد ثبت می‌شود).");
            }

            string empText = Text(row.Cell(9));
            if (empText.Length > 0)
            {
                if (emps.TryGetValue(Norm(empText), out var e)) { asset.EmployeeId = e.Id; asset.EmployeeName = e.Name; }
                else result.Warnings.Add($"ردیف {r}: تحویل‌گیرنده «{empText}» پیدا نشد (بدون تحویل‌گیرنده ثبت می‌شود).");
            }

            result.Valid.Add(asset);
        }
        return result;
    }
}
