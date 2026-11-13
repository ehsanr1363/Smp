using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace SMP.Helpers
{
    public static class NumberHelper
    {
        public static string FormatThousands(decimal value, int decimals = 0, string thousandsSeparator = ",", string decimalSeparator = ".", bool usePersianDigits = false)
        {
            var nfi = new NumberFormatInfo { NumberGroupSeparator = thousandsSeparator, NumberDecimalSeparator = decimalSeparator, NumberDecimalDigits = decimals, NegativeSign = "-" };
            string result = value.ToString("N" + decimals, nfi);
            if (usePersianDigits) { result = StringHelper.ConvertToPersianDigits(result); result = result.Replace(decimalSeparator, "٫").Replace(thousandsSeparator, "٬"); }
            return result;
        }

        public static decimal ParseFormatted(string formatted)
        {
            if (string.IsNullOrWhiteSpace(formatted)) return 0m;

            // 👈 خط Replace("/", ".") اضافه شد تا ممیز با اسلش (7/78) را قبول کند
            string cleaned = StringHelper.ConvertToEnglishDigits(formatted)
                .Replace(",", "")
                .Replace("٬", "")
                .Replace("٫", ".")
                .Replace("/", ".")
                .Replace(" ", "")
                .Trim();

            return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal r) ? r : 0m;
        }

        public static decimal Round(decimal value, int decimals = 2, MidpointRounding mode = MidpointRounding.AwayFromZero)
            => Math.Round(value, decimals, mode);

        // --- عدد به حروف ---
        private static readonly string[] Yekan = { "", "یک", "دو", "سه", "چهار", "پنج", "شش", "هفت", "هشت", "نه" };
        private static readonly string[] Dahgan = { "", "", "بیست", "سی", "چهل", "پنجاه", "شصت", "هفتاد", "هشتاد", "نود" };
        private static readonly string[] Dahyek = { "ده", "یازده", "دوازده", "سیزده", "چهارده", "پانزده", "شانزده", "هفده", "هجده", "نوزده" };
        private static readonly string[] Sadgan = { "", "صد", "دویست", "سیصد", "چهارصد", "پانصد", "ششصد", "هفتصد", "هشتصد", "نهصد" };
        private static readonly string[] Scale = { "", "هزار", "میلیون", "میلیارد", "تریلیون" };

        private static string ThreeDigitsToWords(int num)
        {
            var parts = new List<string>();
            int sad = num / 100; int rem = num % 100;
            if (sad > 0) parts.Add(Sadgan[sad]);
            if (rem >= 10 && rem < 20) parts.Add(Dahyek[rem - 10]);
            else { int dah = rem / 10; int yek = rem % 10; if (dah > 0) parts.Add(Dahgan[dah]); if (yek > 0) parts.Add(Yekan[yek]); }
            return string.Join(" و ", parts);
        }

        public static string ToPersianWords(decimal value)
        {
            if (value == 0m) return "صفر";
            var sb = new StringBuilder();
            if (value < 0) { sb.Append("منفی "); value = decimal.Negate(value); }
            decimal integerPart = Math.Truncate(value);
            var integerBig = new BigInteger(integerPart);
            var parts = new List<string>();
            int scaleIndex = 0;
            while (integerBig > 0 && scaleIndex < Scale.Length)
            {
                int three = (int)(integerBig % 1000);
                if (three > 0)
                {
                    string words = ThreeDigitsToWords(three);
                    if (scaleIndex > 0) words += " " + Scale[scaleIndex];
                    parts.Insert(0, words);
                }
                integerBig /= 1000; scaleIndex++;
            }
            sb.Append(string.Join(" و ", parts));
            return sb.ToString().Trim();
        }

        public static string ToPersianWordsWithUnit(decimal value, string unit) => string.IsNullOrEmpty(unit) ? ToPersianWords(value) : ToPersianWords(value) + " " + unit;
    }
}