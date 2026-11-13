using System;
using System.Text.RegularExpressions;
using SMP.Helpers;

namespace SMP.Helpers
{
    public static class ValidationHelper
    {
        // ✅ FIX 1: پشتیبانی از هر دو نام متد برای جلوگیری از خطای کامپایل در سایر فرم‌ها
        public static bool IsValidIranianNationalCode(string? nationalCode)
        {
            return IsNationalCodeValid(nationalCode);
        }

        // ✅ FIX 2: پاکسازی اعداد فارسی و بررسی دقیق الگوریتم چک‌سام
        public static bool IsNationalCodeValid(string? nationalCode)
        {
            if (string.IsNullOrWhiteSpace(nationalCode)) return false;

            // تبدیل اعداد فارسی/عربی به انگلیسی و حذف فاصله‌های احتمالی
            string cleanCode = StringHelper.ConvertToEnglishDigits(nationalCode.Trim());

            if (cleanCode.Length != 10 || !Regex.IsMatch(cleanCode, @"^\d{10}$"))
                return false;

            // بررسی اینکه همه ارقام یکسان نباشند (مثل 1111111111)
            if (new string(cleanCode[0], 10) == cleanCode)
                return false;

            try
            {
                int sum = 0;
                int checkDigit = int.Parse(cleanCode[9].ToString());

                for (int i = 0; i < 9; i++)
                {
                    sum += int.Parse(cleanCode[i].ToString()) * (10 - i);
                }

                int remainder = sum % 11;
                if (remainder < 2)
                {
                    return checkDigit == remainder;
                }
                else
                {
                    return checkDigit == (11 - remainder);
                }
            }
            catch
            {
                return false;
            }
        }

        // ✅ FIX 3: حذف فاصله، خط تیره و پشتیبانی از اعداد فارسی برای موبایل
        public static bool IsMobileValid(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile)) return false;

            string cleanMobile = StringHelper.ConvertToEnglishDigits(mobile.Trim())
                .Replace(" ", "")
                .Replace("-", "");

            // اعتبارسنجی شماره موبایل ایران (شروع با 09 یا +989 یا 989)
            return Regex.IsMatch(cleanMobile, @"^(?:0|98|\+98)?9\d{9}$");
        }

        // ✅ FIX 4: پاکسازی کاما و ممیز قبل از تبدیل به عدد
        public static bool IsNumeric(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;

            string cleanInput = StringHelper.ConvertToEnglishDigits(input.Trim())
                .Replace(",", "")
                .Replace("٬", "")
                .Replace("٫", ".");

            return decimal.TryParse(cleanInput, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _);
        }
    }
}