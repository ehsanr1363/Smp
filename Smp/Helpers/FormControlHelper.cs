using System;
using System.Windows.Forms;

namespace Smp.Helpers;

public static class FormControlHelper
{
    /// <summary>
    /// تمام تکست‌باکس‌ها، کمبوباکس‌ها و چک‌باکس‌های فرم را پاک می‌کند.
    /// </summary>
    public static void ClearAllInputs(this Control parent)
    {
        foreach (Control ctrl in parent.Controls)
        {
            if (ctrl is TextBox txt) txt.Clear();
            else if (ctrl is ComboBox cb) cb.SelectedIndex = -1;
            else if (ctrl is NumericUpDown num) num.Value = 0;
            else if (ctrl is CheckBox chk) chk.Checked = false;
            else if (ctrl is DateTimePicker dtp) dtp.Value = DateTime.Now;

            if (ctrl.HasChildren) ctrl.ClearAllInputs(); // جستجوی بازگشتی
        }
    }

    /// <summary>
    /// تمام فیلدهای ورودی را فعال یا غیرفعال (قفل) می‌کند.
    /// </summary>
    public static void SetInputsEnabled(this Control parent, bool enabled)
    {
        foreach (Control ctrl in parent.Controls)
        {
            // فقط فیلدهای ورودی را تغییر بده، دکمه‌ها را دست نزن
            if (ctrl is TextBox || ctrl is ComboBox || ctrl is NumericUpDown || ctrl is CheckBox || ctrl is DateTimePicker)
            {
                ctrl.Enabled = enabled;
            }

            if (ctrl.HasChildren) ctrl.SetInputsEnabled(enabled);
        }
    }

    /// <summary>
    /// فرم را وسط صفحه نمایش می‌دهد و کل فرم را راست‌چین می‌کند.
    /// </summary>
    public static void SetupAsDialog(this Form form, string title)
    {
        form.Text = title;
        form.StartPosition = FormStartPosition.CenterParent;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.RightToLeft = RightToLeft.Yes;
        form.RightToLeftLayout = true;
        form.Font = ModernTheme.GetFont(11f);
    }
}
