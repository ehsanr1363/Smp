USE [PersonnelDB];
GO

-- ۱. جدول حساب‌های بانکی و صندوق شرکت
IF OBJECT_ID('dbo.CompanyBankAccounts') IS NULL
CREATE TABLE dbo.CompanyBankAccounts (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Title NVARCHAR(150) NOT NULL,
    AccountNumber NVARCHAR(100) NULL,
    InitialBalance DECIMAL(18,0) NOT NULL DEFAULT 0,
    CurrentBalance DECIMAL(18,0) NOT NULL DEFAULT 0,
    IsCashDesk BIT DEFAULT 0,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME DEFAULT GETDATE()
);
GO

-- ۲. جدول تراکنش‌های نقدی (دریافت و پرداخت)
IF OBJECT_ID('dbo.CashTransactions', 'U') IS NULL
CREATE TABLE dbo.CashTransactions (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    AccountId INT NOT NULL,
    TransactionDate DATETIME NOT NULL,
    Amount DECIMAL(18,0) NOT NULL,
    TransactionType NVARCHAR(50) NOT NULL,
    Category NVARCHAR(100) NULL,
    Description NVARCHAR(MAX) NULL,
    ReferenceNumber NVARCHAR(100) NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (AccountId) REFERENCES dbo.CompanyBankAccounts(Id)
);
GO

-- ۳. جدول چک‌ها
IF OBJECT_ID('dbo.CashChecks', 'U') IS NULL
CREATE TABLE dbo.CashChecks (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CheckNumber NVARCHAR(100) NOT NULL,
    BankName NVARCHAR(150) NULL,
    DueDate DATETIME NOT NULL,
    Amount DECIMAL(18,0) NOT NULL,
    CheckType NVARCHAR(50) NOT NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT N'در جریان',
    PartyId INT NULL,
    Description NVARCHAR(MAX) NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (PartyId) REFERENCES dbo.Parties(Id)
);
GO

PRINT N'✅ جداول مربوط به مدیریت نقدینگی با موفقیت ساخته شدند.';
