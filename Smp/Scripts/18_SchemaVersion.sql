/* ============================================================
   18_SchemaVersion.sql
   جدول نسخه‌ی ساختار دیتابیس. برنامه هنگام شروع نسخه را با نسخه‌ی موردنیازش مقایسه می‌کند
   و اگر دیتابیس قدیمی باشد، به‌جای محاسبه‌ی غلط، پیام واضح می‌دهد.

   ⚠️ قبل از اجرا: اسکریپت‌های ۰۱ تا ۱۷ باید اجرا شده باشند (به‌خصوص ۱۷).
   این اسکریپت «نسخه‌ی ۱۷» را فقط وقتی ثبت می‌کند که نشانه‌های ۱۷ در دیتابیس دیده شود.
   قبل از اجرا Backup بگیرید. چندبار اجرا شود بی‌خطر است.

   قاعده برای اسکریپت‌های بعدی (۱۹ و ...): در انتها این را بگذارید و در برنامه
   DatabaseInitializer.RequiredSchemaVersion را هم بالا ببرید:
       IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersion WHERE Version = 19)
           INSERT INTO dbo.SchemaVersion (Version, ScriptName) VALUES (19, N'19_Xxx.sql');
   ============================================================ */

USE [PersonnelDB];
GO

IF OBJECT_ID(N'dbo.SchemaVersion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaVersion
    (
        Version     INT           NOT NULL CONSTRAINT PK_SchemaVersion PRIMARY KEY,
        ScriptName  NVARCHAR(200) NOT NULL,
        AppliedAt   DATETIME2(0)  NOT NULL CONSTRAINT DF_SchemaVersion_AppliedAt DEFAULT SYSDATETIME(),
        AppliedBy   SYSNAME       NOT NULL CONSTRAINT DF_SchemaVersion_AppliedBy DEFAULT SUSER_SNAME()
    );
END
GO

-- نسخه‌ی پایه (۱۷): اگر اسکریپت ۱۷ هنوز اجرا نشده، ثبت نمی‌شود و خطا می‌دهد
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersion WHERE Version = 17)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'dbo.ClientContracts')
                     AND name = N'FeeDivisorPercentage' AND [precision] >= 9)
        THROW 50001, N'اسکریپت 17_FeeRatePrecision.sql هنوز اجرا نشده است. ابتدا آن را اجرا کنید.', 1;

    INSERT INTO dbo.SchemaVersion (Version, ScriptName) VALUES (17, N'پایه: اسکریپت‌های 01 تا 17');
END

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersion WHERE Version = 18)
    INSERT INTO dbo.SchemaVersion (Version, ScriptName) VALUES (18, N'18_SchemaVersion.sql');
GO

SELECT * FROM dbo.SchemaVersion ORDER BY Version;
GO
