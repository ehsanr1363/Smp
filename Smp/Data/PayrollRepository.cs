using DocumentFormat.OpenXml.Office.Word;
using Microsoft.Data.SqlClient;
using SMP.Helpers;
using SMP.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace SMP.Data
{
    public class PayrollRepository
    {
        private readonly string _connectionString;

        public PayrollRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<bool> HasPayrollsForMonthAsync(int year, int month)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("SELECT COUNT(1) FROM Payrolls WHERE Year = @Year AND Month = @Month", conn);
            cmd.Parameters.AddWithValue("@Year", year);
            cmd.Parameters.AddWithValue("@Month", month);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }

        public async Task SavePayrollsBatchAsync(List<PayrollRecord> payrolls)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                foreach (var p in payrolls)
                {
                    string sql = @"
                        IF EXISTS (SELECT 1 FROM Payrolls WHERE EmployeeId = @EmpId AND Year = @Year AND Month = @Month)
                        BEGIN
                            UPDATE Payrolls SET 
                                BaseSalary = @BaseSalary, TotalEarnings = @TotalEarnings, 
                                TotalDeductions = @TotalDeductions, NetPay = @NetPay,
                                InsuranceEmployee = @InsuranceEmployee, InsuranceEmployer = @InsuranceEmployer,
                                IncomeTax = @IncomeTax, LoanDeduction = @LoanDeduction,
                                SupplementaryInsuranceDeduction = @SupplementaryInsuranceDeduction,
                                CarInsuranceDeduction = @CarInsuranceDeduction,
                                OtherDeductions = @OtherDeductions, OvertimePay = @OvertimePay,
                                AbsenceDeduction = @AbsenceDeduction,
                                CalculatedAt = GETDATE()
                            WHERE EmployeeId = @EmpId AND Year = @Year AND Month = @Month
                        END
                        ELSE
                        BEGIN
                            INSERT INTO Payrolls 
                                (EmployeeId, Year, Month, BaseSalary, TotalEarnings, TotalDeductions, NetPay, 
                                 InsuranceEmployee, InsuranceEmployer, IncomeTax, LoanDeduction, 
                                 SupplementaryInsuranceDeduction, CarInsuranceDeduction, OtherDeductions, OvertimePay, AbsenceDeduction)
                            VALUES 
                                (@EmpId, @Year, @Month, @BaseSalary, @TotalEarnings, @TotalDeductions, @NetPay,
                                 @InsuranceEmployee, @InsuranceEmployer, @IncomeTax, @LoanDeduction,
                                 @SupplementaryInsuranceDeduction, @CarInsuranceDeduction, @OtherDeductions, @OvertimePay, @AbsenceDeduction)
                        END";

                    using var cmd = new SqlCommand(sql, conn, transaction);
                    cmd.Parameters.AddWithValue("@EmpId", p.EmployeeId);
                    cmd.Parameters.AddWithValue("@Year", p.Year);
                    cmd.Parameters.AddWithValue("@Month", p.Month);
                    cmd.Parameters.AddWithValue("@BaseSalary", p.BaseSalary);
                    cmd.Parameters.AddWithValue("@TotalEarnings", p.TotalEarnings);
                    cmd.Parameters.AddWithValue("@TotalDeductions", p.TotalDeductions);
                    cmd.Parameters.AddWithValue("@NetPay", p.NetPay);
                    cmd.Parameters.AddWithValue("@InsuranceEmployee", p.InsuranceEmployee);
                    cmd.Parameters.AddWithValue("@InsuranceEmployer", p.InsuranceEmployer);
                    cmd.Parameters.AddWithValue("@IncomeTax", p.IncomeTax);
                    cmd.Parameters.AddWithValue("@LoanDeduction", p.LoanDeduction);
                    cmd.Parameters.AddWithValue("@SupplementaryInsuranceDeduction", p.SupplementaryInsuranceDeduction);
                    cmd.Parameters.AddWithValue("@CarInsuranceDeduction", p.CarInsuranceDeduction);
                    cmd.Parameters.AddWithValue("@OtherDeductions", p.OtherDeductions);
                    cmd.Parameters.AddWithValue("@OvertimePay", p.OvertimePay);
                    cmd.Parameters.AddWithValue("@AbsenceDeduction", p.AbsenceDeduction);

                    await cmd.ExecuteNonQueryAsync();
                }
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task SavePayrollsAndDeductInstallmentsAsync(
            List<PayrollRecord> payrolls,
            Dictionary<int, decimal> loanDeductions,
            Dictionary<int, decimal> supplementaryInsuranceDeductions,
            Dictionary<int, decimal> carInsuranceDeductions)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                foreach (var p in payrolls)
                {
                    string sql = @"
                        IF EXISTS (SELECT 1 FROM Payrolls WHERE EmployeeId = @EmpId AND Year = @Year AND Month = @Month)
                        BEGIN
                            UPDATE Payrolls SET 
                                BaseSalary = @BaseSalary, TotalEarnings = @TotalEarnings, 
                                TotalDeductions = @TotalDeductions, NetPay = @NetPay,
                                InsuranceEmployee = @InsuranceEmployee, InsuranceEmployer = @InsuranceEmployer,
                                IncomeTax = @IncomeTax, LoanDeduction = @LoanDeduction,
                                SupplementaryInsuranceDeduction = @SupplementaryInsuranceDeduction,
                                CarInsuranceDeduction = @CarInsuranceDeduction,
                                OtherDeductions = @OtherDeductions, OvertimePay = @OvertimePay,
                                AbsenceDeduction = @AbsenceDeduction,
                                CalculatedAt = GETDATE()
                            WHERE EmployeeId = @EmpId AND Year = @Year AND Month = @Month
                        END
                        ELSE
                        BEGIN
                            INSERT INTO Payrolls 
                                (EmployeeId, Year, Month, BaseSalary, TotalEarnings, TotalDeductions, NetPay, 
                                 InsuranceEmployee, InsuranceEmployer, IncomeTax, LoanDeduction, 
                                 SupplementaryInsuranceDeduction, CarInsuranceDeduction, OtherDeductions, OvertimePay, AbsenceDeduction)
                            VALUES 
                                (@EmpId, @Year, @Month, @BaseSalary, @TotalEarnings, @TotalDeductions, @NetPay,
                                 @InsuranceEmployee, @InsuranceEmployer, @IncomeTax, @LoanDeduction,
                                 @SupplementaryInsuranceDeduction, @CarInsuranceDeduction, @OtherDeductions, @OvertimePay, @AbsenceDeduction)
                        END";

                    using var cmd = new SqlCommand(sql, conn, transaction);
                    cmd.Parameters.AddWithValue("@EmpId", p.EmployeeId);
                    cmd.Parameters.AddWithValue("@Year", p.Year);
                    cmd.Parameters.AddWithValue("@Month", p.Month);
                    cmd.Parameters.AddWithValue("@BaseSalary", p.BaseSalary);
                    cmd.Parameters.AddWithValue("@TotalEarnings", p.TotalEarnings);
                    cmd.Parameters.AddWithValue("@TotalDeductions", p.TotalDeductions);
                    cmd.Parameters.AddWithValue("@NetPay", p.NetPay);
                    cmd.Parameters.AddWithValue("@InsuranceEmployee", p.InsuranceEmployee);
                    cmd.Parameters.AddWithValue("@InsuranceEmployer", p.InsuranceEmployer);
                    cmd.Parameters.AddWithValue("@IncomeTax", p.IncomeTax);
                    cmd.Parameters.AddWithValue("@LoanDeduction", p.LoanDeduction);
                    cmd.Parameters.AddWithValue("@SupplementaryInsuranceDeduction", p.SupplementaryInsuranceDeduction);
                    cmd.Parameters.AddWithValue("@CarInsuranceDeduction", p.CarInsuranceDeduction);
                    cmd.Parameters.AddWithValue("@OtherDeductions", p.OtherDeductions);
                    cmd.Parameters.AddWithValue("@OvertimePay", p.OvertimePay);
                    cmd.Parameters.AddWithValue("@AbsenceDeduction", p.AbsenceDeduction);

                    await cmd.ExecuteNonQueryAsync();
                }

                if (loanDeductions != null && loanDeductions.Count > 0)
                {
                    foreach (var kv in loanDeductions)
                    {
                        if (kv.Value <= 0) continue;
                        await DeductFromInstallmentTableAsync(conn, transaction, "EmployeeLoans", kv.Key, kv.Value);
                    }
                }

                if (supplementaryInsuranceDeductions != null && supplementaryInsuranceDeductions.Count > 0)
                {
                    foreach (var kv in supplementaryInsuranceDeductions)
                    {
                        if (kv.Value <= 0) continue;
                        await DeductFromInstallmentTableAsync(conn, transaction, "EmployeeSupplementaryInsurances", kv.Key, kv.Value);
                    }
                }

                if (carInsuranceDeductions != null && carInsuranceDeductions.Count > 0)
                {
                    foreach (var kv in carInsuranceDeductions)
                    {
                        if (kv.Value <= 0) continue;
                        await DeductFromInstallmentTableAsync(conn, transaction, "EmployeeCarInsurances", kv.Key, kv.Value);
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

        private static async Task DeductFromInstallmentTableAsync(
            SqlConnection conn, SqlTransaction transaction, string tableName, int employeeId, decimal amountToDeduct)
        {
            if (tableName != "EmployeeLoans" && tableName != "EmployeeSupplementaryInsurances" && tableName != "EmployeeCarInsurances")
                throw new ArgumentException("نام جدول نامعتبر است.", nameof(tableName));

            string orderColumn = tableName == "EmployeeLoans" ? "LoanDate" : "StartDate";

            var itemsToDeduct = new List<(int Id, decimal Remaining)>();
            string getSql = $@"
                SELECT Id, RemainingAmount 
                FROM {tableName}
                WHERE EmployeeId = @EmpId AND IsActive = 1 AND RemainingAmount > 0
                ORDER BY {orderColumn} ASC, Id ASC";

            using (var cmdGet = new SqlCommand(getSql, conn, transaction))
            {
                cmdGet.Parameters.AddWithValue("@EmpId", employeeId);
                using var reader = await cmdGet.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    itemsToDeduct.Add((reader.GetInt32(0), reader.GetDecimal(1)));
                }
            }

            if (itemsToDeduct.Count == 0) return;

            foreach (var (id, remaining) in itemsToDeduct)
            {
                if (amountToDeduct <= 0) break;

                decimal deductFromThis = Math.Min(remaining, amountToDeduct);
                decimal newRemaining = remaining - deductFromThis;
                int newIsActive = newRemaining > 0 ? 1 : 0;

                string updateSql = $@"
                    UPDATE {tableName}
                    SET RemainingAmount = @NewRemaining,
                        IsActive = @NewIsActive
                    WHERE Id = @Id";

                using var cmdUpdate = new SqlCommand(updateSql, conn, transaction);
                cmdUpdate.Parameters.AddWithValue("@NewRemaining", newRemaining);
                cmdUpdate.Parameters.AddWithValue("@NewIsActive", newIsActive);
                cmdUpdate.Parameters.AddWithValue("@Id", id);
                await cmdUpdate.ExecuteNonQueryAsync();

                amountToDeduct -= deductFromThis;
            }
        }

        public async Task SavePayrollsAndDeductLoansAsync(
            List<PayrollRecord> payrolls,
            Dictionary<int, decimal> loanDeductions)
        {
            await SavePayrollsAndDeductInstallmentsAsync(
                payrolls,
                loanDeductions,
                new Dictionary<int, decimal>(),
                new Dictionary<int, decimal>());
        }

        public async Task DeletePayrollAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                int employeeId = 0;
                decimal loanDed = 0, suppDed = 0, carDed = 0;

                using (var read = new SqlCommand(
                    @"SELECT EmployeeId, LoanDeduction, SupplementaryInsuranceDeduction, CarInsuranceDeduction 
                      FROM Payrolls WHERE Id = @Id", conn, transaction))
                {
                    read.Parameters.AddWithValue("@Id", id);
                    using var r = await read.ExecuteReaderAsync();
                    if (!await r.ReadAsync())
                    {
                        transaction.Rollback();
                        return;
                    }
                    employeeId = r.GetInt32(0);
                    loanDed = r.IsDBNull(1) ? 0 : r.GetDecimal(1);
                    suppDed = r.IsDBNull(2) ? 0 : r.GetDecimal(2);
                    carDed = r.IsDBNull(3) ? 0 : r.GetDecimal(3);
                }

                using (var del = new SqlCommand("DELETE FROM Payrolls WHERE Id = @Id", conn, transaction))
                {
                    del.Parameters.AddWithValue("@Id", id);
                    await del.ExecuteNonQueryAsync();
                }

                if (loanDed > 0) await RestoreInstallmentAsync(conn, transaction, "EmployeeLoans", employeeId, loanDed);
                if (suppDed > 0) await RestoreInstallmentAsync(conn, transaction, "EmployeeSupplementaryInsurances", employeeId, suppDed);
                if (carDed > 0) await RestoreInstallmentAsync(conn, transaction, "EmployeeCarInsurances", employeeId, carDed);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeletePayrollsForMonthAsync(int year, int month)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                var payrolls = new List<(int Id, int EmployeeId, decimal LoanDed, decimal SuppDed, decimal CarDed)>();
                using (var read = new SqlCommand(
                    @"SELECT Id, EmployeeId, LoanDeduction, SupplementaryInsuranceDeduction, CarInsuranceDeduction 
                      FROM Payrolls WHERE Year = @Year AND Month = @Month", conn, transaction))
                {
                    read.Parameters.AddWithValue("@Year", year);
                    read.Parameters.AddWithValue("@Month", month);
                    using var r = await read.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        payrolls.Add((
                            r.GetInt32(0),
                            r.GetInt32(1),
                            r.IsDBNull(2) ? 0 : r.GetDecimal(2),
                            r.IsDBNull(3) ? 0 : r.GetDecimal(3),
                            r.IsDBNull(4) ? 0 : r.GetDecimal(4)
                        ));
                    }
                }

                using (var del = new SqlCommand("DELETE FROM Payrolls WHERE Year = @Year AND Month = @Month", conn, transaction))
                {
                    del.Parameters.AddWithValue("@Year", year);
                    del.Parameters.AddWithValue("@Month", month);
                    await del.ExecuteNonQueryAsync();
                }

                foreach (var p in payrolls)
                {
                    if (p.LoanDed > 0) await RestoreInstallmentAsync(conn, transaction, "EmployeeLoans", p.EmployeeId, p.LoanDed);
                    if (p.SuppDed > 0) await RestoreInstallmentAsync(conn, transaction, "EmployeeSupplementaryInsurances", p.EmployeeId, p.SuppDed);
                    if (p.CarDed > 0) await RestoreInstallmentAsync(conn, transaction, "EmployeeCarInsurances", p.EmployeeId, p.CarDed);
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static async Task RestoreInstallmentAsync(
            SqlConnection conn, SqlTransaction transaction, string tableName, int employeeId, decimal amountToRestore)
        {
            if (tableName != "EmployeeLoans" && tableName != "EmployeeSupplementaryInsurances" && tableName != "EmployeeCarInsurances")
                throw new ArgumentException("نام جدول نامعتبر است.", nameof(tableName));

            string orderColumn = tableName == "EmployeeLoans" ? "LoanDate" : "StartDate";

            string getSql = $@"
                SELECT Id, RemainingAmount, TotalAmount
                FROM {tableName}
                WHERE EmployeeId = @EmpId AND TotalAmount > RemainingAmount
                ORDER BY IsActive DESC, {orderColumn} DESC, Id DESC";

            var records = new List<(int Id, decimal Remaining, decimal Total)>();
            using (var cmd = new SqlCommand(getSql, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@EmpId", employeeId);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    records.Add((r.GetInt32(0), r.GetDecimal(1), r.GetDecimal(2)));
                }
            }

            foreach (var rec in records)
            {
                if (amountToRestore <= 0) break;

                decimal space = rec.Total - rec.Remaining;
                if (space <= 0) continue;

                decimal restore = Math.Min(space, amountToRestore);
                decimal newRemaining = rec.Remaining + restore;

                string updateSql = $@"
                    UPDATE {tableName}
                    SET RemainingAmount = @NewRemaining, IsActive = 1
                    WHERE Id = @Id";
                using var cmdUpd = new SqlCommand(updateSql, conn, transaction);
                cmdUpd.Parameters.AddWithValue("@NewRemaining", newRemaining);
                cmdUpd.Parameters.AddWithValue("@Id", rec.Id);
                await cmdUpd.ExecuteNonQueryAsync();

                amountToRestore -= restore;
            }
        }

        public async Task ReSavePayrollsForMonthWithInstallmentRestoreAsync(
            int year, int month,
            List<PayrollRecord> newRecords,
            Dictionary<int, decimal> newLoanDeductions,
            Dictionary<int, decimal> newSuppInsDeductions,
            Dictionary<int, decimal> newCarInsDeductions)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                var oldPayrolls = new List<(int EmployeeId, decimal LoanDed, decimal SuppDed, decimal CarDed)>();
                using (var read = new SqlCommand(
                    @"SELECT EmployeeId, 
                             ISNULL(LoanDeduction, 0), 
                             ISNULL(SupplementaryInsuranceDeduction, 0), 
                             ISNULL(CarInsuranceDeduction, 0)
                      FROM Payrolls WHERE Year = @Year AND Month = @Month", conn, transaction))
                {
                    read.Parameters.AddWithValue("@Year", year);
                    read.Parameters.AddWithValue("@Month", month);
                    using var r = await read.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        oldPayrolls.Add((
                            r.GetInt32(0),
                            r.IsDBNull(1) ? 0 : r.GetDecimal(1),
                            r.IsDBNull(2) ? 0 : r.GetDecimal(2),
                            r.IsDBNull(3) ? 0 : r.GetDecimal(3)
                        ));
                    }
                }

                foreach (var p in oldPayrolls)
                {
                    if (p.LoanDed > 0) await RestoreInstallmentAsync(conn, transaction, "EmployeeLoans", p.EmployeeId, p.LoanDed);
                    if (p.SuppDed > 0) await RestoreInstallmentAsync(conn, transaction, "EmployeeSupplementaryInsurances", p.EmployeeId, p.SuppDed);
                    if (p.CarDed > 0) await RestoreInstallmentAsync(conn, transaction, "EmployeeCarInsurances", p.EmployeeId, p.CarDed);
                }

                using (var del = new SqlCommand("DELETE FROM Payrolls WHERE Year = @Year AND Month = @Month", conn, transaction))
                {
                    del.Parameters.AddWithValue("@Year", year);
                    del.Parameters.AddWithValue("@Month", month);
                    await del.ExecuteNonQueryAsync();
                }

                foreach (var p in newRecords)
                {
                    const string insertSql = @"
                        INSERT INTO Payrolls 
                            (EmployeeId, Year, Month, BaseSalary, TotalEarnings, TotalDeductions, NetPay, 
                             InsuranceEmployee, InsuranceEmployer, IncomeTax, LoanDeduction, 
                             SupplementaryInsuranceDeduction, CarInsuranceDeduction, OtherDeductions, OvertimePay, AbsenceDeduction)
                        VALUES 
                            (@EmpId, @Year, @Month, @BaseSalary, @TotalEarnings, @TotalDeductions, @NetPay,
                             @InsuranceEmployee, @InsuranceEmployer, @IncomeTax, @LoanDeduction,
                             @SupplementaryInsuranceDeduction, @CarInsuranceDeduction, @OtherDeductions, @OvertimePay, @AbsenceDeduction)";
                    using var cmd = new SqlCommand(insertSql, conn, transaction);
                    cmd.Parameters.AddWithValue("@EmpId", p.EmployeeId);
                    cmd.Parameters.AddWithValue("@Year", p.Year);
                    cmd.Parameters.AddWithValue("@Month", p.Month);
                    cmd.Parameters.AddWithValue("@BaseSalary", p.BaseSalary);
                    cmd.Parameters.AddWithValue("@TotalEarnings", p.TotalEarnings);
                    cmd.Parameters.AddWithValue("@TotalDeductions", p.TotalDeductions);
                    cmd.Parameters.AddWithValue("@NetPay", p.NetPay);
                    cmd.Parameters.AddWithValue("@InsuranceEmployee", p.InsuranceEmployee);
                    cmd.Parameters.AddWithValue("@InsuranceEmployer", p.InsuranceEmployer);
                    cmd.Parameters.AddWithValue("@IncomeTax", p.IncomeTax);
                    cmd.Parameters.AddWithValue("@LoanDeduction", p.LoanDeduction);
                    cmd.Parameters.AddWithValue("@SupplementaryInsuranceDeduction", p.SupplementaryInsuranceDeduction);
                    cmd.Parameters.AddWithValue("@CarInsuranceDeduction", p.CarInsuranceDeduction);
                    cmd.Parameters.AddWithValue("@OtherDeductions", p.OtherDeductions);
                    cmd.Parameters.AddWithValue("@OvertimePay", p.OvertimePay);
                    cmd.Parameters.AddWithValue("@AbsenceDeduction", p.AbsenceDeduction);
                    await cmd.ExecuteNonQueryAsync();
                }

                foreach (var kv in newLoanDeductions)
                    if (kv.Value > 0) await DeductFromInstallmentTableAsync(conn, transaction, "EmployeeLoans", kv.Key, kv.Value);

                foreach (var kv in newSuppInsDeductions)
                    if (kv.Value > 0) await DeductFromInstallmentTableAsync(conn, transaction, "EmployeeSupplementaryInsurances", kv.Key, kv.Value);

                foreach (var kv in newCarInsDeductions)
                    if (kv.Value > 0) await DeductFromInstallmentTableAsync(conn, transaction, "EmployeeCarInsurances", kv.Key, kv.Value);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<DataTable> GetSavedPayrollsAsync(int year, int month)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // ✅ اضافه شدن NationalCode و Position برای هدر فیش حقوقی
            string sql = @"
                SELECT 
                    p.Id AS Id,
                    p.EmployeeId AS EmployeeId,
                    e.PersonnelCode AS PersonnelCode,
                    e.NationalCode AS NationalCode,
                    e.Position AS Position,
                    e.FirstName + ' ' + e.LastName AS EmployeeName,
                    ISNULL(e.PersonnelType, N'ماهانه') AS PersonnelType, 
                    e.InsuranceNumber AS InsuranceNumber,
                    
                    ISNULL(b.ShebaNumber, '---') AS ShebaNumber,
                    
                    p.BaseSalary AS BaseSalary,
                    ISNULL(p.OvertimePay, 0) AS OvertimePay,
                    p.TotalEarnings AS TotalEarnings,
                    p.InsuranceEmployee AS InsuranceEmployee,
                    p.InsuranceEmployer AS InsuranceEmployer,
                    p.SupplementaryInsuranceDeduction AS SupplementaryInsuranceDeduction,
                    p.CarInsuranceDeduction AS CarInsuranceDeduction,
                    p.IncomeTax AS IncomeTax,
                    p.LoanDeduction AS LoanDeduction,
                    p.OtherDeductions AS OtherDeductions,
                    p.AbsenceDeduction AS AbsenceDeduction,
                    
                    ISNULL(a.WorkingDays, 0) AS WorkingDays,
                    ISNULL(NULLIF(a.OvertimeHours, 0), NULLIF(a.NetOvertime, 0)) AS OvertimeHours,
                    ISNULL(a.AbsenceHours, 0) AS AbsenceHours,
                    
                    (ISNULL(p.AbsenceDeduction, 0) + ISNULL(p.InsuranceEmployee, 0) + ISNULL(p.IncomeTax, 0) + ISNULL(p.LoanDeduction, 0) + ISNULL(p.SupplementaryInsuranceDeduction, 0) + ISNULL(p.CarInsuranceDeduction, 0) + ISNULL(p.OtherDeductions, 0)) AS TotalDeductions,
                    
                    (p.TotalEarnings - (ISNULL(p.AbsenceDeduction, 0) + ISNULL(p.InsuranceEmployee, 0) + ISNULL(p.IncomeTax, 0) + ISNULL(p.LoanDeduction, 0) + ISNULL(p.SupplementaryInsuranceDeduction, 0) + ISNULL(p.CarInsuranceDeduction, 0) + ISNULL(p.OtherDeductions, 0))) AS NetPay,
                    
                    ISNULL(e.TransportationAllowance, 0) AS TransportationAllowance,
                    ISNULL(e.HousingAllowance, 0) AS HousingAllowance,
                    ISNULL(e.GroceryAllowance, 0) AS GroceryAllowance,
                    ISNULL(e.ChildAllowance, 0) AS ChildAllowance,
                    ISNULL(e.MarriageAllowance, 0) AS MarriageAllowance,
                    ISNULL(e.YearsBonus, 0) AS YearsBonus,
                    ISNULL(e.AttractionAllowance, 0) AS AttractionAllowance,
                    ISNULL(e.ResponsibilityAllowance, 0) AS ResponsibilityAllowance,
                    ISNULL(e.ManagementAllowance, 0) AS ManagementAllowance,
                    ISNULL(e.TechnicalAllowance, 0) AS TechnicalAllowance,
                    ISNULL(e.OtherAllowances, 0) AS OtherAllowances
                FROM Payrolls p
                INNER JOIN Employees e ON p.EmployeeId = e.Id
                LEFT JOIN Attendance a ON p.EmployeeId = a.EmployeeId AND p.Year = a.Year AND p.Month = a.Month
                LEFT JOIN EmployeeBankAccounts b ON e.Id = b.EmployeeId AND b.IsMainAccount = 1
                WHERE p.Year = @Year AND p.Month = @Month
                ORDER BY e.LastName, e.FirstName";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Year", year);
            cmd.Parameters.AddWithValue("@Month", month);

            using var adapter = new SqlDataAdapter(cmd);
            var dt = new DataTable();
            await Task.Run(() => adapter.Fill(dt));
            return dt;
        }

        public async Task<DataTable> GetBankPaymentListAsync(int year, int month)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT 
                    e.PersonnelCode AS [کد پرسنلی],
                    e.FirstName + ' ' + e.LastName AS [نام و نام خانوادگی],
                    e.NationalCode AS [کد ملی],
                    ISNULL(b.ShebaNumber, '') AS [شماره شبا], 
                    p.NetPay AS [مبلغ واریزی (ریال)]
                FROM Payrolls p
                INNER JOIN Employees e ON p.EmployeeId = e.Id
                LEFT JOIN EmployeeBankAccounts b ON e.Id = b.EmployeeId AND b.IsMainAccount = 1
                WHERE p.Year = @Year AND p.Month = @Month
                ORDER BY e.LastName, e.FirstName";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Year", year);
            cmd.Parameters.AddWithValue("@Month", month);

            using var adapter = new SqlDataAdapter(cmd);
            var dt = new DataTable();
            await Task.Run(() => adapter.Fill(dt));
            return dt;
        }

        public async Task<DataTable> GetEmployeePayrollHistoryAsync(int employeeId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT 
                    p.Year AS [سال],
                    p.Month AS [ماه],
                    p.BaseSalary AS [حقوق پایه],
                    p.TotalEarnings AS [جمع مزایا],
                    p.TotalDeductions AS [جمع کسورات],
                    p.NetPay AS [خالص پرداختی]
                FROM Payrolls p
                WHERE p.EmployeeId = @EmpId
                ORDER BY p.Year DESC, p.Month DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EmpId", employeeId);

            using var adapter = new SqlDataAdapter(cmd);
            var dt = new DataTable();
            await Task.Run(() => adapter.Fill(dt));
            return dt;
        }

        public async Task<DataTable> GetOfficialInsuranceReportAsync(int year, int month)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT 
                    e.PersonnelCode AS [کد پرسنلی],
                    e.FirstName + ' ' + e.LastName AS [نام و نام خانوادگی],
                    e.NationalCode AS [کد ملی],
                    e.InsuranceNumber AS [شماره بیمه],
                    (SELECT WorkingDays FROM Attendance a 
                     WHERE a.EmployeeId = e.Id AND a.Year = p.Year AND a.Month = p.Month) AS [روزهای کارکرد],
                    p.BaseSalary AS [حقوق پایه],
                    p.TotalEarnings AS [مبلغ مشمول بیمه],
                    p.InsuranceEmployee AS [سهم کارمند],
                    p.InsuranceEmployer AS [سهم کارفرما],
                    (p.InsuranceEmployee + p.InsuranceEmployer) AS [جمع بیمه]
                FROM Payrolls p
                INNER JOIN Employees e ON p.EmployeeId = e.Id
                WHERE p.Year = @Year AND p.Month = @Month
                ORDER BY e.LastName, e.FirstName";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Year", year);
            cmd.Parameters.AddWithValue("@Month", month);

            using var adapter = new SqlDataAdapter(cmd);
            var dt = new DataTable();
            await Task.Run(() => adapter.Fill(dt));
            return dt;
        }

        public async Task<DataTable> GetOfficialTaxReportAsync(int year, int month)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT 
                    e.PersonnelCode AS [کد پرسنلی],
                    e.FirstName + ' ' + e.LastName AS [نام و نام خانوادگی],
                    e.NationalCode AS [کد ملی],
                    p.BaseSalary AS [حقوق پایه],
                    p.TotalEarnings AS [جمع حقوق و مزایا],
                    p.InsuranceEmployee AS [بیمه کسر شده],
                    p.SupplementaryInsuranceDeduction AS [بیمه تکمیلی],
                    (p.TotalEarnings - p.InsuranceEmployee - p.SupplementaryInsuranceDeduction) AS [درآمد مشمول مالیات],
                    p.IncomeTax AS [مالیات محاسبه شده]
                FROM Payrolls p
                INNER JOIN Employees e ON p.EmployeeId = e.Id
                WHERE p.Year = @Year AND p.Month = @Month
                ORDER BY e.LastName, e.FirstName";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Year", year);
            cmd.Parameters.AddWithValue("@Month", month);

            using var adapter = new SqlDataAdapter(cmd);
            var dt = new DataTable();
            await Task.Run(() => adapter.Fill(dt));
            return dt;
        }
    }
}