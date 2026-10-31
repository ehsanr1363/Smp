using PersonnelApp.Services;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace PersonnelApp.Helpers
{
    public static class ValidationHelper
    {
        // ==========================================================
        // ۱. اعتبارسنجی کد ملی (حفظ متد قبلی برای سازگاری)
        // ==========================================================
        public static bool IsValidIranianNationalCode(string input)
        {
            // برای جلوگیری از تکرار کد، از NationalCodeHelper استفاده می‌کنیم
            return NationalCodeHelper.IsValid(input);
        }

        // ==========================================================
        // ۲. اعتبارسنجی‌های عمومی فرم‌ها
        // ==========================================================

        /// <summary>بررسی اینکه فیلد خالی یا فقط شامل فاصله نباشد</summary>
        public static bool IsRequired(string input)
        {
            return !string.IsNullOrWhiteSpace(input);
        }

        /// <summary>بررسی شماره موبایل (مثال: 09123456789 یا ۰۹۱۲۳۴۵۶۷۸۹)</summary>
        public static bool IsValidMobile(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            // تبدیل ارقام فارسی به انگلیسی
            string mobile = StringHelper.ConvertToEnglishDigits(input).Trim();
            // شروع با 09 و ۱۱ رقم بودن
            return Regex.IsMatch(mobile, @"^09\d{9}$");
        }

        /// <summary>بررسی شماره تلفن ثابت (۸ رقم)</summary>
        public static bool IsValidPhone(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            string phone = StringHelper.ConvertToEnglishDigits(input).Trim();
            // حذف خط تیره و فاصله برای بررسی دقیق‌تر
            phone = phone.Replace("-", "").Replace(" ", "");
            return Regex.IsMatch(phone, @"^\d{8}$");
        }

        /// <summary>بررسی ایمیل</summary>
        public static bool IsValidEmail(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            try
            {
                var addr = new System.Net.Mail.MailAddress(input);
                return addr.Address == input;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>بررسی اینکه رشته فقط شامل عدد است (با پشتیبانی از اعشار)</summary>
        public static bool IsNumber(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            string num = StringHelper.ConvertToEnglishDigits(input).Replace(",", "").Trim();
            return decimal.TryParse(num, out _);
        }

        /// <summary>بررسی اعتبار تاریخ شمسی (مثلاً 1403/01/01)</summary>
        public static bool IsValidPersianDate(string input)
        {
            var dt = DateHelper.ParsePersianDate(input);
            return dt.HasValue;
        }

        /// <summary>بررسی معتبر بودن شماره شبا</summary>
        public static bool IsValidSheba(string input)
        {
            return BankHelper.IsValidSheba(input);
        }

        /// <summary>بررسی طول رشته (مثلاً برای کد پرسنلی ۵ رقمی)</summary>
        public static bool HasExactLength(string input, int length)
        {
            if (string.IsNullOrEmpty(input)) return false;
            return input.Trim().Length == length;
        }
    }
}