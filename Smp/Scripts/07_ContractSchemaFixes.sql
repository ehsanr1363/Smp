/* =====================================================================
   اسکریپت ۰۷ — تکمیل ساختار بخش قراردادها و پیمانکاری + ایندکس‌ها
   قبل از اجرا: Backup بگیرید. اسکریپت چندبار اجرا هم بی‌خطر است (idempotent).

   ۱) ستون‌هایی که کد برنامه به آن‌ها نیاز دارد ولی در 01_InstallDatabase.sql نیست
   ۲) ایندکس روی ستون‌های ارتباطی (برای سرعت داشبورد و گزارش‌ها)
   ۳) پیش‌نمایش داده‌های تکراری (فقط گزارش؛ تغییری نمی‌دهد)
   ===================================================================== */

USE [PersonnelDB];
GO

SET NOCOUNT ON;

-------------------------------------------------------------
-- ۱) ستون‌های ناقص
-------------------------------------------------------------

-- قراردادهای کارفرما: درصدها و پیش‌دریافت (کد از این ستون‌ها می‌خواند و می‌نویسد)
IF COL_LENGTH(N'dbo.ClientContracts', N'VatPercentage')            IS NULL ALTER TABLE dbo.ClientContracts ADD VatPercentage            DECIMAL(5,2)  NOT NULL DEFAULT 0;
IF COL_LENGTH(N'dbo.ClientContracts', N'RetentionPercentage')      IS NULL ALTER TABLE dbo.ClientContracts ADD RetentionPercentage      DECIMAL(5,2)  NOT NULL DEFAULT 0;
IF COL_LENGTH(N'dbo.ClientContracts', N'InsurancePercentage')      IS NULL ALTER TABLE dbo.ClientContracts ADD InsurancePercentage      DECIMAL(5,2)  NOT NULL DEFAULT 0;
IF COL_LENGTH(N'dbo.ClientContracts', N'AdvancePaymentPercentage') IS NULL ALTER TABLE dbo.ClientContracts ADD AdvancePaymentPercentage DECIMAL(5,2)  NOT NULL DEFAULT 0;
IF COL_LENGTH(N'dbo.ClientContracts', N'TotalAdvanceAmount')       IS NULL ALTER TABLE dbo.ClientContracts ADD TotalAdvanceAmount       DECIMAL(18,2) NOT NULL DEFAULT 0;

-- صورت‌وضعیت کارفرما: کسورات تفکیکی (قبلاً فقط ستون Deductions بود)
IF COL_LENGTH(N'dbo.ClientStatements', N'RetentionDeduction') IS NULL ALTER TABLE dbo.ClientStatements ADD RetentionDeduction DECIMAL(18,0) NOT NULL DEFAULT 0;
IF COL_LENGTH(N'dbo.ClientStatements', N'InsuranceDeduction') IS NULL ALTER TABLE dbo.ClientStatements ADD InsuranceDeduction DECIMAL(18,0) NOT NULL DEFAULT 0;
IF COL_LENGTH(N'dbo.ClientStatements', N'AdvanceRecovery')    IS NULL ALTER TABLE dbo.ClientStatements ADD AdvanceRecovery    DECIMAL(18,0) NOT NULL DEFAULT 0;
IF COL_LENGTH(N'dbo.ClientStatements', N'OtherDeductions')    IS NULL ALTER TABLE dbo.ClientStatements ADD OtherDeductions    DECIMAL(18,0) NOT NULL DEFAULT 0;

-- پرداخت‌های قدیمی پیمانکار: اتصال به قرارداد کارفرما و ماهیت هزینه
IF COL_LENGTH(N'dbo.ContractorPayments', N'ClientContractId') IS NULL ALTER TABLE dbo.ContractorPayments ADD ClientContractId INT NULL;
IF COL_LENGTH(N'dbo.ContractorPayments', N'CostType')         IS NULL ALTER TABLE dbo.ContractorPayments ADD CostType NVARCHAR(50) NULL;

-- قرارداد پیمانکار: وضعیت و تاریخ فسخ (برای دیتابیس‌های قدیمی‌تر)
IF COL_LENGTH(N'dbo.ContractorContracts', N'Status')          IS NULL ALTER TABLE dbo.ContractorContracts ADD Status NVARCHAR(50) NULL DEFAULT N'فعال';
IF COL_LENGTH(N'dbo.ContractorContracts', N'TerminationDate') IS NULL ALTER TABLE dbo.ContractorContracts ADD TerminationDate DATETIME NULL;
GO

-------------------------------------------------------------
-- ۲) ایندکس‌ها (بدون ایندکس، زیرکوئری‌های داشبورد با رشد داده کند می‌شوند)
--    از EXEC استفاده شده تا اگر ستونی وجود نداشت، کل اسکریپت خطا ندهد.
-------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ContractorStatements_ContractId' AND object_id = OBJECT_ID(N'dbo.ContractorStatements'))
    EXEC(N'CREATE INDEX IX_ContractorStatements_ContractId ON dbo.ContractorStatements (ContractId) INCLUDE (GrossAmount, NetPayableAmount)');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ClientStatements_ContractId' AND object_id = OBJECT_ID(N'dbo.ClientStatements'))
    EXEC(N'CREATE INDEX IX_ClientStatements_ContractId ON dbo.ClientStatements (ContractId) INCLUDE (NetReceivableAmount)');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ContractorPayments_ContractId' AND object_id = OBJECT_ID(N'dbo.ContractorPayments'))
    EXEC(N'CREATE INDEX IX_ContractorPayments_ContractId ON dbo.ContractorPayments (ContractId) INCLUDE (Amount)');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ContractorGuarantees_ContractId' AND object_id = OBJECT_ID(N'dbo.ContractorGuarantees'))
    EXEC(N'CREATE INDEX IX_ContractorGuarantees_ContractId ON dbo.ContractorGuarantees (ContractId)');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ContractorContracts_ClientContractId' AND object_id = OBJECT_ID(N'dbo.ContractorContracts'))
    EXEC(N'CREATE INDEX IX_ContractorContracts_ClientContractId ON dbo.ContractorContracts (ClientContractId) INCLUDE (Amount)');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ContractorContracts_ContractNumber' AND object_id = OBJECT_ID(N'dbo.ContractorContracts'))
    EXEC(N'CREATE INDEX IX_ContractorContracts_ContractNumber ON dbo.ContractorContracts (ContractNumber)');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ClientContracts_ContractNumber' AND object_id = OBJECT_ID(N'dbo.ClientContracts'))
    EXEC(N'CREATE INDEX IX_ClientContracts_ContractNumber ON dbo.ClientContracts (ContractNumber)');

-- نقدینگی: فقط اگر ستون‌ها (اسکریپت 06) وجود دارند
IF COL_LENGTH(N'dbo.CashTransactions', N'ContractorContractId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashTransactions_ContractorContractId' AND object_id = OBJECT_ID(N'dbo.CashTransactions'))
    EXEC(N'CREATE INDEX IX_CashTransactions_ContractorContractId ON dbo.CashTransactions (ContractorContractId) INCLUDE (Amount, Direction)');

IF COL_LENGTH(N'dbo.CashTransactions', N'ContractId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashTransactions_ContractId' AND object_id = OBJECT_ID(N'dbo.CashTransactions'))
    EXEC(N'CREATE INDEX IX_CashTransactions_ContractId ON dbo.CashTransactions (ContractId) INCLUDE (Amount, Direction)');

IF COL_LENGTH(N'dbo.CashChecks', N'ContractorContractId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashChecks_ContractorContractId' AND object_id = OBJECT_ID(N'dbo.CashChecks'))
    EXEC(N'CREATE INDEX IX_CashChecks_ContractorContractId ON dbo.CashChecks (ContractorContractId) INCLUDE (Amount, CheckType, Status)');

IF COL_LENGTH(N'dbo.CashChecks', N'ContractId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashChecks_ContractId' AND object_id = OBJECT_ID(N'dbo.CashChecks'))
    EXEC(N'CREATE INDEX IX_CashChecks_ContractId ON dbo.CashChecks (ContractId) INCLUDE (Amount, CheckType, Status)');

IF COL_LENGTH(N'dbo.ProjectExpenses', N'ClientContractId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProjectExpenses_ClientContractId' AND object_id = OBJECT_ID(N'dbo.ProjectExpenses'))
    EXEC(N'CREATE INDEX IX_ProjectExpenses_ClientContractId ON dbo.ProjectExpenses (ClientContractId) INCLUDE (Amount)');
GO

-------------------------------------------------------------
-- ۳) پیش‌نمایش داده‌های تکراری (فقط گزارش)
--    برنامه از این پس شماره قرارداد پیمانکار و شماره صورت‌وضعیت تکراری را در ثبت جدید رد می‌کند؛
--    موارد قدیمی را اینجا ببینید و دستی اصلاح کنید.
-------------------------------------------------------------
SELECT N'ContractorContracts' AS Src, ContractNumber, COUNT(*) AS Cnt
FROM dbo.ContractorContracts
WHERE ContractNumber IS NOT NULL AND LTRIM(RTRIM(ContractNumber)) <> N''
GROUP BY ContractNumber HAVING COUNT(*) > 1;

SELECT N'ContractorStatements' AS Src, ContractId, StatementNumber, COUNT(*) AS Cnt
FROM dbo.ContractorStatements
WHERE StatementNumber IS NOT NULL AND LTRIM(RTRIM(StatementNumber)) <> N''
GROUP BY ContractId, StatementNumber HAVING COUNT(*) > 1;

SELECT N'ClientStatements' AS Src, ContractId, StatementNumber, COUNT(*) AS Cnt
FROM dbo.ClientStatements
WHERE StatementNumber IS NOT NULL AND LTRIM(RTRIM(StatementNumber)) <> N''
GROUP BY ContractId, StatementNumber HAVING COUNT(*) > 1;
