using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Smp.Helpers;

public static class SmartInputHelper
{
    // ==========================================================
    // ۱. هوشمندسازی جدول‌ها (DataGridView)
    // ==========================================================
    public static void EnableSmartGridInput(DataGridView dgv, string[] amountColumns, string[] percentageColumns)
    {
        if (dgv is null) return;
        if (dgv.Tag is not null && dgv.Tag.ToString() == "SmartInputEnabled") return;
        dgv.Tag = "SmartInputEnabled";

        dgv.CellValidating += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgv.Columns[e.ColumnIndex].Name;

            if (Array.IndexOf(amountColumns, colName) >= 0 || Array.IndexOf(percentageColumns, colName) >= 0)
            {
                string value = e.FormattedValue?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(value)) return;

                string clean = StringHelper.ConvertToEnglishDigits(value)
                    .Replace("%", "").Replace(",", "").Replace("٬", "").Replace("٫", ".");

                if (!decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                {
                    e.Cancel = true;
                    ToastHelper.Show("لطفاً فقط عدد وارد کنید.", false);
                }
            }
        };

        dgv.CellEndEdit += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgv.Columns[e.ColumnIndex].Name;
            var cell = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex];
            string value = cell.Value?.ToString()?.Trim() ?? "";

            if (string.IsNullOrEmpty(value)) return;

            if (Array.IndexOf(amountColumns, colName) >= 0)
            {
                decimal amount = NumberHelper.ParseFormatted(value);
                if (amount != 0 || value == "0" || value == "۰")
                    cell.Value = NumberHelper.FormatThousands(amount);
            }
            else if (Array.IndexOf(percentageColumns, colName) >= 0)
            {
                string cleanValue = StringHelper.ConvertToEnglishDigits(value)
                    .Replace("%", "").Replace(",", "").Replace("٬", "").Replace("٫", ".");
                if (decimal.TryParse(cleanValue, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal rate))
                {
                    if (rate > 1) rate = rate / 100;
                    cell.Value = rate.ToString("0.00", CultureInfo.InvariantCulture);
                }
            }
        };
    }

    // ==========================================================
    // ۲. هوشمندسازی تکست‌باکس‌ها (TextBox)
    // ==========================================================
    public static void EnableSmartAmountTextBox(TextBox txt)
    {
        if (txt is null || txt.Tag?.ToString() == "SmartAmountEnabled") return;
        txt.Tag = "SmartAmountEnabled";

        txt.Leave += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txt.Text)) return;
            decimal amount = NumberHelper.ParseFormatted(txt.Text);
            txt.Text = NumberHelper.FormatThousands(amount);
        };
    }

    // 👇 متد گمشده که باعث خطا شده بود 👇
    public static void EnableSmartPercentageTextBox(TextBox txt)
    {
        if (txt is null || txt.Tag?.ToString() == "SmartPercentEnabled") return;
        txt.Tag = "SmartPercentEnabled";

        txt.Leave += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txt.Text)) return;
            string clean = StringHelper.ConvertToEnglishDigits(txt.Text).Replace("%", "").Replace("٫", ".");
            if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal rate))
            {
                if (rate > 1) rate = rate / 100;
                txt.Text = rate.ToString("0.00", CultureInfo.InvariantCulture);
            }
        };
    }

    /// <summary>
    /// فیلد درصد بدون تبدیل خودکار: همان عدد درصد (مثلاً ۱ یا ۰٫۵ یا ۱۰) نگه داشته می‌شود.
    /// </summary>
    public static void EnablePercentPointsTextBox(TextBox txt)
    {
        if (txt is null || txt.Tag?.ToString() == "PercentPointsEnabled") return;
        txt.Tag = "PercentPointsEnabled";

        txt.Leave += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txt.Text)) return;
            string clean = StringHelper.ConvertToEnglishDigits(txt.Text).Replace("%", "").Replace("٫", ".").Replace("/", ".").Trim();
            if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v))
                txt.Text = v.ToString("0.##", CultureInfo.InvariantCulture);
        };
    }
}
