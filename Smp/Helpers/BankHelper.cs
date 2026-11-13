using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SMP.Helpers
{
    public static class BankHelper
    {
        public static readonly Dictionary<string, string> BankList = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "010", "بانک مرکزی" },
            { "011", "بانک صنعت و معدن" },
            { "012", "بانک ملت" },
            { "013", "بانک رفاه کارگران" },
            { "014", "بانک مسکن" },
            { "015", "بانک سپه" },
            { "016", "بانک کشاورزی" },
            { "017", "بانک ملی ایران" },
            { "018", "بانک تجارت" },
            { "019", "بانک صادرات ایران" },
            { "021", "پست بانک ایران" },
            { "022", "بانک توسعه تعاون" }, // ✅ اضافه شد
            { "053", "بانک کارآفرین" },
            { "054", "بانک پارسیان" },
            { "055", "بانک اقتصاد نوین" },
            { "056", "بانک سامان" },
            { "057", "بانک پاسارگاد" },
            { "058", "بانک سرمایه" },
            { "059", "بانک سینا" },
            { "060", "بانک قرض‌الحسنه مهر ایران" },
            { "061", "بانک شهر" },
            { "062", "بانک آینده" },
            { "064", "بانک گردشگری" },
            { "066", "بانک دی" },
            { "069", "بانک ایران زمین" },
            { "070", "بانک قرض‌الحساب رسالت" },
            { "078", "بانک خاورمیانه" }
        };

        public static string GetBankNameByCode(string? bankCode)
        {
            if (string.IsNullOrWhiteSpace(bankCode)) return "نامشخص";
            string cleanCode = NormalizeDigits(bankCode.Trim());
            if (BankList.TryGetValue(cleanCode, out var name))
            {
                return name;
            }
            return $"سایر ({cleanCode})";
        }

        public static string NormalizeDigits(string input)
        {
            return StringHelper.ConvertToEnglishDigits(input);
        }

        // ✅ استخراج کد ۳ رقمی بانک (نادیده گرفتن ۲ رقم چک‌سام)
        public static string? GetBankCodeFromSheba(string sheba)
        {
            if (string.IsNullOrWhiteSpace(sheba)) return null;
            string clean = NormalizeSheba(sheba);
            if (string.IsNullOrEmpty(clean) || clean.Length != 24) return null;

            // ۲ رقم اول چک‌سام است، ۳ رقم بعدی کد بانک است
            return clean.Substring(2, 3);
        }

        public static string GetBankNameFromSheba(string sheba)
        {
            string? code = GetBankCodeFromSheba(sheba);
            if (string.IsNullOrEmpty(code)) return "نامشخص";
            return GetBankNameByCode(code);
        }

        public static string NormalizeSheba(string sheba)
        {
            if (string.IsNullOrWhiteSpace(sheba)) return "";
            string clean = NormalizeDigits(sheba)
                .Replace(" ", "").Replace("-", "").Trim().ToUpperInvariant();
            if (clean.StartsWith("IR")) clean = clean.Substring(2);
            clean = new string(clean.Where(c => char.IsLetterOrDigit(c)).ToArray());
            return clean;
        }

        public static bool ValidateSheba(string? sheba)
        {
            if (string.IsNullOrWhiteSpace(sheba)) return false;
            string clean = NormalizeSheba(sheba);
            if (clean.Length != 24) return false;
            string fullSheba = "IR" + clean;
            try
            {
                string rearranged = fullSheba.Substring(4) + fullSheba.Substring(0, 4);
                string numericSheba = "";
                foreach (char c in rearranged)
                {
                    if (char.IsLetter(c)) numericSheba += (c - 'A' + 10).ToString();
                    else if (char.IsDigit(c)) numericSheba += c;
                    else return false;
                }
                return Mod97(numericSheba) == 1;
            }
            catch { return false; }
        }

        private static int Mod97(string numericString)
        {
            int remainder = 0;
            foreach (char c in numericString) remainder = (remainder * 10 + (c - '0')) % 97;
            return remainder;
        }
    }
}