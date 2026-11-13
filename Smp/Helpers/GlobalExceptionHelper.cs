using System;
using System.Windows.Forms;

namespace SMP.Helpers
{
    public static class GlobalExceptionHelper
    {
        public static void Setup()
        {
            // خطاهای UI Thread → می‌توانیم فرم سفارشی نمایش دهیم
            Application.ThreadException += (s, e) => HandleUiException(e.Exception);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            // خطاهای Thread پس‌زمینه → ساخت Form ممکن است خطا بدهد
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                HandleBackgroundException(e.ExceptionObject as Exception);
        }

        private static void HandleUiException(Exception ex)
        {
            if (ex == null) return;
            LogHelper.LogException(ex, "UI Thread");

            try
            {
                ModernMessageBox.ShowError(
                    "متأسفیم! یک خطای پیش‌بینی‌نشده رخ داد.\n\n" +
                    $"جزئیات خطا:\n{ex.Message}\n\n" +
                    "اطلاعات کامل در فایل Errors.log ذخیره شد.",
                    "خطای سیستمی");
            }
            catch
            {
                // اگر ModernMessageBox هم شکست خورد، به MessageBox استاندارد برگرد
                try
                {
                    MessageBox.Show(ex.Message, "خطای سیستمی",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { /* فقط لاگ بالا کافی است */ }
            }
        }

        private static void HandleBackgroundException(Exception? ex)
        {
            if (ex == null) return;
            LogHelper.LogException(ex, "Background Thread");

            // در Thread پس‌زمینه (MTA) ساخت Form ممکن است خطا بدهد → MessageBox ساده
            try
            {
                MessageBox.Show(
                    "یک خطای پیش‌بینی‌نشده در پس‌زمینه رخ داد:\n\n" +
                    $"{ex.Message}\n\nاطلاعات کامل در Errors.log ذخیره شد.",
                    "خطای سیستمی",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch { /* اگر حتی MessageBox هم شکست خورد، فقط لاگ */ }
        }
    }
}