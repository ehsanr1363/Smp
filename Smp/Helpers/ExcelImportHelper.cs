using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using SMP.Data;
using SMP.Models;
using SMP.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SMP.Helpers
{
    public static class ExcelImportHelper
    {
        private static XLWorkbook LoadWorkbookSafe(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return new XLWorkbook(stream);
        }

        private static string GetCellText(IXLCell cell)
        {
            if (cell == null || cell.IsEmpty()) return "";
            try
            {
                switch (cell.DataType)
                {
                    case XLDataType.DateTime:
                        return DateHelper.ToPersianDate(cell.GetDateTime());
                    case XLDataType.Number:
                        return cell.GetDouble().ToString(CultureInfo.InvariantCulture);
                    case XLDataType.Boolean:
                        return cell.GetBoolean() ? "بله" : "خیر";
                    default:
                        return cell.GetString().Trim();
                }
            }
            catch
            {
                try { return cell.GetString().Trim(); } catch { return ""; }
            }
        }

        private static int ParseIntSafe(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            string clean = StringHelper.ConvertToEnglishDigits(s).Replace(",", "").Replace("٬", "").Trim();
            return int.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out int result) ? result : 0;
        }

        private static decimal ParseDecimalSafe(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            string clean = StringHelper.ConvertToEnglishDigits(s).Replace(",", "").Replace("٬", "").Replace("٫", ".").Trim();
            return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result) ? result : 0;
        }

        private static string CleanNationalCode(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string s = StringHelper.ConvertToEnglishDigits(raw).Replace(".", "").Replace("-", "").Replace(" ", "").Trim();
            if (s.Length > 0 && s.Length < 10) s = s.PadLeft(10, '0');
            return s;
        }

        public static async Task<DataTable> ParseEmployeesExcelAsync(string filePath)
        {
            var dt = new DataTable();
            string[] headers = {
                "کد پرسنلی", "نام", "نام خانوادگی", "کد ملی", "نام پدر",
                "شماره شناسنامه", "تاریخ تولد", "محل تولد", "محل صدور", "جنسیت",
                "وضعیت تاهل", "وضعیت نظام وظیفه", "تحصیلات", "رشته تخصصی", "تعداد فرزند",
                "شماره بیمه", "شماره تماس", "آدرس", "تاریخ استخدام", "تاریخ شروع قرارداد",
                "تاریخ پایان قرارداد", "سال قرارداد", "شماره قرارداد", "نام شرکت",
                "واحد سازمانی", "سمت", "نوع پرسنلی", "محل کار", "وضعیت استخدام",
                "حقوق مبنا", "پایه سنوات", "حق جذب", "حق مسئولیت", "حق مسکن",
                "حق خواربار", "حق اولاد", "حق تاهل", "حق مدیریت", "حق فنی",
                "سایر مزایا", "وضعیت (فعال/غیرفعال)", "شماره شبا", "وضعیت", "پیام خطا"
            };
            foreach (var h in headers) dt.Columns.Add(h);

            using var workbook = LoadWorkbookSafe(filePath);
            var worksheet = workbook.Worksheet(1);

            // 👇 پیدا کردن ایندکس ستون‌ها بر اساس نام هدر (برای جلوگیری از جابجایی)
            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var firstRow = worksheet.Row(1);
            foreach (var cell in firstRow.CellsUsed())
            {
                string headerName = cell.GetString().Trim();
                if (!string.IsNullOrEmpty(headerName))
                    headerMap[headerName] = cell.Address.ColumnNumber;
            }

            string GetValue(IXLRangeRow row, string headerName, string fallbackHeader = "")
            {
                if (headerMap.TryGetValue(headerName, out int colNum))
                    return GetCellText(row.Cell(colNum));
                if (!string.IsNullOrEmpty(fallbackHeader) && headerMap.TryGetValue(fallbackHeader, out int fallbackColNum))
                    return GetCellText(row.Cell(fallbackColNum));
                return "";
            }

            var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1) ?? Enumerable.Empty<IXLRangeRow>();

            var employeeService = new EmployeeService(new EmployeeRepository(AppConfig.ConnectionString));
            var list = await employeeService.GetEmployeesWithDetailsAsync("", true);

            var existingPersonnelCodes = new HashSet<string>(list.Select(e => (e.PersonnelCode ?? "").Trim()).Where(c => c.Length > 0), StringComparer.Ordinal);
            var existingNationalCodes = new HashSet<string>(list.Select(e => StringHelper.ConvertToEnglishDigits(e.NationalCode ?? "").Trim()).Where(c => c.Length > 0), StringComparer.Ordinal);
            var seenPersonnel = new HashSet<string>(StringComparer.Ordinal);
            var seenNational = new HashSet<string>(StringComparer.Ordinal);

            foreach (var row in rows)
            {
                string pCode = GetValue(row, "کد پرسنلی").Trim();
                string fName = GetValue(row, "نام");
                string lName = GetValue(row, "نام خانوادگی");
                string nCode = CleanNationalCode(GetValue(row, "کد ملی"));

                if (string.IsNullOrEmpty(pCode) && string.IsNullOrEmpty(fName)) continue;

                string status = "معتبر";
                string errorMsg = "";

                if (string.IsNullOrWhiteSpace(pCode)) { status = "خطا"; errorMsg = "کد پرسنلی خالی است"; }
                else if (!NationalCodeHelper.IsValid(nCode)) { status = "خطا"; errorMsg = "کد ملی نامعتبر است"; }
                else if (existingPersonnelCodes.Contains(pCode)) { status = "تکراری"; errorMsg = "کد پرسنلی موجود است"; }
                else if (existingNationalCodes.Contains(nCode)) { status = "تکراری"; errorMsg = "کد ملی موجود است"; }
                else if (seenPersonnel.Contains(pCode) || seenNational.Contains(nCode)) { status = "تکراری در فایل"; errorMsg = "در اکسل تکرار شده"; }
                else { seenPersonnel.Add(pCode); seenNational.Add(nCode); }

                int contractYear = ParseIntSafe(GetValue(row, "سال قرارداد"));
                if (contractYear <= 0) contractYear = 1403;

                dt.Rows.Add(
                    pCode,
                    fName,
                    lName,
                    nCode,
                    GetValue(row, "نام پدر"),
                    GetValue(row, "شماره شناسنامه"),
                    GetValue(row, "تاریخ تولد"),
                    GetValue(row, "محل تولد"),
                    GetValue(row, "محل صدور"),
                    StringHelper.GenderToPersian(GetValue(row, "جنسیت")),
                    GetValue(row, "وضعیت تاهل"),
                    GetValue(row, "وضعیت نظام وظیفه"),
                    GetValue(row, "تحصیلات"),
                    GetValue(row, "رشته تخصصی"),
                    ParseIntSafe(GetValue(row, "تعداد فرزند")),
                    GetValue(row, "شماره بیمه"),
                    GetValue(row, "شماره تماس"),
                    GetValue(row, "آدرس"),
                    GetValue(row, "تاریخ استخدام"),
                    GetValue(row, "تاریخ شروع قرارداد"),
                    GetValue(row, "تاریخ پایان قرارداد"),
                    contractYear,
                    GetValue(row, "شماره قرارداد"),
                    GetValue(row, "نام شرکت"),
                    GetValue(row, "واحد سازمانی"),
                    GetValue(row, "سمت"),
                    GetValue(row, "نوع پرسنلی"),
                    GetValue(row, "محل کار"),
                    GetValue(row, "وضعیت استخدام"),
                    ParseDecimalSafe(GetValue(row, "حقوق مبنا")),
                    ParseDecimalSafe(GetValue(row, "پایه سنوات")),
                    ParseDecimalSafe(GetValue(row, "حق جذب")),
                    ParseDecimalSafe(GetValue(row, "حق مسئولیت")),
                    ParseDecimalSafe(GetValue(row, "حق مسکن")),
                    ParseDecimalSafe(GetValue(row, "حق خواربار")),
                    ParseDecimalSafe(GetValue(row, "حق اولاد")),
                    ParseDecimalSafe(GetValue(row, "حق تاهل")),
                    ParseDecimalSafe(GetValue(row, "حق مدیریت")),
                    ParseDecimalSafe(GetValue(row, "حق فنی")),
                    ParseDecimalSafe(GetValue(row, "سایر مزایا")),
                    GetValue(row, "وضعیت (فعال/غیرفعال)", "وضعیت"),
                    StringHelper.ConvertToEnglishDigits(GetValue(row, "شماره شبا")).ToUpper().Trim(),
                    status, errorMsg);
            }
            return dt;
        }

        public static async Task<string> SaveEmployeesToDbAsync(string connectionString, DataTable validRows)
        {
            int successCount = 0, errorCount = 0;
            string firstError = "";
            var employeeService = new EmployeeService(new EmployeeRepository(connectionString));
            var bankRepo = new EmployeeBankAccountRepository(connectionString);
            var deptRepo = new DepartmentRepository(connectionString);
            var compRepo = new CompanyRepository(connectionString);

            var departments = await Task.Run(() => deptRepo.GetAll());
            var companies = await Task.Run(() => compRepo.GetAll());

            foreach (DataRow r in validRows.Rows)
            {
                try
                {
                    string isActiveStr = (r["وضعیت (فعال/غیرفعال)"].ToString() ?? "").Trim();

                    string deptName = r["واحد سازمانی"].ToString().Trim();
                    int? deptId = null;
                    if (!string.IsNullOrEmpty(deptName))
                    {
                        var match = departments.FirstOrDefault(d => StringHelper.NormalizePersianText(d.Name) == StringHelper.NormalizePersianText(deptName));
                        if (match != null) deptId = match.Id;
                    }

                    string compName = r["نام شرکت"].ToString().Trim();
                    int? compId = null;
                    if (!string.IsNullOrEmpty(compName))
                    {
                        var matchComp = companies.FirstOrDefault(c => StringHelper.NormalizePersianText(c.Name) == StringHelper.NormalizePersianText(compName));
                        if (matchComp != null) compId = matchComp.Id;
                    }

                    string insNumber = r["شماره بیمه"].ToString().Trim();

                    var emp = new Employee
                    {
                        PersonnelCode = r["کد پرسنلی"].ToString(),
                        FirstName = r["نام"].ToString(),
                        LastName = r["نام خانوادگی"].ToString(),
                        NationalCode = r["کد ملی"].ToString(),
                        FatherName = r["نام پدر"].ToString(),
                        BirthCertificateNumber = r["شماره شناسنامه"].ToString(),
                        BirthDate = DateHelper.ParsePersianDate(r["تاریخ تولد"].ToString()),
                        BirthPlace = r["محل تولد"].ToString(),
                        IssuancePlace = r["محل صدور"].ToString(),
                        Gender = r["جنسیت"].ToString(),
                        MaritalStatus = r["وضعیت تاهل"].ToString(),
                        MilitaryStatus = r["وضعیت نظام وظیفه"].ToString(),
                        Education = r["تحصیلات"].ToString(),
                        SpecializedField = r["رشته تخصصی"].ToString(),
                        ChildrenCount = ParseIntSafe(r["تعداد فرزند"].ToString()),
                        InsuranceNumber = insNumber,
                        HasInsurance = !string.IsNullOrEmpty(insNumber),
                        PhoneNumber = r["شماره تماس"].ToString(),
                        Address = r["آدرس"].ToString(),
                        HireDate = DateHelper.ParsePersianDate(r["تاریخ استخدام"].ToString()),
                        ContractStartDate = DateHelper.ParsePersianDate(r["تاریخ شروع قرارداد"].ToString()),
                        ContractEndDate = DateHelper.ParsePersianDate(r["تاریخ پایان قرارداد"].ToString()),
                        ContractYear = ParseIntSafe(r["سال قرارداد"].ToString()),
                        ContractNumber = r["شماره قرارداد"].ToString(),

                        CompanyId = compId,

                        DepartmentId = deptId,
                        DepartmentName = deptName,
                        Position = r["سمت"].ToString(),
                        PersonnelType = r["نوع پرسنلی"].ToString(),
                        WorkLocation = r["محل کار"].ToString(),
                        Status = r["وضعیت استخدام"].ToString(),
                        IsActive = (isActiveStr == "فعال" || isActiveStr == "1" || string.IsNullOrEmpty(isActiveStr)),
                        BaseSalary = ParseDecimalSafe(r["حقوق مبنا"].ToString()),
                        YearsBonus = ParseDecimalSafe(r["پایه سنوات"].ToString()),
                        AttractionAllowance = ParseDecimalSafe(r["حق جذب"].ToString()),
                        ResponsibilityAllowance = ParseDecimalSafe(r["حق مسئولیت"].ToString()),
                        HousingAllowance = ParseDecimalSafe(r["حق مسکن"].ToString()),
                        GroceryAllowance = ParseDecimalSafe(r["حق خواربار"].ToString()),
                        ChildAllowance = ParseDecimalSafe(r["حق اولاد"].ToString()),
                        MarriageAllowance = ParseDecimalSafe(r["حق تاهل"].ToString()),
                        ManagementAllowance = ParseDecimalSafe(r["حق مدیریت"].ToString()),
                        TechnicalAllowance = ParseDecimalSafe(r["حق فنی"].ToString()),
                        OtherAllowances = ParseDecimalSafe(r["سایر مزایا"].ToString())
                    };
                    await employeeService.AddAsync(emp);

                    string sheba = r["شماره شبا"].ToString().Trim();
                    if (!string.IsNullOrEmpty(sheba))
                    {
                        var normalizedSheba = BankHelper.NormalizeSheba(sheba);
                        if (normalizedSheba != null && normalizedSheba.Length == 24)
                        {
                            var bankAcc = new EmployeeBankAccount
                            {
                                EmployeeId = emp.Id,
                                ShebaNumber = "IR" + normalizedSheba,
                                BankCode = BankHelper.GetBankCodeFromSheba(normalizedSheba) ?? "",
                                BankName = BankHelper.GetBankNameFromSheba(normalizedSheba),
                                IsMainAccount = true
                            };
                            await bankRepo.AddAsync(bankAcc);
                        }
                    }

                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    if (string.IsNullOrEmpty(firstError)) firstError = ex.Message;
                    LogHelper.LogException(ex, "SaveEmployeesToDbAsync");
                }
            }
            string msg = $"تعداد {successCount} پرسنل ثبت شد.";
            if (errorCount > 0) msg += $"\nتعداد {errorCount} خطا.\nعلت: {firstError}";
            return msg;
        }

        public static async Task<DataTable> ParseDependentsExcelAsync(string filePath)
        {
            var dt = new DataTable();
            string[] headers = { "کد ملی پرسنل", "نام سرپرست", "نام", "نام خانوادگی", "کد ملی تحت تکفل", "شماره شناسنامه", "نام پدر", "تاریخ تولد", "جنسیت", "محل تولد", "نسبت", "بیمه تکمیلی", "وضعیت" };
            foreach (var h in headers) dt.Columns.Add(h);
            using var workbook = LoadWorkbookSafe(filePath);
            var worksheet = workbook.Worksheet(1);
            var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1) ?? Enumerable.Empty<IXLRangeRow>();
            var dependentService = new DependentService(new DependentRepository(AppConfig.ConnectionString));
            var seenNational = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                string pNatCode = CleanNationalCode(GetCellText(row.Cell(1)));
                string fName = GetCellText(row.Cell(2)); string lName = GetCellText(row.Cell(3)); string dNatCode = CleanNationalCode(GetCellText(row.Cell(4)));
                if (string.IsNullOrEmpty(pNatCode) && string.IsNullOrEmpty(fName)) continue;
                string guardianName = await dependentService.GetEmployeeFullNameByNationalCodeAsync(pNatCode);
                string status = "معتبر";
                if (string.IsNullOrWhiteSpace(guardianName)) { status = "سرپرست یافت نشد"; guardianName = "---"; }
                else if (string.IsNullOrWhiteSpace(dNatCode)) { status = "کد ملی خالی است"; }
                else if (!NationalCodeHelper.IsValid(dNatCode)) { status = "کد ملی نامعتبر"; }
                else
                {
                    bool dupInDb = await dependentService.IsDependentExistsAsync(dNatCode);
                    if (dupInDb) status = "تکراری در سیستم";
                    else if (seenNational.Contains(dNatCode)) status = "تکراری در فایل";
                    else seenNational.Add(dNatCode);
                }
                dt.Rows.Add(pNatCode, guardianName, fName, lName, dNatCode, GetCellText(row.Cell(5)), GetCellText(row.Cell(6)), GetCellText(row.Cell(7)), GetCellText(row.Cell(8)), GetCellText(row.Cell(9)), GetCellText(row.Cell(10)), GetCellText(row.Cell(11)), status);
            }
            return dt;
        }

        public static async Task<string> SaveDependentsToDbAsync(DataTable validRows)
        {
            int successCount = 0, errorCount = 0; string firstError = "";
            var dependentService = new DependentService(new DependentRepository(AppConfig.ConnectionString));
            foreach (DataRow r in validRows.Rows)
            {
                try
                {
                    string pNatCode = r["کد ملی پرسنل"].ToString();
                    int? empId = await dependentService.GetEmployeeIdByNationalCodeAsync(pNatCode);
                    if (!empId.HasValue) throw new Exception("سرپرست یافت نشد");
                    DateTime? parsedBirthDate = DateHelper.ParsePersianDate(r["تاریخ تولد"].ToString());
                    string insStr = (r["بیمه تکمیلی"].ToString() ?? "").Trim();
                    bool hasIns = insStr == "1" || insStr == "دارد" || insStr == "بله" || insStr.Equals("true", StringComparison.OrdinalIgnoreCase) || insStr.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    var dep = new Dependent
                    {
                        EmployeeId = empId.Value,
                        FirstName = r["نام"].ToString(),
                        LastName = r["نام خانوادگی"].ToString(),
                        NationalCode = r["کد ملی تحت تکفل"].ToString(),
                        BirthCertificateNumber = r["شماره شناسنامه"].ToString(),
                        FatherName = r["نام پدر"].ToString(),
                        BirthDate = parsedBirthDate,
                        Gender = r["جنسیت"].ToString(),
                        BirthPlace = r["محل تولد"].ToString(),
                        Relation = r["نسبت"].ToString(),
                        HasSupplementaryInsurance = hasIns
                    };
                    await dependentService.AddAsync(dep);
                    successCount++;
                }
                catch (Exception ex) { errorCount++; if (string.IsNullOrEmpty(firstError)) firstError = ex.Message; LogHelper.LogException(ex, "SaveDependentsToDbAsync"); }
            }
            string msg = $"تعداد {successCount} رکورد ثبت شد.";
            if (errorCount > 0) msg += $"\nتعداد {errorCount} خطا.\nعلت: {firstError}";
            return msg;
        }

        public static async Task<DataTable> ParseContractorContractsExcelAsync(string filePath)
        {
            var dt = new DataTable();
            string[] headers = { "شناسه طرف قرارداد", "شماره قرارداد", "کد کارگاه", "ردیف پیمان", "موضوع", "مبلغ", "تاریخ قرارداد", "تاریخ پایان", "نام پیمانکار (سیستم)", "وضعیت", "پیام خطا" };
            foreach (var h in headers) dt.Columns.Add(h);
            using var workbook = LoadWorkbookSafe(filePath);
            var worksheet = workbook.Worksheet(1);
            var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1) ?? Enumerable.Empty<IXLRangeRow>();
            using var conn = new SqlConnection(AppConfig.ConnectionString);
            await conn.OpenAsync();
            var seenContracts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                string identifier = StringHelper.ConvertToEnglishDigits(GetCellText(row.Cell(1))).Trim();
                string contractNum = GetCellText(row.Cell(2)).Trim();
                string workshopCode = StringHelper.ConvertToEnglishDigits(GetCellText(row.Cell(3))).Trim();
                string contractRow = StringHelper.ConvertToEnglishDigits(GetCellText(row.Cell(4))).Trim();
                if (string.IsNullOrEmpty(identifier) && string.IsNullOrEmpty(contractNum)) continue;
                string status = "معتبر"; string errorMsg = ""; string partyName = "---"; int? partyId = null;
                if (!string.IsNullOrWhiteSpace(identifier))
                {
                    using var cmdFind = new SqlCommand("SELECT TOP 1 Id, CASE WHEN PartyType = N'حقیقی' THEN FirstName + N' ' + LastName ELSE CompanyName END FROM Parties WHERE NationalCode = @id OR NationalId = @id", conn);
                    cmdFind.Parameters.AddWithValue("@id", identifier);
                    using var reader = await cmdFind.ExecuteReaderAsync();
                    if (await reader.ReadAsync()) { partyId = reader.GetInt32(0); partyName = reader.IsDBNull(1) ? "" : reader.GetString(1); }
                }
                if (partyId == null) { status = "خطا"; errorMsg = "پیمانکار یافت نشد."; }
                else if (string.IsNullOrWhiteSpace(contractNum)) { status = "خطا"; errorMsg = "شماره قرارداد خالی است."; }
                else if (seenContracts.Contains(contractNum)) { status = "تکراری در فایل"; errorMsg = "شماره قرارداد در فایل تکرار شده."; }
                else
                {
                    using var cmdCheck = new SqlCommand("SELECT COUNT(1) FROM ContractorContracts WHERE ContractNumber = @num", conn);
                    cmdCheck.Parameters.AddWithValue("@num", contractNum);
                    if (Convert.ToInt32(await cmdCheck.ExecuteScalarAsync()) > 0) { status = "تکراری"; errorMsg = "قبلاً ثبت شده است."; }
                    else seenContracts.Add(contractNum);
                }
                dt.Rows.Add(identifier, contractNum, workshopCode, contractRow, GetCellText(row.Cell(5)), StringHelper.ConvertToEnglishDigits(GetCellText(row.Cell(6))).Replace(",", "").Replace("٬", ""), GetCellText(row.Cell(7)), GetCellText(row.Cell(8)), partyName, status, errorMsg);
            }
            return dt;
        }

        public static async Task<string> SaveContractorContractsToDbAsync(DataTable validRows)
        {
            int successCount = 0, errorCount = 0; string firstError = "";
            using var conn = new SqlConnection(AppConfig.ConnectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                foreach (DataRow r in validRows.Rows)
                {
                    try
                    {
                        string identifier = r["شناسه طرف قرارداد"].ToString(); int partyId;
                        using (var cmdFind = new SqlCommand("SELECT TOP 1 Id FROM Parties WHERE NationalCode = @id OR NationalId = @id", conn, transaction))
                        {
                            cmdFind.Parameters.AddWithValue("@id", identifier);
                            var scalar = await cmdFind.ExecuteScalarAsync();
                            if (scalar == null || scalar == DBNull.Value) throw new Exception($"طرف قرارداد با شناسه «{identifier}» پیدا نشد.");
                            partyId = Convert.ToInt32(scalar);
                        }
                        decimal amount = ParseDecimalSafe(r["مبلغ"].ToString());
                        DateTime? cd = DateHelper.ParsePersianDate(r["تاریخ قرارداد"].ToString());
                        DateTime? ed = DateHelper.ParsePersianDate(r["تاریخ پایان"].ToString());
                        string cdStr = DateHelper.ToPersianDate(cd); string edStr = DateHelper.ToPersianDate(ed);
                        using var cmdInsert = new SqlCommand(@"
                            INSERT INTO ContractorContracts (PartyId, ContractNumber, WorkshopCode, ContractRow, Subject, Amount, ContractDate, EndDate, CreatedAt, Status)
                            VALUES (@pid, @num, @wc, @cr, @sub, @amt, @cd, @ed, @created, N'فعال');", conn, transaction);
                        cmdInsert.Parameters.AddWithValue("@pid", partyId); cmdInsert.Parameters.AddWithValue("@num", r["شماره قرارداد"].ToString());
                        cmdInsert.Parameters.AddWithValue("@wc", (r["کد کارگاه"].ToString() ?? "").Trim()); cmdInsert.Parameters.AddWithValue("@cr", (r["ردیف پیمان"].ToString() ?? "").Trim());
                        cmdInsert.Parameters.AddWithValue("@sub", r["موضوع"].ToString() ?? ""); cmdInsert.Parameters.AddWithValue("@amt", amount);
                        cmdInsert.Parameters.AddWithValue("@cd", string.IsNullOrEmpty(cdStr) ? (object)DBNull.Value : cdStr);
                        cmdInsert.Parameters.AddWithValue("@ed", string.IsNullOrEmpty(edStr) ? (object)DBNull.Value : edStr);
                        cmdInsert.Parameters.AddWithValue("@created", DateTime.Now);
                        await cmdInsert.ExecuteNonQueryAsync(); successCount++;
                    }
                    catch (Exception ex) { errorCount++; if (string.IsNullOrEmpty(firstError)) firstError = ex.Message; LogHelper.LogException(ex, "SaveContractorContractsToDbAsync"); }
                }
                transaction.Commit();
            }
            catch (Exception ex) { transaction.Rollback(); LogHelper.LogException(ex, "SaveContractorContractsToDbAsync - Transaction"); return "خطا: " + ex.Message; }
            string msg = $"تعداد {successCount} قرارداد ثبت شد.";
            if (errorCount > 0) msg += $"\nتعداد {errorCount} خطا.\nعلت: {firstError}";
            return msg;
        }

        public static async Task<DataTable> ParseClientContractsExcelAsync(string filePath)
        {
            var dt = new DataTable();
            string[] headers = { "شناسه طرف قرارداد", "شماره قرارداد", "ردیف قرارداد", "موضوع", "مبلغ", "تاریخ قرارداد", "تاریخ پایان", "نام کارفرما (سیستم)", "وضعیت", "پیام خطا" };
            foreach (var h in headers) dt.Columns.Add(h);
            using var workbook = LoadWorkbookSafe(filePath);
            var worksheet = workbook.Worksheet(1);
            var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1) ?? Enumerable.Empty<IXLRangeRow>();
            using var conn = new SqlConnection(AppConfig.ConnectionString);
            await conn.OpenAsync();
            var seenContracts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                string identifier = StringHelper.ConvertToEnglishDigits(GetCellText(row.Cell(1))).Trim();
                string contractNum = GetCellText(row.Cell(2)).Trim();
                string contractRow = StringHelper.ConvertToEnglishDigits(GetCellText(row.Cell(3))).Trim();
                if (string.IsNullOrEmpty(identifier) && string.IsNullOrEmpty(contractNum)) continue;
                string status = "معتبر"; string errorMsg = ""; string partyName = "---"; int? partyId = null;
                if (!string.IsNullOrWhiteSpace(identifier))
                {
                    using var cmdFind = new SqlCommand("SELECT TOP 1 Id, CASE WHEN PartyType = N'حقیقی' THEN FirstName + N' ' + LastName ELSE CompanyName END FROM Parties WHERE NationalCode = @id OR NationalId = @id", conn);
                    cmdFind.Parameters.AddWithValue("@id", identifier);
                    using var reader = await cmdFind.ExecuteReaderAsync();
                    if (await reader.ReadAsync()) { partyId = reader.GetInt32(0); partyName = reader.IsDBNull(1) ? "" : reader.GetString(1); }
                }
                if (partyId == null) { status = "خطا"; errorMsg = "کارفرما یافت نشد."; }
                else if (string.IsNullOrWhiteSpace(contractNum)) { status = "خطا"; errorMsg = "شماره قرارداد خالی است."; }
                else if (seenContracts.Contains(contractNum)) { status = "تکراری در فایل"; errorMsg = "شماره قرارداد در فایل تکرار شده."; }
                else
                {
                    using var cmdCheck = new SqlCommand("SELECT COUNT(1) FROM ClientContracts WHERE ContractNumber = @num", conn);
                    cmdCheck.Parameters.AddWithValue("@num", contractNum);
                    if (Convert.ToInt32(await cmdCheck.ExecuteScalarAsync()) > 0) { status = "تکراری"; errorMsg = "قبلاً ثبت شده است."; }
                    else seenContracts.Add(contractNum);
                }
                dt.Rows.Add(identifier, contractNum, contractRow, GetCellText(row.Cell(4)), StringHelper.ConvertToEnglishDigits(GetCellText(row.Cell(5))).Replace(",", "").Replace("٬", ""), GetCellText(row.Cell(6)), GetCellText(row.Cell(7)), partyName, status, errorMsg);
            }
            return dt;
        }

        public static async Task<string> SaveClientContractsToDbAsync(DataTable validRows)
        {
            int successCount = 0, errorCount = 0; string firstError = "";
            using var conn = new SqlConnection(AppConfig.ConnectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                foreach (DataRow r in validRows.Rows)
                {
                    try
                    {
                        string identifier = r["شناسه طرف قرارداد"].ToString(); int partyId;
                        using (var cmdFind = new SqlCommand("SELECT TOP 1 Id FROM Parties WHERE NationalCode = @id OR NationalId = @id", conn, transaction))
                        {
                            cmdFind.Parameters.AddWithValue("@id", identifier);
                            var scalar = await cmdFind.ExecuteScalarAsync();
                            if (scalar == null || scalar == DBNull.Value) throw new Exception($"طرف قرارداد با شناسه «{identifier}» پیدا نشد.");
                            partyId = Convert.ToInt32(scalar);
                        }
                        decimal amount = ParseDecimalSafe(r["مبلغ"].ToString());
                        DateTime? cd = DateHelper.ParsePersianDate(r["تاریخ قرارداد"].ToString());
                        DateTime? ed = DateHelper.ParsePersianDate(r["تاریخ پایان"].ToString());
                        string cdStr = DateHelper.ToPersianDate(cd); string edStr = DateHelper.ToPersianDate(ed);
                        using var cmdInsert = new SqlCommand(@"
                            INSERT INTO ClientContracts (PartyId, ContractNumber, ContractRow, Subject, Amount, ContractDate, EndDate, CreatedAt, Status)
                            VALUES (@pid, @num, @cr, @sub, @amt, @cd, @ed, @created, N'فعال');", conn, transaction);
                        cmdInsert.Parameters.AddWithValue("@pid", partyId); cmdInsert.Parameters.AddWithValue("@num", r["شماره قرارداد"].ToString());
                        cmdInsert.Parameters.AddWithValue("@cr", (r["ردیف قرارداد"].ToString() ?? "").Trim()); cmdInsert.Parameters.AddWithValue("@sub", r["موضوع"].ToString() ?? "");
                        cmdInsert.Parameters.AddWithValue("@amt", amount);
                        cmdInsert.Parameters.AddWithValue("@cd", string.IsNullOrEmpty(cdStr) ? (object)DBNull.Value : cdStr);
                        cmdInsert.Parameters.AddWithValue("@ed", string.IsNullOrEmpty(edStr) ? (object)DBNull.Value : edStr);
                        cmdInsert.Parameters.AddWithValue("@created", DateTime.Now);
                        await cmdInsert.ExecuteNonQueryAsync(); successCount++;
                    }
                    catch (Exception ex) { errorCount++; if (string.IsNullOrEmpty(firstError)) firstError = ex.Message; LogHelper.LogException(ex, "SaveClientContractsToDbAsync"); }
                }
                transaction.Commit();
            }
            catch (Exception ex) { transaction.Rollback(); LogHelper.LogException(ex, "SaveClientContractsToDbAsync - Transaction"); return "خطا: " + ex.Message; }
            string msg = $"تعداد {successCount} قرارداد ثبت شد.";
            if (errorCount > 0) msg += $"\nتعداد {errorCount} خطا.\nعلت: {firstError}";
            return msg;
        }

        // ✅ متد جدید: خواندن و اعتبارسنجی اکسل چک‌ها
        public static async Task<DataTable> ParseCashChecksExcelAsync(string filePath)
        {
            var dt = new DataTable();
            string[] headers = {
                "شماره چک", "تاریخ سررسید", "مبلغ", "نوع چک", "شماره حساب", "نام بانک",
                "کد تفصیلی گیرنده", "نام گیرنده", "کد ملی گیرنده", "شخص حقوقی", "شناسه صیاد", "شماره قرارداد",
                "نوع هزینه", "توضیحات", "AccountId", "ContractId", "ContractTitle", "وضعیت"
            };
            foreach (var h in headers) dt.Columns.Add(h);

            using var workbook = LoadWorkbookSafe(filePath);
            var worksheet = workbook.Worksheet(1);
            var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1) ?? Enumerable.Empty<IXLRangeRow>();

            using var conn = new SqlConnection(AppConfig.ConnectionString);
            await conn.OpenAsync();

            foreach (var row in rows)
            {
                string checkNumber = GetCellText(row.Cell(1)).Trim();
                string dueDateStr = GetCellText(row.Cell(2));
                string amountStr = GetCellText(row.Cell(3));
                string checkType = GetCellText(row.Cell(4));
                string accountNumber = GetCellText(row.Cell(5)).Trim();
                string bankName = GetCellText(row.Cell(6));

                string tafsiliCode = GetCellText(row.Cell(7)).Trim(); // ✅ کد تفصیلی
                string recipientName = GetCellText(row.Cell(8)).Trim();
                string recipientId = GetCellText(row.Cell(9)).Trim();
                string isLegalStr = GetCellText(row.Cell(10)).Trim();

                string sayadNumber = GetCellText(row.Cell(11));
                string contractNumber = GetCellText(row.Cell(12)).Trim();
                string costType = GetCellText(row.Cell(13));
                string description = GetCellText(row.Cell(14));

                if (string.IsNullOrWhiteSpace(checkNumber)) continue;

                string status = "معتبر";
                string errorMsg = "";
                int? accountId = null;
                int? contractId = null;
                string contractTitle = "";

                // ۱. اگر کد تفصیلی وارد شده بود، نام و کد ملی را خودکار پیدا کن
                if (!string.IsNullOrWhiteSpace(tafsiliCode))
                {
                    using var cmdTaf = new SqlCommand("SELECT Name, NationalCode, NationalId, PersonType FROM Tafsilis WHERE Code = @code", conn);
                    cmdTaf.Parameters.AddWithValue("@code", tafsiliCode);
                    using var readerTaf = await cmdTaf.ExecuteReaderAsync();
                    if (await readerTaf.ReadAsync())
                    {
                        recipientName = readerTaf.IsDBNull(0) ? "" : readerTaf.GetString(0);
                        string personType = readerTaf.IsDBNull(3) ? "شخص حقیقی" : readerTaf.GetString(3);
                        bool isLegal = personType.Contains("حقوقی");
                        isLegalStr = isLegal ? "بله" : "خیر";
                        recipientId = isLegal ? (readerTaf.IsDBNull(2) ? "" : readerTaf.GetString(2)) : (readerTaf.IsDBNull(1) ? "" : readerTaf.GetString(1));
                    }
                    else
                    {
                        status = "خطا"; errorMsg = "کد تفصیلی در سیستم یافت نشد";
                    }
                }
                else if (string.IsNullOrWhiteSpace(recipientName) || string.IsNullOrWhiteSpace(recipientId))
                {
                    status = "خطا"; errorMsg = "نام گیرنده یا کد ملی خالی است (و کد تفصیلی هم وارد نشده)";
                }

                // ۲. پیدا کردن حساب بانکی
                if (status == "معتبر")
                {
                    if (string.IsNullOrWhiteSpace(accountNumber)) { status = "خطا"; errorMsg = "شماره حساب خالی است"; }
                    else
                    {
                        using var cmdAcc = new SqlCommand("SELECT TOP 1 Id FROM CompanyBankAccounts WHERE AccountNumber = @num", conn);
                        cmdAcc.Parameters.AddWithValue("@num", accountNumber);
                        var resAcc = await cmdAcc.ExecuteScalarAsync();
                        if (resAcc != null && resAcc != DBNull.Value) accountId = Convert.ToInt32(resAcc);
                        else { status = "خطا"; errorMsg = "شماره حساب در سیستم یافت نشد"; }
                    }
                }

                // ۳. پیدا کردن قرارداد
                if (status == "معتبر")
                {
                    if (string.IsNullOrWhiteSpace(contractNumber)) { status = "خطا"; errorMsg = "شماره قرارداد خالی است"; }
                    else
                    {
                        using var cmdCon = new SqlCommand("SELECT TOP 1 Id, Subject FROM ClientContracts WHERE ContractNumber = @num", conn);
                        cmdCon.Parameters.AddWithValue("@num", contractNumber);
                        using var reader = await cmdCon.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            contractId = reader.GetInt32(0);
                            contractTitle = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        }
                        else { status = "خطا"; errorMsg = "شماره قرارداد در سیستم یافت نشد"; }
                    }
                }

                DateTime? dueDate = DateHelper.ParsePersianDate(dueDateStr);
                if (dueDate == null && status == "معتبر") { status = "خطا"; errorMsg = "تاریخ سررسید نامعتبر است"; }

                decimal amount = ParseDecimalSafe(amountStr);
                if (amount <= 0 && status == "معتبر") { status = "خطا"; errorMsg = "مبلغ نامعتبر است"; }

                dt.Rows.Add(
                    checkNumber, dueDateStr, amount, checkType, accountNumber, bankName,
                    tafsiliCode, recipientName, recipientId, isLegalStr, sayadNumber, contractNumber,
                    costType, description, accountId, contractId, contractTitle, status + (string.IsNullOrEmpty(errorMsg) ? "" : $" - {errorMsg}")
                );
            }
            return dt;
        }

        // ✅ متد جدید: ذخیره چک‌ها در دیتابیس
        public static async Task<string> SaveCashChecksToDbAsync(DataTable validRows)
        {
            int successCount = 0, errorCount = 0;
            string firstError = "";
            var repo = new CashCheckRepository(AppConfig.ConnectionString);

            foreach (DataRow r in validRows.Rows)
            {
                try
                {
                    string checkType = r["نوع چک"].ToString();
                    string isLegalStr = r["شخص حقوقی"].ToString();

                    var check = new CashCheck
                    {
                        CheckNumber = r["شماره چک"].ToString(),
                        DueDate = DateHelper.ParsePersianDate(r["تاریخ سررسید"].ToString()) ?? DateTime.Now,
                        Amount = ParseDecimalSafe(r["مبلغ"].ToString()),
                        CheckType = checkType,
                        AccountId = Convert.ToInt32(r["AccountId"]),
                        BankName = r["نام بانک"].ToString(),
                        RecipientName = r["نام گیرنده"].ToString(),
                        RecipientNationalId = r["کد ملی گیرنده"].ToString(),
                        IsLegalEntity = isLegalStr == "بله" || isLegalStr == "1",
                        SayadNumber = r["شناسه صیاد"].ToString(),
                        ContractId = Convert.ToInt32(r["ContractId"]),
                        ContractTitle = r["ContractTitle"].ToString(),
                        CostType = r["نوع هزینه"].ToString(),
                        Description = r["توضیحات"].ToString(),
                        Status = "در جریان",
                        CreatedAt = DateTime.Now
                    };

                    await repo.AddAsync(check);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    if (string.IsNullOrEmpty(firstError)) firstError = ex.Message;
                    LogHelper.LogException(ex, "SaveCashChecksToDbAsync");
                }
            }
            string msg = $"تعداد {successCount} چک ثبت شد.";
            if (errorCount > 0) msg += $"\nتعداد {errorCount} خطا.\nعلت: {firstError}";
            return msg;
        }

        private static string sayidOrEmpty(string val) => string.IsNullOrWhiteSpace(val) ? "" : val;
    }
}