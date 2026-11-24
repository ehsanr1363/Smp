-- ============================================================
-- Migration: Fix Payrolls columns + Create EmployeeCarInsurances + Normalize TaxRate
-- این اسکریپت idempotent است — چند بار اجرا شود مشکلی ایجاد نمی‌کند
-- ============================================================

USE [PersonnelDB];
GO

-- ============================================================
-- ۱. رفع باگ P0: اضافه کردن دو ستون گمشده به Payrolls
-- ============================================================
IF COL_LENGTH('dbo.Payrolls', 'CarInsuranceDeduction') IS NULL
BEGIN
    ALTER TABLE dbo.Payrolls ADD CarInsuranceDeduction DECIMAL(18,0) DEFAULT 0;
    PRINT N'✅ ستون CarInsuranceDeduction به Payrolls اضافه شد.';
END
ELSE
    PRINT N'ℹ️ ستون CarInsuranceDeduction از قبل وجود دارد.';
GO

IF COL_LENGTH('dbo.Payrolls', 'OtherDeductions') IS NULL
BEGIN
    ALTER TABLE dbo.Payrolls ADD OtherDeductions DECIMAL(18,0) DEFAULT 0;
    PRINT N'✅ ستون OtherDeductions به Payrolls اضافه شد.';
END
ELSE
    PRINT N'ℹ️ ستون OtherDeductions از قبل وجود دارد.';
GO

-- ============================================================
-- ۲. رفع باگ P0: ساختن جدول EmployeeCarInsurances
-- ============================================================
IF OBJECT_ID('dbo.EmployeeCarInsurances', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeCarInsurances (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        EmployeeId INT NOT NULL,
        TotalAmount DECIMAL(18,0) NOT NULL,
        MonthlyInstallment DECIMAL(18,0) NOT NULL,
        RemainingAmount DECIMAL(18,0) NOT NULL,
        StartDate DATETIME NOT NULL DEFAULT GETDATE(),
        Description NVARCHAR(MAX) NULL,
        IsActive INT DEFAULT 1,
        CreatedAt DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id)
    );
    PRINT N'✅ جدول EmployeeCarInsurances ساخته شد.';
END
ELSE
    PRINT N'ℹ️ جدول EmployeeCarInsurances از قبل وجود دارد.';
GO

-- ============================================================
-- ۳. یکسان‌سازی ذخیره‌ی درصد (TaxBrackets: 0.10 → 10)
-- ============================================================
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.TaxBrackets')
           AND name = 'TaxRate' AND precision = 5 AND scale = 4)
BEGIN
    ALTER TABLE dbo.TaxBrackets ALTER COLUMN TaxRate DECIMAL(6,2) NOT NULL;
    PRINT N'✅ نوع ستون TaxBrackets.TaxRate به DECIMAL(6,2) تغییر یافت.';
END
GO

UPDATE dbo.TaxBrackets
SET TaxRate = TaxRate * 100
WHERE TaxRate > 0 AND TaxRate < 1;
GO

PRINT N'🎉 Migration با موفقیت انجام شد.';
GO
