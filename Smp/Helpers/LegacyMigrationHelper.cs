using System;
using System.IO;

namespace SMP.Helpers
{
    public static class LegacyMigrationHelper
    {
        public static void MigrateIfNeeded()
        {
            try
            {
                string oldAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PersonnelApp");
                string newAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SMP");

                if (Directory.Exists(newAppData)) return;
                if (!Directory.Exists(oldAppData)) return;

                Directory.CreateDirectory(newAppData);
                foreach (var file in Directory.GetFiles(oldAppData, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        string relative = Path.GetRelativePath(oldAppData, file);
                        string target = Path.Combine(newAppData, relative);
                        string? dir = Path.GetDirectoryName(target);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        File.Copy(file, target, overwrite: false);
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}