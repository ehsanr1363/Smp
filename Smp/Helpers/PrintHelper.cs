using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Windows.Forms;

namespace Smp.Helpers;

public static class PrintHelper
{
    public static void PrintDataGridView(DataGridView dgv, string title, string subtitle = "", string summaryText = "", Image? logo = null)
    {
        if (dgv is null) return;

        using var pd = new PrintDocument();
        pd.DefaultPageSettings.Landscape = true;
        pd.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);

        int rowIndex = 0;
        const int HeaderHeight = 35;
        const float FixedRowHeight = 32f;

        pd.BeginPrint += (s, e) =>
        {
            rowIndex = 0;
        };

        pd.PrintPage += (sender, e) =>
        {
            Graphics g = e.Graphics;
            if (g is null) return;

            var titleFont = ModernTheme.GetFont(16f, FontStyle.Bold);
            var subtitleFont = ModernTheme.GetFont(12f, FontStyle.Bold);
            var summaryFont = ModernTheme.GetFont(10f, FontStyle.Bold);
            var headerFont = ModernTheme.GetFont(10f, FontStyle.Bold);
            var cellFont = ModernTheme.GetFont(10f, FontStyle.Regular);

            float pageWidth = e.MarginBounds.Width;
            float yPos = e.MarginBounds.Top;

            var visibleCols = dgv.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).ToList();
            if (visibleCols.Count == 0) return;

            float colWidth = pageWidth / visibleCols.Count;

            using StringFormat titleFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.DirectionRightToLeft
            };

            if (rowIndex == 0)
            {
                float titleWidth = pageWidth;

                // ✅ اصلاح شد: قرار دادن لوگو در سمت چپ
                if (logo is not null)
                {
                    try
                    {
                        float logoWidth = 75f;
                        float logoHeight = 75f;
                        float logoX = e.MarginBounds.Left; // سمت چپ
                        float logoY = yPos - 5;

                        g.DrawImage(logo, logoX, logoY, logoWidth, logoHeight);
                    }
                    catch (ArgumentException) { }
                }

                var titleRect = new RectangleF(e.MarginBounds.Left, yPos, titleWidth, 30);
                g.DrawString(title, titleFont, Brushes.Black, titleRect, titleFormat);
                yPos += 30;

                if (!string.IsNullOrWhiteSpace(subtitle))
                {
                    var subRect = new RectangleF(e.MarginBounds.Left, yPos, titleWidth, 25);
                    g.DrawString(subtitle, subtitleFont, Brushes.DarkBlue, subRect, titleFormat);
                    yPos += 25;
                }

                if (!string.IsNullOrWhiteSpace(summaryText))
                {
                    var sumRect = new RectangleF(e.MarginBounds.Left, yPos, titleWidth, 25);
                    g.DrawString(summaryText, summaryFont, Brushes.DarkSlateGray, sumRect, titleFormat);
                    yPos += 30;
                }

                if (logo is not null && yPos < e.MarginBounds.Top + 80)
                {
                    yPos = e.MarginBounds.Top + 80;
                }

                yPos += 5;
            }

            using StringFormat headerFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter
            };

            float currentX = e.MarginBounds.Right;

            for (int i = 0; i < visibleCols.Count; i++)
            {
                currentX -= colWidth;
                var rect = new RectangleF(currentX, yPos, colWidth, HeaderHeight);

                g.FillRectangle(Brushes.LightGray, rect);
                g.DrawRectangle(Pens.Black, rect.X, rect.Y, rect.Width, rect.Height);
                g.DrawString(visibleCols[i].HeaderText, headerFont, Brushes.Black, rect, headerFormat);
            }
            yPos += HeaderHeight;

            using StringFormat cellFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter
            };

            while (rowIndex < dgv.Rows.Count)
            {
                if (yPos + FixedRowHeight > e.MarginBounds.Bottom)
                {
                    e.HasMorePages = true;
                    return;
                }

                DataGridViewRow row = dgv.Rows[rowIndex];
                if (row.IsNewRow) { rowIndex++; continue; }

                currentX = e.MarginBounds.Right;
                for (int i = 0; i < visibleCols.Count; i++)
                {
                    currentX -= colWidth;
                    string cellValue = row.Cells[visibleCols[i].Index].FormattedValue?.ToString() ?? "";
                    var rect = new RectangleF(currentX, yPos, colWidth, FixedRowHeight);

                    g.DrawRectangle(Pens.Black, rect.X, rect.Y, rect.Width, rect.Height);

                    Font currentFont = GetFittedFont(g, cellValue, cellFont, colWidth - 8, cellFormat);
                    g.DrawString(cellValue, currentFont, Brushes.Black, rect, cellFormat);

                    if (currentFont != cellFont) currentFont.Dispose();
                }

                yPos += FixedRowHeight;
                rowIndex++;
            }
        };

        using var previewForm = new Form
        {
            Text = "پیش‌نمایش چاپ",
            WindowState = FormWindowState.Maximized,
            RightToLeft = RightToLeft.Yes,
            RightToLeftLayout = true,
            StartPosition = FormStartPosition.CenterScreen,
            Font = ModernTheme.GetFont(11f)
        };

        var previewControl = new PrintPreviewControl
        {
            Document = pd,
            Dock = DockStyle.Fill,
            UseAntiAlias = true,
            Zoom = 1.0
        };

        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.FromArgb(248, 249, 250),
            Padding = new Padding(10)
        };

        var btnPrint = ModernButton.Create("🖨️ چاپ یا ذخیره PDF", ModernTheme.Primary, 180, 40);
        btnPrint.Click += (s, ev) =>
        {
            using var printDialog = new PrintDialog { Document = pd, UseEXDialog = true };
            if (printDialog.ShowDialog(previewForm) == DialogResult.OK)
            {
                pd.Print();
            }
        };

        var btnClose = ModernButton.Create("بستن", ModernTheme.Neutral, 100, 40);
        btnClose.Click += (s, ev) => previewForm.Close();

        var flowLayout = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        flowLayout.Controls.Add(btnPrint);
        flowLayout.Controls.Add(btnClose);

        topPanel.Controls.Add(flowLayout);

        previewForm.Controls.Add(previewControl);
        previewForm.Controls.Add(topPanel);

        previewForm.ShowDialog();
    }

    private static Font GetFittedFont(Graphics g, string text, Font originalFont, float maxWidth, StringFormat format)
    {
        if (string.IsNullOrEmpty(text)) return originalFont;
        SizeF size = g.MeasureString(text, originalFont, int.MaxValue, format);
        if (size.Width <= maxWidth) return originalFont;

        float currentSize = originalFont.Size - 0.5f;
        while (currentSize > 6f)
        {
            Font testFont = new Font(originalFont.FontFamily, currentSize, originalFont.Style);
            size = g.MeasureString(text, testFont, int.MaxValue, format);
            if (size.Width <= maxWidth) return testFont;
            testFont.Dispose();
            currentSize -= 0.5f;
        }
        return new Font(originalFont.FontFamily, 6f, originalFont.Style);
    }
}
