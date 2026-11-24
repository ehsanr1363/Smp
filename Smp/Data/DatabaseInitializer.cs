using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;

namespace Smp.Data;

/// <summary>
/// ⚠️ این کلاس هیچ جدول یا ستونی نمی‌سازد.
/// فقط وجود ساختار را تأیید می‌کند.
/// ساختار فقط از طریق اسکریپت‌های Scripts/*.sql ایجاد می‌شود.
/// </summary>
public static class DatabaseInitializer
{
    private static bool _isVerified = false;
    private static readonly object _lock = new object();

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
        "CompanyBankAccounts", "CashTransactions", "CashChecks",
        "RealCompanyBankAccounts", // ✅ اضافه شدن جدول اصلی جدید
        "Tankhahs",                // ✅ اضافه شدن جدول تنخواه‌ها
        "TankhahLists"             // ✅ اضافه شدن جدول لیست‌های تنخواه
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
            if (result is null || result == DBNull.Value)
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

        // بررسی وجود جداول یا ویوها
        var missing = new List<string>();
        foreach (string tableName in RequiredTables)
        {
            // ✅ تغییر مهم: به جای sys.tables از sys.objects استفاده می‌کنیم تا View ها را هم قبول کند
            using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM sys.objects WHERE name = @name AND type IN ('U', 'V')", conn);
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
                "  3. Scripts/06_UnifyLiquiditySchema.sql\n" +
                "  4. Scripts/04_FixPayrollAndCarInsurance.sql\n" +
                "  5. Scripts/07_ContractSchemaFixes.sql");
        }

        // بررسی ستون‌های حیاتی
        VerifyCriticalColumn(conn, "Payrolls", "SupplementaryInsuranceDeduction");
        VerifyCriticalColumn(conn, "Payrolls", "CarInsuranceDeduction");
        VerifyCriticalColumn(conn, "Payrolls", "OtherDeductions");
        VerifyCriticalColumn(conn, "EmployeeContracts", "TransportationAllowance");
        VerifyCriticalColumn(conn, "ClientContracts", "Status");
        VerifyCriticalColumn(conn, "ContractorContracts", "ClientContractId");

        // ✅ ستون‌هایی که بخش قراردادها و پیمانکاری به آن‌ها وابسته است (اسکریپت 07)
        VerifyContractColumn(conn, "ContractorContracts", "TerminationDate");
        VerifyContractColumn(conn, "ClientContracts", "RetentionPercentage");
        VerifyContractColumn(conn, "ClientContracts", "TotalAdvanceAmount");
        VerifyContractColumn(conn, "ClientStatements", "RetentionDeduction");
        VerifyContractColumn(conn, "ClientStatements", "AdvanceRecovery");
        VerifyContractColumn(conn, "ClientStatements", "FeeAmount");
        VerifyContractColumn(conn, "ClientContracts", "HasFeeSection");
        VerifyContractColumn(conn, "ClientContracts", "FeeDivisorPercentage");
        VerifyContractColumn(conn, "Payrolls", "WorkLocation");
        VerifyContractColumn(conn, "ContractorPayments", "ClientContractId");
        VerifyContractColumn(conn, "ContractorPayments", "CostType");

        // ✅ ساختار نقدینگی (اسکریپت 06)
        VerifyLiquidityColumn(conn, "CashTransactions", "Direction");
        VerifyLiquidityColumn(conn, "CashTransactions", "PaymentScope");
        VerifyLiquidityColumn(conn, "CashChecks", "AccountId");
        VerifyLiquidityColumn(conn, "CashChecks", "SayadNumber");
        VerifyLiquidityColumn(conn, "CompanyBankAccounts", "IsCashDesk");
        VerifyLiquidityColumn(conn, "CompanyBankAccounts", "BankCode");

        // ✅ نسخه‌ی ساختار دیتابیس (جدول SchemaVersion)
        VerifySchemaVersion(conn);
    }

    /// <summary>
    /// آخرین نسخه‌ی ساختار دیتابیس که این نسخه از برنامه لازم دارد = شماره‌ی آخرین اسکریپت در Scripts/.
    /// هر اسکریپت جدید باید در انتها ردیف خودش را در dbo.SchemaVersion ثبت کند و این عدد هم بالا برود.
    /// </summary>
    public const int RequiredSchemaVersion = 17;

    private static void VerifySchemaVersion(SqlConnection conn)
    {
        bool hasTable;
        using (var chk = new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.SchemaVersion', N'U') IS NULL THEN 0 ELSE 1 END", conn))
            hasTable = Convert.ToInt32(chk.ExecuteScalar()) == 1;

        if (!hasTable)
        {
            throw new InvalidOperationException(
                "جدول نسخه‌ی ساختار دیتابیس (SchemaVersion) وجود ندارد.\n\n" +
                "لطفاً (بعد از گرفتن بکاپ) اسکریپت‌های Scripts/17_FeeRatePrecision.sql و سپس " +
                "Scripts/18_SchemaVersion.sql را در SSMS اجرا کنید و برنامه را دوباره باز کنید.");
        }

        int current;
        using (var cmd = new SqlCommand("SELECT ISNULL(MAX(Version), 0) FROM dbo.SchemaVersion", conn))
            current = Convert.ToInt32(cmd.ExecuteScalar());

        if (current < RequiredSchemaVersion)
        {
            throw new InvalidOperationException(
                $"ساختار دیتابیس قدیمی است (نسخه‌ی {current}؛ این برنامه نسخه‌ی {RequiredSchemaVersion} را لازم دارد).\n\n" +
                $"اسکریپت‌های شماره‌دارِ بعد از {current:00} در پوشه‌ی Scripts را به ترتیب در SSMS اجرا کنید (قبلش بکاپ بگیرید).");
        }
    }

    private static void VerifyLiquidityColumn(SqlConnection conn, string table, string column)
    {
        using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(@table) AND name = @col", conn);
        cmd.Parameters.AddWithValue("@table", table);
        cmd.Parameters.AddWithValue("@col", column);
        if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
            throw new InvalidOperationException(
                $"ساختار بخش نقدینگی قدیمی است: ستون «{table}.{column}» وجود ندارد.\n\n" +
                "لطفاً اسکریپت‌های Scripts/03_CreateLiquidityTables.sql، Scripts/06_UnifyLiquiditySchema.sql و Scripts/04_FixPayrollAndCarInsurance.sql را به ترتیب در SSMS اجرا کنید (قبلش بکاپ بگیرید).");
    }

    private static void VerifyContractColumn(SqlConnection conn, string table, string column)
    {
        using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(@table) AND name = @col", conn);
        cmd.Parameters.AddWithValue("@table", table);
        cmd.Parameters.AddWithValue("@col", column);

        if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
        {
            throw new InvalidOperationException(
                $"ستون «{table}.{column}» در دیتابیس وجود ندارد.\n\n" +
                "لطفاً اسکریپت‌های Scripts/07_ContractSchemaFixes.sql و Scripts/14_ClientFeeSection.sql را در SSMS اجرا کنید (قبلش بکاپ بگیرید).");
        }
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
