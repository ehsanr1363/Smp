-- ساخت ۵۵ لیست تنخواه («لیست 1» تا «لیست 55») برای تنخواه‌دارهای ظریف و سرایی
-- اسکریپت ایمپوتنت است: لیستی که قبلاً با همان عنوان وجود دارد دوباره ساخته نمی‌شود.
USE [PersonnelDB];
GO

SET NOCOUNT ON;
DECLARE @Count INT = 55;

DECLARE @Owners TABLE (Id INT, Value NVARCHAR(200));
INSERT INTO @Owners (Id, Value)
SELECT Id, Value
FROM dbo.LookupItems
WHERE Category = N'TankhahOwner'
  AND ( REPLACE(REPLACE(Value, N'ي', N'ی'), N'ك', N'ک') LIKE N'%ظریف%'
     OR REPLACE(REPLACE(Value, N'ي', N'ی'), N'ك', N'ک') LIKE N'%سرایی%' );

SELECT N'تنخواه‌دارهای پیدا شده' AS Info, Id, Value FROM @Owners;   -- باید دقیقاً دو ردیف باشد

;WITH Nums AS (
    SELECT 1 AS N
    UNION ALL
    SELECT N + 1 FROM Nums WHERE N < @Count
)
INSERT INTO dbo.TankhahLists (TankhahId, Title, ChargeDate, ChargeAmount, CurrentBalance, IsActive, CreatedAt)
SELECT o.Id, N'لیست ' + CAST(n.N AS NVARCHAR(10)), GETDATE(), 0, 0, 1, GETDATE()
FROM @Owners o
CROSS JOIN Nums n
WHERE NOT EXISTS (SELECT 1 FROM dbo.TankhahLists l WHERE l.TankhahId = o.Id AND l.Title = N'لیست ' + CAST(n.N AS NVARCHAR(10)))
OPTION (MAXRECURSION 1000);

SELECT o.Value AS Owner, COUNT(l.Id) AS ListCount
FROM @Owners o LEFT JOIN dbo.TankhahLists l ON l.TankhahId = o.Id
GROUP BY o.Value;
