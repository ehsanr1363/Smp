using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;

namespace PersonnelApp.Helpers
{
    public static class GridSearchHelper
    {
        /// <summary>
        /// جستجوی زنده روی DataGridView متصل به DataTable.
        /// کاراکترهای خاص RowFilter به‌درستی Escape می‌شوند تا خطا رخ ندهد.
        /// </summary>
        public static void ApplyLiveSearch(TextBox txtSearch, DataGridView dgv, string[] searchColumns)
        {
            if (txtSearch == null || dgv == null || searchColumns == null || searchColumns.Length == 0)
                return;

            txtSearch.TextChanged += (s, e) =>
            {
                // فقط برای DataTable/DataView پشتیبانی می‌شود
                DataTable table = null;
                if (dgv.DataSource is DataTable t) table = t;
                else if (dgv.DataSource is DataView v) table = v.Table;

                if (table == null) return;

                string search = (txtSearch.Text ?? "").Trim();
                if (string.IsNullOrEmpty(search))
                {
                    table.DefaultView.RowFilter = "";
                    return;
                }

                // نرمال‌سازی فارسی + Escape کامل کاراکترهای خاص RowFilter
                string safe = StringHelper.NormalizePersianText(search)
                    .Replace("'", "''")
                    .Replace("[", "[[]")
                    .Replace("%", "[%]")
                    .Replace("*", "[*]");

                var parts = new List<string>();
                foreach (string col in searchColumns)
                {
                    if (string.IsNullOrWhiteSpace(col)) continue;
                    if (!table.Columns.Contains(col)) continue;
                    parts.Add($"{col} LIKE '%{safe}%'");
                }

                if (parts.Count == 0)
                {
                    table.DefaultView.RowFilter = "";
                    return;
                }

                try
                {
                    table.DefaultView.RowFilter = string.Join(" OR ", parts);
                }
                catch (Exception ex)
                {
                    LogHelper.LogWarning("RowFilter error: " + ex.Message, "GridSearchHelper");
                    table.DefaultView.RowFilter = "";
                }
            };
        }
    }
}