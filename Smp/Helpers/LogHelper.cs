using System;
using System.IO;

namespace Smp.Helpers;

public static class LogHelper
{
    private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

    // ✅ FIX 1: قفل اختصاصی برای جلوگیری از تداخل نوشتن همزمان در فایل
    private static readonly object _fileLock = new object();

    // حداکثر حجم فایل لاگ (مثلاً ۵ مگابایت) قبل از بایگانی
    private const long MaxLogSize = 5 * 1024 * 1024;

    public static void LogException(Exception ex, string moduleName = "General")
    {
        if (ex is null) return;
        string logContent = $"[{DateTime.Now:HH:mm:ss}] [Module: {moduleName}] [Error: {ex.Message}]\nStackTrace:\n{ex.StackTrace}\n--------------------------------------------------\n";
        WriteToFile("ErrorLog", logContent);
    }

    public static void LogInfo(string message, string moduleName = "General")
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        string logContent = $"[{DateTime.Now:HH:mm:ss}] [{moduleName}] {message}\n";
        WriteToFile("InfoLog", logContent);
    }

    public static void LogWarning(string message, string moduleName = "General")
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        string logContent = $"[{DateTime.Now:HH:mm:ss}] [{moduleName}] [WARNING] {message}\n";
        WriteToFile("WarningLog", logContent);
    }

    // ✅ FIX 2: متد مرکزی برای نوشتن با قفل، ساخت خودکار پوشه و مدیریت حجم
    private static void WriteToFile(string filePrefix, string content)
    {
        try
        {
            lock (_fileLock) // قفل کردن برای جلوگیری از خطای Sharing Violation
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                string filePath = Path.Combine(LogDirectory, $"{filePrefix}_{DateTime.Now:yyyy-MM-dd}.txt");

                // ✅ FIX 3: اگر فایل بزرگتر از ۵ مگابایت شد، آن را بایگانی کن
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length > MaxLogSize)
                    {
                        string archivePath = Path.Combine(LogDirectory, $"{filePrefix}_{DateTime.Now:yyyy-MM-dd}_{DateTime.Now:HHmmss}.bak");
                        File.Move(filePath, archivePath);
                    }
                }

                File.AppendAllText(filePath, content);
            }
        }
        catch
        {
            // اگر نوشتن در فایل با خطا مواجه شد، برنامه کرش نکند
        }
    }
}
