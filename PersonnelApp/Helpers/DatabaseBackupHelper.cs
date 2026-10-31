using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace PersonnelApp.Helpers
{
    public static class DatabaseBackupHelper
    {
        public static string GetDefaultBackupPath(string connectionString)
        {
            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();

                using var cmd = new SqlCommand(
                    "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS NVARCHAR(512))", conn);
                var result = cmd.ExecuteScalar()?.ToString();
                if (!string.IsNullOrWhiteSpace(result) && Directory.Exists(result))
                    return result;

                using var cmd2 = new SqlCommand(
                    "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS NVARCHAR(512))", conn);
                var result2 = cmd2.ExecuteScalar()?.ToString();
                if (!string.IsNullOrWhiteSpace(result2) && Directory.Exists(result2))
                    return result2;
            }
            catch (Exception ex)
            {
                LogHelper.LogWarning("GetDefaultBackupPath: " + ex.Message, "DatabaseBackupHelper");
            }

            string fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
                "PersonnelApp_Backups");
            if (!Directory.Exists(fallback)) Directory.CreateDirectory(fallback);
            return fallback;
        }

        public static void BackupDatabase(string connectionString, string backupFilePath)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            string dbName = builder.InitialCatalog;
            ValidateDbName(dbName);

            // اطمینان از وجود پوشه
            string dir = Path.GetDirectoryName(backupFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using var conn = new SqlConnection(connectionString);
            conn.Open();

            string query = $@"
                BACKUP DATABASE [{dbName}]
                TO DISK = @path
                WITH FORMAT, MEDIANAME = 'SQLServerBackups',
                     NAME = N'Full Backup of {dbName}';";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@path", backupFilePath);
            cmd.CommandTimeout = 300;
            cmd.ExecuteNonQuery();
        }

        public static void RestoreDatabase(string connectionString, string backupFilePath)
        {
            if (!File.Exists(backupFilePath))
                throw new FileNotFoundException("فایل بک‌آپ یافت نشد.", backupFilePath);

            var builder = new SqlConnectionStringBuilder(connectionString);
            string dbName = builder.InitialCatalog;
            ValidateDbName(dbName);

            string defaultDataPath = GetDefaultDataPath(connectionString);

            builder.InitialCatalog = "master";
            using var conn = new SqlConnection(builder.ConnectionString);
            conn.Open();

            // آیا دیتابیس وجود دارد؟
            bool dbExists;
            using (var cmdExists = new SqlCommand("SELECT DB_ID(@name)", conn))
            {
                cmdExists.Parameters.AddWithValue("@name", dbName);
                var res = cmdExists.ExecuteScalar();
                dbExists = res != null && res != DBNull.Value;
            }

            // لیست فایل‌های منطقی داخل بک‌آپ
            var moveClauses = new List<string>();
            using (var cmd = new SqlCommand("RESTORE FILELISTONLY FROM DISK = @path", conn))
            {
                cmd.Parameters.AddWithValue("@path", backupFilePath);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string logicalName = reader.GetString(reader.GetOrdinal("LogicalName"));
                    string type = reader.GetString(reader.GetOrdinal("Type"));
                    string ext = (type == "L") ? ".ldf" : ".mdf";
                    string physical = Path.Combine(defaultDataPath, $"{dbName}_{logicalName}{ext}");
                    moveClauses.Add(
                        $"MOVE N'{logicalName.Replace("'", "''")}' TO N'{physical.Replace("'", "''")}'");
                }
            }

            if (moveClauses.Count == 0)
                throw new InvalidOperationException("لیست فایل‌های بک‌آپ خالی است.");

            string moveSql = string.Join(", ", moveClauses);

            string pre = dbExists
                ? $"ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;"
                : "";

            string query = $@"
                {pre}
                BEGIN TRY
                    RESTORE DATABASE [{dbName}] FROM DISK = @path
                    WITH REPLACE, {moveSql};
                    ALTER DATABASE [{dbName}] SET MULTI_USER;
                END TRY
                BEGIN CATCH
                    IF DB_ID('{dbName}') IS NOT NULL
                        ALTER DATABASE [{dbName}] SET MULTI_USER;
                    THROW;
                END CATCH";

            using var cmdExec = new SqlCommand(query, conn);
            cmdExec.Parameters.AddWithValue("@path", backupFilePath);
            cmdExec.CommandTimeout = 600;
            cmdExec.ExecuteNonQuery();
        }

        private static string GetDefaultDataPath(string connectionString)
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            using var cmd = new SqlCommand(
                "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS NVARCHAR(512))", conn);
            var result = cmd.ExecuteScalar()?.ToString();

            if (!string.IsNullOrWhiteSpace(result) && Directory.Exists(result))
                return result;

            string fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "PersonnelApp_Data");
            if (!Directory.Exists(fallback)) Directory.CreateDirectory(fallback);
            return fallback;
        }

        // 👈 تغییر اعمال شد: از private به public تغییر کرد تا از فرم بازیابی هم قابل دسترسی باشد
        public static void ValidateDbName(string dbName)
        {
            if (string.IsNullOrWhiteSpace(dbName))
                throw new InvalidOperationException("نام دیتابیس خالی است.");
            if (!Regex.IsMatch(dbName, @"^[A-Za-z0-9_\u0600-\u06FF]+$"))
                throw new InvalidOperationException("نام دیتابیس شامل کاراکترهای غیرمجاز است.");
        }
    }
}