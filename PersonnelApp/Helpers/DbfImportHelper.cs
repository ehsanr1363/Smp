using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DbfDataReader;
using KamiSama.IranSystem;
using Microsoft.Data.SqlClient;

namespace PersonnelApp.Helpers
{
    public class DbfImportResult
    {
        public int HeaderInsertCount { get; set; }
        public int HeaderUpdateCount { get; set; }
        public int DetailsInsertCount { get; set; }
        public int DetailsUpdateCount { get; set; }
        public int ErrorCount { get; set; }
        public int InvalidCount { get; set; }
        public int SkippedWorkshopCount { get; set; }
        public List<string> MissingPersonnel { get; set; } = new List<string>();
    }

    public static class DbfImportHelper
    {
        static DbfImportHelper()
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
            catch { }
        }

        private static string NormalizeContractRow(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            string cleaned = StringHelper.ConvertToEnglishDigits(input).Trim();
            if (long.TryParse(cleaned, out long num)) return num.ToString();
            return cleaned;
        }

        public static bool CheckCompanyPrerequisites(string connectionString, out string errorMessage)
        {
            errorMessage = "";
            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();

                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Companies", conn))
                {
                    if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                    {
                        errorMessage = "در سیستم هیچ شرکتی تعریف نشده است.\n\nابتدا باید حداقل یک شرکت (کارگاه) تعریف کنید.";
                        return false;
                    }
                }

                try
                {
                    using var cmdCheck = new SqlCommand("SELECT TOP 1 WorkshopCode FROM Companies", conn);
                    cmdCheck.ExecuteNonQuery();
                }
                catch (SqlException)
                {
                    errorMessage = "دیتابیس شما با نسخه جدید برنامه سازگار نیست.\nلطفاً یک بار برنامه را بسته و دوباره باز کنید.";
                    return false;
                }

                using (var cmdContract = new SqlCommand(
                    "SELECT COUNT(*) FROM ClientContracts " +
                    "WHERE ContractRow IS NOT NULL AND LTRIM(RTRIM(ContractRow)) != ''", conn))
                {
                    if (Convert.ToInt32(cmdContract.ExecuteScalar()) == 0)
                    {
                        errorMessage = "در سیستم هیچ قراردادی با کارفرما که دارای «ردیف پیمان» باشد ثبت نشده است.\n\nابتدا قرارداد کارفرما را ثبت کنید.";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "خطا در بررسی پیش‌نیازها:\n" + ex.Message;
                LogHelper.LogException(ex, "DbfImportHelper.CheckCompanyPrerequisites");
                return false;
            }
        }

        private static Dictionary<string, int> BuildFieldIndex(DbfDataReader.DbfDataReader dbf)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < dbf.FieldCount; i++)
            {
                string name = dbf.GetName(i).Trim();
                if (!map.ContainsKey(name)) map[name] = i;
            }
            return map;
        }

        public static DbfImportResult ImportCombined(string connectionString,
                                                     string headerFilePath,
                                                     string detailsFilePath)
        {
            var result = new DbfImportResult();

            using var conn = new SqlConnection(connectionString);
            conn.Open();
            using var transaction = conn.BeginTransaction();

            var headerContractMap = new Dictionary<string, string>();
            var validWorkshopCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var validNationalCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var validContractRows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var cmd = new SqlCommand(
                "SELECT WorkshopCode FROM Companies WHERE WorkshopCode IS NOT NULL AND LTRIM(RTRIM(WorkshopCode)) != ''",
                conn, transaction))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read()) validWorkshopCodes.Add(reader.GetString(0).Trim());

            using (var cmd = new SqlCommand(
                "SELECT NationalCode FROM Employees WHERE NationalCode IS NOT NULL AND LTRIM(RTRIM(NationalCode)) != ''",
                conn, transaction))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read()) validNationalCodes.Add(reader.GetString(0).Trim());

            using (var cmd = new SqlCommand(
                "SELECT ContractRow FROM ClientContracts WHERE ContractRow IS NOT NULL AND LTRIM(RTRIM(ContractRow)) != ''",
                conn, transaction))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read()) validContractRows.Add(NormalizeContractRow(reader.GetString(0)));

            var options = new DbfDataReaderOptions
            {
                SkipDeletedRecords = true,
                Encoding = Encoding.GetEncoding("ISO-8859-1")
            };

            // ---------- بخش اول: هدر ----------
            try
            {
                using var dbfHeader = new DbfDataReader.DbfDataReader(headerFilePath, options);
                var fieldIndex = BuildFieldIndex(dbfHeader);
                int rowNum = 0;

                while (dbfHeader.Read())
                {
                    rowNum++;
                    string savePoint = $"H_{rowNum}";
                    transaction.Save(savePoint);

                    try
                    {
                        string wc = StringHelper.ConvertToEnglishDigits(GetDbfValue(dbfHeader, fieldIndex, "DSK_ID")).Trim();
                        string yr = GetDbfValue(dbfHeader, fieldIndex, "DSK_YY").PadLeft(2, '0');
                        string mm = GetDbfValue(dbfHeader, fieldIndex, "DSK_MM").PadLeft(2, '0');
                        string ln = StringHelper.ConvertToEnglishDigits(GetDbfValue(dbfHeader, fieldIndex, "DSK_LISTNO")).Trim();
                        string cr = NormalizeContractRow(GetDbfValue(dbfHeader, fieldIndex, "MON_PYM"));

                        if (!validWorkshopCodes.Contains(wc))
                        {
                            result.SkippedWorkshopCount++;
                            continue;
                        }

                        if (!string.IsNullOrEmpty(cr) && !validContractRows.Contains(cr))
                        {
                            throw new Exception($"ردیف پیمان «{cr}» در فایل هدر (DBF) یافت شد، اما در سیستم ثبت نشده است.");
                        }

                        string key = $"{wc}_{yr}_{mm}_{ln}";
                        headerContractMap[key] = cr;

                        int? companyId = null;
                        using (var cmdFind = new SqlCommand(
                            "SELECT TOP 1 Id FROM Companies WHERE LTRIM(RTRIM(WorkshopCode)) = @wc;",
                            conn, transaction))
                        {
                            cmdFind.Parameters.AddWithValue("@wc", wc);
                            var res = cmdFind.ExecuteScalar();
                            if (res != null && res != DBNull.Value) companyId = Convert.ToInt32(res);
                        }

                        using var cmdCheck = new SqlCommand(@"
                            SELECT TOP 1 Id FROM WorkshopRecords
                            WHERE LTRIM(RTRIM(WorkshopCode)) = @wc
                              AND LTRIM(RTRIM(DSK_YY))     = @yr
                              AND LTRIM(RTRIM(DSK_MM))     = @mm
                              AND LTRIM(RTRIM(DSK_LISTNO)) = @ln
                              AND LTRIM(RTRIM(COALESCE(ContractRow, ''))) = @cr;",
                            conn, transaction);
                        cmdCheck.Parameters.AddWithValue("@wc", wc);
                        cmdCheck.Parameters.AddWithValue("@yr", yr);
                        cmdCheck.Parameters.AddWithValue("@mm", mm);
                        cmdCheck.Parameters.AddWithValue("@ln", ln);
                        cmdCheck.Parameters.AddWithValue("@cr", cr);
                        var existingId = cmdCheck.ExecuteScalar();
                        bool isUpdate = existingId != null;

                        string sql = isUpdate ? @"
                            UPDATE WorkshopRecords SET
                                CompanyId=@compId, DSK_NAME=@dsk_name, DSK_FARM=@dsk_farm,
                                DSK_ADRS=@dsk_adrs, DSK_KIND=@dsk_kind, DSK_DISC=@dsk_disc,
                                DSK_NUM=@dsk_num, DSK_TDD=@dsk_tdd, DSK_TROOZ=@dsk_trooz,
                                DSK_TMAH=@dsk_tmah, DSK_TMAZ=@dsk_tmaz, DSK_TMASH=@dsk_tmash,
                                DSK_TTOTL=@dsk_ttotl, DSK_TBIME=@dsk_tbime, DSK_TKOSO=@dsk_tkoso,
                                DSK_BIC=@dsk_bic, DSK_RATE=@dsk_rate, DSK_PRATE=@dsk_prate,
                                DSK_BIMH=@dsk_bimh, MON_PYM=@mon_pym, DSK_INC=@dsk_inc,
                                DSK_SPOUSE=@dsk_spouse
                            WHERE Id = @id;" : @"
                            INSERT INTO WorkshopRecords (
                                WorkshopCode, CompanyId, DSK_NAME, DSK_FARM, DSK_ADRS,
                                DSK_KIND, DSK_YY, DSK_MM, DSK_LISTNO, DSK_DISC, DSK_NUM,
                                DSK_TDD, DSK_TROOZ, DSK_TMAH, DSK_TMAZ, DSK_TMASH,
                                DSK_TTOTL, DSK_TBIME, DSK_TKOSO, DSK_BIC, DSK_RATE,
                                DSK_PRATE, DSK_BIMH, MON_PYM, ContractRow, DSK_INC, DSK_SPOUSE
                            ) VALUES (
                                @wc, @compId, @dsk_name, @dsk_farm, @dsk_adrs,
                                @dsk_kind, @yr, @mm, @ln, @dsk_disc, @dsk_num,
                                @dsk_tdd, @dsk_trooz, @dsk_tmah, @dsk_tmaz, @dsk_tmash,
                                @dsk_ttotl, @dsk_tbime, @dsk_tkoso, @dsk_bic, @dsk_rate,
                                @dsk_prate, @dsk_bimh, @mon_pym, @cr, @dsk_inc, @dsk_spouse
                            );";

                        using var cmdInsert = new SqlCommand(sql, conn, transaction);
                        if (isUpdate) cmdInsert.Parameters.AddWithValue("@id", Convert.ToInt32(existingId));
                        else
                        {
                            cmdInsert.Parameters.AddWithValue("@wc", wc);
                            cmdInsert.Parameters.AddWithValue("@yr", yr);
                            cmdInsert.Parameters.AddWithValue("@mm", mm);
                            cmdInsert.Parameters.AddWithValue("@ln", ln);
                            cmdInsert.Parameters.AddWithValue("@cr", cr);
                        }
                        cmdInsert.Parameters.AddWithValue("@compId", companyId.HasValue ? (object)companyId.Value : DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@dsk_name", GetDbfValue(dbfHeader, fieldIndex, "DSK_NAME"));
                        cmdInsert.Parameters.AddWithValue("@dsk_farm", GetDbfValue(dbfHeader, fieldIndex, "DSK_FARM"));
                        cmdInsert.Parameters.AddWithValue("@dsk_adrs", GetDbfValue(dbfHeader, fieldIndex, "DSK_ADRS"));
                        cmdInsert.Parameters.AddWithValue("@dsk_kind", GetDbfValue(dbfHeader, fieldIndex, "DSK_KIND"));
                        cmdInsert.Parameters.AddWithValue("@dsk_disc", GetDbfValue(dbfHeader, fieldIndex, "DSK_DISC"));
                        cmdInsert.Parameters.AddWithValue("@dsk_num", GetDbfValue(dbfHeader, fieldIndex, "DSK_NUM"));
                        cmdInsert.Parameters.AddWithValue("@dsk_tdd", GetDbfValue(dbfHeader, fieldIndex, "DSK_TDD"));
                        cmdInsert.Parameters.AddWithValue("@dsk_trooz", GetDbfValue(dbfHeader, fieldIndex, "DSK_TROOZ"));
                        cmdInsert.Parameters.AddWithValue("@dsk_tmah", GetDbfValue(dbfHeader, fieldIndex, "DSK_TMAH"));
                        cmdInsert.Parameters.AddWithValue("@dsk_tmaz", GetDbfValue(dbfHeader, fieldIndex, "DSK_TMAZ"));
                        cmdInsert.Parameters.AddWithValue("@dsk_tmash", GetDbfValue(dbfHeader, fieldIndex, "DSK_TMASH"));
                        cmdInsert.Parameters.AddWithValue("@dsk_ttotl", GetDbfValue(dbfHeader, fieldIndex, "DSK_TTOTL"));
                        cmdInsert.Parameters.AddWithValue("@dsk_tbime", GetDbfValue(dbfHeader, fieldIndex, "DSK_TBIME"));
                        cmdInsert.Parameters.AddWithValue("@dsk_tkoso", GetDbfValue(dbfHeader, fieldIndex, "DSK_TKOSO"));
                        cmdInsert.Parameters.AddWithValue("@dsk_bic", GetDbfValue(dbfHeader, fieldIndex, "DSK_BIC"));
                        cmdInsert.Parameters.AddWithValue("@dsk_rate", GetDbfValue(dbfHeader, fieldIndex, "DSK_RATE"));
                        cmdInsert.Parameters.AddWithValue("@dsk_prate", GetDbfValue(dbfHeader, fieldIndex, "DSK_PRATE"));
                        cmdInsert.Parameters.AddWithValue("@dsk_bimh", GetDbfValue(dbfHeader, fieldIndex, "DSK_BIMH"));
                        cmdInsert.Parameters.AddWithValue("@mon_pym", cr);
                        cmdInsert.Parameters.AddWithValue("@dsk_inc", GetDbfValue(dbfHeader, fieldIndex, "DSK_INC"));
                        cmdInsert.Parameters.AddWithValue("@dsk_spouse", GetDbfValue(dbfHeader, fieldIndex, "DSK_SPOUSE"));

                        cmdInsert.ExecuteNonQuery();
                        if (isUpdate) result.HeaderUpdateCount++; else result.HeaderInsertCount++;
                    }
                    catch (Exception ex)
                    {
                        try { transaction.Rollback(savePoint); } catch { }
                        result.ErrorCount++;
                        LogHelper.LogException(ex, $"DBF Header Row {rowNum}");
                    }
                }
            }
            catch (IOException)
            {
                transaction.Rollback();
                throw new Exception("فایل هدر (فایل ۱) در حال حاضر توسط برنامه دیگری باز است.\nلطفاً فایل را بسته و دوباره تلاش کنید.");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            // ---------- بخش دوم: جزئیات ----------
            try
            {
                using var dbfDetails = new DbfDataReader.DbfDataReader(detailsFilePath, options);
                var fieldIndex = BuildFieldIndex(dbfDetails);
                int rowNum = 0;

                while (dbfDetails.Read())
                {
                    rowNum++;
                    string savePoint = $"D_{rowNum}";
                    transaction.Save(savePoint);

                    try
                    {
                        string nc = CleanNationalCode(GetDbfValue(dbfDetails, fieldIndex,
                            "PER_NATCOD", "DSW_NATCOD", "DSW_NAT_CODE", "DSW_MELI"));

                        string yr = GetDbfValue(dbfDetails, fieldIndex, "DSW_YY").PadLeft(2, '0');
                        string mm = GetDbfValue(dbfDetails, fieldIndex, "DSW_MM").PadLeft(2, '0');
                        string wc = StringHelper.ConvertToEnglishDigits(GetDbfValue(dbfDetails, fieldIndex, "DSW_ID")).Trim();
                        string ln = StringHelper.ConvertToEnglishDigits(GetDbfValue(dbfDetails, fieldIndex, "DSW_LISTNO")).Trim();
                        string cr = NormalizeContractRow(GetDbfValue(dbfDetails, fieldIndex, "MON_PYM"));

                        string key = $"{wc}_{yr}_{mm}_{ln}";
                        if (headerContractMap.TryGetValue(key, out string? headerCR)
                            && !string.IsNullOrEmpty(headerCR))
                            cr = NormalizeContractRow(headerCR);

                        if (!string.IsNullOrEmpty(cr) && !validContractRows.Contains(cr))
                        {
                            throw new Exception($"ردیف پیمان «{cr}» در فایل جزئیات یافت شد، اما در سیستم ثبت نشده است.");
                        }

                        int? employeeId = null;
                        using (var cmdFindEmp = new SqlCommand(
                            "SELECT TOP 1 Id FROM Employees WHERE NationalCode = @nc;", conn, transaction))
                        {
                            cmdFindEmp.Parameters.AddWithValue("@nc", nc);
                            var resEmp = cmdFindEmp.ExecuteScalar();
                            if (resEmp != null && resEmp != DBNull.Value)
                                employeeId = Convert.ToInt32(resEmp);
                        }

                        if (!employeeId.HasValue)
                        {
                            result.InvalidCount++;
                            string fName = GetDbfValue(dbfDetails, fieldIndex, "DSW_FNAME");
                            string lName = GetDbfValue(dbfDetails, fieldIndex, "DSW_LNAME");
                            result.MissingPersonnel.Add($"{fName} {lName} (کد ملی: {nc})");
                            continue;
                        }

                        using var cmdCheck = new SqlCommand(@"
                            SELECT TOP 1 Id FROM InsuranceRecords
                            WHERE LTRIM(RTRIM(NationalCode)) = @nc
                              AND LTRIM(RTRIM(Year))         = @yr
                              AND LTRIM(RTRIM(Month))        = @mm
                              AND LTRIM(RTRIM(WorkshopCode)) = @wc
                              AND LTRIM(RTRIM(ListNumber))   = @ln
                              AND LTRIM(RTRIM(COALESCE(ContractRow, ''))) = @cr;",
                            conn, transaction);
                        cmdCheck.Parameters.AddWithValue("@nc", nc);
                        cmdCheck.Parameters.AddWithValue("@yr", yr);
                        cmdCheck.Parameters.AddWithValue("@mm", mm);
                        cmdCheck.Parameters.AddWithValue("@wc", wc);
                        cmdCheck.Parameters.AddWithValue("@ln", ln);
                        cmdCheck.Parameters.AddWithValue("@cr", cr);
                        var existingIdObj = cmdCheck.ExecuteScalar();
                        bool isUpdate = existingIdObj != null;

                        string sql = isUpdate ? @"
                            UPDATE InsuranceRecords SET
                                EmployeeId = @empId, FirstName = @fname, LastName = @lname,
                                FatherName = @iname, InsuranceNumber = @insNum, DaysWorked = @days,
                                Wage = @wage, JobTitle = @job, BirthCertificate = @idNum,
                                BirthPlace = @birthPlace, BirthDate = @birthDate, Gender = @gender,
                                Nationality = @nationality, HireDate = @hireDate, ExitDate = @exitDate,
                                MonthlyWage = @monthlyWage, MonthlyBenefits = @monthlyBenefits,
                                WageAndBenefitsIncluded = @wageBenefitsInc,
                                WageAndBenefitsTotal = @wageBenefitsTotal,
                                WorkerInsurance = @workerIns, JobCode = @jobCode,
                                ContractRow = @contractRow,
                                DSW_IDATE = @idate, DSW_PRATE = @prate,
                                DSW_INC = @inc, DSW_SPOUSE = @spouse, DSW_DD = @days
                            WHERE Id = @id;" : @"
                            INSERT INTO InsuranceRecords (
                                EmployeeId, NationalCode, FirstName, LastName, FatherName,
                                InsuranceNumber, ListNumber, Year, Month, DaysWorked, Wage,
                                JobTitle, DbfPersonnelCode, BirthCertificate, BirthPlace, BirthDate,
                                WorkshopCode, ListKind, Gender, Nationality, HireDate, ExitDate,
                                MonthlyWage, MonthlyBenefits, WageAndBenefitsIncluded,
                                WageAndBenefitsTotal, WorkerInsurance, Department1, JobCode,
                                YearsBonusDaily, MarriageAllowance, ContractRow,
                                DSW_IDATE, DSW_PRATE, DSW_INC, DSW_SPOUSE, DSW_DD
                            ) VALUES (
                                @empId, @nc, @fname, @lname, @iname,
                                @insNum, @ln, @yr, @mm, @days, @wage,
                                @job, @dbfCode, @idNum, @birthPlace, @birthDate,
                                @wc, @listKind, @gender, @nationality, @hireDate, @exitDate,
                                @monthlyWage, @monthlyBenefits, @wageBenefitsInc,
                                @wageBenefitsTotal, @workerIns, @dept1, @jobCode,
                                @yearsBonusDaily, @marriageAllowance, @contractRow,
                                @idate, @prate, @inc, @spouse, @days
                            );";

                        using var cmdInsert = new SqlCommand(sql, conn, transaction);
                        if (isUpdate) cmdInsert.Parameters.AddWithValue("@id", Convert.ToInt32(existingIdObj));
                        else
                        {
                            cmdInsert.Parameters.AddWithValue("@nc", nc);
                            cmdInsert.Parameters.AddWithValue("@ln", ln);
                            cmdInsert.Parameters.AddWithValue("@yr", yr);
                            cmdInsert.Parameters.AddWithValue("@mm", mm);
                            cmdInsert.Parameters.AddWithValue("@wc", wc);
                            cmdInsert.Parameters.AddWithValue("@dbfCode", "");
                            cmdInsert.Parameters.AddWithValue("@listKind", "");
                            cmdInsert.Parameters.AddWithValue("@dept1", "");
                            cmdInsert.Parameters.AddWithValue("@yearsBonusDaily", "");
                            cmdInsert.Parameters.AddWithValue("@marriageAllowance", "");
                        }

                        cmdInsert.Parameters.AddWithValue("@contractRow", cr);
                        cmdInsert.Parameters.AddWithValue("@empId", employeeId.Value);
                        cmdInsert.Parameters.AddWithValue("@fname", GetDbfValue(dbfDetails, fieldIndex, "DSW_FNAME"));
                        cmdInsert.Parameters.AddWithValue("@lname", GetDbfValue(dbfDetails, fieldIndex, "DSW_LNAME"));
                        cmdInsert.Parameters.AddWithValue("@iname", GetDbfValue(dbfDetails, fieldIndex, "DSW_DNAME"));
                        cmdInsert.Parameters.AddWithValue("@insNum", GetDbfValue(dbfDetails, fieldIndex, "DSW_ID1", "DSW_ISTNO"));
                        cmdInsert.Parameters.AddWithValue("@days", GetDbfValue(dbfDetails, fieldIndex, "DSW_DD"));
                        cmdInsert.Parameters.AddWithValue("@wage", GetDbfValue(dbfDetails, fieldIndex, "DSW_ROOZ"));
                        cmdInsert.Parameters.AddWithValue("@job", GetDbfValue(dbfDetails, fieldIndex, "DSW_OCP"));
                        cmdInsert.Parameters.AddWithValue("@idNum", GetDbfValue(dbfDetails, fieldIndex, "DSW_IDNO"));
                        cmdInsert.Parameters.AddWithValue("@birthPlace", GetDbfValue(dbfDetails, fieldIndex, "DSW_IDPLC"));
                        cmdInsert.Parameters.AddWithValue("@birthDate", GetDbfValue(dbfDetails, fieldIndex, "DSW_BDATE"));
                        cmdInsert.Parameters.AddWithValue("@gender", GetDbfValue(dbfDetails, fieldIndex, "DSW_SEX"));
                        cmdInsert.Parameters.AddWithValue("@nationality", GetDbfValue(dbfDetails, fieldIndex, "DSW_NAT"));
                        cmdInsert.Parameters.AddWithValue("@hireDate", GetDbfValue(dbfDetails, fieldIndex, "DSW_SDATE"));
                        cmdInsert.Parameters.AddWithValue("@exitDate", GetDbfValue(dbfDetails, fieldIndex, "DSW_EDATE"));
                        cmdInsert.Parameters.AddWithValue("@monthlyWage", GetDbfValue(dbfDetails, fieldIndex, "DSW_MAH"));
                        cmdInsert.Parameters.AddWithValue("@monthlyBenefits", GetDbfValue(dbfDetails, fieldIndex, "DSW_MAZ"));
                        cmdInsert.Parameters.AddWithValue("@wageBenefitsInc", GetDbfValue(dbfDetails, fieldIndex, "DSW_MASH"));
                        cmdInsert.Parameters.AddWithValue("@wageBenefitsTotal", GetDbfValue(dbfDetails, fieldIndex, "DSW_TOTL"));
                        cmdInsert.Parameters.AddWithValue("@workerIns", GetDbfValue(dbfDetails, fieldIndex, "DSW_BIME"));
                        cmdInsert.Parameters.AddWithValue("@jobCode", GetDbfValue(dbfDetails, fieldIndex, "DSW_JOB"));
                        cmdInsert.Parameters.AddWithValue("@idate", GetDbfValue(dbfDetails, fieldIndex, "DSW_IDATE"));
                        cmdInsert.Parameters.AddWithValue("@prate", GetDbfValue(dbfDetails, fieldIndex, "DSW_PRATE"));
                        cmdInsert.Parameters.AddWithValue("@inc", GetDbfValue(dbfDetails, fieldIndex, "DSW_INC"));
                        cmdInsert.Parameters.AddWithValue("@spouse", GetDbfValue(dbfDetails, fieldIndex, "DSW_SPOUSE"));

                        cmdInsert.ExecuteNonQuery();
                        if (isUpdate) result.DetailsUpdateCount++; else result.DetailsInsertCount++;
                    }
                    catch (Exception rowEx)
                    {
                        try { transaction.Rollback(savePoint); } catch { }
                        result.ErrorCount++;
                        LogHelper.LogException(rowEx, $"DBF Details Row {rowNum}");
                    }
                }
            }
            catch (IOException)
            {
                transaction.Rollback();
                throw new Exception("فایل جزئیات (فایل ۲) در حال حاضر توسط برنامه دیگری باز است.\nلطفاً فایل را بسته و دوباره تلاش کنید.");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            transaction.Commit();
            return result;
        }

        private static string CleanNationalCode(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string s = StringHelper.ConvertToEnglishDigits(raw)
                .Replace(".", "").Replace("-", "").Replace(" ", "").Trim();
            if (s.Length > 0 && s.Length < 10) s = s.PadLeft(10, '0');
            return s;
        }

        private static string GetDbfValue(DbfDataReader.DbfDataReader dbf,
                                          Dictionary<string, int> fieldIndex,
                                          params string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                if (!fieldIndex.TryGetValue(name, out int i)) continue;
                if (dbf.IsDBNull(i)) return "";

                string rawStr = dbf.GetValue(i).ToString()!;
                try
                {
                    if (string.IsNullOrWhiteSpace(rawStr)) return "";

                    bool hasNonAscii = rawStr.Any(c => c > 127);
                    if (hasNonAscii)
                    {
                        byte[] rawBytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(rawStr);
                        var iranSystemEncoder = new IranSystemEncoding();
                        string decodedStr = iranSystemEncoder.GetString(rawBytes).Trim()
                            .Normalize(NormalizationForm.FormKC).Replace(".", "").Trim();

                        if (Regex.IsMatch(decodedStr, @"[\u0600-\u06FF]"))
                        {
                            char[] charArray = decodedStr.ToCharArray();
                            Array.Reverse(charArray);
                            decodedStr = new string(charArray).Trim();
                        }
                        return decodedStr;
                    }
                    return rawStr.Trim();
                }
                catch
                {
                    return rawStr;
                }
            }
            return "";
        }
    }
}