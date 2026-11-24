/* =====================================================================
   اسکریپت ۱۱ — نرمال‌سازی متن فارسی در دیتابیس
   (ي عربی → ی  ،  ك عربی → ک  ،  ى (الف مقصوره) → ی)

   • پیش‌فرض «فقط گزارش» است: @Apply = 0 هیچ داده‌ای را تغییر نمی‌دهد.
   • برای اعمال تغییرات، ابتدا Backup بگیرید و @Apply را 1 کنید.
   • اسکریپت idempotent است (چند بار اجرا بی‌خطر است).
   • هر ستون در تراکنش جداگانه تغییر می‌کند؛ اگر ستونی به‌خاطر کلید یکتا/FK
     خطا بدهد، فقط همان ستون برگردانده می‌شود و در گزارش با خطا نمایش داده می‌شود.
   • جدول Users (نام کاربری/ورود)، AuditLogs (سابقه) و ستون‌های Hash عمداً حذف شده‌اند.
   ===================================================================== */
USE [PersonnelDB];
GO
SET NOCOUNT ON;

DECLARE @Apply BIT = 0;   -- 0 = فقط گزارش ، 1 = اعمال تغییرات

-- نویسه‌ها با کد یونیکد ساخته می‌شوند تا شکل‌های مشابه با هم اشتباه نشوند
DECLARE @ArabicYeh  NCHAR(1) = NCHAR(1610);  -- U+064A  ي
DECLARE @AlefMaksur NCHAR(1) = NCHAR(1609);  -- U+0649  ى
DECLARE @ArabicKaf  NCHAR(1) = NCHAR(1603);  -- U+0643  ك
DECLARE @PersianYeh NCHAR(1) = NCHAR(1740);  -- U+06CC  ی
DECLARE @PersianKaf NCHAR(1) = NCHAR(1705);  -- U+06A9  ک

CREATE TABLE #Report (
    TableName    SYSNAME       NOT NULL,
    ColumnName   SYSNAME       NOT NULL,
    AffectedRows INT           NOT NULL,
    Applied      BIT           NOT NULL,
    ErrorMessage NVARCHAR(400) NULL
);

DECLARE @Schema SYSNAME, @Table SYSNAME, @Column SYSNAME;
DECLARE @Full NVARCHAR(400), @Col NVARCHAR(200), @Sql NVARCHAR(MAX), @Cnt INT, @Err NVARCHAR(400);

DECLARE cols CURSOR LOCAL FAST_FORWARD FOR
    SELECT s.name, t.name, c.name
    FROM sys.columns c
    JOIN sys.tables  t ON t.object_id = c.object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE c.system_type_id IN (231, 239)          -- nvarchar / nchar
      AND c.is_computed = 0
      AND s.name = N'dbo'
      AND t.name NOT IN (N'Users', N'AuditLogs')
      AND c.name NOT LIKE N'%Hash%'
    ORDER BY t.name, c.column_id;

OPEN cols;
FETCH NEXT FROM cols INTO @Schema, @Table, @Column;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Full = QUOTENAME(@Schema) + N'.' + QUOTENAME(@Table);
    SET @Col  = QUOTENAME(@Column);
    SET @Cnt = 0;  SET @Err = NULL;

    -- مقایسه‌ی باینری، تا ی/ي و ک/ك به‌خاطر Collation یکسان دیده نشوند
    SET @Sql = N'SELECT @c = COUNT(*) FROM ' + @Full + N' WHERE '
             + N'CHARINDEX(@y  COLLATE Latin1_General_BIN2, ' + @Col + N' COLLATE Latin1_General_BIN2) > 0 OR '
             + N'CHARINDEX(@am COLLATE Latin1_General_BIN2, ' + @Col + N' COLLATE Latin1_General_BIN2) > 0 OR '
             + N'CHARINDEX(@k  COLLATE Latin1_General_BIN2, ' + @Col + N' COLLATE Latin1_General_BIN2) > 0;';
    EXEC sp_executesql @Sql,
         N'@c INT OUTPUT, @y NCHAR(1), @am NCHAR(1), @k NCHAR(1)',
         @c = @Cnt OUTPUT, @y = @ArabicYeh, @am = @AlefMaksur, @k = @ArabicKaf;

    IF @Cnt > 0
    BEGIN
        IF @Apply = 1
        BEGIN
            BEGIN TRY
                BEGIN TRAN;
                SET @Sql = N'UPDATE ' + @Full + N' SET ' + @Col + N' = '
                         + N'REPLACE(REPLACE(REPLACE(' + @Col + N' COLLATE Latin1_General_BIN2, '
                         + N'@y  COLLATE Latin1_General_BIN2, @py), '
                         + N'@am COLLATE Latin1_General_BIN2, @py), '
                         + N'@k  COLLATE Latin1_General_BIN2, @pk) '
                         + N'WHERE CHARINDEX(@y  COLLATE Latin1_General_BIN2, ' + @Col + N' COLLATE Latin1_General_BIN2) > 0 OR '
                         + N'CHARINDEX(@am COLLATE Latin1_General_BIN2, ' + @Col + N' COLLATE Latin1_General_BIN2) > 0 OR '
                         + N'CHARINDEX(@k  COLLATE Latin1_General_BIN2, ' + @Col + N' COLLATE Latin1_General_BIN2) > 0;';
                EXEC sp_executesql @Sql,
                     N'@y NCHAR(1), @am NCHAR(1), @k NCHAR(1), @py NCHAR(1), @pk NCHAR(1)',
                     @y = @ArabicYeh, @am = @AlefMaksur, @k = @ArabicKaf, @py = @PersianYeh, @pk = @PersianKaf;
                COMMIT;
                INSERT #Report VALUES (@Table, @Column, @Cnt, 1, NULL);
            END TRY
            BEGIN CATCH
                IF @@TRANCOUNT > 0 ROLLBACK;
                INSERT #Report VALUES (@Table, @Column, @Cnt, 0, LEFT(ERROR_MESSAGE(), 400));
            END CATCH
        END
        ELSE
            INSERT #Report VALUES (@Table, @Column, @Cnt, 0, NULL);
    END

    FETCH NEXT FROM cols INTO @Schema, @Table, @Column;
END
CLOSE cols;
DEALLOCATE cols;

IF NOT EXISTS (SELECT 1 FROM #Report)
    PRINT N'✅ هیچ ي/ك/ى عربی در داده‌ها پیدا نشد؛ نیازی به تغییر نیست.';
ELSE
BEGIN
    IF @Apply = 0
        PRINT N'ℹ️ حالت گزارش: چیزی تغییر نکرد. برای اعمال، @Apply را 1 کنید (قبلش Backup).';
    SELECT TableName, ColumnName, AffectedRows, Applied, ErrorMessage
    FROM #Report
    ORDER BY TableName, ColumnName;
END

DROP TABLE #Report;
GO
