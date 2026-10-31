using System;
using System.IO;
using System.Text;

namespace PersonnelApp.Helpers
{
    public static class LogHelper
    {
        private static readonly string LogFolder = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string LogFilePath = Path.Combine(LogFolder, "Errors.log");
        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

        private static readonly object _lock = new object();

        public static void LogException(Exception ex, string context = "")
            => LogError(ex, context);

        public static void LogError(Exception ex, string context = "")
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine($"تاریخ و زمان: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("سطح: ERROR");
            if (!string.IsNullOrEmpty(context))
                sb.AppendLine($"بخش برنامه: {context}");
            sb.AppendLine($"نوع خطا: {ex.GetType().FullName}");
            sb.AppendLine($"پیام خطا: {ex.Message}");
            sb.AppendLine($"جزئیات فنی:\n{ex.StackTrace}");

            if (ex.InnerException != null)
            {
                sb.AppendLine("--- خطای داخلی ---");
                sb.AppendLine($"پیام: {ex.InnerException.Message}");
                sb.AppendLine($"جزئیات: {ex.InnerException.StackTrace}");
            }
            sb.AppendLine("==================================================");
            sb.AppendLine();

            Write(sb.ToString());
        }

        public static void LogInfo(string message, string context = "") => LogLevel("INFO", message, context);
        public static void LogWarning(string message, string context = "") => LogLevel("WARNING", message, context);

        private static void LogLevel(string level, string message, string context)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {(string.IsNullOrEmpty(context) ? "" : $"[{context}] ")}{message}");
            Write(sb.ToString());
        }

        private static void Write(string content)
        {
            lock (_lock)
            {
                try
                {
                    RotateIfNeeded();
                    File.AppendAllText(LogFilePath, content, Encoding.UTF8);
                }
                catch { /* اگر لاگ نویسی هم خطا داد، برنامه کرش نکند */ }
            }
        }

        private static void RotateIfNeeded()
        {
            try
            {
                if (!File.Exists(LogFilePath)) return;
                var fi = new FileInfo(LogFilePath);
                if (fi.Length < MaxFileSizeBytes) return;

                string archive = Path.Combine(LogFolder,
                    $"Errors_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                File.Move(LogFilePath, archive);
            }
            catch { /* rotation خطا داد — ادامه بده */ }
        }
    }
}