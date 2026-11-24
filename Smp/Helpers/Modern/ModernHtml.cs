using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Smp.Helpers;

// ═══════════════════════════════════════════════════════════════════════
//  ModernHtml — قالب مدرن برای گزارش‌ها و چاپ‌های HTML (WebBrowser / WebView2 / PDF)
//  راست‌به‌چپ، A4، جدول زبرا، کارت‌های خلاصه، آماده‌ی چاپ.
//
//     string body = ModernHtml.Header("گزارش قراردادها", "از ۱۴۰۵/۰۱/۰۱ تا ۱۴۰۵/۰۶/۳۱")
//                 + ModernHtml.Stats(("جمع قراردادها","۱۲"), ("جمع مبلغ","۱٬۲۰۰٬۰۰۰"))
//                 + ModernHtml.Table(headers, rows);
//     string html = ModernHtml.Document("گزارش", body);
// ═══════════════════════════════════════════════════════════════════════
public static class ModernHtml
{
    public static string Encode(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    public const string Css = @"
:root{--bg:#fdfbf7;--surface:#fff;--border:#ebe1d7;--text:#3c322d;--muted:#64748b;--head:#f6efe7;--head-text:#5a3c28;--zebra:#fffcf8;--primary:#3b82f6;--danger:#ef4444;--success:#16a34a}
*{box-sizing:border-box}
html,body{margin:0;padding:0}
body{font-family:'Vazirmatn','IRANSans','B Nazanin',Tahoma,sans-serif;direction:rtl;color:var(--text);background:var(--bg);font-size:12.5px;line-height:1.7}
.page{max-width:1100px;margin:0 auto;padding:22px 26px}
.hdr{display:flex;align-items:center;justify-content:space-between;border-bottom:2px solid var(--border);padding-bottom:12px;margin-bottom:16px}
.hdr h1{margin:0;font-size:21px}
.hdr .sub{color:var(--muted);font-size:12px;margin-top:2px}
.hdr img{height:56px}
.stats{display:flex;gap:10px;flex-wrap:wrap;margin:0 0 16px}
.stat{flex:1;min-width:150px;background:var(--surface);border:1px solid var(--border);border-right:5px solid var(--primary);border-radius:12px;padding:10px 14px}
.stat .t{font-size:11.5px;color:var(--muted);font-weight:bold}
.stat .v{font-size:18px;font-weight:bold;margin-top:2px}
.stat.neg .v{color:var(--danger)}
table{width:100%;border-collapse:collapse;background:var(--surface);border:1px solid var(--border);border-radius:10px;overflow:hidden}
th{background:var(--head);color:var(--head-text);font-size:12px;padding:9px 8px;text-align:center;border-bottom:1px solid var(--border)}
td{padding:7px 8px;text-align:center;border-bottom:1px solid #f0e8e0}
tbody tr:nth-child(even) td{background:var(--zebra)}
td.num{direction:ltr;font-variant-numeric:tabular-nums}
td.neg{color:var(--danger)}
tfoot td{background:var(--head);font-weight:bold;border-top:2px solid var(--border)}
.badge{display:inline-block;padding:1px 10px;border-radius:999px;font-size:11px;font-weight:bold}
.badge.ok{background:#dcfce7;color:#166534}.badge.warn{background:#fff3cd;color:#856404}.badge.bad{background:#fee2e2;color:#991b1b}
.sign{display:flex;justify-content:space-between;margin-top:46px}
.sign div{width:30%;text-align:center;border-top:1px solid var(--border);padding-top:6px;color:var(--muted)}
.foot{margin-top:20px;color:var(--muted);font-size:11px;text-align:center}
@page{size:A4;margin:12mm}
@media print{body{background:#fff}.page{padding:0;max-width:none}.noprint{display:none}tr{page-break-inside:avoid}thead{display:table-header-group}}
";

    /// <summary>سند کامل HTML با CSS مدرن</summary>
    public static string Document(string title, string bodyHtml, string extraCss = "")
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"fa\" dir=\"rtl\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">");
        sb.Append("<title>").Append(Encode(title)).Append("</title><style>").Append(Css).Append(extraCss).Append("</style></head><body><div class=\"page\">");
        sb.Append(bodyHtml);
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    /// <summary>سربرگ گزارش؛ logoDataUri مثلاً «data:image/png;base64,...»</summary>
    public static string Header(string title, string? subtitle = null, string? logoDataUri = null)
    {
        var sb = new StringBuilder("<div class=\"hdr\"><div><h1>");
        sb.Append(Encode(title)).Append("</h1>");
        if (!string.IsNullOrWhiteSpace(subtitle)) sb.Append("<div class=\"sub\">").Append(Encode(subtitle)).Append("</div>");
        sb.Append("</div>");
        if (!string.IsNullOrWhiteSpace(logoDataUri)) sb.Append("<img src=\"").Append(logoDataUri).Append("\" alt=\"\">");
        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>کارت‌های خلاصه: هر عضو (عنوان، مقدار)</summary>
    public static string Stats(params (string Title, string Value)[] items)
    {
        var sb = new StringBuilder("<div class=\"stats\">");
        foreach (var (t, v) in items)
        {
            bool neg = v.TrimStart().StartsWith("-");
            sb.Append("<div class=\"stat").Append(neg ? " neg" : "").Append("\"><div class=\"t\">")
              .Append(Encode(t)).Append("</div><div class=\"v\">").Append(Encode(v)).Append("</div></div>");
        }
        return sb.Append("</div>").ToString();
    }

    /// <summary>
    /// جدول: ستون‌هایی که اعداد دارند را با numericColumns (اندیس صفر-مبنا) مشخص کنید تا چپ‌به‌راست و منفی‌ها قرمز شوند.
    /// footer (اختیاری) ردیف جمع است.
    /// </summary>
    public static string Table(IList<string> headers, IEnumerable<IList<string>> rows,
                               ISet<int>? numericColumns = null, IList<string>? footer = null)
    {
        var sb = new StringBuilder("<table><thead><tr>");
        foreach (var h in headers) sb.Append("<th>").Append(Encode(h)).Append("</th>");
        sb.Append("</tr></thead><tbody>");

        foreach (var row in rows)
        {
            sb.Append("<tr>");
            for (int i = 0; i < row.Count; i++)
                sb.Append(Cell(row[i], numericColumns is not null && numericColumns.Contains(i)));
            sb.Append("</tr>");
        }
        sb.Append("</tbody>");

        if (footer is not null)
        {
            sb.Append("<tfoot><tr>");
            for (int i = 0; i < footer.Count; i++)
                sb.Append("<td").Append(numericColumns is not null && numericColumns.Contains(i) ? " class=\"num\"" : "").Append(">")
                  .Append(Encode(footer[i])).Append("</td>");
            sb.Append("</tr></tfoot>");
        }
        return sb.Append("</table>").ToString();
    }

    private static string Cell(string value, bool numeric)
    {
        if (!numeric) return "<td>" + Encode(value) + "</td>";
        bool neg = value.TrimStart().StartsWith("-");
        return "<td class=\"num" + (neg ? " neg" : "") + "\">" + Encode(value) + "</td>";
    }

    /// <summary>نشان رنگی وضعیت. kind: ok / warn / bad</summary>
    public static string Badge(string text, string kind = "ok")
        => "<span class=\"badge " + Encode(kind) + "\">" + Encode(text) + "</span>";

    /// <summary>محل امضاها در پایین گزارش</summary>
    public static string Signatures(params string[] names)
    {
        var sb = new StringBuilder("<div class=\"sign\">");
        foreach (var n in names) sb.Append("<div>").Append(Encode(n)).Append("</div>");
        return sb.Append("</div>").ToString();
    }
}
