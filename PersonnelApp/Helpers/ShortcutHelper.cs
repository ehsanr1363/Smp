using System;
using System.Windows.Forms;

namespace PersonnelApp.Helpers
{
    public static class ShortcutHelper
    {
        /// <summary>
        /// میانبرهای استاندارد را به فرم متصل می‌کند (Ctrl+S برای ذخیره، Esc برای بستن).
        /// </summary>
        public static void ApplyStandardShortcuts(Form form, Button btnSave = null, Button btnCancel = null)
        {
            form.KeyPreview = true; // فرم باید کلیدها را قبل از کنترل‌ها دریافت کند
            form.KeyDown += (s, e) =>
            {
                // ذخیره (Ctrl + S)
                if (e.Control && e.KeyCode == Keys.S)
                {
                    e.SuppressKeyPress = true; // جلوگیری از بوق زدن ویندوز
                    if (btnSave != null && btnSave.Enabled && btnSave.Visible)
                        btnSave.PerformClick();
                }

                // انصراف/بستن (Esc)
                if (e.KeyCode == Keys.Escape)
                {
                    e.SuppressKeyPress = true;
                    if (btnCancel != null && btnCancel.Enabled && btnCancel.Visible)
                        btnCancel.PerformClick();
                    else
                        form.Close();
                }
            };
        }
    }
}