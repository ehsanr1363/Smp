using System;
using System.Windows.Forms;

namespace Smp.Helpers;

public static class ClipboardHelper
{
    /// <summary>
    /// فعال‌سازی Paste از اکسل به DataGridView.
    /// ستون‌های Hidden و ReadOnly و Cell.ReadOnly نادیده گرفته می‌شوند.
    /// از افزودن Handler تکراری جلوگیری می‌کند.
    /// </summary>
    public static void EnableExcelPaste(DataGridView dgv)
    {
        if (dgv is null) return;
        if (dgv.Tag is not null && dgv.Tag.ToString() == "ExcelPasteEnabled") return;
        dgv.Tag = "ExcelPasteEnabled";

        dgv.KeyDown += (s, e) =>
        {
            if (!e.Control || e.KeyCode != Keys.V) return;

            // اگر سلول در حالت ویرایش است، اجازه بده Paste پیش‌فرض TextBox انجام شود
            if (dgv.IsCurrentCellInEditMode) return;

            try
            {
                string text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text)) return;

                string[] lines = text.Split(new[] { "\r\n", "\r", "\n" },
                                            StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0) return;

                int startRow = dgv.CurrentCell?.RowIndex ?? 0;
                int startCol = dgv.CurrentCell?.ColumnIndex ?? 0;

                int maxRow = dgv.AllowUserToAddRows ? dgv.Rows.Count - 1 : dgv.Rows.Count;
                int row = startRow;

                foreach (string line in lines)
                {
                    if (row >= maxRow) break;

                    string[] cells = line.Split('\t');
                    int col = startCol;

                    foreach (string raw in cells)
                    {
                        // ستون بعدی قابل نوشتن را پیدا کن
                        while (col < dgv.Columns.Count)
                        {
                            var column = dgv.Columns[col];
                            var cell = dgv.Rows[row].Cells[col];
                            if (column.Visible && !column.ReadOnly && !cell.ReadOnly)
                            {
                                cell.Value = raw.Trim();
                                col++;
                                break;
                            }
                            col++;
                        }
                    }
                    row++;
                }
                e.Handled = true;
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "ClipboardHelper");
            }
        };
    }
}
