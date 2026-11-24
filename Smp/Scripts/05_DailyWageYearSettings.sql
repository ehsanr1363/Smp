-- ضریب اضافه‌کار روزمزدها به‌صورت سالانه (نرخ طبقه‌ها در DailyWageRates است).
-- ساعت موظفی روز عادی/تعطیل از قبل در SalarySettings هست و نیازی به ستون جدید ندارد.
-- چند بار اجرا شدن بی‌خطر است.
USE [PersonnelDB];
GO

IF OBJECT_ID('dbo.DailyWageYearSettings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DailyWageYearSettings
    (
        [Year]             INT           NOT NULL CONSTRAINT PK_DailyWageYearSettings PRIMARY KEY,
        OvertimeMultiplier DECIMAL(5,2)  NOT NULL CONSTRAINT DF_DailyWageYearSettings_Mult DEFAULT (1.4)
    );
END
GO
