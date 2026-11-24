using System;
using System.Data;
using System.Globalization;
using System.Windows.Forms;

namespace Smp.Helpers;

public static class ComboBoxHelper
{
    /// <summary>
    /// اتصال DataTable/List به ComboBox با ردیف پیش‌فرض.
    /// DataTable ورودی را تغییر نمی‌دهد (کپی می‌سازد) و از تکرار ردیف پیش‌فرض جلوگیری می‌کند.
    /// </summary>
    public static void BindData(ComboBox cmb, object dataSource, string valueMember, string displayMember,
                                string defaultText = "--- انتخاب کنید ---")
    {
        if (cmb is null) return;

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
                if (val is null || val == DBNull.Value) continue;
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
        if (cmb is null) return;
        cmb.DataSource = null;
        cmb.Items.Clear();
        cmb.Items.Add(defaultText);
        if (items is not null && items.Length > 0) cmb.Items.AddRange(items);
        cmb.SelectedIndex = 0;
    }

    /// <summary>اولین سال شمسی قابل انتخاب در گزارش‌ها (سال شروع داده‌ها)</summary>
    public const int DefaultStartYear = 1400;

    public static int CurrentPersianYear() => new PersianCalendar().GetYear(DateTime.Now);
    public static int CurrentPersianMonth() => new PersianCalendar().GetMonth(DateTime.Now);

    /// <summary>
    /// سال‌های شمسی را از startYear تا (سال جاری + yearsAhead) به‌صورت int در ComboBox می‌ریزد.
    /// بازه با گذشت سال خودکار بزرگ می‌شود و سال‌های قدیمی حذف نمی‌شوند.
    /// selectCurrent=false (پیش‌فرض) رفتار قبلی را حفظ می‌کند و رویداد انتخاب را زودهنگام فعال نمی‌کند.
    /// </summary>
    public static void FillYears(ComboBox cmb, int startYear = DefaultStartYear, int yearsAhead = 5, bool selectCurrent = false)
    {
        if (cmb is null) return;
        int current = CurrentPersianYear();
        int first = Math.Min(startYear, current);
        int last = current + Math.Max(0, yearsAhead);

        cmb.DataSource = null;
        cmb.Items.Clear();
        for (int y = first; y <= last; y++) cmb.Items.Add(y);

        if (selectCurrent) cmb.SelectedItem = current;
    }

    /// <summary>ماه‌های ۱ تا ۱۲ را به‌صورت int در ComboBox می‌ریزد.</summary>
    public static void FillMonths(ComboBox cmb, bool selectCurrent = false)
    {
        if (cmb is null) return;
        cmb.DataSource = null;
        cmb.Items.Clear();
        for (int m = 1; m <= 12; m++) cmb.Items.Add(m);

        if (selectCurrent) cmb.SelectedItem = CurrentPersianMonth();
    }

    /// <summary>سال و ماه را پر می‌کند و سال/ماه جاری را انتخاب می‌کند (برای فرم‌های جدید).</summary>
    public static void FillPeriod(ComboBox cmbYear, ComboBox cmbMonth)
    {
        FillYears(cmbYear, selectCurrent: true);
        FillMonths(cmbMonth, selectCurrent: true);
    }

    /// <summary>شناسه انتخاب‌شده (صفر اگر آیتم پیش‌فرض یا نامعتبر).</summary>
    public static int GetSelectedId(this ComboBox cmb)
    {
        if (cmb is null || cmb.SelectedIndex < 0) return 0;
        if (cmb.SelectedIndex == 0) return 0;
        if (cmb.SelectedValue is null || cmb.SelectedValue == DBNull.Value) return 0;

        return int.TryParse(cmb.SelectedValue.ToString(), out int id) ? id : 0;
    }

    /// <summary>مقدار رشته‌ای انتخاب‌شده (خالی اگر پیش‌فرض).</summary>
    public static string GetSelectedText(this ComboBox cmb)
    {
        if (cmb is null || cmb.SelectedIndex <= 0) return "";
        return cmb.Text ?? "";
    }
}
