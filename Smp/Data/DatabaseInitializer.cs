using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;

namespace SMP.Data
{
    /// <summary>
    /// ⚠️ این کلاس هیچ جدول یا ستونی نمی‌سازد.
    /// فقط وجود ساختار را تأیید می‌کند.
    /// ساختار فقط از طریق اسکریپت‌های Scripts/*.sql ایجاد می‌شود.
    /// </summary>
    public static class DatabaseInitializer
    {
        private static bool _isVerified = false;
        private static readonly object _lock = new object();

        // ✅ FIX: جدول Banks از لیست جداول ضروری حذف شد (چون به BankHelper هاردکد منتقل شد)
        private static readonly string[] RequiredTables =
        {
            "Employees", "Users", "Departments", "Companies", "LookupItems",
            "Parties", "Dependents", "EmployeeBankAccounts",
            "ClientContracts", "ContractorContracts", "ContractArchives",
            "InsuranceRecords", "WorkshopRecords", "Payrolls",
            "SalarySettings", "TaxBrackets", "ClientPayments", "ClientStatements",
            "ContractorGuarantees", "ContractorStatements", "ContractorPayments",
            "ProjectExpenses", "PartyBankAccounts", "DailyWageRates",
            "EmployeeContracts", "EmployeeLoans", "LeaveRequests", "Attendance",
            "AuditLogs", "Attachments", "Settlements", "YearEndBonuses",
            "EmployeeSupplementaryInsurances", "EmployeeCarInsurances", "Tafsilis",
            "CompanyBankAccounts", "CashTransactions", "CashChecks"
        };

        public static void Initialize()
        {
            if (_isVerified) return;
            lock (_lock)
            {
                if (_isVerified) return;
                VerifyDatabaseExists();
                VerifySchemaExists();
                _isVerified = true;
            }
        }

        private static void VerifyDatabaseExists()
        {
            string connectionString = AppConfig.ConnectionString;
            string dbName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

            var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };

            try
            {
                using var conn = new SqlConnection(builder.ConnectionString);
                conn.Open();

                using var cmd = new SqlCommand("SELECT DB_ID(@name)", conn);
                cmd.Parameters.AddWithValue("@name", dbName);

                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                {
                    throw new InvalidOperationException(
                        $"دیتابیس «{dbName}» وجود ندارد.\n\n" +
                        "برای راه‌اندازی اولیه:\n" +
                        "  1. فایل Scripts/01_InstallDatabase.sql را باز کنید\n" +
                        "  2. در SQL Server Management Studio (SSMS) اجرا کنید\n" +
                        "  3. سپس برنامه را دوباره باز کنید");
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException(
                    "اتصال به SQL Server برقرار نشد.\n\n" +
                    "موارد زیر را بررسی کنید:\n" +
                    "• سرویس SQL Server در حال اجراست؟\n" +
                    "• نام سرور در App.config درست است؟\n" +
                    "• کاربر Windows شما دسترسی دارد؟\n\n" +
                    "جزئیات: " + ex.Message, ex);
            }
        }

        private static void VerifySchemaExists()
        {
            using var conn = new SqlConnection(AppConfig.ConnectionString);
            try { conn.Open(); }
            catch (SqlException ex) when (ex.Number == 4060)
            {
                throw new InvalidOperationException(
                    "دیتابیس یافت نشد. لطفاً اسکریپت نصب را اجرا کنید.", ex);
            }

            // بررسی وجود جداول
            var missing = new List<string>();
            foreach (string tableName in RequiredTables)
            {
                using var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM sys.tables WHERE name = @name", conn);
                cmd.Parameters.AddWithValue("@name", tableName);
                if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                    missing.Add(tableName);
            }

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "ساختار دیتابیس ناقص است. جداول زیر یافت نشدند:\n\n  • " +
                    string.Join("\n  • ", missing) +
                    "\n\nلطفاً فایل‌های زیر را به ترتیب در SSMS اجرا کنید:\n" +
                    "  1. Scripts/01_InstallDatabase.sql\n" +
                    "  2. Scripts/03_CreateLiquidityTables.sql\n" +
                    "  3. Scripts/04_FixPayrollAndCarInsurance.sql");
            }

            // بررسی ستون‌های حیاتی
            VerifyCriticalColumn(conn, "Payrolls", "SupplementaryInsuranceDeduction");
            VerifyCriticalColumn(conn, "Payrolls", "CarInsuranceDeduction");
            VerifyCriticalColumn(conn, "Payrolls", "OtherDeductions");
            VerifyCriticalColumn(conn, "EmployeeContracts", "TransportationAllowance");
            VerifyCriticalColumn(conn, "ClientContracts", "Status");
            VerifyCriticalColumn(conn, "ContractorContracts", "ClientContractId");
        }

        private static void VerifyCriticalColumn(SqlConnection conn, string table, string column)
        {
            using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM sys.columns " +
                "WHERE object_id = OBJECT_ID(@table) AND name = @col", conn);
            cmd.Parameters.AddWithValue("@table", table);
            cmd.Parameters.AddWithValue("@col", column);

            if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
            {
                throw new InvalidOperationException(
                    $"ستون «{table}.{column}» در دیتابیس وجود ندارد.\n\n" +
                    "لطفاً اسکریپت Scripts/04_FixPayrollAndCarInsurance.sql را اجرا کنید.");
            }
        }
    }
}