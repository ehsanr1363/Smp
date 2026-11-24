using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Windows.Forms;

namespace Smp.Helpers;

// ═══════════════════════════════════════════════════════════════════════
//  ModernPrint — چاپ و پیش‌نمایش مدرن گزارش‌ها (جایگزین PrintHelper.PrintDataGridView)
//   • عرض ستون‌ها متناسب با عرض واقعی ستون‌های گرید
//   • هدر رنگی، ردیف‌های زبرا، شماره صفحه «صفحه x از y»، تاریخ چاپ
//   • سربرگ در هر صفحه تکرار می‌شود
// ═══════════════════════════════════════════════════════════════════════
public static class ModernPrint
{
    private const float RowH = 26f;
    private const float HeadH = 32f;

    public static void PrintGrid(DataGridView dgv, string title, string subtitle = "", string summaryText = "",
                                 Image? logo = null, bool landscape = true)
    {
        if (dgv is null) return;

        var cols = dgv.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex).ToList();
        if (cols.Count == 0) return;

        var rows = dgv.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow && r.Visible).ToList();

        using var pd = new PrintDocument();
        pd.DefaultPageSettings.Landscape = landscape;
        pd.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);

        int rowIndex = 0, page = 0, totalPages = 1;
        string printedAt = "تاریخ چاپ: " + StringHelper.ConvertToPersianDigits(DateHelper.ToPersianDate(DateTime.Now));

        pd.BeginPrint += (s, e) => { rowIndex = 0; page = 0; };

        pd.PrintPage += (s, e) =>
        {
            var g = e.Graphics;
            if (g is null) return;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var mb = e.MarginBounds;
            page++;

            // ── سربرگ ──
            float y = DrawHeader(g, mb, title, subtitle, summaryText, logo, printedAt, page == 1);

            // مجموع صفحات: پس از اولین صفحه با هندسه‌ی ثابت محاسبه می‌شود
            if (page == 1)
            {
                float availFirst = mb.Bottom - 24 - (mb.Top + HeaderHeight(true, summaryText) + HeadH);
                float availOther = mb.Bottom - 24 - (mb.Top + HeaderHeight(false, summaryText) + HeadH);
                int perFirst = Math.Max(1, (int)(availFirst / RowH));
                int perOther = Math.Max(1, (int)(availOther / RowH));
                totalPages = rows.Count <= perFirst ? 1 : 1 + (int)Math.Ceiling((rows.Count - perFirst) / (double)perOther);
            }

            // ── عرض ستون‌ها (متناسب با عرض گرید) ──
            float sum = cols.Sum(c => (float)Math.Max(40, c.Width));
            float scale = mb.Width / sum;

            // ── ردیف عنوان ستون‌ها ──
            DrawRow(g, mb, y, HeadH, cols, scale, null, true, false);
            y += HeadH;

            // ── ردیف‌ها ──
            int zebra = 0;
            while (rowIndex < rows.Count)
            {
                if (y + RowH > mb.Bottom - 24) { e.HasMorePages = true; break; }
                DrawRow(g, mb, y, RowH, cols, scale, rows[rowIndex], false, zebra++ % 2 == 1);
                y += RowH;
                rowIndex++;
            }

            // ── پاورقی ──
            DrawFooter(g, mb, page, totalPages);
            if (rowIndex >= rows.Count) e.HasMorePages = false;
        };

        ShowPreview(pd);
    }

    // ───────────── سربرگ ─────────────
    /// <summary>ارتفاع دقیق سربرگ (باید با خروجی DrawHeader یکی باشد)</summary>
    private static float HeaderHeight(bool first, string summary)
        => 66f + (first && !string.IsNullOrWhiteSpace(summary) ? 30f : 0f);

    private static float DrawHeader(Graphics g, Rectangle mb, string title, string subtitle, string summary,
                                    Image? logo, string printedAt, bool first)
    {
        float y = mb.Top;
        using var rtl = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft };   // در RTL «Near» یعنی راست
        using var ltr = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

        float logoW = 0;
        if (logo is not null)
        {
            try
            {
                logoW = 54;
                g.DrawImage(logo, mb.Right - logoW, y, logoW, logoW);
            }
            catch (ArgumentException) { logoW = 0; }
        }

        using (var f = (Font)ModernTheme.GetFont(16f, FontStyle.Bold).Clone())
        using (var br = new SolidBrush(ModernTheme.Text))
            g.DrawString(title, f, br, new RectangleF(mb.Left + 170, y, mb.Width - logoW - 180, 30), rtl);

        if (!string.IsNullOrWhiteSpace(subtitle))
            using (var f = (Font)ModernTheme.GetFont(10.5f).Clone())
            using (var br = new SolidBrush(ModernTheme.TextMuted))
                g.DrawString(subtitle, f, br, new RectangleF(mb.Left + 170, y + 28, mb.Width - logoW - 180, 22), rtl);

        using (var f = (Font)ModernTheme.GetFont(9f).Clone())
        using (var br = new SolidBrush(ModernTheme.TextMuted))
            g.DrawString(printedAt, f, br, new RectangleF(mb.Left, y + 4, 200, 20), ltr);

        y += 58;
        using (var pen = new Pen(ModernTheme.InputBorder, 1.2f))
            g.DrawLine(pen, mb.Left, y, mb.Right, y);
        y += 6;

        if (first && !string.IsNullOrWhiteSpace(summary))
        {
            var box = new RectangleF(mb.Left, y, mb.Width, 24);
            using (var br = new SolidBrush(ModernTheme.GridHeaderBack)) g.FillRectangle(br, box);
            using (var f = (Font)ModernTheme.GetFont(10f, FontStyle.Bold).Clone())
            using (var tb = new SolidBrush(ModernTheme.GridHeaderText))
                g.DrawString(summary, f, tb, new RectangleF(box.X + 8, box.Y, box.Width - 16, box.Height), rtl);
            y += 30;
        }
        return y + 2;
    }

    private static void DrawRow(Graphics g, Rectangle mb, float y, float h, List<DataGridViewColumn> cols, float scale,
                                DataGridViewRow? row, bool header, bool zebra)
    {
        using var fmt = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter
        };
        using var font = (Font)ModernTheme.GetFont(header ? 9.5f : 9f, header ? FontStyle.Bold : FontStyle.Regular).Clone();
        using var linePen = new Pen(ModernTheme.GridLine, 1f);
        using var textBrush = new SolidBrush(header ? ModernTheme.GridHeaderText : ModernTheme.Text);

        var bandRect = new RectangleF(mb.Left, y, mb.Width, h);
        if (header)
            using (var br = new SolidBrush(ModernTheme.GridHeaderBack)) g.FillRectangle(br, bandRect);
        else if (zebra)
            using (var br = new SolidBrush(ModernTheme.SurfaceAlt)) g.FillRectangle(br, bandRect);

        float x = mb.Right;
        foreach (var col in cols)
        {
            float w = Math.Max(40, col.Width) * scale;
            x -= w;
            string text = header
                ? col.HeaderText
                : (row is not null && col.Index < row.Cells.Count ? row.Cells[col.Index].FormattedValue?.ToString() ?? string.Empty : string.Empty);
            g.DrawString(text, font, textBrush, new RectangleF(x + 3, y, w - 6, h), fmt);
        }

        g.DrawLine(linePen, mb.Left, y + h, mb.Right, y + h);
    }

    private static void DrawFooter(Graphics g, Rectangle mb, int page, int total)
    {
        using var f = (Font)ModernTheme.GetFont(8.5f).Clone();
        using var br = new SolidBrush(ModernTheme.TextMuted);
        using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.DirectionRightToLeft };
        string t = StringHelper.ConvertToPersianDigits($"صفحه {page} از {total}");
        g.DrawString(t, f, br, new RectangleF(mb.Left, mb.Bottom - 18, mb.Width, 18), center);
    }

    // ───────────── پیش‌نمایش ─────────────
    public static void ShowPreview(PrintDocument pd, string title = "پیش‌نمایش چاپ")
    {
        using var form = new Form
        {
            Text = title,
            WindowState = FormWindowState.Maximized,
            RightToLeft = RightToLeft.Yes,
            RightToLeftLayout = true,
            StartPosition = FormStartPosition.CenterScreen,
            Font = ModernTheme.GetFont(11f),
            BackColor = ModernTheme.Background
        };

        var preview = new PrintPreviewControl { Document = pd, Dock = DockStyle.Fill, UseAntiAlias = true, Zoom = 1.0 };

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 60,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10, 9, 10, 5),
            BackColor = ModernTheme.Surface
        };

        var btnPrint = ModernButton.Create("چاپ یا ذخیره PDF", ModernTheme.Primary, 180, 40);
        btnPrint.Click += (s, ev) =>
        {
            using var dlg = new PrintDialog { Document = pd, UseEXDialog = true };
            if (dlg.ShowDialog(form) == DialogResult.OK) pd.Print();
        };
        var btnZoomIn = ModernButton.Create("بزرگ‌نمایی", ModernTheme.Neutral, 110, 40, ModernButtonStyle.Soft);
        btnZoomIn.Click += (s, ev) => preview.Zoom *= 1.2;
        var btnZoomOut = ModernButton.Create("کوچک‌نمایی", ModernTheme.Neutral, 110, 40, ModernButtonStyle.Soft);
        btnZoomOut.Click += (s, ev) => preview.Zoom /= 1.2;
        var btnClose = ModernButton.Create("بستن", ModernTheme.Neutral, 100, 40, ModernButtonStyle.Outline);
        btnClose.Click += (s, ev) => form.Close();

        bar.Controls.Add(btnPrint);
        bar.Controls.Add(btnZoomIn);
        bar.Controls.Add(btnZoomOut);
        bar.Controls.Add(btnClose);

        form.Controls.Add(preview);
        form.Controls.Add(bar);
        form.ShowDialog();
    }
}
