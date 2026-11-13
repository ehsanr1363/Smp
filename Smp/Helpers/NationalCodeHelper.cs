using System;
using System.Linq;

namespace SMP.Helpers
{
    public static class NationalCodeHelper
    {
        /// <summary>بررسی صحت کد ملی ایران</summary>
        public static bool IsValid(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;

            // ✅ FIX STEP 2: واگذاری به StringHelper (حذف پیاده‌سازی تکراری)
            string nationalCode = StringHelper.ConvertToEnglishDigits(input).Trim();

            if (nationalCode.Length != 10) return false;

            // بررسی تکراری نبودن تمام ارقام
            if (new string(nationalCode[0], 10) == nationalCode) return false;

            int sum = 0;
            for (int i = 0; i < 9; i++)
            {
                sum += (nationalCode[i] - '0') * (10 - i);
            }

            int r = sum % 11;
            int check = nationalCode[9] - '0';

            return (r < 2 && check == r) || (r >= 2 && check == 11 - r);
        }

        // ✅ FIX STEP 2: واگذاری به StringHelper (حذف پیاده‌سازی تکراری)
        // این متد برای سازگاری نگه داشته شده - از همه‌جا می‌توانید از StringHelper استفاده کنید
        public static string ConvertToEnglishDigits(string input)
            => StringHelper.ConvertToEnglishDigits(input);
    }
}