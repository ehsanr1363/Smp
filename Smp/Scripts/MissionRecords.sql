-- جدول ماموریت‌ها + ستون‌های جدید تنظیمات حقوق (برنامه این‌ها را خودکار هم می‌سازد؛ اجرای دستی اختیاری است)
USE [PersonnelDB];
GO

IF OBJECT_ID(N'dbo.MissionRecords', N'U') IS NULL
CREATE TABLE dbo.MissionRecords (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId    INT NOT NULL,
    Province      NVARCHAR(100) NULL,
    Location      NVARCHAR(200) NULL,
    Subject       NVARCHAR(500) NULL,
    DepartDate    DATE NOT NULL,
    DepartTime    NVARCHAR(5) NULL,
    ReturnDate    DATE NOT NULL,
    ReturnTime    NVARCHAR(5) NULL,
    DaysCount     INT NOT NULL DEFAULT 0,
    SalaryBase    DECIMAL(18,0) NOT NULL DEFAULT 0,
    MissionRate   DECIMAL(9,4) NOT NULL DEFAULT 0,
    DailyAmount   DECIMAL(18,0) NOT NULL DEFAULT 0,
    DaysTotal     DECIMAL(18,0) NOT NULL DEFAULT 0,
    NightsCount   INT NOT NULL DEFAULT 0,
    NightUnit     DECIMAL(18,0) NOT NULL DEFAULT 0,
    NightsTotal   DECIMAL(18,0) NOT NULL DEFAULT 0,
    StayCount     INT NOT NULL DEFAULT 0,
    StayUnit      DECIMAL(18,0) NOT NULL DEFAULT 0,
    StayTotal     DECIMAL(18,0) NOT NULL DEFAULT 0,
    TravelCost    DECIMAL(18,0) NOT NULL DEFAULT 0,
    LunchCount    INT NOT NULL DEFAULT 0,
    LunchUnit     DECIMAL(18,0) NOT NULL DEFAULT 0,
    LunchTotal    DECIMAL(18,0) NOT NULL DEFAULT 0,
    GrandTotal    DECIMAL(18,0) NOT NULL DEFAULT 0,
    Notes         NVARCHAR(1000) NULL,
    CreatedAt     DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_MissionRecords_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id)
);
IF COL_LENGTH(N'dbo.SalarySettings', N'MissionIncludeComponents') IS NULL ALTER TABLE dbo.SalarySettings ADD MissionIncludeComponents NVARCHAR(400) NULL;
IF COL_LENGTH(N'dbo.SalarySettings', N'MissionRate') IS NULL ALTER TABLE dbo.SalarySettings ADD MissionRate DECIMAL(9,4) NULL;
