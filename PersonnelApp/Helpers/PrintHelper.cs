using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace PersonnelApp.Helpers
{
    public static class PrintHelper
    {
        public static void PrintDataGridView(DataGridView dgv, string title)
        {
            if (dgv == null) return;

            using var pd = new PrintDocument();
            pd.DefaultPageSettings.Landscape = true;

            int rowIndex = 0;
            const int ColWidth = 110;
            const int RowHeight = 26;
            const int HeaderHeight = 30;

            pd.PrintPage += (sender, e) =>
            {
                Graphics g = e.Graphics;
                Font titleFont = UIHelper.PersianFont(16f, FontStyle.Bold);
                Font headerFont = UIHelper.PersianFont(10f, FontStyle.Bold);
                Font cellFont = UIHelper.PersianFont(10f);

                int yPos = e.MarginBounds.Top;

                // ۱. عنوان (فقط صفحه اول)
                if (rowIndex == 0)
                {
                    var titleRect = new Rectangle(e.MarginBounds.Left, yPos,
                                                  e.MarginBounds.Width, 40);
                    TextRenderer.DrawText(g, title, titleFont, titleRect, Color.Black,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                        | TextFormatFlags.RightToLeft);
                    yPos += 45;
                }

                // ۲. هدر جدول
                int xPos = e.MarginBounds.Right;
                foreach (DataGridViewColumn col in dgv.Columns)
                {
                    if (!col.Visible) continue;
                    xPos -= ColWidth;
                    var rect = new Rectangle(xPos, yPos, ColWidth, HeaderHeight);
                    g.FillRectangle(Brushes.LightGray, rect);
                    g.DrawRectangle(Pens.Black, rect);
                    TextRenderer.DrawText(g, col.HeaderText, headerFont, rect, Color.Black,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                        | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis);
                }
                yPos += HeaderHeight;

                // ۳. ردیف‌ها
                while (rowIndex < dgv.Rows.Count)
                {
                    if (yPos + RowHeight > e.MarginBounds.Bottom)
                    {
                        e.HasMorePages = true;
                        return;
                    }

                    DataGridViewRow row = dgv.Rows[rowIndex];
                    if (row.IsNewRow) { rowIndex++; continue; }

                    xPos = e.MarginBounds.Right;
                    foreach (DataGridViewColumn col in dgv.Columns)
                    {
                        if (!col.Visible) continue;
                        xPos -= ColWidth;

                        string cellValue = row.Cells[col.Index].Value?.ToString() ?? "";
                        var rect = new Rectangle(xPos, yPos, ColWidth, RowHeight);
                        g.DrawRectangle(Pens.Black, rect);
                        TextRenderer.DrawText(g, cellValue, cellFont, rect, Color.Black,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                            | TextFormatFlags.RightToLeft | TextFormatFlags.EndEllipsis);
                    }
                    yPos += RowHeight;
                    rowIndex++;
                }
            };

            // پیش‌نمایش چاپ
            using var preview = new PrintPreviewDialog
            {
                Document = pd,
                Width = 1000,
                Height = 800,
                RightToLeft = RightToLeft.Yes,
                Text = "پیش‌نمایش چاپ"
            };
            preview.ShowDialog();
        }
    }
}