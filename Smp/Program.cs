using System;
using System.Windows.Forms;
using SMP.Data;
using SMP.Forms;
using SMP.Helpers;

namespace SMP
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

            try
            {
                LegacyMigrationHelper.MigrateIfNeeded();
                DatabaseInitializer.Initialize();

                using var loginForm = new LoginForm(AppConfig.ConnectionString);
                if (loginForm.ShowDialog() != DialogResult.OK) return;

                Application.Run(new MainForm(loginForm.AuthenticatedUser));
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "Program.Main");
                MessageBox.Show("خطا در راه‌اندازی برنامه:\n\n" + ex.Message, "خطای سیستمی — سمپ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}