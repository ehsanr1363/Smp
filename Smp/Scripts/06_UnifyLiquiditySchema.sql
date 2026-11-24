-- ============================================================
-- 06: یکپارچه‌سازی ساختار حساب‌های بانکی شرکت (نقدینگی)
--   جدول واقعی:  dbo.RealCompanyBankAccounts
--   نمای سازگار: dbo.CompanyBankAccounts  (کدهای قدیمی از همین نام می‌خوانند و UPDATE می‌زنند)
-- اجرای چندباره بی‌خطر است. اگر View از قبل وجود داشته باشد، دست‌نخورده می‌ماند.
-- ترتیب: بعد از 03 و قبل از 04. قبل از اجرا روی دیتابیس موجود Backup بگیرید.
-- ============================================================
USE [PersonnelDB];
GO
SET NOCOUNT ON;

-- 1) نصب قدیمی: CompanyBankAccounts هنوز «جدول» است -> تغییر نام (شناسه‌ها و کلیدهای خارجی حفظ می‌شوند)
IF OBJECT_ID('dbo.RealCompanyBankAccounts') IS NULL AND OBJECT_ID('dbo.CompanyBankAccounts', 'U') IS NOT NULL
    EXEC sp_rename 'dbo.CompanyBankAccounts', 'RealCompanyBankAccounts';
GO

-- 2) نصب تازه: ساخت جدول واقعی
IF OBJECT_ID('dbo.RealCompanyBankAccounts') IS NULL
CREATE TABLE dbo.RealCompanyBankAccounts (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Title NVARCHAR(150) NOT NULL,
    AccountNumber NVARCHAR(100) NULL,
    InitialBalance DECIMAL(18,0) NOT NULL DEFAULT 0,
    CurrentBalance DECIMAL(18,0) NOT NULL DEFAULT 0,
    IsCashDesk BIT NULL DEFAULT 0,
    IsActive BIT NULL DEFAULT 1,
    CreatedAt DATETIME NULL DEFAULT GETDATE(),
    BankCode NVARCHAR(10) NULL,
    HasCheckbook BIT NOT NULL DEFAULT 0,
    AccountType NVARCHAR(50) NULL,
    Branch NVARCHAR(100) NULL
);
GO

-- 3) ستون‌های جدیدتر (برای جدول‌های قدیمی)
IF COL_LENGTH('dbo.RealCompanyBankAccounts', 'BankCode') IS NULL
    EXEC(N'ALTER TABLE dbo.RealCompanyBankAccounts ADD BankCode NVARCHAR(10) NULL');
IF COL_LENGTH('dbo.RealCompanyBankAccounts', 'HasCheckbook') IS NULL
    EXEC(N'ALTER TABLE dbo.RealCompanyBankAccounts ADD HasCheckbook BIT NOT NULL CONSTRAINT DF_RealCompanyBankAccounts_HasCheckbook DEFAULT 0');
IF COL_LENGTH('dbo.RealCompanyBankAccounts', 'AccountType') IS NULL
    EXEC(N'ALTER TABLE dbo.RealCompanyBankAccounts ADD AccountType NVARCHAR(50) NULL');
IF COL_LENGTH('dbo.RealCompanyBankAccounts', 'Branch') IS NULL
    EXEC(N'ALTER TABLE dbo.RealCompanyBankAccounts ADD Branch NVARCHAR(100) NULL');
GO

-- 4) نمای سازگار (ساده و قابل UPDATE)
IF OBJECT_ID('dbo.CompanyBankAccounts') IS NULL
    EXEC(N'CREATE VIEW dbo.CompanyBankAccounts AS
           SELECT Id, Title, AccountNumber, InitialBalance, CurrentBalance, IsCashDesk, IsActive,
                  CreatedAt, BankCode, HasCheckbook, AccountType, Branch
           FROM dbo.RealCompanyBankAccounts');
GO

PRINT N'ساختار حساب‌های بانکی شرکت بررسی/یکپارچه شد.';
