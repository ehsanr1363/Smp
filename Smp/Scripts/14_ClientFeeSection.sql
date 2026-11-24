/* =====================================================================
   اسکریپت ۱۴ — قرارداد دو بخشی کارفرما: کارکرد (بدون ارزش افزوده) + حق‌الزحمه (مشمول ارزش افزوده)
   قبل از اجرا: Backup بگیرید. اسکریپت چندبار اجرا هم بی‌خطر است (idempotent).

   • ClientContracts.HasFeeSection : 0 = رفتار قدیمی (ارزش افزوده روی کل کارکرد) | 1 = دو بخشی
   • ClientContracts.FeePercentage : درصد پیش‌فرض حق‌الزحمه نسبت به کارکرد
   • ClientStatements.FeeAmount    : مبلغ حق‌الزحمه‌ی هر صورت‌وضعیت
   • ClientContracts.WorkLocation  : محل خدمت (از «مدیریت لیست‌ها») — اتصال قرارداد به کارگران روزمزد همان محل
   • ClientContracts.OverheadPercentage    : درصد بالاسری
   • ClientContracts.FeeDivisorPercentage  : درصد بیمه‌ی مقسوم‌علیه (حق‌الزحمه = (حقوق و اضافه‌کار − بیمه ۷٪) × بالاسری ÷ این درصد)
   صورت‌وضعیت‌های قبلی تغییری نمی‌کنند (FeeAmount = 0 و VAT ذخیره‌شده حفظ می‌شود).
   ===================================================================== */

USE [PersonnelDB];
GO

SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.ClientContracts', N'HasFeeSection') IS NULL
    ALTER TABLE dbo.ClientContracts ADD HasFeeSection BIT NOT NULL CONSTRAINT DF_ClientContracts_HasFeeSection DEFAULT 0;

IF COL_LENGTH(N'dbo.ClientContracts', N'FeePercentage') IS NULL
    ALTER TABLE dbo.ClientContracts ADD FeePercentage DECIMAL(5,2) NOT NULL CONSTRAINT DF_ClientContracts_FeePercentage DEFAULT 0;

IF COL_LENGTH(N'dbo.ClientStatements', N'FeeAmount') IS NULL
    ALTER TABLE dbo.ClientStatements ADD FeeAmount DECIMAL(18,0) NOT NULL CONSTRAINT DF_ClientStatements_FeeAmount DEFAULT 0;

IF COL_LENGTH(N'dbo.ClientContracts', N'WorkLocation') IS NULL
    ALTER TABLE dbo.ClientContracts ADD WorkLocation NVARCHAR(150) NULL;

IF COL_LENGTH(N'dbo.ClientContracts', N'OverheadPercentage') IS NULL
    ALTER TABLE dbo.ClientContracts ADD OverheadPercentage DECIMAL(9,4) NOT NULL CONSTRAINT DF_ClientContracts_Overhead DEFAULT 0;

IF COL_LENGTH(N'dbo.ClientContracts', N'FeeDivisorPercentage') IS NULL
    ALTER TABLE dbo.ClientContracts ADD FeeDivisorPercentage DECIMAL(9,4) NOT NULL CONSTRAINT DF_ClientContracts_FeeDivisor DEFAULT 0;
GO

-------------------------------------------------------------
-- عکس فوری «محل خدمت» و «نوع پرسنلی» روی هر ردیف حقوق
-- (تا جابه‌جایی بعدی کارگر، حقوق ماه‌های گذشته را به محل جدید نبرد)
-------------------------------------------------------------
IF COL_LENGTH(N'dbo.Payrolls', N'WorkLocation')  IS NULL ALTER TABLE dbo.Payrolls ADD WorkLocation  NVARCHAR(150) NULL;
IF COL_LENGTH(N'dbo.Payrolls', N'PersonnelType') IS NULL ALTER TABLE dbo.Payrolls ADD PersonnelType NVARCHAR(100) NULL;
GO

-- پر کردن ردیف‌های قدیمی از کارت فعلی پرسنل (بهترین تقریب ممکن؛ فقط ردیف‌هایی که هنوز خالی‌اند)
-- ⚠ برای ماه‌هایی که کارگر بعداً جابه‌جا شده، قبل از ثبت صورت‌وضعیت، ستون Payrolls.WorkLocation همان ماه را دستی اصلاح کنید.
EXEC(N'
UPDATE p
SET p.WorkLocation  = LTRIM(RTRIM(e.WorkLocation)),
    p.PersonnelType = LTRIM(RTRIM(e.PersonnelType))
FROM dbo.Payrolls p
INNER JOIN dbo.Employees e ON e.Id = p.EmployeeId
WHERE p.WorkLocation IS NULL AND p.PersonnelType IS NULL;
');
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payrolls_Period_Location' AND object_id = OBJECT_ID(N'dbo.Payrolls'))
    EXEC(N'CREATE INDEX IX_Payrolls_Period_Location ON dbo.Payrolls (Year, Month, WorkLocation)');
GO

PRINT N'اسکریپت ۱۴ با موفقیت اجرا شد.';
