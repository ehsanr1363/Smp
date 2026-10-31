using PersonnelApp.Data;
using PersonnelApp.Forms;
using PersonnelApp.Helpers;
using PersonnelApp.Models;
using System;
using System.Windows.Forms;

namespace PersonnelApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // ۱. راه‌اندازی تنظیمات اولیه ویندوز فرم
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ۲. فعال‌سازی مدیریت خطای سراسری (برای جلوگیری از کرش کردن برنامه)
            GlobalExceptionHelper.Setup();

            // ۳. بررسی ساختار دیتابیس (فقط چک کردن، بدون ساختن جدول)
            try
            {
                DatabaseInitializer.Initialize();
            }
            catch (InvalidOperationException ex)
            {
                // اگر دیتابیس یا جداول نبودند، پیام واضح نشان بده
                MessageBox.Show(
                    ex.Message,
                    "خطای راه‌اندازی دیتابیس",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            catch (Exception ex)
            {
                // هر خطای غیرمنتظره دیگری
                LogHelper.LogException(ex, "Program.Main - DatabaseInitializer");
                MessageBox.Show(
                    "خطای غیرمنتظره در راه‌اندازی برنامه:\n\n" + ex.Message +
                    "\n\nجزئیات کامل در فایل Errors.log ثبت شد.",
                    "خطای سیستمی",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // ۴. نمایش فرم ورود (Login)
            User? loggedInUser = null;

            using (var loginForm = new LoginForm(AppConfig.ConnectionString))
            {
                if (loginForm.ShowDialog() != DialogResult.OK)
                {
                    return; // کاربر روی دکمه انصراف کلیک کرد
                }

                loggedInUser = loginForm.AuthenticatedUser;

                if (loggedInUser == null)
                {
                    MessageBox.Show("خطا در احراز هویت. برنامه بسته می‌شود.",
                        "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            // ۵. ذخیره اطلاعات کاربر در Session
            SessionManager.UserId = loggedInUser.Id;
            SessionManager.Username = loggedInUser.Username;
            SessionManager.FullName = loggedInUser.EmployeeFullName ?? "کاربر";
            SessionManager.Role = loggedInUser.Role;

            // ۶. باز کردن فرم اصلی برنامه
            Application.Run(new MainForm(loggedInUser));
        }
    }
}