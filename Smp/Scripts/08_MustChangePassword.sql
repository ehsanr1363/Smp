-- ============================================================
-- 08: اجبار تغییر رمز در اولین ورود (برای دیتابیس‌های موجود)
-- اجرای چندباره بی‌خطر است.
-- ============================================================
USE [PersonnelDB];
GO

IF COL_LENGTH('dbo.Users', 'MustChangePassword') IS NULL
    ALTER TABLE dbo.Users ADD MustChangePassword BIT NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT 0;
GO

-- اگر ادمین هنوز رمز پیش‌فرض (admin) دارد، در ورود بعدی باید رمز را عوض کند
UPDATE dbo.Users SET MustChangePassword = 1
WHERE Username = N'admin'
  AND PasswordHash = '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918';
GO
