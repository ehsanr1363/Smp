using System;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace PersonnelApp.Helpers
{
    public static class EnterNavigationHelper
    {
        private static readonly ConditionalWeakTable<Form, object> _forms = new();
        private static readonly ConditionalWeakTable<DataGridView, object> _grids = new();

        public static void EnableForForm(Form form)
        {
            if (form == null || _forms.TryGetValue(form, out _)) return;
            _forms.Add(form, new object());

            form.KeyPreview = true;
            form.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                if (e.Control || e.Shift || e.Alt) return;

                var active = form.ActiveControl;
                if (active is DataGridView) return;
                if (active is Button) return;
                if (active is TextBox tb && tb.Multiline) return;
                if (active is ComboBox cb && cb.DroppedDown) return;

                e.Handled = true;
                e.SuppressKeyPress = true;
                form.SelectNextControl(active, true, true, true, true);
            };
        }

        public static void EnableForGrid(DataGridView dgv, bool wrap = false)
        {
            if (dgv == null || _grids.TryGetValue(dgv, out _)) return;
            _grids.Add(dgv, new object());

            dgv.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;

                int currentRow = dgv.CurrentCell?.RowIndex ?? -1;
                if (currentRow < 0) return;

                int maxRow = dgv.AllowUserToAddRows ? dgv.Rows.Count - 1 : dgv.Rows.Count;
                int nextRow = currentRow + 1;

                if (nextRow >= maxRow)
                {
                    if (!wrap) { e.Handled = true; e.SuppressKeyPress = true; return; }
                    nextRow = 0;
                }

                if (nextRow < 0 || nextRow >= maxRow) return;

                e.Handled = true;
                e.SuppressKeyPress = true;

                int col = dgv.CurrentCell?.ColumnIndex ?? 0;
                if (col >= dgv.Columns.Count) col = 0;
                dgv.CurrentCell = dgv.Rows[nextRow].Cells[col];
            };
        }
    }
}