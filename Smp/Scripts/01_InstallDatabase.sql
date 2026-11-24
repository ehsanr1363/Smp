-- ============================================================
-- Smp — Installation Script
-- اجرای این اسکریپت قبل از اولین اجرای برنامه الزامی است.
-- این اسکریپت idempotent است (چند بار اجرا شود مشکلی ایجاد نمی‌کند)
-- ============================================================

USE master;
GO

IF DB_ID('PersonnelDB') IS NULL
    CREATE DATABASE [PersonnelDB];
GO

USE [PersonnelDB];
GO

-- ============================================================
-- 1. Departments
-- ============================================================
IF OBJECT_ID('dbo.Departments', 'U') IS NULL
CREATE TABLE dbo.Departments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Code NVARCHAR(50) NULL,
    Name NVARCHAR(255) NOT NULL,
    Address NVARCHAR(MAX) NULL
);
GO

-- ============================================================
-- 2. Companies
-- ============================================================
IF OBJECT_ID('dbo.Companies', 'U') IS NULL
CREATE TABLE dbo.Companies (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL,
    Address NVARCHAR(MAX) NULL,
    Phone NVARCHAR(50) NULL,
    WorkshopCode NVARCHAR(100) NULL,
    NationalId NVARCHAR(50) NULL,
    EconomicCode NVARCHAR(50) NULL,
    PostalCode NVARCHAR(50) NULL,
    Logo VARBINARY(MAX) NULL
);
GO

-- ============================================================
-- 3. LookupItems
-- ============================================================
IF OBJECT_ID('dbo.LookupItems', 'U') IS NULL
CREATE TABLE dbo.LookupItems (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Category NVARCHAR(100) NOT NULL,
    Value NVARCHAR(255) NOT NULL
);
GO

-- ============================================================
-- 4. Parties
-- ============================================================
IF OBJECT_ID('dbo.Parties', 'U') IS NULL
CREATE TABLE dbo.Parties (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PartyType NVARCHAR(50) NOT NULL,
    IsForeign INT DEFAULT 0,
    IsContractor INT DEFAULT 0,
    IsClient INT DEFAULT 1,
    NationalCode NVARCHAR(50) NULL,
    FirstName NVARCHAR(100) NULL,
    LastName NVARCHAR(100) NULL,
    CompanyName NVARCHAR(255) NULL,
    NationalId NVARCHAR(50) NULL,
    EconomicCode NVARCHAR(50) NULL,
    Phone NVARCHAR(50) NULL,
    Address NVARCHAR(MAX) NULL,
    CreatedAt DATETIME DEFAULT GETDATE()
);
GO

-- ============================================================
-- 5. Employees — تاریخ‌ها با DATETIME
-- ============================================================
IF OBJECT_ID('dbo.Employees', 'U') IS NULL
CREATE TABLE dbo.Employees (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PersonnelCode NVARCHAR(50) NULL,
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    FatherName NVARCHAR(100) NULL,
    NationalCode NVARCHAR(50) NULL,
    BirthCertificateNumber NVARCHAR(50) NULL,
    BirthDate DATETIME NULL,
    BirthPlace NVARCHAR(100) NULL,
    IssuancePlace NVARCHAR(100) NULL,
    MaritalStatus NVARCHAR(50) NULL,
    MilitaryStatus NVARCHAR(50) NULL,
    Education NVARCHAR(100) NULL,
    SpecializedField NVARCHAR(150) NULL,
    ChildrenCount INT DEFAULT 0,
    InsuranceNumber NVARCHAR(50) NULL,
    Photo VARBINARY(MAX) NULL,
    ContractStartDate DATETIME NULL,
    ContractEndDate DATETIME NULL,
    ContractYear INT NULL,
    ContractNumber NVARCHAR(50) NULL,
    CompanyName NVARCHAR(255) NULL,
    CompanyId INT NULL,
    DepartmentId INT NULL,
    Position NVARCHAR(150) NULL,
    PersonnelType NVARCHAR(100) NULL,
    DailyWageCategory NVARCHAR(100) NULL,
    WorkLocation NVARCHAR(150) NULL,
    Status NVARCHAR(50) NULL,
    PhoneNumber NVARCHAR(50) NULL,
    BaseSalary DECIMAL(18,0) DEFAULT 0,
    YearsBonus DECIMAL(18,0) DEFAULT 0,
    AttractionAllowance DECIMAL(18,0) DEFAULT 0,
    ResponsibilityAllowance DECIMAL(18,0) DEFAULT 0,
    GroceryAllowance DECIMAL(18,0) DEFAULT 0,
    HousingAllowance DECIMAL(18,0) DEFAULT 0,
    ChildAllowance DECIMAL(18,0) DEFAULT 0,
    MarriageAllowance DECIMAL(18,0) DEFAULT 0,
    ManagementAllowance DECIMAL(18,0) DEFAULT 0,
    TechnicalAllowance DECIMAL(18,0) DEFAULT 0,
    OtherAllowances DECIMAL(18,0) DEFAULT 0,
    DailyWage DECIMAL(18,0) DEFAULT 0,
    HourlyWage DECIMAL(18,0) DEFAULT 0,
    HasInsurance INT DEFAULT 1,
    Username NVARCHAR(100) NULL,
    IsActive INT DEFAULT 1,
    Address NVARCHAR(MAX) NULL,
    HireDate DATETIME NULL,
    Gender NVARCHAR(50) NULL
);
GO

-- ============================================================
-- 6. Users
-- ============================================================
IF OBJECT_ID('dbo.Users', 'U') IS NULL
CREATE TABLE dbo.Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId INT NULL,
    Username NVARCHAR(100) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(256) NOT NULL,
    Role NVARCHAR(50) NOT NULL DEFAULT N'User',
    IsActive INT DEFAULT 1,
    LastLogin DATETIME NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    MustChangePassword BIT NOT NULL DEFAULT 0,
    FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id) ON DELETE SET NULL
);
GO

-- ============================================================
-- 7. Dependents
-- ============================================================
IF OBJECT_ID('dbo.Dependents', 'U') IS NULL
CREATE TABLE dbo.Dependents (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId INT NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    NationalCode NVARCHAR(50) NULL,
    BirthCertificateNumber NVARCHAR(50) NULL,
    FatherName NVARCHAR(100) NULL,
    BirthDate DATETIME NULL,
    Gender NVARCHAR(50) NULL,
    BirthPlace NVARCHAR(100) NULL,
    Relation NVARCHAR(50) NULL,
    HasSupplementaryInsurance INT DEFAULT 0,
    FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id) ON DELETE CASCADE
);
GO

-- ============================================================
-- 8. Banks
-- ============================================================
IF OBJECT_ID('dbo.Banks', 'U') IS NULL
CREATE TABLE dbo.Banks (
    BankCode NVARCHAR(50) PRIMARY KEY,
    BankName NVARCHAR(150) NOT NULL
);
GO

-- ============================================================
-- 9. EmployeeBankAccounts
-- ============================================================
IF OBJECT_ID('dbo.EmployeeBankAccounts', 'U') IS NULL
CREATE TABLE dbo.EmployeeBankAccounts (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId INT NOT NULL,
    AccountNumber NVARCHAR(100) NULL,
    ShebaNumber NVARCHAR(100) NULL,
    BankCode NVARCHAR(50) NULL,
    IsMainAccount INT DEFAULT 0,
    FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id) ON DELETE CASCADE
);
GO

-- ============================================================
-- 10. ClientContracts
-- ============================================================
IF OBJECT_ID('dbo.ClientContracts', 'U') IS NULL
CREATE TABLE dbo.ClientContracts (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PartyId INT NOT NULL,
    ContractNumber NVARCHAR(100) NULL,
    ContractRow NVARCHAR(50) NULL,
    Subject NVARCHAR(MAX) NULL,
    Amount DECIMAL(18,0) DEFAULT 0,
    ContractDate DATETIME NULL,
    EndDate DATETIME NULL,
    TerminationDate DATETIME NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    Status NVARCHAR(50) DEFAULT N'فعال',
    FOREIGN KEY (PartyId) REFERENCES dbo.Parties(Id)
);
GO

-- ============================================================
-- 11. ContractorContracts
-- ============================================================
IF OBJECT_ID('dbo.ContractorContracts', 'U') IS NULL
CREATE TABLE dbo.ContractorContracts (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PartyId INT NOT NULL,
    ClientContractId INT NULL,
    ContractNumber NVARCHAR(100) NULL,
    WorkshopCode NVARCHAR(50) NULL,
    ContractRow NVARCHAR(50) NULL,
    Subject NVARCHAR(MAX) NULL,
    Amount DECIMAL(18,0) DEFAULT 0,
    ContractDate DATETIME NULL,
    EndDate DATETIME NULL,
    TerminationDate DATETIME NULL,
    GuaranteePercentage DECIMAL(5,2) DEFAULT 0,
    RetentionPercentage DECIMAL(5,2) DEFAULT 0,
    InsurancePercentage DECIMAL(5,2) DEFAULT 0,
    AdvancePaymentPercentage DECIMAL(5,2) DEFAULT 0,
    TotalAdvanceAmount DECIMAL(18,2) DEFAULT 0,
    VatPercentage DECIMAL(5,2) DEFAULT 0,
    Status NVARCHAR(50) DEFAULT N'فعال',
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (PartyId) REFERENCES dbo.Parties(Id),
    FOREIGN KEY (ClientContractId) REFERENCES dbo.ClientContracts(Id)
);
GO

-- ============================================================
-- 12. ContractArchives
-- ============================================================
IF OBJECT_ID('dbo.ContractArchives', 'U') IS NULL
CREATE TABLE dbo.ContractArchives (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId INT NOT NULL,
    ArchiveDate DATETIME NULL,
    ContractNumber NVARCHAR(100) NULL,
    ContractStartDate DATETIME NULL,
    ContractEndDate DATETIME NULL,
    BaseSalary DECIMAL(18,0) DEFAULT 0,
    YearsBonus DECIMAL(18,0) DEFAULT 0,
    AttractionAllowance DECIMAL(18,0) DEFAULT 0,
    ResponsibilityAllowance DECIMAL(18,0) DEFAULT 0,
    GroceryAllowance DECIMAL(18,0) DEFAULT 0,
    HousingAllowance DECIMAL(18,0) DEFAULT 0,
    ChildAllowance DECIMAL(18,0) DEFAULT 0,
    MarriageAllowance DECIMAL(18,0) DEFAULT 0,
    ManagementAllowance DECIMAL(18,0) DEFAULT 0,
    TechnicalAllowance DECIMAL(18,0) DEFAULT 0,
    OtherAllowances DECIMAL(18,0) DEFAULT 0,
    Position NVARCHAR(150) NULL,
    DepartmentId INT NULL,
    DepartmentName NVARCHAR(100) NULL,
    CompanyId INT NULL,
    CompanyTitle NVARCHAR(255) NULL,
    FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id) ON DELETE CASCADE
);
GO

-- ============================================================
-- 13. InsuranceRecords
-- ============================================================
IF OBJECT_ID('dbo.InsuranceRecords', 'U') IS NULL
CREATE TABLE dbo.InsuranceRecords (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId INT NULL,
    NationalCode NVARCHAR(50) NULL,
    FirstName NVARCHAR(100) NULL,
    LastName NVARCHAR(100) NULL,
    FatherName NVARCHAR(100) NULL,
    InsuranceNumber NVARCHAR(50) NULL,
    ListNumber NVARCHAR(50) NULL,
    Year NVARCHAR(10) NULL,
    Month NVARCHAR(10) NULL,
    DaysWorked NVARCHAR(50) NULL,
    Wage NVARCHAR(50) NULL,
    JobTitle NVARCHAR(150) NULL,
    DbfPersonnelCode NVARCHAR(50) NULL,
    BirthCertificate NVARCHAR(50) NULL,
    BirthPlace NVARCHAR(100) NULL,
    BirthDate NVARCHAR(50) NULL,
    WorkshopCode NVARCHAR(50) NULL,
    ListKind NVARCHAR(50) NULL,
    Gender NVARCHAR(50) NULL,
    Nationality NVARCHAR(50) NULL,
    HireDate NVARCHAR(50) NULL,
    ExitDate NVARCHAR(50) NULL,
    MonthlyWage NVARCHAR(50) NULL,
    MonthlyBenefits NVARCHAR(50) NULL,
    WageAndBenefitsIncluded NVARCHAR(50) NULL,
    WageAndBenefitsTotal NVARCHAR(50) NULL,
    WorkerInsurance NVARCHAR(50) NULL,
    Department1 NVARCHAR(150) NULL,
    JobCode NVARCHAR(50) NULL,
    YearsBonusDaily NVARCHAR(50) NULL,
    MarriageAllowance NVARCHAR(50) NULL,
    ContractRow NVARCHAR(50) NULL,
    DSW_IDATE NVARCHAR(50) NULL,
    DSW_PRATE NVARCHAR(50) NULL,
    DSW_INC NVARCHAR(50) NULL,
    DSW_SPOUSE NVARCHAR(50) NULL,
    DSW_DD NVARCHAR(50) NULL,
    CreatedAt DATETIME DEFAULT GETDATE()
);
GO

-- ============================================================
-- 14. WorkshopRecords
-- ============================================================
IF OBJECT_ID('dbo.WorkshopRecords', 'U') IS NULL
CREATE TABLE dbo.WorkshopRecords (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    WorkshopCode NVARCHAR(50) NULL,
    CompanyId INT NULL,
    DSK_NAME NVARCHAR(255) NULL,
    DSK_FARM NVARCHAR(255) NULL,
    DSK_ADRS NVARCHAR(MAX) NULL,
    DSK_KIND NVARCHAR(50) NULL,
    DSK_YY NVARCHAR(10) NULL,
    DSK_MM NVARCHAR(10) NULL,
    DSK_LISTNO NVARCHAR(50) NULL,
    DSK_DISC NVARCHAR(MAX) NULL,
    DSK_NUM NVARCHAR(50) NULL,
    DSK_TDD NVARCHAR(50) NULL,
    DSK_TROOZ NVARCHAR(50) NULL,
    DSK_TMAH NVARCHAR(50) NULL,
    DSK_TMAZ NVARCHAR(50) NULL,
    DSK_TMASH NVARCHAR(50) NULL,
    DSK_TTOTL NVARCHAR(50) NULL,
    DSK_TBIME NVARCHAR(50) NULL,
    DSK_TKOSO NVARCHAR(50) NULL,
    DSK_BIC NVARCHAR(50) NULL,
    DSK_RATE NVARCHAR(50) NULL,
    DSK_PRATE NVARCHAR(50) NULL,
    DSK_BIMH NVARCHAR(50) NULL,
    MON_PYM NVARCHAR(50) NULL,
    ContractRow NVARCHAR(50) NULL,
    DSK_INC NVARCHAR(50) NULL,
    DSK_SPOUSE NVARCHAR(50) NULL,
    CreatedAt DATETIME DEFAULT GETDATE()
);
GO

-- ============================================================
-- 15. Payrolls — شامل SupplementaryInsuranceDeduction
-- ============================================================
IF OBJECT_ID('dbo.Payrolls', 'U') IS NULL
CREATE TABLE dbo.Payrolls (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId INT NOT NULL,
    Year INT NOT NULL,
    Month INT NOT NULL,
    BaseSalary DECIMAL(18,0) NOT NULL DEFAULT 0,
    TotalEarnings DECIMAL(18,0) NOT NULL DEFAULT 0,
    TotalDeductions DECIMAL(18,0) NOT NULL DEFAULT 0,
    NetPay DECIMAL(18,0) NOT NULL DEFAULT 0,
    InsuranceEmployee DECIMAL(18,0) DEFAULT 0,
    InsuranceEmployer DECIMAL(18,0) DEFAULT 0,
    IncomeTax DECIMAL(18,0) DEFAULT 0,
    LoanDeduction DECIMAL(18,0) DEFAULT 0,
    SupplementaryInsuranceDeduction DECIMAL(18,0) DEFAULT 0,
    CalculatedAt DATETIME DEFAULT GETDATE(),
    CONSTRAINT UQ_Payroll UNIQUE (EmployeeId, Year, Month),
    FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id)
);
GO

-- ستون حیاتی — برای دیتابیس‌های قبلی
IF COL_LENGTH('dbo.Payrolls', 'SupplementaryInsuranceDeduction') IS NULL
    ALTER TABLE dbo.Payrolls ADD SupplementaryInsuranceDeduction DECIMAL(18,0) DEFAULT 0;
GO

-- ============================================================
-- 16. SalarySettings
-- ============================================================
IF OBJECT_ID('dbo.SalarySettings', 'U') IS NULL
CREATE TABLE dbo.SalarySettings (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Year INT NOT NULL,
    MinimumWage DECIMAL(18,0) NOT NULL DEFAULT 0,
    HousingAllowance DECIMAL(18,0) NOT NULL DEFAULT 0,
    FoodAllowance DECIMAL(18,0) NOT NULL DEFAULT 0,
    ChildAllowanceRate DECIMAL(18,2) NOT NULL DEFAULT 10,
    InsuranceEmployeeRate DECIMAL(18,2) NOT NULL DEFAULT 7,
    InsuranceEmployerRate DECIMAL(18,2) NOT NULL DEFAULT 23,
    TaxExemptionLimit DECIMAL(18,0) NOT NULL DEFAULT 0,
    UpdatedAt DATETIME NULL,
    CONSTRAINT UQ_SalarySettings_Year UNIQUE (Year)
);
GO

-- ============================================================
-- 17. TaxBrackets
-- ============================================================
IF OBJECT_ID('dbo.TaxBrackets', 'U') IS NULL
CREATE TABLE dbo.TaxBrackets (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Year INT NOT NULL,
    FromAmount DECIMAL(18,2) NOT NULL,
    ToAmount DECIMAL(18,2) NULL,
    TaxRate DECIMAL(5,4) NOT NULL,
    CONSTRAINT FK_TaxBrackets_SalarySettings
        FOREIGN KEY (Year) REFERENCES dbo.SalarySettings(Year) ON DELETE CASCADE
);
GO

-- ============================================================
-- 18. ClientPayments
-- ============================================================
IF OBJECT_ID('dbo.ClientPayments', 'U') IS NULL
CREATE TABLE dbo.ClientPayments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ContractId INT NOT NULL,
    PaymentDate DATETIME NOT NULL,
    Amount DECIMAL(18,0) NOT NULL,
    PaymentType NVARCHAR(50) NULL,
    Description NVARCHAR(MAX) NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (ContractId) REFERENCES dbo.ClientContracts(Id)
);
GO

-- ============================================================
-- 19. ClientStatements
-- ============================================================
IF OBJECT_ID('dbo.ClientStatements', 'U') IS NULL
CREATE TABLE dbo.ClientStatements (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ContractId INT NOT NULL,
    StatementNumber NVARCHAR(100) NULL,
    StatementDate DATETIME NOT NULL,
    GrossAmount DECIMAL(18,0) DEFAULT 0,
    VAT DECIMAL(18,0) DEFAULT 0,
    Deductions DECIMAL(18,0) DEFAULT 0,
    NetReceivableAmount DECIMAL(18,0) DEFAULT 0,
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (ContractId) REFERENCES dbo.ClientContracts(Id)
);
GO

-- ============================================================
-- 20. ContractorGuarantees
-- ============================================================
IF OBJECT_ID('dbo.ContractorGuarantees', 'U') IS NULL
CREATE TABLE dbo.ContractorGuarantees (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ContractId INT NOT NULL,
    GuaranteeNumber NVARCHAR(100) NULL,
    BankName NVARCHAR(150) NULL,
    Amount DECIMAL(18,0) DEFAULT 0,
    IssueDate DATETIME NULL,
    MaturityDate DATETIME NULL,
    GuaranteeType NVARCHAR(100) NULL,
    Status NVARCHAR(50) DEFAULT N'فعال',
    Description NVARCHAR(MAX) NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (ContractId) REFERENCES dbo.ContractorContracts(Id)
);
GO

-- ============================================================
-- 21. ContractorStatements
-- ============================================================
IF OBJECT_ID('dbo.ContractorStatements', 'U') IS NULL
CREATE TABLE dbo.ContractorStatements (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ContractId INT NOT NULL,
    StatementNumber NVARCHAR(100) NULL,
    StatementDate DATETIME NOT NULL,
    GrossAmount DECIMAL(18,0) DEFAULT 0,
    VAT DECIMAL(18,0) DEFAULT 0,
    RetentionDeduction DECIMAL(18,0) DEFAULT 0,
    InsuranceDeduction DECIMAL(18,0) DEFAULT 0,
    OtherDeductions DECIMAL(18,0) DEFAULT 0,
    AdvanceRecovery DECIMAL(18,0) DEFAULT 0,
    NetPayableAmount DECIMAL(18,0) DEFAULT 0,
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (ContractId) REFERENCES dbo.ContractorContracts(Id)
);
GO

-- ============================================================
-- 22. ContractorPayments
-- ============================================================
IF OBJECT_ID('dbo.ContractorPayments', 'U') IS NULL
CREATE TABLE dbo.ContractorPayments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ContractId INT NOT NULL,
    PaymentDate DATETIME NOT NULL,
    Amount DECIMAL(18,0) NOT NULL,
    PaymentType NVARCHAR(50) NULL,
    Description NVARCHAR(MAX) NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (ContractId) REFERENCES dbo.ContractorContracts(Id)
);
GO

-- ============================================================
-- 23. ProjectExpenses
-- ============================================================
IF OBJECT_ID('dbo.ProjectExpenses', 'U') IS NULL
CREATE TABLE dbo.ProjectExpenses (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ClientContractId INT NOT NULL,
    PartyId INT NULL,
    InvoiceNumber NVARCHAR(100) NULL,
    InvoiceDate DATETIME NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    FOREIGN KEY (ClientContractId) REFERENCES dbo.ClientContracts(Id),
    FOREIGN KEY (PartyId) REFERENCES dbo.Parties(Id)
);
GO

-- ============================================================
-- 24. PartyBankAccounts
-- ============================================================
IF OBJECT_ID('dbo.PartyBankAccounts', 'U') IS NULL
CREATE TABLE dbo.PartyBankAccounts (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PartyId INT NOT NULL,
    ShebaNumber NVARCHAR(100) NOT NULL,
    BankCode NVARCHAR(50) NULL,
    BankName NVARCHAR(150) NULL,
    IsMainAccount INT DEFAULT 0,
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (PartyId) REFERENCES dbo.Parties(Id)
);
GO

-- ============================================================
-- 25. DailyWageRates
-- ============================================================
IF OBJECT_ID('dbo.DailyWageRates', 'U') IS NULL
CREATE TABLE dbo.DailyWageRates (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Year INT NOT NULL,
    Category NVARCHAR(100) NOT NULL,
    Amount DECIMAL(18,0) NOT NULL,
    CONSTRAINT UQ_DailyWageRate UNIQUE (Year, Category)
);
GO

-- ============================================================
-- 26. EmployeeContracts
-- ============================================================
IF OBJECT_ID('dbo.EmployeeContracts', 'U') IS NULL
CREATE TABLE dbo.EmployeeContracts (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId INT NOT NULL,
    ContractNumber NVARCHAR(100) NULL,
    StartDate DATETIME NOT NULL,
    EndDate DATETIME NULL,
    Position NVARCHAR(150) NULL,
    PersonnelType NVARCHAR(100) NULL,
    DailyWageCategory NVARCHAR(100) NULL,
    BaseSalary DECIMAL(18,0) DEFAULT 0,
    YearsBonus DECIMAL(18,0) DEFAULT 0,
    AttractionAllowance DECIMAL(18,0) DEFAULT 0,
    ResponsibilityAllowance DECIMAL(18,0) DEFAULT 0,
    ManagementAllowance DECIMAL(18,0) DEFAULT 0,
    TechnicalAllowance DECIMAL(18,0) DEFAULT 0,
    HousingAllowance DECIMAL(18,0) DEFAULT 0,
    GroceryAllowance DECIMAL(18,0) DEFAULT 0,
    ChildAllowance DECIMAL(18,0) DEFAULT 0,
    MarriageAllowance DECIMAL(18,0) DEFAULT 0,
    OtherAllowances DECIMAL(18,0) DEFAULT 0,
    TransportationAllowance DECIMAL(18,0) DEFAULT 0,
    DailyWage DECIMAL(18,0) DEFAULT 0,
    HourlyWage DECIMAL(18,0) DEFAULT 0,
    HasInsurance INT DEFAULT 1,
    Status NVARCHAR(50) DEFAULT N'فعال',
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id)
);
GO

-- ============================================================
-- 27. EmployeeLoans
-- ============================================================
IF OBJECT_ID('dbo.EmployeeLoans', 'U') IS NULL
CREATE TABLE dbo.EmployeeLoans (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId INT NOT NULL,
    TotalAmount DECIMAL(18,0) NOT NULL,
    MonthlyInstallment DECIMAL(18,0) NOT NULL,
    RemainingAmount DECIMAL(18,0) NOT NULL,
    LoanDate DATETIME NOT NULL DEFAULT GETDATE(),
    Description NVARCHAR(MAX) NULL,
    IsActive INT DEFAULT 1,
    FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id)
);
GO

-- ============================================================
-- 28. LeaveRequests
-- ============================================================
IF OBJECT_ID('dbo.LeaveRequests', 'U') IS NULL
CREATE TABLE dbo.LeaveRequests (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId INT NOT NULL,
    LeaveType NVARCHAR(50) NOT NULL,
    StartDate DATETIME NOT NULL,
    EndDate DATETIME NOT NULL,
    DaysCount INT NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Status NVARCHAR(50) DEFAULT N'در انتظار',
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id)
);
GO

-- ============================================================
-- 29. Attendance
-- ============================================================
IF OBJECT_ID('dbo.Attendance', 'U') IS NULL
CREATE TABLE dbo.Attendance (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId INT NOT NULL,
    Year INT NOT NULL,
    Month INT NOT NULL,
    WorkingDays INT DEFAULT 30,
    HoursWorked DECIMAL(18,2) DEFAULT 0,
    OvertimeHours DECIMAL(18,2) DEFAULT 0,
    AbsenceDays INT DEFAULT 0,
    CreatedAt DATETIME DEFAULT GETDATE(),
    CONSTRAINT UQ_Attendance UNIQUE (EmployeeId, Year, Month),
    FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id)
);
GO

-- ============================================================
-- 30. AuditLogs
-- ============================================================
IF OBJECT_ID('dbo.AuditLogs', 'U') IS NULL
CREATE TABLE dbo.AuditLogs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(100) NULL,
    ActionType NVARCHAR(50) NULL,
    ModuleName NVARCHAR(100) NULL,
    Description NVARCHAR(MAX) NULL,
    LogDate DATETIME DEFAULT GETDATE()
);
GO

-- ============================================================
-- 31. Attachments
-- ============================================================
IF OBJECT_ID('dbo.Attachments', 'U') IS NULL
CREATE TABLE dbo.Attachments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    EntityType NVARCHAR(50) NOT NULL,
    EntityId INT NOT NULL,
    FileName NVARCHAR(255) NOT NULL,
    FileData VARBINARY(MAX) NOT NULL,
    FileSize BIGINT NOT NULL,
    UploadedAt DATETIME DEFAULT GETDATE()
);
GO

-- ============================================================
-- 32. Settlements
-- ============================================================
IF OBJECT_ID('dbo.Settlements', 'U') IS NULL
CREATE TABLE dbo.Settlements (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId INT NOT NULL,
    SettlementDate DATETIME NOT NULL,
    YearsOfWork INT DEFAULT 0,
    ServiceBonus DECIMAL(18,0) DEFAULT 0,
    LeaveBonus DECIMAL(18,0) DEFAULT 0,
    SeverancePay DECIMAL(18,0) DEFAULT 0,
    RemainingLoan DECIMAL(18,0) DEFAULT 0,
    NetSettlement DECIMAL(18,0) DEFAULT 0,
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id)
);
GO

-- ============================================================
-- 33. YearEndBonuses
-- ============================================================
IF OBJECT_ID('dbo.YearEndBonuses', 'U') IS NULL
CREATE TABLE dbo.YearEndBonuses (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId INT NOT NULL,
    Year INT NOT NULL,
    MonthsWorked INT DEFAULT 0,
    BaseSalary DECIMAL(18,0) DEFAULT 0,
    BonusAmount DECIMAL(18,0) DEFAULT 0,
    Tax DECIMAL(18,0) DEFAULT 0,
    NetBonus DECIMAL(18,0) DEFAULT 0,
    CreatedAt DATETIME DEFAULT GETDATE(),
    CONSTRAINT UQ_YearEndBonus UNIQUE (EmployeeId, Year),
    FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id)
);
GO

-- ============================================================
-- 34. EmployeeSupplementaryInsurances
-- ============================================================
IF OBJECT_ID('dbo.EmployeeSupplementaryInsurances', 'U') IS NULL
CREATE TABLE dbo.EmployeeSupplementaryInsurances (
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
GO

-- ============================================================
-- 35. Tafsilis
-- ============================================================
IF OBJECT_ID('dbo.Tafsilis', 'U') IS NULL
CREATE TABLE dbo.Tafsilis (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Code NVARCHAR(50) NOT NULL,
    PersonType NVARCHAR(50) DEFAULT N'شخص حقیقی',
    FirstName NVARCHAR(100) NULL,
    LastName NVARCHAR(100) NULL,
    CompanyName NVARCHAR(255) NULL,
    Name NVARCHAR(255) NULL,
    Type NVARCHAR(50) NOT NULL,
    NationalCode NVARCHAR(50) NULL,
    NationalId NVARCHAR(50) NULL,
    EconomicCode NVARCHAR(50) NULL,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME DEFAULT GETDATE()
);
GO

-- ============================================================
-- Indexes
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Employees_PersonnelCode')
    CREATE INDEX IX_Employees_PersonnelCode ON dbo.Employees(PersonnelCode);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Employees_NationalCode')
    CREATE INDEX IX_Employees_NationalCode ON dbo.Employees(NationalCode);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payrolls_Year_Month')
    CREATE INDEX IX_Payrolls_Year_Month ON dbo.Payrolls(Year, Month);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Attendance_Year_Month')
    CREATE INDEX IX_Attendance_Year_Month ON dbo.Attendance(Year, Month);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_LogDate')
    CREATE INDEX IX_AuditLogs_LogDate ON dbo.AuditLogs(LogDate DESC);
GO

-- ============================================================
-- Seed Data — Banks
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Banks)
BEGIN
    INSERT INTO dbo.Banks (BankCode, BankName) VALUES
    (N'010', N'بانک مرکزی جمهوری اسلامی ایران'),
    (N'011', N'بانک صنعت و معدن'),
    (N'012', N'بانک ملت'),
    (N'013', N'بانک رفاه کارگران'),
    (N'014', N'بانک مسکن'),
    (N'015', N'بانک سپه'),
    (N'016', N'بانک کشاورزی'),
    (N'017', N'بانک ملی ایران'),
    (N'018', N'بانک تجارت'),
    (N'019', N'بانک صادرات ایران'),
    (N'020', N'بانک توسعه صادرات ایران'),
    (N'021', N'پست بانک ایران'),
    (N'022', N'بانک توسعه تعاون'),
    (N'053', N'بانک کارآفرین'),
    (N'054', N'بانک پارسیان'),
    (N'055', N'بانک اقتصاد نوین'),
    (N'056', N'بانک سامان'),
    (N'057', N'بانک پاسارگاد'),
    (N'058', N'بانک سرمایه'),
    (N'059', N'بانک سینا'),
    (N'060', N'بانک قوامین'),
    (N'061', N'بانک شهر'),
    (N'063', N'بانک انصار'),
    (N'069', N'بانک توسعه تعاون'),
    (N'070', N'بانک دی'),
    (N'075', N'بانک ایران زمین');
END
GO

-- ============================================================
-- Seed Data — Departments
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Departments)
BEGIN
    INSERT INTO dbo.Departments (Name) VALUES
    (N'مدیریت'), (N'فنی'), (N'مالی'), (N'منابع انسانی');
END
GO

-- ============================================================
-- Seed Data — LookupItems
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.LookupItems)
BEGIN
    INSERT INTO dbo.LookupItems (Category, Value) VALUES
    (N'Gender', N'مرد'), (N'Gender', N'زن'),
    (N'Education', N'زیر دیپلم'), (N'Education', N'دیپلم'),
    (N'Education', N'کاردانی'), (N'Education', N'کارشناسی'),
    (N'Education', N'کارشناسی ارشد'), (N'Education', N'دکتری'),
    (N'MilitaryStatus', N'مشمول'), (N'MilitaryStatus', N'معافیت'),
    (N'MilitaryStatus', N'پایان خدمت'),
    (N'MaritalStatus', N'مجرد'), (N'MaritalStatus', N'متاهل');
END
GO

-- ============================================================
-- Seed Data — SalarySettings 1403
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.SalarySettings WHERE Year = 1403)
BEGIN
    INSERT INTO dbo.SalarySettings
        (Year, MinimumWage, HousingAllowance, FoodAllowance,
         ChildAllowanceRate, InsuranceEmployeeRate, InsuranceEmployerRate, TaxExemptionLimit)
    VALUES (1403, 71696040, 9000000, 14000000, 10, 7, 23, 1200000000);
END
GO

-- ============================================================
-- Seed Data — TaxBrackets 1403
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.TaxBrackets WHERE Year = 1403)
BEGIN
    INSERT INTO dbo.TaxBrackets (Year, FromAmount, ToAmount, TaxRate) VALUES
    (1403, 0,           1000000000, 0.10),
    (1403, 1000000000,  1500000000, 0.15),
    (1403, 1500000000,  2300000000, 0.20),
    (1403, 2300000000,  NULL,       0.30);
END
GO

-- ============================================================
-- Seed Data — admin (رمز: admin — SHA256)
-- ⚠️ بلافاصله بعد از اولین ورود رمز را تغییر دهید!
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = N'admin')
BEGIN
    INSERT INTO dbo.Users (Username, PasswordHash, Role, IsActive, MustChangePassword)
    VALUES (N'admin',
            '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918',
            N'Admin', 1, 1);
END
GO

PRINT N'✅ اسکریپت نصب با موفقیت اجرا شد.';
GO
