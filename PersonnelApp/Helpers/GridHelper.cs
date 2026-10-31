using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace PersonnelApp.Helpers
{
    public static class GridHelper
    {
        private static readonly string[] DefaultHiddenColumns =
        {
            "Id", "PartyId", "NationalId", "TerminationDate", "EmployeeId", "CompanyId", "DepartmentId",
            "ClientContractId", "Password", "Photo"
        };

        public static void ApplyModernStyle(this DataGridView dgv, params string[] columnsToHide)
        {
            if (dgv == null) return;

            // جلوگیری از اعمال دوباره (و در نتیجه Subscribe تکراری)
            if (dgv.Tag != null && dgv.Tag.ToString() == "ModernStyleApplied")
            {
                ApplyHiddenAndTranslate(dgv, columnsToHide);
                return;
            }
            dgv.Tag = "ModernStyleApplied";

            dgv.EnableHeadersVisualStyles = false;
            dgv.BackgroundColor = System.Drawing.Color.FromArgb(253, 251, 247);
            dgv.BorderStyle = BorderStyle.None;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            dgv.GridColor = System.Drawing.Color.FromArgb(225, 215, 205);

            dgv.ReadOnly = true;
            dgv.RowHeadersVisible = false;
            dgv.AllowUserToAddRows = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            dgv.RightToLeft = RightToLeft.Yes;
            dgv.Font = UIHelper.PersianFont(11f);
            dgv.RowTemplate.Height = 35;

            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            dgv.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 8, 4, 8);

            dgv.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(248, 237, 227);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(90, 60, 40);
            dgv.ColumnHeadersDefaultCellStyle.Font = UIHelper.PersianFont(11f, System.Drawing.FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(248, 237, 227);

            dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(245, 215, 190);
            dgv.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.FromArgb(60, 50, 45);
            dgv.AlternatingRowsDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(255, 252, 248);

            // ★ ناوبری با Enter در گرید (سطر بعدی) — جدید
            EnterNavigationHelper.EnableForGrid(dgv);

            dgv.DataError += (s, e) =>
            {
                e.ThrowException = false;
                e.Cancel = true;
            };

            dgv.DataBindingComplete += (s, e) => ApplyHiddenAndTranslate(dgv, columnsToHide);
        }

        private static void ApplyHiddenAndTranslate(DataGridView dgv, string[] columnsToHide)
        {
            var allHide = DefaultHiddenColumns.Concat(columnsToHide ?? Array.Empty<string>()).Distinct();
            foreach (var colName in allHide)
                if (dgv.Columns.Contains(colName))
                    dgv.Columns[colName].Visible = false;

            foreach (DataGridViewColumn col in dgv.Columns)
            {
                string key = !string.IsNullOrWhiteSpace(col.DataPropertyName) ? col.DataPropertyName : col.Name;
                string translated = TranslationHelper.GetColumnName(key);
                if (!string.IsNullOrWhiteSpace(translated))
                    col.HeaderText = translated;
            }

            try
            {
                dgv.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells);
                dgv.AutoResizeColumnHeadersHeight();
            }
            catch { /* گرید خالی */ }
        }

        public static void TranslateHeaders(this DataGridView dgv, Dictionary<string, string> translations)
        {
            if (dgv == null || translations == null) return;
            foreach (var item in translations)
                if (dgv.Columns.Contains(item.Key))
                    dgv.Columns[item.Key].HeaderText = item.Value;
        }
    }
}