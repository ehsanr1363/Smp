using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Smp.Helpers;

/// <summary>
/// پوشش نازک روی NumberHelper؛ پارسر واحد پروژه NumberHelper است (تا دو پیاده‌سازی موازی نداشته باشیم).
/// </summary>
public static class FinancialHelper
{
    // ۱. تبدیل عدد به رشته با جداکننده سه رقمی (صحیح بدون اعشار، اعشاری با دو رقم)
    public static string Format(decimal value)
    {
        string fmt = value == decimal.Truncate(value) ? "N0" : "N2";
        return value.ToString(fmt, CultureInfo.InvariantCulture);
    }

    public static string NormalizeDigits(string? text) => NumberHelper.NormalizeNumberText(text);

    public static bool TryParse(string? text, out decimal value) => NumberHelper.TryParseFormatted(text, out value);

    public static decimal ParseStrict(string? text, string fieldName = "مبلغ") => NumberHelper.ParseFormattedStrict(text, fieldName);

    /// <summary>سازگار با کد قبلی: خالی یا نامعتبر = ۰ (نامعتبر در لاگ ثبت می‌شود).</summary>
    public static decimal Parse(string? text) => NumberHelper.ParseFormatted(text ?? "");

    // ۳. اعمال خودکار فرمت روی TextBox (ورودی نامعتبر دست‌نخورده می‌ماند و قرمز می‌شود)
    public static void AttachToTextBox(TextBox textBox)
    {
        Color normalColor = textBox.BackColor;
        Color errorColor = Color.FromArgb(255, 220, 220);

        textBox.Leave += (sender, e) =>
        {
            if (string.IsNullOrWhiteSpace(textBox.Text)) { textBox.BackColor = normalColor; return; }

            if (TryParse(textBox.Text, out decimal value))
            {
                textBox.Text = Format(value);
                textBox.BackColor = normalColor;
            }
            else
            {
                textBox.BackColor = errorColor;
            }
        };
    }

    public static string ToWords(decimal amount) => NumberHelper.ToPersianWords(amount);
}
