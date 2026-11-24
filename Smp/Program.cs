using System;
using System.Windows.Forms;
using Smp.Data;
using Smp.Forms;
using Smp.Helpers;

namespace Smp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            GlobalExceptionHelper.Setup();
            ModernSkin.EnableGlobalGridSweep();   // هر جدولی که جایی مدرن نشده باشد خودکار مدرن می‌شود

            try
            {
                DatabaseInitializer.Initialize();

                using var loginForm = new LoginForm(AppConfig.ConnectionString);
                if (loginForm.ShowDialog() != DialogResult.OK) return;

                var user = loginForm.AuthenticatedUser!;
                SessionManager.Start(user);

                // تغییر رمز اجباری (اولین ورود / رمز پیش‌فرض / بازنشانی توسط مدیر)
                if (user.MustChangePassword)
                {
                    using var changeForm = new ChangePasswordForm(user, new UserRepository(AppConfig.ConnectionString));
                    if (changeForm.ShowDialog() != DialogResult.OK) return;
                }

                AuthorizationHelper.Initialize(AppConfig.ConnectionString);
                AuditHelper.Log("ورود", "احراز هویت", "ورود موفق به سیستم");

                Application.Run(new MainForm(user));
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "Program.Main");
                MessageBox.Show("خطا در راه‌اندازی برنامه:\n\n" + ex.Message, "خطای سیستمی — سمپ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}