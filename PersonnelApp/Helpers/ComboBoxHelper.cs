using System;
using System.Data;
using System.Windows.Forms;

namespace PersonnelApp.Helpers
{
    public static class ComboBoxHelper
    {
        /// <summary>
        /// اتصال DataTable/List به ComboBox با ردیف پیش‌فرض.
        /// DataTable ورودی را تغییر نمی‌دهد (کپی می‌سازد) و از تکرار ردیف پیش‌فرض جلوگیری می‌کند.
        /// </summary>
        public static void BindData(ComboBox cmb, object dataSource, string valueMember, string displayMember,
                                    string defaultText = "--- انتخاب کنید ---")
        {
            if (cmb == null) return;

            // جلوگیری از رخداد SelectedIndexChanged هنگام بازسازی
            cmb.DataSource = null;
            cmb.Items.Clear();

            if (dataSource is DataTable original)
            {
                // کپی مستقل تا DataTable کش/اصلی تغییر نکند
                var dt = original.Copy();

                // حذف هر ردیف پیش‌فرض قبلی (اگر کسی جای دیگری اضافه کرده)
                for (int i = dt.Rows.Count - 1; i >= 0; i--)
                {
                    var val = dt.Rows[i][valueMember];
                    if (val == null || val == DBNull.Value) continue;
                    if (val.ToString() == "0" && dt.Rows[i][displayMember]?.ToString() == defaultText)
                        dt.Rows.RemoveAt(i);
                }

                var def = dt.NewRow();
                def[valueMember] = 0;
                def[displayMember] = defaultText;
                dt.Rows.InsertAt(def, 0);

                cmb.DataSource = dt;
                cmb.ValueMember = valueMember;
                cmb.DisplayMember = displayMember;
            }
            else
            {
                cmb.DataSource = dataSource;
                if (!string.IsNullOrWhiteSpace(valueMember)) cmb.ValueMember = valueMember;
                if (!string.IsNullOrWhiteSpace(displayMember)) cmb.DisplayMember = displayMember;
            }

            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        }

        /// <summary>لیست ساده (مثل جنسیت) با آیتم پیش‌فرض.</summary>
        public static void BindList(ComboBox cmb, string[] items, string defaultText = "--- انتخاب کنید ---")
        {
            if (cmb == null) return;
            cmb.DataSource = null;
            cmb.Items.Clear();
            cmb.Items.Add(defaultText);
            if (items != null && items.Length > 0) cmb.Items.AddRange(items);
            cmb.SelectedIndex = 0;
        }

        /// <summary>شناسه انتخاب‌شده (صفر اگر آیتم پیش‌فرض یا نامعتبر).</summary>
        public static int GetSelectedId(this ComboBox cmb)
        {
            if (cmb == null || cmb.SelectedIndex < 0) return 0;
            if (cmb.SelectedIndex == 0) return 0;
            if (cmb.SelectedValue == null || cmb.SelectedValue == DBNull.Value) return 0;

            return int.TryParse(cmb.SelectedValue.ToString(), out int id) ? id : 0;
        }

        /// <summary>مقدار رشته‌ای انتخاب‌شده (خالی اگر پیش‌فرض).</summary>
        public static string GetSelectedText(this ComboBox cmb)
        {
            if (cmb == null || cmb.SelectedIndex <= 0) return "";
            return cmb.Text ?? "";
        }
    }
}