using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SMP.Helpers
{
    public static class FocusHelper
    {
        private static readonly Color FocusBackColor = Color.FromArgb(240, 248, 255);
        private static readonly Color FocusBorderColor = Color.FromArgb(59, 130, 246);

        // نگهداری رنگ اصلی هر کنترل + جلوگیری از Attach تکراری
        private static readonly Dictionary<Control, Color> _originalColors = new();
        private static readonly HashSet<Control> _attached = new();

        public static void ApplyToAll(Control parent)
        {
            if (parent == null) return;
            foreach (Control ctrl in parent.Controls)
            {
                if (ctrl is TextBox || ctrl is ComboBox || ctrl is NumericUpDown)
                    Attach(ctrl);
                else if (ctrl.HasChildren)
                    ApplyToAll(ctrl);
            }
        }

        private static void Attach(Control ctrl)
        {
            if (!_attached.Add(ctrl)) return; // قبلاً وصل شده
            _originalColors[ctrl] = ctrl.BackColor;

            ctrl.Enter += (s, e) =>
            {
                if (ctrl is TextBox txt && txt.ReadOnly) return;
                ctrl.BackColor = FocusBackColor;
                ctrl.Parent?.Invalidate();
            };
            ctrl.Leave += (s, e) =>
            {
                ctrl.BackColor = _originalColors.TryGetValue(ctrl, out var c) ? c : Color.White;
                ctrl.Parent?.Invalidate();
            };
            ctrl.Disposed += (s, e) =>
            {
                _attached.Remove(ctrl);
                _originalColors.Remove(ctrl);
            };
        }
    }
}