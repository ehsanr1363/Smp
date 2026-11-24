using Smp.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;

namespace Smp.Helpers;

public enum AttendanceGroup { Contractual = 0, Daily = 1, Hourly = 2 }

/// <summary>
/// چاپ مجزای کارکرد ماهانه (با لوگو و نام شرکت).
/// هر گروه (قراردادی / روزمزد / ساعتی) یک «بخش» مستقل است با ستون‌های مخصوص خودش،
/// سربرگ در هر صفحه تکرار می‌شود، ردیف جمع کل و جای امضا در انتهای هر بخش می‌آید
/// و هر بخش از صفحه‌ی جدید شروع می‌شود (شماره صفحه هر بخش جداگانه است).
/// </summary>
public class AttendancePrinter
{
    private enum Kind { Text, Int, Time, Dec }

    private class Col
    {
        public string Header = "";
        public float Weight = 1f;
        public Kind Kind = Kind.Text;
        public bool Left;                                // تراز به سمت راست (برای نام)
        public Func<Attendance, string>? Text;           // برای ستون‌های متنی
        public Func<Attendance, decimal>? Value;         // برای ستون‌های عددی
        public bool HasTotal => Value is not null;
    }

    private class Section
    {
        public string Title = "";
        public bool Landscape = true;
        public List<Attendance> Rows = new();
        public List<Col> Cols = new();
    }

    // ───────── ابعاد ثابت چیدمان ─────────
    private const float RowH = 22f;
    private const float HeadH = 40f;
    private const float HeaderBlockH = 84f;
    private const float FooterBandH = 26f;
    private const float SignBlockH = 84f;
    private const float RowNumW = 34f;

    private readonly string _companyName;
    private readonly Image? _logo;
    private readonly string _period;
    private readonly List<Section> _sections = new();

    public AttendancePrinter(string companyName, Image? logo, int year, int month)
    {
        _companyName = string.IsNullOrWhiteSpace(companyName) ? "شرکت" : companyName;
        _logo = logo;
        _period = StringHelper.ConvertToPersianDigits($"{TranslationHelper.GetMonthName(month)} {year}");
    }

    public bool HasRows => _sections.Count > 0;

    /// <summary>یک گروه پرسنلی را به‌عنوان بخش مجزا اضافه می‌کند (گروه خالی نادیده گرفته می‌شود)</summary>
    public void AddSection(AttendanceGroup group, IEnumerable<Attendance> rows)
    {
        var list = rows?.ToList() ?? new List<Attendance>();
        if (list.Count == 0) return;

        var s = new Section { Rows = list };
        switch (group)
        {
            case AttendanceGroup.Contractual:
                s.Title = "لیست کارکرد ماهانه پرسنل قراردادی / ماهانه";
                s.Landscape = true;
                s.Cols = ContractualCols();
                break;
            case AttendanceGroup.Daily:
                s.Title = "لیست کارکرد ماهانه پرسنل روزمزد";
                s.Landscape = true;
                s.Cols = DailyCols();
                break;
            default:
                s.Title = "لیست کارکرد ماهانه پرسنل ساعتی";
                s.Landscape = false;
                s.Cols = HourlyCols();
                break;
        }
        _sections.Add(s);
    }

    // ───────────────────────── تعریف ستون‌ها ─────────────────────────
    private static Col Code() => new() { Header = "کد پرسنلی", Weight = 1.1f, Text = a => a.PersonnelCode };
    private static Col Name() => new() { Header = "نام و نام خانوادگی", Weight = 2.6f, Left = true, Text = a => a.EmployeeName };
    private static Col IntCol(string h, Func<Attendance, decimal> v, float w = 1f) => new() { Header = h, Weight = w, Kind = Kind.Int, Value = v };
    private static Col TimeCol(string h, Func<Attendance, decimal> v, float w = 1f) => new() { Header = h, Weight = w, Kind = Kind.Time, Value = v };
    private static Col DecCol(string h, Func<Attendance, decimal> v, float w = 1f) => new() { Header = h, Weight = w, Kind = Kind.Dec, Value = v };

    private static List<Col> ContractualCols() => new()
    {
        Code(), Name(),
        IntCol("کل کارکرد (روز)", a => a.WorkingDays),
        TimeCol("حضور کل (ساعت)", a => a.TotalPresence),
        TimeCol("اضافه کار ناخالص", a => a.GrossOvertime),
        TimeCol("کسر کار (ساعت)", a => a.AbsenceHours),
        TimeCol("اضافه کار اول وقت", a => a.EarlyOvertime),
        TimeCol("سایر اضافه کار", a => a.OtherOvertime),
        TimeCol("اضافه کار (کسر کار)", a => a.NetOvertime),
        TimeCol("اضافه کار قابل پرداخت", a => a.PayableOvertime),
        DecCol("مرخصی ساعتی", a => a.HourlyLeave),
        IntCol("مرخصی روزانه", a => a.DailyLeave),
        IntCol("روزهای نهاری", a => a.LunchDays),
    };

    private static List<Col> DailyCols() => new()
    {
        Code(), Name(),
        IntCol("روزهای عادی", a => a.WorkingDays),
        IntCol("روزهای تعطیل", a => a.HolidayDays),
        IntCol("جمع کارکرد", a => a.WorkingDays + a.HolidayDays),
        TimeCol("موظفی", a => TimeHelper.ParseTimeString(a.ExpectedHoursStr)),
        TimeCol("حضور کل", a => a.TotalPresence),
        TimeCol("اضافه کار اول وقت", a => a.EarlyOvertime),
        TimeCol("اضافه کار (کسر کار)", a => a.NetOvertime),
    };

    private static List<Col> HourlyCols() => new()
    {
        Code(), Name(),
        DecCol("تعداد ساعات کارکرد", a => a.HoursWorked, 1.6f),
    };

    private static string Fmt(Kind k, decimal v)
    {
        string s = k switch
        {
            Kind.Time => TimeHelper.FormatToTimeString(v),
            Kind.Dec => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', '/'),
            _ => ((long)Math.Round(v)).ToString()
        };
        return StringHelper.ConvertToPersianDigits(s);
    }

    // ───────────────────────── صفحه‌بندی ─────────────────────────
    /// <summary>تعداد ردیف هر صفحه؛ صفحه‌ی آخر جا برای جمع کل و امضا نگه می‌دارد</summary>
    private static List<int> Plan(int rowCount, float marginHeight)
    {
        float avail = marginHeight - HeaderBlockH - HeadH - FooterBandH;
        int cap = Math.Max(2, (int)(avail / RowH));
        int reserve = (int)Math.Ceiling((RowH + SignBlockH) / RowH);
        int capLast = Math.Max(1, cap - reserve);

        var pages = new List<int>();
        int rem = rowCount;
        while (true)
        {
            if (rem <= capLast) { pages.Add(rem); break; }
            if (rem <= cap) { pages.Add(rem - 1); rem = 1; continue; }   // تا جمع کل تنها در صفحه‌ی بعد نیفتد
            pages.Add(cap);
            rem -= cap;
        }
        return pages;
    }

    // ───────────────────────── پیش‌نمایش / چاپ ─────────────────────────
    public void ShowPreview()
    {
        if (_sections.Count == 0) return;

        using var pd = new PrintDocument { DocumentName = "کارکرد ماهانه" };
        pd.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
        pd.DefaultPageSettings.Landscape = _sections[0].Landscape;

        int secIdx = 0, pageInSec = 0, rowStart = 0;
        List<int> plan = new();
        string printedAt = "تاریخ چاپ: " + StringHelper.ConvertToPersianDigits(DateHelper.ToPersianDate(DateTime.Now));

        pd.BeginPrint += (s, e) => { secIdx = 0; pageInSec = 0; rowStart = 0; plan = new(); };

        // جهت صفحه برای هر بخش جداست (ساعتی عمودی، بقیه افقی)
        pd.QueryPageSettings += (s, e) =>
        {
            if (secIdx < _sections.Count) e.PageSettings.Landscape = _sections[secIdx].Landscape;
        };

        pd.PrintPage += (s, e) =>
        {
            var g = e.Graphics;
            if (g is null || secIdx >= _sections.Count) { e.HasMorePages = false; return; }

            var sec = _sections[secIdx];
            var mb = e.MarginBounds;
            if (pageInSec == 0) { plan = Plan(sec.Rows.Count, mb.Height); rowStart = 0; }

            int count = plan[pageInSec];
            bool last = pageInSec == plan.Count - 1;

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            float y = DrawHeader(g, mb, sec, printedAt);

            // عرض ستون‌ها: از راست به چپ (ردیف، کد، نام، ...)
            float totalWeight = sec.Cols.Sum(c => c.Weight);
            float dataW = mb.Width - RowNumW;
            var widths = sec.Cols.Select(c => c.Weight / totalWeight * dataW).ToList();

            float baseFont = sec.Cols.Count >= 12 ? 8f : sec.Cols.Count >= 8 ? 9f : 10f;

            DrawTableHeader(g, mb, y, sec, widths, baseFont);
            y += HeadH;

            for (int i = 0; i < count; i++)
            {
                int idx = rowStart + i;
                DrawBodyRow(g, mb, y, sec, widths, sec.Rows[idx], idx + 1, i % 2 == 1, baseFont);
                y += RowH;
            }

            if (last)
            {
                DrawTotalRow(g, mb, y, sec, widths, baseFont);
                y += RowH;
                DrawSignatures(g, mb, y + 14);
            }

            DrawFooter(g, mb, pageInSec + 1, plan.Count);

            rowStart += count;
            pageInSec++;
            if (last)
            {
                secIdx++;
                pageInSec = 0;
                e.HasMorePages = secIdx < _sections.Count;
            }
            else e.HasMorePages = true;
        };

        ModernPrint.ShowPreview(pd, "پیش‌نمایش چاپ کارکرد");
    }

    // ───────────────────────── رسم ─────────────────────────
    private static StringFormat Sf(StringAlignment align, bool wrap = false) => new()
    {
        Alignment = align,
        LineAlignment = StringAlignment.Center,
        FormatFlags = wrap
            ? StringFormatFlags.DirectionRightToLeft
            : StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap,
        Trimming = StringTrimming.EllipsisCharacter
    };

    private float DrawHeader(Graphics g, Rectangle mb, Section sec, string printedAt)
    {
        float y = mb.Top;

        // لوگو (راست)
        float logoW = 0;
        if (_logo is not null)
        {
            try
            {
                const float box = 66f;
                float ratio = (float)_logo.Width / _logo.Height;
                float w = box, h = box;
                if (ratio >= 1f) h = box / ratio; else w = box * ratio;
                g.DrawImage(_logo, mb.Right - w, y + (box - h) / 2f, w, h);
                logoW = box + 10;
            }
            catch (Exception ex) { LogHelper.LogException(ex, "AttendancePrinter"); logoW = 0; }
        }

        float sideLeft = 170f;
        var centerRect = new RectangleF(mb.Left + sideLeft, y, mb.Width - sideLeft - Math.Max(logoW, sideLeft - 40), 24);

        using var center = Sf(StringAlignment.Center);
        // فونت‌ها از کش ModernTheme می‌آیند؛ نباید Dispose شوند
        var fCompany = ModernTheme.GetFont(16f, FontStyle.Bold);
        var fTitle = ModernTheme.GetFont(12f, FontStyle.Bold);
        var fPeriod = ModernTheme.GetFont(10.5f);
        var fSmall = ModernTheme.GetFont(8.5f);

        g.DrawString(_companyName, fCompany, Brushes.Black, new RectangleF(centerRect.X, y + 2, centerRect.Width, 26), center);
        g.DrawString(sec.Title, fTitle, Brushes.Black, new RectangleF(centerRect.X, y + 30, centerRect.Width, 22), center);
        g.DrawString("ماه: " + _period, fPeriod, Brushes.Black, new RectangleF(centerRect.X, y + 53, centerRect.Width, 20), center);

        // اطلاعات چاپ (چپ)
        using (var left = Sf(StringAlignment.Far))   // در RTL «Far» یعنی چپ
        {
            g.DrawString(printedAt, fSmall, Brushes.DimGray, new RectangleF(mb.Left, y + 4, sideLeft, 18), left);
            g.DrawString(StringHelper.ConvertToPersianDigits($"تعداد پرسنل: {sec.Rows.Count}"), fSmall, Brushes.DimGray,
                new RectangleF(mb.Left, y + 22, sideLeft, 18), left);
        }

        y += 76;
        using (var pen = new Pen(Color.Black, 1.4f)) g.DrawLine(pen, mb.Left, y, mb.Right, y);
        return mb.Top + HeaderBlockH;
    }

    private void DrawTableHeader(Graphics g, Rectangle mb, float y, Section sec, List<float> widths, float font)
    {
        using var fill = new SolidBrush(Color.FromArgb(222, 226, 232));
        g.FillRectangle(fill, mb.Left, y, mb.Width, HeadH);
        using var pen = new Pen(Color.Black, 0.8f);
        g.DrawRectangle(pen, mb.Left, y, mb.Width, HeadH);

        var f = ModernTheme.GetFont(font, FontStyle.Bold);
        using var fmt = Sf(StringAlignment.Center, wrap: true);

        float x = mb.Right - RowNumW;
        g.DrawLine(pen, x, y, x, y + HeadH);
        g.DrawString("ردیف", f, Brushes.Black, new RectangleF(x, y, RowNumW, HeadH), fmt);

        for (int i = 0; i < sec.Cols.Count; i++)
        {
            x -= widths[i];
            g.DrawLine(pen, x, y, x, y + HeadH);
            g.DrawString(sec.Cols[i].Header, f, Brushes.Black, new RectangleF(x + 1, y, widths[i] - 2, HeadH), fmt);
        }
    }

    private void DrawBodyRow(Graphics g, Rectangle mb, float y, Section sec, List<float> widths,
                             Attendance a, int rowNo, bool zebra, float font)
    {
        if (zebra)
            using (var br = new SolidBrush(Color.FromArgb(245, 246, 248)))
                g.FillRectangle(br, mb.Left, y, mb.Width, RowH);

        using var pen = new Pen(Color.FromArgb(120, 120, 120), 0.6f);
        g.DrawRectangle(pen, mb.Left, y, mb.Width, RowH);

        var f = ModernTheme.GetFont(font);
        using var center = Sf(StringAlignment.Center);
        using var near = Sf(StringAlignment.Near);

        float x = mb.Right - RowNumW;
        g.DrawLine(pen, x, y, x, y + RowH);
        g.DrawString(StringHelper.ConvertToPersianDigits(rowNo.ToString()), f, Brushes.Black,
            new RectangleF(x, y, RowNumW, RowH), center);

        for (int i = 0; i < sec.Cols.Count; i++)
        {
            var c = sec.Cols[i];
            x -= widths[i];
            g.DrawLine(pen, x, y, x, y + RowH);

            string text = c.Text is not null
                ? StringHelper.ConvertToPersianDigits(c.Text(a) ?? "")
                : Fmt(c.Kind, c.Value!(a));

            var rect = new RectangleF(x + 3, y, widths[i] - 6, RowH);
            g.DrawString(text, f, Brushes.Black, rect, c.Left ? near : center);
        }
    }

    private void DrawTotalRow(Graphics g, Rectangle mb, float y, Section sec, List<float> widths, float font)
    {
        using var fill = new SolidBrush(Color.FromArgb(255, 249, 214));
        g.FillRectangle(fill, mb.Left, y, mb.Width, RowH);
        using var pen = new Pen(Color.Black, 0.8f);
        g.DrawRectangle(pen, mb.Left, y, mb.Width, RowH);

        var f = ModernTheme.GetFont(font, FontStyle.Bold);
        using var center = Sf(StringAlignment.Center);

        // «جمع کل» روی ستون‌های ردیف + کد + نام ادغام می‌شود
        int firstDataCol = sec.Cols.FindIndex(c => c.HasTotal);
        float labelW = RowNumW + widths.Take(firstDataCol).Sum();
        g.DrawString("جمع کل", f, Brushes.Black, new RectangleF(mb.Right - labelW, y, labelW, RowH), center);

        float x = mb.Right - labelW;
        g.DrawLine(pen, x, y, x, y + RowH);
        for (int i = firstDataCol; i < sec.Cols.Count; i++)
        {
            var c = sec.Cols[i];
            x -= widths[i];
            g.DrawLine(pen, x, y, x, y + RowH);
            if (!c.HasTotal) continue;

            decimal sum = sec.Rows.Sum(r => c.Value!(r));
            g.DrawString(Fmt(c.Kind, sum), f, Brushes.Black, new RectangleF(x + 2, y, widths[i] - 4, RowH), center);
        }
    }

    private static void DrawSignatures(Graphics g, Rectangle mb, float y)
    {
        float w = mb.Width / 3f;
        var f = ModernTheme.GetFont(10f, FontStyle.Bold);
        var fs = ModernTheme.GetFont(9f);
        using var center = Sf(StringAlignment.Center);
        string[] titles = { "تهیه کننده", "بررسی کننده", "تایید کننده" };

        for (int i = 0; i < 3; i++)
        {
            float left = mb.Right - w * (i + 1);
            g.DrawString(titles[i], f, Brushes.Black, new RectangleF(left, y, w, 20), center);
            g.DrawString("نام و امضاء:", fs, Brushes.Black, new RectangleF(left, y + 24, w, 18), center);
            using var pen = new Pen(Color.Gray, 0.8f);
            g.DrawLine(pen, left + w * 0.2f, y + 62, left + w * 0.8f, y + 62);
        }
    }

    private static void DrawFooter(Graphics g, Rectangle mb, int page, int total)
    {
        var f = ModernTheme.GetFont(8.5f);
        using var center = Sf(StringAlignment.Center);
        g.DrawString(StringHelper.ConvertToPersianDigits($"صفحه {page} از {total}"), f, Brushes.DimGray,
            new RectangleF(mb.Left, mb.Bottom - 18, mb.Width, 18), center);
    }
}
