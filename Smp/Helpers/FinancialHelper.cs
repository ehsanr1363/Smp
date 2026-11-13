using System;
using System.Globalization;
using System.Windows.Forms;

namespace SMP.Helpers
{
    public static class FinancialHelper
    {
        // ۱. تبدیل عدد به رشته با جداکننده سه رقمی
        public static string Format(decimal value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        // ۲. تبدیل رشته وارد شده به عدد
        public static decimal Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            string cleanText = text.Replace(",", "").Replace("،", "").Replace(" ", "").Trim();
            if (decimal.TryParse(cleanText, out decimal result)) return result;
            return 0;
        }

        // ۳. اعمال خودکار فرمت روی TextBox
        public static void AttachToTextBox(TextBox textBox)
        {
            textBox.Leave += (sender, e) =>
            {
                if (!string.IsNullOrWhiteSpace(textBox.Text))
                {
                    decimal value = Parse(textBox.Text);
                    textBox.Text = Format(value);
                }
            };
        }

        // ✅ FIX STEP 2: ToWords به NumberHelper.ToPersianWords واگذار شد
        // (کد پیاده‌سازی تکراری حذف شد - NumberHelper از BigInteger پشتیبانی می‌کند)
        public static string ToWords(decimal amount)
            => NumberHelper.ToPersianWords(amount);
    }
}