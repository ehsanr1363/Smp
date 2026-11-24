# اسکریپت‌های دیتابیس Smp

همه‌ی اسکریپت‌ها idempotent هستند (چندبار اجرا بی‌خطر است) و حالا همگی با
`USE [PersonnelDB]` شروع می‌شوند و نام جدول‌ها را با پیشوند `dbo.` می‌نویسند.
قبل از اجرای هر اسکریپتی که داده را تغییر می‌دهد، Backup بگیرید.

## ترتیب اجرا

| ترتیب | فایل | کار |
|---|---|---|
| 1 | `01_InstallDatabase.sql` | ساخت دیتابیس و جدول‌های پایه |
| 2 | `03_CreateLiquidityTables.sql` | حساب‌های بانکی، تراکنش نقدی و چک (به `Parties` از اسکریپت ۱ وابسته است) |
| 3 | `06_UnifyLiquiditySchema.sql` | جدول `RealCompanyBankAccounts` + نمای سازگار `CompanyBankAccounts` (بعد از 03) |
| 4 | `04_FixPayrollAndCarInsurance.sql` | ستون‌های گم‌شده‌ی `Payrolls`، جدول بیمه ماشین، یکسان‌سازی `TaxRate` |
| 4 | `05_DailyWageYearSettings.sql` | ضریب اضافه‌کار سالانه‌ی روزمزدها |
| 5 | `05_FixContractPercentsAndNet.sql` | یکسان‌سازی درصدها و خالص صورت‌وضعیت (ابتدا بخش «پیش‌نمایش» را بررسی کنید) |
| 6 | `07_ContractSchemaFixes.sql` | ستون‌ها و ایندکس‌های بخش قراردادها |
| 7 | `08_MustChangePassword.sql` | اجبار تغییر رمز پیش‌فرض admin |
| 8 | `09_CashTransactionsAccountIdNullable.sql` | `AccountId` در `CashTransactions` نال‌پذیر می‌شود |
| 9 | `MissionRecords.sql` | جدول ماموریت‌ها (برنامه خودکار هم می‌سازد) |
| 10 | `10_CreateTankhahLists_Zarif_Saraei.sql` | داده‌ی اولیه‌ی لیست‌های تنخواه |
| 11 | `12_PerformanceIndexes.sql` | ایندکس ستون‌های کلید خارجی (سرعت لیست‌ها و گزارش‌ها) |
| اختیاری | `11_NormalizePersianText.sql` | نرمال‌سازی ي/ك/ى عربی در داده‌ها (پیش‌فرض فقط گزارش) |

## نکته‌های مهم

* شماره‌گذاری عمداً تغییر نکرد چون پیام‌های برنامه به همین نام‌ها ارجاع می‌دهند
  (`01`, `04`, `06`, `07`).
* دو فایل با پیشوند `05_` وجود دارد؛ ترتیب بالا (حروف الفبا) درست است.
