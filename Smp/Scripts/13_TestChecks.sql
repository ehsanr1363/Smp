-- ============================================================
-- 13: تست سلامت دیتابیس (فقط خواندنی - هیچ داده‌ای تغییر نمی‌کند)
-- بعد از اجرای اسکریپت‌های 01 تا 12 اجرا کنید. خروجی: یک جدول با ستون Status (OK / WARN / FAIL)
-- ============================================================
USE [PersonnelDB];
GO
SET NOCOUNT ON;

CREATE TABLE #R (Id INT IDENTITY, CheckName NVARCHAR(200), Status VARCHAR(5), Detail NVARCHAR(400));

-- نویسه‌های عربی با کد یونیکد (برای جلوگیری از اشتباه‌گرفتن ی/ي و ک/ك در ویرایشگر)
DECLARE @y NCHAR(1) = NCHAR(1610), @am NCHAR(1) = NCHAR(1609), @k NCHAR(1) = NCHAR(1603), @zw NCHAR(1) = NCHAR(8204);
DECLARE @n INT, @sql NVARCHAR(MAX);

-- 1) وجود جدول‌ها
DECLARE @t TABLE (Name SYSNAME);
INSERT @t VALUES ('Employees'),('Parties'),('Departments'),('Companies'),('Users'),('Payrolls'),('Attendance'),
                 ('Attachments'),('Dependents'),('EmployeeContracts'),('EmployeeLoans'),('LeaveRequests'),
                 ('CashTransactions'),('CashChecks'),('CompanyBankAccounts'),('RealCompanyBankAccounts');
INSERT #R (CheckName, Status, Detail)
SELECT N'Table ' + Name, CASE WHEN OBJECT_ID('dbo.' + Name) IS NULL THEN 'FAIL' ELSE 'OK' END,
       CASE WHEN OBJECT_ID('dbo.' + Name) IS NULL THEN N'جدول/View وجود ندارد'
            WHEN OBJECT_ID('dbo.' + Name, 'V') IS NOT NULL THEN N'View است (قابل قبول)' ELSE N'' END
FROM @t;

-- 2) وجود ایندکس‌های اسکریپت 12
DECLARE @ix TABLE (Tbl SYSNAME, Ix SYSNAME);
INSERT @ix VALUES ('Attachments','IX_Attachments_Entity'),('Payrolls','IX_Payrolls_EmployeeId'),
 ('Dependents','IX_Dependents_EmployeeId'),('Attendance','IX_Attendance_EmployeeId'),
 ('EmployeeContracts','IX_EmployeeContracts_Employee_Status'),('EmployeeLoans','IX_EmployeeLoans_EmployeeId'),
 ('LeaveRequests','IX_LeaveRequests_EmployeeId'),('CashTransactions','IX_CashTransactions_Account_Date'),
 ('CashChecks','IX_CashChecks_DueDate_Status'),('CashChecks','IX_CashChecks_PartyId');
INSERT #R (CheckName, Status, Detail)
SELECT N'Index ' + Ix,
       CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = Ix AND object_id = OBJECT_ID('dbo.' + Tbl)) THEN 'OK' ELSE 'WARN' END,
       N'اسکریپت 12 اجرا نشده یا ستون‌ها وجود ندارند'
FROM @ix;

-- 3) حروف عربی (ي ى ك) در ستون‌های متنی کلیدی - مقایسه باینری
DECLARE @cols TABLE (Tbl SYSNAME, Col SYSNAME);
INSERT @cols VALUES ('Employees','FirstName'),('Employees','LastName'),('Employees','FatherName'),
 ('Parties','FirstName'),('Parties','LastName'),('Parties','CompanyName'),('Departments','Name'),('Companies','Name');
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT Tbl, Col FROM @cols;
DECLARE @tb SYSNAME, @cl SYSNAME;
OPEN c; FETCH NEXT FROM c INTO @tb, @cl;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF COL_LENGTH('dbo.' + @tb, @cl) IS NOT NULL
    BEGIN
        SET @sql = N'SELECT @n = COUNT(*) FROM dbo.' + QUOTENAME(@tb) + N' WHERE '
                 + N'CHARINDEX(@y COLLATE Latin1_General_BIN2, ' + QUOTENAME(@cl) + N' COLLATE Latin1_General_BIN2) > 0 OR '
                 + N'CHARINDEX(@am COLLATE Latin1_General_BIN2, ' + QUOTENAME(@cl) + N' COLLATE Latin1_General_BIN2) > 0 OR '
                 + N'CHARINDEX(@k COLLATE Latin1_General_BIN2, ' + QUOTENAME(@cl) + N' COLLATE Latin1_General_BIN2) > 0';
        EXEC sp_executesql @sql, N'@n INT OUTPUT, @y NCHAR(1), @am NCHAR(1), @k NCHAR(1)', @n OUTPUT, @y, @am, @k;
        INSERT #R VALUES (N'ArabicChars ' + @tb + N'.' + @cl, CASE WHEN @n = 0 THEN 'OK' ELSE 'WARN' END,
                          CASE WHEN @n = 0 THEN N'' ELSE CONCAT(@n, N' ردیف دارای ي/ك/ى عربی - اسکریپت 11 را اجرا کنید') END);
    END
    FETCH NEXT FROM c INTO @tb, @cl;
END
CLOSE c; DEALLOCATE c;

-- 4) نیم‌فاصله در نام‌ها (فقط اطلاع‌رسانی: برنامه‌ی جدید آن را به فاصله تبدیل می‌کند)
IF OBJECT_ID('dbo.Employees','U') IS NOT NULL
BEGIN
    SELECT @n = COUNT(*) FROM dbo.Employees
    WHERE CHARINDEX(@zw COLLATE Latin1_General_BIN2, FirstName + LastName COLLATE Latin1_General_BIN2) > 0;
    INSERT #R VALUES (N'ZWNJ in Employee names', CASE WHEN @n = 0 THEN 'OK' ELSE 'WARN' END,
                      CASE WHEN @n = 0 THEN N'' ELSE CONCAT(@n, N' نام دارای نیم‌فاصله است') END);
END

-- 5) کد ملی تکراری
IF OBJECT_ID('dbo.Employees','U') IS NOT NULL
BEGIN
    SELECT @n = COUNT(*) FROM (SELECT NationalCode FROM dbo.Employees
        WHERE NULLIF(LTRIM(RTRIM(NationalCode)), '') IS NOT NULL GROUP BY NationalCode HAVING COUNT(*) > 1) d;
    INSERT #R VALUES (N'Duplicate Employees.NationalCode', CASE WHEN @n = 0 THEN 'OK' ELSE 'FAIL' END,
                      CASE WHEN @n = 0 THEN N'' ELSE CONCAT(@n, N' کد ملی تکراری') END);
END
IF OBJECT_ID('dbo.Parties','U') IS NOT NULL
BEGIN
    SELECT @n = COUNT(*) FROM (SELECT NationalCode FROM dbo.Parties
        WHERE NULLIF(LTRIM(RTRIM(NationalCode)), '') IS NOT NULL GROUP BY NationalCode HAVING COUNT(*) > 1) d;
    INSERT #R VALUES (N'Duplicate Parties.NationalCode', CASE WHEN @n = 0 THEN 'OK' ELSE 'FAIL' END,
                      CASE WHEN @n = 0 THEN N'' ELSE CONCAT(@n, N' کد ملی تکراری') END);
END

-- 6) رکوردهای یتیم (بدون والد)
SELECT @n = COUNT(*) FROM dbo.Payrolls p WHERE NOT EXISTS (SELECT 1 FROM dbo.Employees e WHERE e.Id = p.EmployeeId);
INSERT #R VALUES (N'Orphan Payrolls', CASE WHEN @n = 0 THEN 'OK' ELSE 'FAIL' END, CASE WHEN @n = 0 THEN N'' ELSE CONCAT(@n, N' فیش بدون کارمند') END);

IF COL_LENGTH('dbo.CashTransactions','AccountId') IS NOT NULL
BEGIN
    SELECT @n = COUNT(*) FROM dbo.CashTransactions t
    WHERE t.AccountId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.CompanyBankAccounts a WHERE a.Id = t.AccountId);
    INSERT #R VALUES (N'Orphan CashTransactions.AccountId', CASE WHEN @n = 0 THEN 'OK' ELSE 'FAIL' END, CASE WHEN @n = 0 THEN N'' ELSE CONCAT(@n, N' تراکنش با حساب نامعتبر') END);
END

-- 7) کاربر admin و رمز پیش‌فرض
IF OBJECT_ID('dbo.Users','U') IS NOT NULL
BEGIN
    SELECT @n = COUNT(*) FROM dbo.Users WHERE LOWER(Username) = 'admin';
    INSERT #R VALUES (N'Admin user exists', CASE WHEN @n = 1 THEN 'OK' ELSE 'WARN' END, CONCAT(N'تعداد: ', @n));
    IF COL_LENGTH('dbo.Users','MustChangePassword') IS NOT NULL
    BEGIN
        SELECT @n = COUNT(*) FROM dbo.Users WHERE LOWER(Username) = 'admin' AND MustChangePassword = 1;
        INSERT #R VALUES (N'Admin must change password', CASE WHEN @n = 1 THEN 'WARN' ELSE 'OK' END, N'WARN یعنی admin هنوز رمز را عوض نکرده');
    END
END

-- 8) آمار ردیف‌های جدول‌های اصلی (برای تشخیص جدول‌های سنگین)
INSERT #R
SELECT N'RowCount ' + OBJECT_NAME(object_id), 'OK', CONCAT(SUM(row_count), N' ردیف')
FROM sys.dm_db_partition_stats
WHERE index_id IN (0,1) AND OBJECT_SCHEMA_NAME(object_id) = 'dbo'
GROUP BY object_id HAVING SUM(row_count) > 10000;

SELECT Id, CheckName, Status, Detail FROM #R ORDER BY CASE Status WHEN 'FAIL' THEN 0 WHEN 'WARN' THEN 1 ELSE 2 END, Id;
SELECT Status, COUNT(*) AS Total FROM #R GROUP BY Status;
DROP TABLE #R;
