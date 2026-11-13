using System;
using System.Text.RegularExpressions;

namespace SMP.Helpers
{
    public static class StringHelper
    {
        public static string ConvertToEnglishDigits(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return input.Replace("۰", "0").Replace("۱", "1").Replace("۲", "2").Replace("۳", "3").Replace("۴", "4")
                        .Replace("۵", "5").Replace("۶", "6").Replace("۷", "7").Replace("۸", "8").Replace("۹", "9")
                        .Replace("٠", "0").Replace("١", "1").Replace("٢", "2").Replace("٣", "3").Replace("٤", "4")
                        .Replace("٥", "5").Replace("٦", "6").Replace("٧", "7").Replace("٨", "8").Replace("٩", "9");
        }

        public static string ConvertToPersianDigits(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return input.Replace("0", "۰").Replace("1", "۱").Replace("2", "۲").Replace("3", "۳").Replace("4", "۴")
                        .Replace("5", "۵").Replace("6", "۶").Replace("7", "۷").Replace("8", "۸").Replace("9", "۹");
        }

        public static string NormalizePersianText(string? input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            input = input.Replace('ي', 'ی').Replace('ك', 'ک').Replace('ى', 'ی').Replace('ة', 'ه').Replace('\u200C', ' ').Replace("\u200E", "").Replace("\u200F", "");
            return Regex.Replace(input, @"[\u064B-\u065F]", "").Trim();
        }

        public static string GenderToPersian(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "نامشخص";
            return value.Trim().ToLower() switch
            {
                "male" or "m" or "مرد" or "1" => "مرد",
                "female" or "f" or "زن" or "2" => "زن",
                _ => "نامشخص"
            };
        }
    }
}