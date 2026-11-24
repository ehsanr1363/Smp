/* ============================================================
   17_FeeRatePrecision.sql
   یکسان‌سازی مقیاس «درصد بالاسری» و «مقسوم‌علیه حق‌الزحمه» در ClientContracts

   قرارداد: هر دو «عدد درصد» هستند (۱۰ یعنی ۱۰٪ ؛ ۸۳٫۳۳ یعنی ۸۳٫۳۳٪).
   - ستون‌ها از DECIMAL(5,2) به DECIMAL(9,4) تغییر می‌کنند تا ۸۳٫۳۳۳۳ دقیق ذخیره شود.
   - مقسوم‌علیه‌هایی که به‌صورت کسر ذخیره شده‌اند (مثل ۰٫۸۳) به درصد تبدیل می‌شوند (۰٫۸۳ → ۸۳٫۳۳۳۳).
   - اسکریپت idempotent است: فقط وقتی ستون هنوز DECIMAL(5,2) است تبدیل را انجام می‌دهد.
   قبل از اجرا Backup بگیرید.
   ============================================================ */

USE [PersonnelDB];
GO

-- ۱) پیش‌نمایش مقادیر فعلی (فقط نمایش)
SELECT c.Id, c.WorkLocation, c.HasFeeSection, c.OverheadPercentage, c.FeeDivisorPercentage
FROM dbo.ClientContracts c
WHERE c.OverheadPercentage <> 0 OR c.FeeDivisorPercentage <> 0;
GO

-- ۲) تغییر نوع ستون و تبدیل مقادیر
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.ClientContracts')
             AND name = N'FeeDivisorPercentage' AND [precision] = 5)
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRAN;

    -- ALTER COLUMN با DEFAULT موجود خطا می‌دهد؛ موقتاً حذف و بعد دوباره ساخته می‌شود
    IF OBJECT_ID(N'dbo.DF_ClientContracts_Overhead', N'D') IS NOT NULL
        ALTER TABLE dbo.ClientContracts DROP CONSTRAINT DF_ClientContracts_Overhead;
    IF OBJECT_ID(N'dbo.DF_ClientContracts_FeeDivisor', N'D') IS NOT NULL
        ALTER TABLE dbo.ClientContracts DROP CONSTRAINT DF_ClientContracts_FeeDivisor;

    ALTER TABLE dbo.ClientContracts ALTER COLUMN OverheadPercentage   DECIMAL(9,4) NOT NULL;
    ALTER TABLE dbo.ClientContracts ALTER COLUMN FeeDivisorPercentage DECIMAL(9,4) NOT NULL;

    ALTER TABLE dbo.ClientContracts ADD CONSTRAINT DF_ClientContracts_Overhead   DEFAULT 0 FOR OverheadPercentage;
    ALTER TABLE dbo.ClientContracts ADD CONSTRAINT DF_ClientContracts_FeeDivisor DEFAULT 0 FOR FeeDivisorPercentage;

    -- مقسوم‌علیه به‌صورت کسر (۰ < مقدار ≤ ۱) → درصد. بازه ۰٫۸ تا ۰٫۸۶ همان ۰٫۸۳۳۳ است.
    UPDATE dbo.ClientContracts
       SET FeeDivisorPercentage = CASE WHEN FeeDivisorPercentage BETWEEN 0.80 AND 0.86
                                       THEN 83.3333
                                       ELSE FeeDivisorPercentage * 100 END
     WHERE FeeDivisorPercentage > 0 AND FeeDivisorPercentage <= 1;

    COMMIT;
END
GO

-- ۳) نتیجه
SELECT c.Id, c.WorkLocation, c.HasFeeSection, c.OverheadPercentage, c.FeeDivisorPercentage
FROM dbo.ClientContracts c
WHERE c.OverheadPercentage <> 0 OR c.FeeDivisorPercentage <> 0;
GO

-- ۴) ثبت نسخه (فقط اگر جدول SchemaVersion از قبل ساخته شده باشد؛ وگرنه اسکریپت ۱۸ آن را ثبت می‌کند)
IF OBJECT_ID(N'dbo.SchemaVersion', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.SchemaVersion WHERE Version = 17)
    INSERT INTO dbo.SchemaVersion (Version, ScriptName) VALUES (17, N'17_FeeRatePrecision.sql');
GO
