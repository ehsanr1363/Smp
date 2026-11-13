USE [PersonnelDB];
GO

-- ۱. جدول حساب‌های بانکی و صندوق شرکت
IF OBJECT_ID('CompanyBankAccounts', 'U') IS NULL
CREATE TABLE CompanyBankAccounts (
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
IF OBJECT_ID('CashTransactions', 'U') IS NULL
CREATE TABLE CashTransactions (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    AccountId INT NOT NULL,
    TransactionDate DATETIME NOT NULL,
    Amount DECIMAL(18,0) NOT NULL,
    TransactionType NVARCHAR(50) NOT NULL, 
    Category NVARCHAR(100) NULL,           
    Description NVARCHAR(MAX) NULL,
    ReferenceNumber NVARCHAR(100) NULL,    
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (AccountId) REFERENCES CompanyBankAccounts(Id)
);
GO

-- ۳. جدول چک‌ها
IF OBJECT_ID('CashChecks', 'U') IS NULL
CREATE TABLE CashChecks (
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
    FOREIGN KEY (PartyId) REFERENCES Parties(Id)
);
GO

PRINT N'✅ جداول مربوط به مدیریت نقدینگی با موفقیت ساخته شدند.';