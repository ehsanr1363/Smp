/* =====================================================================
   اسکریپت ۰۵ — یکسان‌سازی درصدها و اصلاح خالص صورت‌وضعیت پیمانکار
   قبل از اجرا: Backup بگیرید. ابتدا بخش‌های «پیش‌نمایش» را اجرا و بررسی کنید.
   قرارداد جدید: درصدها همیشه «عدد درصد» هستند (۱۰ = ۱۰٪).
   ===================================================================== */

-------------------------------------------------------------
-- ۱) پیش‌نمایش: درصدهایی که احتمالاً به‌صورت کسر ذخیره شده‌اند (0 < x < 1)
-------------------------------------------------------------
USE [PersonnelDB];
GO

SELECT N'Client' AS Src, Id, ContractNumber, VatPercentage, RetentionPercentage, InsurancePercentage, AdvancePaymentPercentage
FROM dbo.ClientContracts
WHERE (VatPercentage > 0 AND VatPercentage < 1) OR (RetentionPercentage > 0 AND RetentionPercentage < 1)
   OR (InsurancePercentage > 0 AND InsurancePercentage < 1) OR (AdvancePaymentPercentage > 0 AND AdvancePaymentPercentage < 1);

SELECT N'Contractor' AS Src, Id, ContractNumber, VatPercentage, RetentionPercentage, InsurancePercentage, AdvancePaymentPercentage, GuaranteePercentage
FROM dbo.ContractorContracts
WHERE (VatPercentage > 0 AND VatPercentage < 1) OR (RetentionPercentage > 0 AND RetentionPercentage < 1)
   OR (InsurancePercentage > 0 AND InsurancePercentage < 1) OR (AdvancePaymentPercentage > 0 AND AdvancePaymentPercentage < 1)
   OR (GuaranteePercentage > 0 AND GuaranteePercentage < 1);

-- ⚠ اگر ردیفی واقعاً ۰٫۵٪ بوده (و نه ۵۰٪)، آن را دستی اصلاح کنید؛ سپس UPDATEها را اجرا کنید.
-- ⚠ قراردادهای کارفرمایی که قبلاً با تایپ «1» به‌اشتباه ۱۰۰ ذخیره شده‌اند را باید دستی بررسی کنید:
SELECT Id, ContractNumber, VatPercentage, RetentionPercentage, InsurancePercentage, AdvancePaymentPercentage
FROM dbo.ClientContracts
WHERE VatPercentage >= 100 OR RetentionPercentage >= 100 OR InsurancePercentage >= 100 OR AdvancePaymentPercentage >= 100;

-------------------------------------------------------------
-- ۲) تبدیل کسرها به درصد
-------------------------------------------------------------
BEGIN TRAN;

UPDATE dbo.ClientContracts SET
    VatPercentage            = CASE WHEN VatPercentage > 0 AND VatPercentage < 1 THEN VatPercentage * 100 ELSE VatPercentage END,
    RetentionPercentage      = CASE WHEN RetentionPercentage > 0 AND RetentionPercentage < 1 THEN RetentionPercentage * 100 ELSE RetentionPercentage END,
    InsurancePercentage      = CASE WHEN InsurancePercentage > 0 AND InsurancePercentage < 1 THEN InsurancePercentage * 100 ELSE InsurancePercentage END,
    AdvancePaymentPercentage = CASE WHEN AdvancePaymentPercentage > 0 AND AdvancePaymentPercentage < 1 THEN AdvancePaymentPercentage * 100 ELSE AdvancePaymentPercentage END;

UPDATE dbo.ContractorContracts SET
    VatPercentage            = CASE WHEN VatPercentage > 0 AND VatPercentage < 1 THEN VatPercentage * 100 ELSE VatPercentage END,
    RetentionPercentage      = CASE WHEN RetentionPercentage > 0 AND RetentionPercentage < 1 THEN RetentionPercentage * 100 ELSE RetentionPercentage END,
    InsurancePercentage      = CASE WHEN InsurancePercentage > 0 AND InsurancePercentage < 1 THEN InsurancePercentage * 100 ELSE InsurancePercentage END,
    AdvancePaymentPercentage = CASE WHEN AdvancePaymentPercentage > 0 AND AdvancePaymentPercentage < 1 THEN AdvancePaymentPercentage * 100 ELSE AdvancePaymentPercentage END,
    GuaranteePercentage      = CASE WHEN GuaranteePercentage > 0 AND GuaranteePercentage < 1 THEN GuaranteePercentage * 100 ELSE GuaranteePercentage END;

COMMIT;

-------------------------------------------------------------
-- ۳) پیش‌نمایش: صورت‌وضعیت‌های پیمانکار که خالص ذخیره‌شده با فرمول درست نمی‌خواند
-------------------------------------------------------------
SELECT Id, ContractId, StatementNumber, NetPayableAmount AS StoredNet,
       (GrossAmount + VAT) - (RetentionDeduction + InsuranceDeduction + OtherDeductions + AdvanceRecovery) AS CorrectNet
FROM dbo.ContractorStatements
WHERE NetPayableAmount <> (GrossAmount + VAT) - (RetentionDeduction + InsuranceDeduction + OtherDeductions + AdvanceRecovery);

-- پس از بررسی ردیف‌ها، در صورت تأیید:
-- UPDATE ContractorStatements
-- SET NetPayableAmount = (GrossAmount + VAT) - (RetentionDeduction + InsuranceDeduction + OtherDeductions + AdvanceRecovery)
-- WHERE NetPayableAmount <> (GrossAmount + VAT) - (RetentionDeduction + InsuranceDeduction + OtherDeductions + AdvanceRecovery);
