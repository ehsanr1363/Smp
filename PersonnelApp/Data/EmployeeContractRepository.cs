using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using PersonnelApp.Models;
using PersonnelApp.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PersonnelApp.Data
{
    public class EmployeeContractRepository
    {
        private readonly string _connectionString;

        public EmployeeContractRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<EmployeeContract>> GetContractsByEmployeeIdAsync(int employeeId)
        {
            var list = new List<EmployeeContract>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT c.*, e.FirstName + ' ' + e.LastName AS EmployeeName
                FROM EmployeeContracts c
                INNER JOIN Employees e ON c.EmployeeId = e.Id
                WHERE c.EmployeeId = @EmpId
                ORDER BY c.StartDate DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EmpId", employeeId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new EmployeeContract
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    EmployeeId = reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                    ContractNumber = reader.IsDBNull(reader.GetOrdinal("ContractNumber")) ? "" : reader.GetString(reader.GetOrdinal("ContractNumber")),
                    StartDate = DateHelper.ParseDbDate(reader["StartDate"]) ?? DateTime.Now,
                    EndDate = DateHelper.ParseDbDate(reader["EndDate"]),
                    Position = reader.IsDBNull(reader.GetOrdinal("Position")) ? null : reader.GetString(reader.GetOrdinal("Position")),
                    PersonnelType = reader.IsDBNull(reader.GetOrdinal("PersonnelType")) ? null : reader.GetString(reader.GetOrdinal("PersonnelType")),
                    DailyWageCategory = reader.IsDBNull(reader.GetOrdinal("DailyWageCategory")) ? null : reader.GetString(reader.GetOrdinal("DailyWageCategory")),
                    BaseSalary = reader.IsDBNull(reader.GetOrdinal("BaseSalary")) ? 0 : Convert.ToDecimal(reader["BaseSalary"]),
                    YearsBonus = reader.IsDBNull(reader.GetOrdinal("YearsBonus")) ? 0 : Convert.ToDecimal(reader["YearsBonus"]),
                    AttractionAllowance = reader.IsDBNull(reader.GetOrdinal("AttractionAllowance")) ? 0 : Convert.ToDecimal(reader["AttractionAllowance"]),
                    ResponsibilityAllowance = reader.IsDBNull(reader.GetOrdinal("ResponsibilityAllowance")) ? 0 : Convert.ToDecimal(reader["ResponsibilityAllowance"]),
                    ManagementAllowance = reader.IsDBNull(reader.GetOrdinal("ManagementAllowance")) ? 0 : Convert.ToDecimal(reader["ManagementAllowance"]),
                    TechnicalAllowance = reader.IsDBNull(reader.GetOrdinal("TechnicalAllowance")) ? 0 : Convert.ToDecimal(reader["TechnicalAllowance"]),
                    HousingAllowance = reader.IsDBNull(reader.GetOrdinal("HousingAllowance")) ? 0 : Convert.ToDecimal(reader["HousingAllowance"]),
                    GroceryAllowance = reader.IsDBNull(reader.GetOrdinal("GroceryAllowance")) ? 0 : Convert.ToDecimal(reader["GroceryAllowance"]),
                    ChildAllowance = reader.IsDBNull(reader.GetOrdinal("ChildAllowance")) ? 0 : Convert.ToDecimal(reader["ChildAllowance"]),
                    MarriageAllowance = reader.IsDBNull(reader.GetOrdinal("MarriageAllowance")) ? 0 : Convert.ToDecimal(reader["MarriageAllowance"]),
                    OtherAllowances = reader.IsDBNull(reader.GetOrdinal("OtherAllowances")) ? 0 : Convert.ToDecimal(reader["OtherAllowances"]),
                    TransportationAllowance = reader.IsDBNull(reader.GetOrdinal("TransportationAllowance")) ? 0 : Convert.ToDecimal(reader["TransportationAllowance"]),
                    DailyWage = reader.IsDBNull(reader.GetOrdinal("DailyWage")) ? 0 : Convert.ToDecimal(reader["DailyWage"]),
                    HourlyWage = reader.IsDBNull(reader.GetOrdinal("HourlyWage")) ? 0 : Convert.ToDecimal(reader["HourlyWage"]),
                    HasInsurance = reader.IsDBNull(reader.GetOrdinal("HasInsurance")) || reader.GetBoolean(reader.GetOrdinal("HasInsurance")),
                    Status = reader.IsDBNull(reader.GetOrdinal("Status")) ? "فعال" : reader.GetString(reader.GetOrdinal("Status")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    EmployeeName = reader.GetString(reader.GetOrdinal("EmployeeName"))
                });
            }
            return list;
        }

        public async Task<EmployeeContract?> GetActiveContractByEmployeeIdAsync(int employeeId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            const string sql = @"
                SELECT TOP 1 c.*, e.FirstName + ' ' + e.LastName AS EmployeeName
                FROM EmployeeContracts c
                INNER JOIN Employees e ON c.EmployeeId = e.Id
                WHERE c.EmployeeId = @EmpId AND c.Status = N'فعال'
                ORDER BY c.StartDate DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EmpId", employeeId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new EmployeeContract
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                EmployeeId = reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                ContractNumber = reader.IsDBNull(reader.GetOrdinal("ContractNumber")) ? "" : reader.GetString(reader.GetOrdinal("ContractNumber")),
                StartDate = DateHelper.ParseDbDate(reader["StartDate"]) ?? DateTime.Now,
                EndDate = DateHelper.ParseDbDate(reader["EndDate"]),
                Position = reader.IsDBNull(reader.GetOrdinal("Position")) ? null : reader.GetString(reader.GetOrdinal("Position")),
                PersonnelType = reader.IsDBNull(reader.GetOrdinal("PersonnelType")) ? null : reader.GetString(reader.GetOrdinal("PersonnelType")),
                DailyWageCategory = reader.IsDBNull(reader.GetOrdinal("DailyWageCategory")) ? null : reader.GetString(reader.GetOrdinal("DailyWageCategory")),
                BaseSalary = Convert.ToDecimal(reader["BaseSalary"]),
                YearsBonus = Convert.ToDecimal(reader["YearsBonus"]),
                AttractionAllowance = Convert.ToDecimal(reader["AttractionAllowance"]),
                ResponsibilityAllowance = Convert.ToDecimal(reader["ResponsibilityAllowance"]),
                ManagementAllowance = Convert.ToDecimal(reader["ManagementAllowance"]),
                TechnicalAllowance = Convert.ToDecimal(reader["TechnicalAllowance"]),
                HousingAllowance = Convert.ToDecimal(reader["HousingAllowance"]),
                GroceryAllowance = Convert.ToDecimal(reader["GroceryAllowance"]),
                ChildAllowance = Convert.ToDecimal(reader["ChildAllowance"]),
                MarriageAllowance = Convert.ToDecimal(reader["MarriageAllowance"]),
                OtherAllowances = Convert.ToDecimal(reader["OtherAllowances"]),
                TransportationAllowance = Convert.ToDecimal(reader["TransportationAllowance"]),
                DailyWage = Convert.ToDecimal(reader["DailyWage"]),
                HourlyWage = Convert.ToDecimal(reader["HourlyWage"]),
                HasInsurance = Convert.ToBoolean(reader["HasInsurance"]),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                EmployeeName = reader.GetString(reader.GetOrdinal("EmployeeName"))
            };
        }

        public async Task AddNewContractAsync(EmployeeContract c)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                string terminateSql = @"
                    UPDATE EmployeeContracts 
                    SET Status = N'خاتمه یافته', EndDate = @NewStartDate 
                    WHERE EmployeeId = @EmpId AND Status = N'فعال'";
                using (var cmdTerminate = new SqlCommand(terminateSql, conn, transaction))
                {
                    cmdTerminate.Parameters.AddWithValue("@EmpId", c.EmployeeId);
                    cmdTerminate.Parameters.AddWithValue("@NewStartDate", c.StartDate.Date);
                    await cmdTerminate.ExecuteNonQueryAsync();
                }

                string insertSql = @"
                    INSERT INTO EmployeeContracts 
                    (EmployeeId, ContractNumber, StartDate, EndDate, Position, PersonnelType, DailyWageCategory, 
                     BaseSalary, YearsBonus, AttractionAllowance, ResponsibilityAllowance, 
                     ManagementAllowance, TechnicalAllowance, HousingAllowance, GroceryAllowance, 
                     ChildAllowance, MarriageAllowance, OtherAllowances, TransportationAllowance, DailyWage, HourlyWage, 
                     HasInsurance, Status)
                    VALUES 
                    (@EmpId, @Num, @Start, @End, @Pos, @Type, @DailyCat, 
                     @Base, @Years, @Attr, @Resp, 
                     @Mgmt, @Tech, @House, @Groc, 
                     @Child, @Marr, @Other, @Transport, @Daily, @Hourly, 
                     @Ins, N'فعال');
                    SELECT SCOPE_IDENTITY();";

                int newId;
                using (var cmdInsert = new SqlCommand(insertSql, conn, transaction))
                {
                    cmdInsert.Parameters.AddWithValue("@EmpId", c.EmployeeId);
                    cmdInsert.Parameters.AddWithValue("@Num", c.ContractNumber);
                    cmdInsert.Parameters.AddWithValue("@Start", c.StartDate.Date);
                    cmdInsert.Parameters.AddWithValue("@End", (object?)c.EndDate?.Date ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@Pos", (object?)c.Position ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@Type", (object?)c.PersonnelType ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@DailyCat", (object?)c.DailyWageCategory ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@Base", c.BaseSalary);
                    cmdInsert.Parameters.AddWithValue("@Years", c.YearsBonus);
                    cmdInsert.Parameters.AddWithValue("@Attr", c.AttractionAllowance);
                    cmdInsert.Parameters.AddWithValue("@Resp", c.ResponsibilityAllowance);
                    cmdInsert.Parameters.AddWithValue("@Mgmt", c.ManagementAllowance);
                    cmdInsert.Parameters.AddWithValue("@Tech", c.TechnicalAllowance);
                    cmdInsert.Parameters.AddWithValue("@House", c.HousingAllowance);
                    cmdInsert.Parameters.AddWithValue("@Groc", c.GroceryAllowance);
                    cmdInsert.Parameters.AddWithValue("@Child", c.ChildAllowance);
                    cmdInsert.Parameters.AddWithValue("@Marr", c.MarriageAllowance);
                    cmdInsert.Parameters.AddWithValue("@Other", c.OtherAllowances);
                    cmdInsert.Parameters.AddWithValue("@Transport", c.TransportationAllowance);
                    cmdInsert.Parameters.AddWithValue("@Daily", c.DailyWage);
                    cmdInsert.Parameters.AddWithValue("@Hourly", c.HourlyWage);
                    cmdInsert.Parameters.AddWithValue("@Ins", c.HasInsurance ? 1 : 0);
                    newId = Convert.ToInt32(await cmdInsert.ExecuteScalarAsync());
                }

                // ✅ ۴ فیلد مسکن، خواربار، اولاد و تاهل از این آپدیت حذف شدند تا مقادیر قبلی پاک نشوند
                string updateEmpSql = @"
                    UPDATE Employees SET 
                        ContractNumber = @Num, ContractStartDate = @Start, ContractEndDate = @End,
                        PersonnelType = @Type, 
                        BaseSalary = @Base, YearsBonus = @Years, AttractionAllowance = @Attr,
                        ResponsibilityAllowance = @Resp, ManagementAllowance = @Mgmt, TechnicalAllowance = @Tech,
                        OtherAllowances = @Other, TransportationAllowance = @Transport,
                        DailyWage = @Daily, HourlyWage = @Hourly, HasInsurance = @Ins
                    WHERE Id = @EmpId";
                using (var cmdUpdate = new SqlCommand(updateEmpSql, conn, transaction))
                {
                    cmdUpdate.Parameters.AddWithValue("@EmpId", c.EmployeeId);
                    cmdUpdate.Parameters.AddWithValue("@Num", c.ContractNumber);
                    cmdUpdate.Parameters.AddWithValue("@Start", c.StartDate.Date);
                    cmdUpdate.Parameters.AddWithValue("@End", (object?)c.EndDate?.Date ?? DBNull.Value);
                    cmdUpdate.Parameters.AddWithValue("@Type", (object?)c.PersonnelType ?? DBNull.Value);
                    cmdUpdate.Parameters.AddWithValue("@Base", c.BaseSalary);
                    cmdUpdate.Parameters.AddWithValue("@Years", c.YearsBonus);
                    cmdUpdate.Parameters.AddWithValue("@Attr", c.AttractionAllowance);
                    cmdUpdate.Parameters.AddWithValue("@Resp", c.ResponsibilityAllowance);
                    cmdUpdate.Parameters.AddWithValue("@Mgmt", c.ManagementAllowance);
                    cmdUpdate.Parameters.AddWithValue("@Tech", c.TechnicalAllowance);
                    cmdUpdate.Parameters.AddWithValue("@Other", c.OtherAllowances);
                    cmdUpdate.Parameters.AddWithValue("@Transport", c.TransportationAllowance);
                    cmdUpdate.Parameters.AddWithValue("@Daily", c.DailyWage);
                    cmdUpdate.Parameters.AddWithValue("@Hourly", c.HourlyWage);
                    cmdUpdate.Parameters.AddWithValue("@Ins", c.HasInsurance ? 1 : 0);
                    await cmdUpdate.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                c.Id = newId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateContractAsync(EmployeeContract c)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                string updateSql = @"
                    UPDATE EmployeeContracts SET 
                        ContractNumber = @Num, StartDate = @Start, EndDate = @End,
                        Position = @Pos, PersonnelType = @Type, DailyWageCategory = @DailyCat,
                        BaseSalary = @Base, YearsBonus = @Years, AttractionAllowance = @Attr,
                        ResponsibilityAllowance = @Resp, ManagementAllowance = @Mgmt, TechnicalAllowance = @Tech,
                        HousingAllowance = @House, GroceryAllowance = @Groc, ChildAllowance = @Child,
                        MarriageAllowance = @Marr, OtherAllowances = @Other, TransportationAllowance = @Transport,
                        DailyWage = @Daily, HourlyWage = @Hourly, HasInsurance = @Ins
                    WHERE Id = @Id";

                using (var cmd = new SqlCommand(updateSql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", c.Id);
                    cmd.Parameters.AddWithValue("@Num", c.ContractNumber);
                    cmd.Parameters.AddWithValue("@Start", c.StartDate.Date);
                    cmd.Parameters.AddWithValue("@End", (object?)c.EndDate?.Date ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Pos", (object?)c.Position ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Type", (object?)c.PersonnelType ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DailyCat", (object?)c.DailyWageCategory ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Base", c.BaseSalary);
                    cmd.Parameters.AddWithValue("@Years", c.YearsBonus);
                    cmd.Parameters.AddWithValue("@Attr", c.AttractionAllowance);
                    cmd.Parameters.AddWithValue("@Resp", c.ResponsibilityAllowance);
                    cmd.Parameters.AddWithValue("@Mgmt", c.ManagementAllowance);
                    cmd.Parameters.AddWithValue("@Tech", c.TechnicalAllowance);
                    cmd.Parameters.AddWithValue("@House", c.HousingAllowance);
                    cmd.Parameters.AddWithValue("@Groc", c.GroceryAllowance);
                    cmd.Parameters.AddWithValue("@Child", c.ChildAllowance);
                    cmd.Parameters.AddWithValue("@Marr", c.MarriageAllowance);
                    cmd.Parameters.AddWithValue("@Other", c.OtherAllowances);
                    cmd.Parameters.AddWithValue("@Transport", c.TransportationAllowance);
                    cmd.Parameters.AddWithValue("@Daily", c.DailyWage);
                    cmd.Parameters.AddWithValue("@Hourly", c.HourlyWage);
                    cmd.Parameters.AddWithValue("@Ins", c.HasInsurance ? 1 : 0);
                    await cmd.ExecuteNonQueryAsync();
                }

                string statusSql = "SELECT Status FROM EmployeeContracts WHERE Id = @Id";
                string status = "";
                using (var cmdStatus = new SqlCommand(statusSql, conn, transaction))
                {
                    cmdStatus.Parameters.AddWithValue("@Id", c.Id);
                    var res = await cmdStatus.ExecuteScalarAsync();
                    status = res?.ToString() ?? "";
                }

                if (status == "فعال")
                {
                    // ✅ ۴ فیلد مسکن، خواربار، اولاد و تاهل از این آپدیت حذف شدند
                    string updateEmpSql = @"
                        UPDATE Employees SET 
                            ContractNumber = @Num, ContractStartDate = @Start, ContractEndDate = @End,
                            PersonnelType = @Type, 
                            BaseSalary = @Base, YearsBonus = @Years, AttractionAllowance = @Attr,
                            ResponsibilityAllowance = @Resp, ManagementAllowance = @Mgmt, TechnicalAllowance = @Tech,
                            OtherAllowances = @Other, TransportationAllowance = @Transport,
                            DailyWage = @Daily, HourlyWage = @Hourly, HasInsurance = @Ins
                        WHERE Id = @EmpId";
                    using (var cmdUpdateEmp = new SqlCommand(updateEmpSql, conn, transaction))
                    {
                        cmdUpdateEmp.Parameters.AddWithValue("@EmpId", c.EmployeeId);
                        cmdUpdateEmp.Parameters.AddWithValue("@Num", c.ContractNumber);
                        cmdUpdateEmp.Parameters.AddWithValue("@Start", c.StartDate.Date);
                        cmdUpdateEmp.Parameters.AddWithValue("@End", (object?)c.EndDate?.Date ?? DBNull.Value);
                        cmdUpdateEmp.Parameters.AddWithValue("@Type", (object?)c.PersonnelType ?? DBNull.Value);
                        cmdUpdateEmp.Parameters.AddWithValue("@Base", c.BaseSalary);
                        cmdUpdateEmp.Parameters.AddWithValue("@Years", c.YearsBonus);
                        cmdUpdateEmp.Parameters.AddWithValue("@Attr", c.AttractionAllowance);
                        cmdUpdateEmp.Parameters.AddWithValue("@Resp", c.ResponsibilityAllowance);
                        cmdUpdateEmp.Parameters.AddWithValue("@Mgmt", c.ManagementAllowance);
                        cmdUpdateEmp.Parameters.AddWithValue("@Tech", c.TechnicalAllowance);
                        cmdUpdateEmp.Parameters.AddWithValue("@Other", c.OtherAllowances);
                        cmdUpdateEmp.Parameters.AddWithValue("@Transport", c.TransportationAllowance);
                        cmdUpdateEmp.Parameters.AddWithValue("@Daily", c.DailyWage);
                        cmdUpdateEmp.Parameters.AddWithValue("@Hourly", c.HourlyWage);
                        cmdUpdateEmp.Parameters.AddWithValue("@Ins", c.HasInsurance ? 1 : 0);
                        await cmdUpdateEmp.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public void Delete(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using (var cmd = new SqlCommand("SELECT Status FROM EmployeeContracts WHERE Id = @Id", conn))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                var status = cmd.ExecuteScalar()?.ToString();
                if (status == "فعال")
                {
                    throw new InvalidOperationException("شما نمی‌توانید قرارداد فعال کارمند را حذف کنید.\nابتدا یک قرارداد جدید ثبت کنید تا این قرارداد به صورت خودکار خاتمه یابد.");
                }
            }

            using var cmdDel = new SqlCommand("DELETE FROM EmployeeContracts WHERE Id = @Id", conn);
            cmdDel.Parameters.AddWithValue("@Id", id);
            cmdDel.ExecuteNonQuery();
        }

        public async Task<(int inserted, int updated, List<string> errors)> ImportFromExcelAsync(string filePath)
        {
            int inserted = 0;
            int updated = 0;
            var errors = new List<string>();

            using var workbook = new XLWorkbook(filePath);
            var ws = workbook.Worksheet(1);
            var rows = ws.RowsUsed().Skip(1);

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                int rowNum = 1;
                foreach (var row in rows)
                {
                    rowNum++;
                    string savePointName = $"Row_{rowNum}";
                    transaction.Save(savePointName);

                    try
                    {
                        string personnelCode = row.Cell(1).GetString().Trim();
                        string contractNumber = row.Cell(2).GetString().Trim();
                        string startDateStr = row.Cell(3).GetString().Trim();
                        string endDateStr = row.Cell(4).GetString().Trim();

                        string baseSalaryStr = row.Cell(5).GetString().Trim();
                        string yearsBonusStr = row.Cell(6).GetString().Trim();
                        string attractionStr = row.Cell(7).GetString().Trim();
                        string responsibilityStr = row.Cell(8).GetString().Trim();
                        string managementStr = row.Cell(9).GetString().Trim();
                        string technicalStr = row.Cell(10).GetString().Trim();
                        string housingStr = row.Cell(11).GetString().Trim();
                        string groceryStr = row.Cell(12).GetString().Trim();
                        string childStr = row.Cell(13).GetString().Trim();
                        string marriageStr = row.Cell(14).GetString().Trim();
                        string otherStr = row.Cell(15).GetString().Trim();
                        string transportationStr = row.Cell(16).GetString().Trim();
                        string dailyWageStr = row.Cell(17).GetString().Trim();
                        string hourlyWageStr = row.Cell(18).GetString().Trim();
                        string insuranceStr = row.Cell(19).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(personnelCode) || string.IsNullOrWhiteSpace(contractNumber))
                        {
                            errors.Add($"ردیف {rowNum}: کد پرسنلی و شماره قرارداد الزامی است.");
                            continue;
                        }

                        int? employeeId = null;
                        string personnelType = "ماهانه";
                        string dailyWageCategory = "";
                        string position = "";

                        string findEmpSql = @"
                            SELECT Id, ISNULL(PersonnelType, N'ماهانه'), ISNULL(DailyWageCategory, ''), ISNULL(Position, '') 
                            FROM Employees WHERE PersonnelCode = @Code";

                        using (var findCmd = new SqlCommand(findEmpSql, conn, transaction))
                        {
                            findCmd.Parameters.AddWithValue("@Code", personnelCode);
                            using var reader = await findCmd.ExecuteReaderAsync();
                            if (await reader.ReadAsync())
                            {
                                employeeId = reader.GetInt32(0);
                                personnelType = reader.GetString(1);
                                dailyWageCategory = reader.GetString(2);
                                position = reader.GetString(3);
                            }
                        }

                        if (!employeeId.HasValue)
                        {
                            errors.Add($"ردیف {rowNum}: کارمندی با کد «{personnelCode}» یافت نشد.");
                            continue;
                        }

                        DateTime startDate = ParseDate(startDateStr);
                        if (startDate == DateTime.MinValue)
                        {
                            errors.Add($"ردیف {rowNum}: تاریخ شروع نامعتبر است.");
                            continue;
                        }

                        DateTime? endDate = null;
                        if (!string.IsNullOrWhiteSpace(endDateStr))
                        {
                            DateTime tempEnd = ParseDate(endDateStr);
                            if (tempEnd != DateTime.MinValue) endDate = tempEnd;
                        }

                        decimal baseSalary = 0, yearsBonus = 0, attraction = 0, responsibility = 0;
                        decimal management = 0, technical = 0, housing = 0, grocery = 0;
                        decimal child = 0, marriage = 0, other = 0, transportation = 0;
                        decimal dailyWage = 0, hourlyWage = 0;

                        string typeNormalized = personnelType.Trim();

                        if (typeNormalized.Contains("روزمزد") || typeNormalized.Contains("روز مزد"))
                        {
                            int contractYear = startDate.Year;
                            string dailyRateQuery = "SELECT Amount FROM DailyWageRates WHERE Year = @Year AND Category = @Cat";
                            using var cmdRate = new SqlCommand(dailyRateQuery, conn, transaction);
                            cmdRate.Parameters.AddWithValue("@Year", contractYear);
                            cmdRate.Parameters.AddWithValue("@Cat", dailyWageCategory);

                            var rateResult = await cmdRate.ExecuteScalarAsync();
                            if (rateResult != null && rateResult != DBNull.Value)
                            {
                                dailyWage = Convert.ToDecimal(rateResult);
                            }
                            else
                            {
                                decimal.TryParse(dailyWageStr.Replace(",", "").Trim(), out dailyWage);
                            }
                        }
                        else if (typeNormalized.Contains("ساعتی") || typeNormalized.Contains("ساعت"))
                        {
                            decimal.TryParse(hourlyWageStr.Replace(",", "").Trim(), out hourlyWage);
                        }
                        else
                        {
                            decimal.TryParse(baseSalaryStr.Replace(",", "").Trim(), out baseSalary);
                            decimal.TryParse(yearsBonusStr.Replace(",", "").Trim(), out yearsBonus);
                            decimal.TryParse(attractionStr.Replace(",", "").Trim(), out attraction);
                            decimal.TryParse(responsibilityStr.Replace(",", "").Trim(), out responsibility);
                            decimal.TryParse(managementStr.Replace(",", "").Trim(), out management);
                            decimal.TryParse(technicalStr.Replace(",", "").Trim(), out technical);
                            decimal.TryParse(housingStr.Replace(",", "").Trim(), out housing);
                            decimal.TryParse(groceryStr.Replace(",", "").Trim(), out grocery);
                            decimal.TryParse(childStr.Replace(",", "").Trim(), out child);
                            decimal.TryParse(marriageStr.Replace(",", "").Trim(), out marriage);
                            decimal.TryParse(otherStr.Replace(",", "").Trim(), out other);
                            decimal.TryParse(transportationStr.Replace(",", "").Trim(), out transportation);
                        }

                        bool hasInsurance = insuranceStr == "دارد" || insuranceStr == "بله" || insuranceStr == "1";

                        int? existingId = null;
                        string checkSql = "SELECT Id FROM EmployeeContracts WHERE EmployeeId = @EmpId AND ContractNumber = @Num";
                        using (var checkCmd = new SqlCommand(checkSql, conn, transaction))
                        {
                            checkCmd.Parameters.AddWithValue("@EmpId", employeeId.Value);
                            checkCmd.Parameters.AddWithValue("@Num", contractNumber);
                            var result = await checkCmd.ExecuteScalarAsync();
                            if (result != null && result != DBNull.Value)
                                existingId = Convert.ToInt32(result);
                        }

                        if (existingId.HasValue)
                        {
                            string updateSql = @"
                                UPDATE EmployeeContracts SET 
                                    StartDate = @Start, EndDate = @End,
                                    BaseSalary = @Base, YearsBonus = @Years, AttractionAllowance = @Attr,
                                    ResponsibilityAllowance = @Resp, ManagementAllowance = @Mgmt, TechnicalAllowance = @Tech,
                                    HousingAllowance = @House, GroceryAllowance = @Groc, ChildAllowance = @Child,
                                    MarriageAllowance = @Marr, OtherAllowances = @Other, TransportationAllowance = @Transport,
                                    DailyWage = @Daily, HourlyWage = @Hourly, HasInsurance = @Ins,
                                    DailyWageCategory = @DailyCat
                                WHERE Id = @Id";
                            using var updateCmd = new SqlCommand(updateSql, conn, transaction);
                            updateCmd.Parameters.AddWithValue("@Start", startDate.Date);
                            updateCmd.Parameters.AddWithValue("@End", (object?)endDate?.Date ?? DBNull.Value);
                            updateCmd.Parameters.AddWithValue("@Base", baseSalary);
                            updateCmd.Parameters.AddWithValue("@Years", yearsBonus);
                            updateCmd.Parameters.AddWithValue("@Attr", attraction);
                            updateCmd.Parameters.AddWithValue("@Resp", responsibility);
                            updateCmd.Parameters.AddWithValue("@Mgmt", management);
                            updateCmd.Parameters.AddWithValue("@Tech", technical);
                            updateCmd.Parameters.AddWithValue("@House", housing);
                            updateCmd.Parameters.AddWithValue("@Groc", grocery);
                            updateCmd.Parameters.AddWithValue("@Child", child);
                            updateCmd.Parameters.AddWithValue("@Marr", marriage);
                            updateCmd.Parameters.AddWithValue("@Other", other);
                            updateCmd.Parameters.AddWithValue("@Transport", transportation);
                            updateCmd.Parameters.AddWithValue("@Daily", dailyWage);
                            updateCmd.Parameters.AddWithValue("@Hourly", hourlyWage);
                            updateCmd.Parameters.AddWithValue("@Ins", hasInsurance ? 1 : 0);
                            updateCmd.Parameters.AddWithValue("@DailyCat", (object?)dailyWageCategory ?? DBNull.Value);
                            updateCmd.Parameters.AddWithValue("@Id", existingId.Value);
                            await updateCmd.ExecuteNonQueryAsync();
                            updated++;
                        }
                        else
                        {
                            string insertSql = @"
                                INSERT INTO EmployeeContracts 
                                (EmployeeId, ContractNumber, StartDate, EndDate, Position, PersonnelType, DailyWageCategory, 
                                 BaseSalary, YearsBonus, AttractionAllowance, ResponsibilityAllowance, 
                                 ManagementAllowance, TechnicalAllowance, HousingAllowance, GroceryAllowance, 
                                 ChildAllowance, MarriageAllowance, OtherAllowances, TransportationAllowance, DailyWage, HourlyWage, 
                                 HasInsurance, Status)
                                VALUES 
                                (@EmpId, @Num, @Start, @End, @Pos, @Type, @DailyCat, 
                                 @Base, @Years, @Attr, @Resp, 
                                 @Mgmt, @Tech, @House, @Groc, 
                                 @Child, @Marr, @Other, @Transport, @Daily, @Hourly, 
                                 @Ins, N'فعال')";
                            using var insertCmd = new SqlCommand(insertSql, conn, transaction);
                            insertCmd.Parameters.AddWithValue("@EmpId", employeeId.Value);
                            insertCmd.Parameters.AddWithValue("@Num", contractNumber);
                            insertCmd.Parameters.AddWithValue("@Start", startDate.Date);
                            insertCmd.Parameters.AddWithValue("@End", (object?)endDate?.Date ?? DBNull.Value);
                            insertCmd.Parameters.AddWithValue("@Pos", (object?)position ?? DBNull.Value);
                            insertCmd.Parameters.AddWithValue("@Type", personnelType);
                            insertCmd.Parameters.AddWithValue("@DailyCat", (object?)dailyWageCategory ?? DBNull.Value);
                            insertCmd.Parameters.AddWithValue("@Base", baseSalary);
                            insertCmd.Parameters.AddWithValue("@Years", yearsBonus);
                            insertCmd.Parameters.AddWithValue("@Attr", attraction);
                            insertCmd.Parameters.AddWithValue("@Resp", responsibility);
                            insertCmd.Parameters.AddWithValue("@Mgmt", management);
                            insertCmd.Parameters.AddWithValue("@Tech", technical);
                            insertCmd.Parameters.AddWithValue("@House", housing);
                            insertCmd.Parameters.AddWithValue("@Groc", grocery);
                            insertCmd.Parameters.AddWithValue("@Child", child);
                            insertCmd.Parameters.AddWithValue("@Marr", marriage);
                            insertCmd.Parameters.AddWithValue("@Other", other);
                            insertCmd.Parameters.AddWithValue("@Transport", transportation);
                            insertCmd.Parameters.AddWithValue("@Daily", dailyWage);
                            insertCmd.Parameters.AddWithValue("@Hourly", hourlyWage);
                            insertCmd.Parameters.AddWithValue("@Ins", hasInsurance ? 1 : 0);
                            await insertCmd.ExecuteNonQueryAsync();
                            inserted++;
                        }

                        // ✅ ۴ فیلد مسکن، خواربار، اولاد و تاهل از این آپدیت حذف شدند
                        string updateEmpSql = @"
                            UPDATE Employees SET 
                                ContractNumber = @Num, ContractStartDate = @Start, ContractEndDate = @End,
                                BaseSalary = @Base, YearsBonus = @Years, AttractionAllowance = @Attr,
                                ResponsibilityAllowance = @Resp, ManagementAllowance = @Mgmt, TechnicalAllowance = @Tech,
                                OtherAllowances = @Other, TransportationAllowance = @Transport,
                                DailyWage = @Daily, HourlyWage = @Hourly, HasInsurance = @Ins
                            WHERE Id = @EmpId";
                        using var updateEmpCmd = new SqlCommand(updateEmpSql, conn, transaction);
                        updateEmpCmd.Parameters.AddWithValue("@EmpId", employeeId.Value);
                        updateEmpCmd.Parameters.AddWithValue("@Num", contractNumber);
                        updateEmpCmd.Parameters.AddWithValue("@Start", startDate.Date);
                        updateEmpCmd.Parameters.AddWithValue("@End", (object?)endDate?.Date ?? DBNull.Value);
                        updateEmpCmd.Parameters.AddWithValue("@Base", baseSalary);
                        updateEmpCmd.Parameters.AddWithValue("@Years", yearsBonus);
                        updateEmpCmd.Parameters.AddWithValue("@Attr", attraction);
                        updateEmpCmd.Parameters.AddWithValue("@Resp", responsibility);
                        updateEmpCmd.Parameters.AddWithValue("@Mgmt", management);
                        updateEmpCmd.Parameters.AddWithValue("@Tech", technical);
                        updateEmpCmd.Parameters.AddWithValue("@Other", other);
                        updateEmpCmd.Parameters.AddWithValue("@Transport", transportation);
                        updateEmpCmd.Parameters.AddWithValue("@Daily", dailyWage);
                        updateEmpCmd.Parameters.AddWithValue("@Hourly", hourlyWage);
                        updateEmpCmd.Parameters.AddWithValue("@Ins", hasInsurance ? 1 : 0);
                        await updateEmpCmd.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex)
                    {
                        try { transaction.Rollback(savePointName); } catch { }
                        errors.Add($"ردیف {rowNum}: {ex.Message}");
                    }
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return (inserted, updated, errors);
        }

        private DateTime ParseDate(string dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr)) return DateTime.MinValue;
            try
            {
                if (DateTime.TryParse(dateStr, out DateTime result)) return result;
            }
            catch { }

            try
            {
                var parts = dateStr.Split('/', '-');
                if (parts.Length != 3) return DateTime.MinValue;
                int year = Convert.ToInt32(parts[0]);
                int month = Convert.ToInt32(parts[1]);
                int day = Convert.ToInt32(parts[2]);
                var pc = new System.Globalization.PersianCalendar();
                return pc.ToDateTime(year, month, day, 0, 0, 0, 0);
            }
            catch { return DateTime.MinValue; }
        }
    }
}