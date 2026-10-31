using PersonnelApp.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PersonnelApp.Services
{
    public static class BankHelper
    {
        public const string VERSION = "v5-2024-fixed";

        public static readonly Dictionary<string, string> BankList = BuildBankList();

        private static Dictionary<string, string> BuildBankList()
        {
            var raw = new (string Code, string Name)[]
            {
                ("010", "بانک مرکزی جمهوری اسلامی ایران"),
                ("011", "بانک صنعت و معدن"),
                ("012", "بانک ملت"),
                ("013", "بانک رفاه کارگران"),
                ("014", "بانک مسکن"),
                ("015", "بانک سپه"),
                ("016", "بانک کشاورزی"),
                ("017", "بانک ملی ایران"),
                ("018", "بانک تجارت"),
                ("019", "بانک صادرات ایران"),
                ("020", "بانک توسعه صادرات ایران"),
                ("021", "پست بانک ایران"),
                ("022", "بانک توسعه تعاون"),
                ("051", "موسسه اعتباری توسعه"),
                ("053", "بانک کارآفرین"),
                ("054", "بانک پارسیان"),
                ("055", "بانک اقتصاد نوین"),
                ("056", "بانک سامان"),
                ("057", "بانک پاسارگاد"),
                ("058", "بانک سرمایه"),
                ("059", "بانک سینا"),
                ("060", "بانک قوامین"),
                ("061", "بانک شهر"),
                ("062", "بانک تات"),
                ("063", "بانک انصار"),
                ("069", "بانک توسعه تعاون"),
                ("070", "بانک دی"),
                ("075", "بانک ایران زمین")
            };

            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (code, name) in raw)
                dict[NormalizeDigits(code)] = name;
            return dict;
        }

        public static string NormalizeDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c >= '0' && c <= '9') sb.Append(c);
                else if (c >= '۰' && c <= '۹') sb.Append((char)('0' + (c - '۰')));
                else if (c >= '٠' && c <= '٩') sb.Append((char)('0' + (c - '٠')));
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string RemoveInvisibleChars(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new StringBuilder(input.Length);
            foreach (char ch in input)
            {
                switch (ch)
                {
                    case '\u200B':
                    case '\u200C':
                    case '\u200D':
                    case '\u200E':
                    case '\u200F':
                    case '\uFEFF':
                    case '\u00A0':
                    case '\u202A':
                    case '\u202B':
                    case '\u202C':
                    case '\u202D':
                    case '\u202E':
                        continue;
                    default:
                        sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>نرمال‌سازی شبا به ۲۴ رقم ASCII (بدون IR)</summary>
        public static string NormalizeSheba(string sheba)
        {
            if (string.IsNullOrWhiteSpace(sheba)) return null;
            string s = NormalizeDigits(sheba);
            s = RemoveInvisibleChars(s).Trim().ToUpperInvariant();
            if (s.StartsWith("IR")) s = s.Substring(2);
            s = new string(s.Where(c => c >= '0' && c <= '9').ToArray());
            return s;
        }

        // ✅ FIX STEP 2: اعتبارسنجی شبا به ShebaHelper.IsValid واگذار شد
        // (الگوریتم IBAN/MOD97 در ShebaHelper با BigInteger پیاده شده - دقیق‌تر از پیاده‌سازی قبلی)
        public static bool IsValidSheba(string sheba)
            => ShebaHelper.IsValid(sheba);

        public static string GetBankCodeFromSheba(string sheba)
        {
            string digits = NormalizeSheba(sheba);
            if (digits == null || digits.Length != 24) return null;
            return digits.Substring(2, 3);
        }

        public static string GetBankNameFromSheba(string sheba)
        {
            string digits = NormalizeSheba(sheba);
            if (digits == null || digits.Length != 24)
                return "شبا نامعتبر (طول اشتباه)";

            string code = digits.Substring(2, 3);
            return BankList.TryGetValue(code, out string bankName)
                ? bankName
                : $"بانک ناشناخته ({code})";
        }

        public static string GetBankNameByCode(string bankCode)
        {
            if (string.IsNullOrWhiteSpace(bankCode)) return null;
            string normalized = new string(NormalizeDigits(bankCode)
                .Where(c => c >= '0' && c <= '9').ToArray());
            if (normalized.Length != 3) return null;
            return BankList.TryGetValue(normalized, out string name) ? name : null;
        }
    }
}