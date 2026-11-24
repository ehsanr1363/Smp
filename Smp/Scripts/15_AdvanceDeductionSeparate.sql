-- جدا کردن «کسر مساعده» از «قسط وام» در جدول حقوق
-- (برنامه این کار را خودکار هم انجام می‌دهد؛ این فایل فقط برای اجرای دستی در SSMS است)
-- اجرا چندباره بی‌خطر است.

IF COL_LENGTH(N'dbo.Payrolls', N'AdvanceDeduction') IS NULL
BEGIN
    ALTER TABLE dbo.Payrolls ADD AdvanceDeduction DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_Payrolls_AdvanceDeduction DEFAULT 0;

    IF OBJECT_ID(N'dbo.PayrollInstallmentLog', N'U') IS NOT NULL
    BEGIN
        EXEC(N'
            UPDATE l SET l.Kind = N''Advance''
            FROM dbo.PayrollInstallmentLog l
            INNER JOIN dbo.EmployeeLoans el ON el.Id = l.RowId
            WHERE l.Kind = N''Loan'' AND el.LoanType = N''مساعده'';

            UPDATE p SET
                p.AdvanceDeduction = x.Amt,
                p.LoanDeduction = CASE WHEN p.LoanDeduction - x.Amt < 0 THEN 0 ELSE p.LoanDeduction - x.Amt END
            FROM dbo.Payrolls p
            INNER JOIN (SELECT [Year], [Month], EmployeeId, SUM(Amount) AS Amt
                        FROM dbo.PayrollInstallmentLog
                        WHERE Kind = N''Advance''
                        GROUP BY [Year], [Month], EmployeeId) x
                ON x.[Year] = p.[Year] AND x.[Month] = p.[Month] AND x.EmployeeId = p.EmployeeId;');
    END
END
GO
