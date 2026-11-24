-- ============================================================
-- 12: ایندکس‌های عملکردی (سبک‌سازی کوئری‌ها)
-- ستون‌های کلید خارجی که در WHERE/JOIN فرم‌ها و گزارش‌ها استفاده می‌شوند ایندکس نداشتند.
-- اجرای چندباره بی‌خطر است؛ اگر جدول یا ستونی وجود نداشته باشد آن ایندکس رد می‌شود.
-- ============================================================
USE [PersonnelDB];
GO
SET NOCOUNT ON;

IF OBJECT_ID('dbo.Attachments', 'U') IS NOT NULL AND COL_LENGTH('dbo.Attachments', 'EntityType') IS NOT NULL AND COL_LENGTH('dbo.Attachments', 'EntityId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Attachments_Entity' AND object_id = OBJECT_ID('dbo.Attachments'))
    EXEC(N'CREATE INDEX IX_Attachments_Entity ON dbo.Attachments (EntityType, EntityId)');
GO

IF OBJECT_ID('dbo.Payrolls', 'U') IS NOT NULL AND COL_LENGTH('dbo.Payrolls', 'EmployeeId') IS NOT NULL AND COL_LENGTH('dbo.Payrolls', 'Year') IS NOT NULL AND COL_LENGTH('dbo.Payrolls', 'Month') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payrolls_EmployeeId' AND object_id = OBJECT_ID('dbo.Payrolls'))
    EXEC(N'CREATE INDEX IX_Payrolls_EmployeeId ON dbo.Payrolls (EmployeeId, Year, Month)');
GO

IF OBJECT_ID('dbo.Dependents', 'U') IS NOT NULL AND COL_LENGTH('dbo.Dependents', 'EmployeeId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dependents_EmployeeId' AND object_id = OBJECT_ID('dbo.Dependents'))
    EXEC(N'CREATE INDEX IX_Dependents_EmployeeId ON dbo.Dependents (EmployeeId)');
GO

IF OBJECT_ID('dbo.Attendance', 'U') IS NOT NULL AND COL_LENGTH('dbo.Attendance', 'EmployeeId') IS NOT NULL AND COL_LENGTH('dbo.Attendance', 'Year') IS NOT NULL AND COL_LENGTH('dbo.Attendance', 'Month') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Attendance_EmployeeId' AND object_id = OBJECT_ID('dbo.Attendance'))
    EXEC(N'CREATE INDEX IX_Attendance_EmployeeId ON dbo.Attendance (EmployeeId, Year, Month)');
GO

IF OBJECT_ID('dbo.EmployeeContracts', 'U') IS NOT NULL AND COL_LENGTH('dbo.EmployeeContracts', 'EmployeeId') IS NOT NULL AND COL_LENGTH('dbo.EmployeeContracts', 'Status') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeContracts_Employee_Status' AND object_id = OBJECT_ID('dbo.EmployeeContracts'))
    EXEC(N'CREATE INDEX IX_EmployeeContracts_Employee_Status ON dbo.EmployeeContracts (EmployeeId, Status)');
GO

IF OBJECT_ID('dbo.EmployeeLoans', 'U') IS NOT NULL AND COL_LENGTH('dbo.EmployeeLoans', 'EmployeeId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeLoans_EmployeeId' AND object_id = OBJECT_ID('dbo.EmployeeLoans'))
    EXEC(N'CREATE INDEX IX_EmployeeLoans_EmployeeId ON dbo.EmployeeLoans (EmployeeId)');
GO

IF OBJECT_ID('dbo.LeaveRequests', 'U') IS NOT NULL AND COL_LENGTH('dbo.LeaveRequests', 'EmployeeId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LeaveRequests_EmployeeId' AND object_id = OBJECT_ID('dbo.LeaveRequests'))
    EXEC(N'CREATE INDEX IX_LeaveRequests_EmployeeId ON dbo.LeaveRequests (EmployeeId)');
GO

IF OBJECT_ID('dbo.CashTransactions', 'U') IS NOT NULL AND COL_LENGTH('dbo.CashTransactions', 'AccountId') IS NOT NULL AND COL_LENGTH('dbo.CashTransactions', 'TransactionDate') IS NOT NULL AND COL_LENGTH('dbo.CashTransactions', 'Amount') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CashTransactions_Account_Date' AND object_id = OBJECT_ID('dbo.CashTransactions'))
    EXEC(N'CREATE INDEX IX_CashTransactions_Account_Date ON dbo.CashTransactions (AccountId, TransactionDate) INCLUDE (Amount)');
GO

IF OBJECT_ID('dbo.CashChecks', 'U') IS NOT NULL AND COL_LENGTH('dbo.CashChecks', 'DueDate') IS NOT NULL AND COL_LENGTH('dbo.CashChecks', 'Status') IS NOT NULL AND COL_LENGTH('dbo.CashChecks', 'Amount') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CashChecks_DueDate_Status' AND object_id = OBJECT_ID('dbo.CashChecks'))
    EXEC(N'CREATE INDEX IX_CashChecks_DueDate_Status ON dbo.CashChecks (DueDate, Status) INCLUDE (Amount)');
GO

IF OBJECT_ID('dbo.CashChecks', 'U') IS NOT NULL AND COL_LENGTH('dbo.CashChecks', 'PartyId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CashChecks_PartyId' AND object_id = OBJECT_ID('dbo.CashChecks'))
    EXEC(N'CREATE INDEX IX_CashChecks_PartyId ON dbo.CashChecks (PartyId)');
GO

PRINT N'ایندکس‌های عملکردی بررسی/ایجاد شدند.';
