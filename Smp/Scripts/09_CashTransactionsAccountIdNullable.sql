-- ردیف‌های هزینه‌ی تنخواه به حساب بانکی وصل نیستند (پول هنگام شارژ از بانک خارج شده)،
-- پس CashTransactions.AccountId باید NULL بپذیرد. (قبلش بکاپ بگیرید)
USE [PersonnelDB];
GO

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.CashTransactions') AND name = 'AccountId' AND is_nullable = 0)
BEGIN
    ALTER TABLE dbo.CashTransactions ALTER COLUMN AccountId INT NULL;
    PRINT N'AccountId اکنون NULL می‌پذیرد.';
END
ELSE
    PRINT N'نیازی به تغییر نبود.';
GO
