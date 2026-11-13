using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using SMP.Models;
using SMP.Helpers;

namespace SMP.Data
{
    public class ClientContractRepository
    {
        private readonly string _connectionString;

        public ClientContractRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<ClientContract> GetAll(string search = "")
        {
            var list = new List<ClientContract>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string sql = @"
                SELECT c.Id, c.PartyId, c.ContractNumber, c.ContractRow, c.Subject, 
                       c.Amount, c.ContractDate, c.EndDate, c.Status, c.TerminationDate, c.CreatedAt,
                       c.VatPercentage, c.RetentionPercentage, c.InsurancePercentage, c.AdvancePaymentPercentage, c.TotalAdvanceAmount,
                       CASE WHEN p.PartyType = N'حقیقی' THEN ISNULL(p.FirstName,'') + N' ' + ISNULL(p.LastName,'') ELSE ISNULL(p.CompanyName,'') END AS DisplayName
                FROM ClientContracts c
                LEFT JOIN Parties p ON c.PartyId = p.Id
                WHERE 1 = 1";

            if (!string.IsNullOrWhiteSpace(search))
            {
                sql += @" AND (c.ContractNumber LIKE @search OR c.Subject LIKE @search OR p.FirstName LIKE @search 
                         OR p.LastName LIKE @search OR p.CompanyName LIKE @search)";
            }

            sql += " ORDER BY c.Id DESC";

            using var cmd = new SqlCommand(sql, conn);
            if (!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("@search", "%" + search.Trim() + "%");

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(MapReaderToContract(reader));
            }

            return list;
        }

        public ClientContract? GetById(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string sql = @"
                SELECT c.Id, c.PartyId, c.ContractNumber, c.ContractRow, c.Subject, 
                       c.Amount, c.ContractDate, c.EndDate, c.Status, c.TerminationDate, c.CreatedAt,
                       c.VatPercentage, c.RetentionPercentage, c.InsurancePercentage, c.AdvancePaymentPercentage, c.TotalAdvanceAmount,
                       CASE WHEN p.PartyType = N'حقیقی' THEN ISNULL(p.FirstName,'') + N' ' + ISNULL(p.LastName,'') ELSE ISNULL(p.CompanyName,'') END AS DisplayName
                FROM ClientContracts c
                LEFT JOIN Parties p ON c.PartyId = p.Id
                WHERE c.Id = @Id";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return MapReaderToContract(reader);
            }

            return null;
        }

        public void Add(ClientContract contract)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string sql = @"
                INSERT INTO ClientContracts 
                (PartyId, ContractNumber, ContractRow, Subject, Amount, ContractDate, EndDate, Status, TerminationDate, CreatedAt,
                 VatPercentage, RetentionPercentage, InsurancePercentage, AdvancePaymentPercentage, TotalAdvanceAmount)
                VALUES 
                (@PartyId, @ContractNumber, @ContractRow, @Subject, @Amount, @ContractDate, @EndDate, @Status, @TerminationDate, @CreatedAt,
                 @VatPercentage, @RetentionPercentage, @InsurancePercentage, @AdvancePaymentPercentage, @TotalAdvanceAmount);
                SELECT SCOPE_IDENTITY();";

            using var cmd = new SqlCommand(sql, conn);
            AddContractParameters(cmd, contract);
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.Date);

            var result = cmd.ExecuteScalar();
            if (result != null && result != DBNull.Value)
            {
                contract.Id = Convert.ToInt32(result);
            }
        }

        public void Update(ClientContract contract)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string sql = @"
                UPDATE ClientContracts SET 
                    PartyId = @PartyId, 
                    ContractNumber = @ContractNumber, 
                    ContractRow = @ContractRow, 
                    Subject = @Subject, 
                    Amount = @Amount, 
                    ContractDate = @ContractDate, 
                    EndDate = @EndDate, 
                    Status = @Status, 
                    TerminationDate = @TerminationDate,
                    VatPercentage = @VatPercentage,
                    RetentionPercentage = @RetentionPercentage,
                    InsurancePercentage = @InsurancePercentage,
                    AdvancePaymentPercentage = @AdvancePaymentPercentage,
                    TotalAdvanceAmount = @TotalAdvanceAmount
                WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, conn);
            AddContractParameters(cmd, contract);
            cmd.Parameters.AddWithValue("@Id", contract.Id);

            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using (var check1 = new SqlCommand("SELECT COUNT(1) FROM ContractorContracts WHERE ClientContractId = @Id", conn))
            {
                check1.Parameters.AddWithValue("@Id", id);
                if (Convert.ToInt32(check1.ExecuteScalar()) > 0)
                    throw new InvalidOperationException("این قرارداد کارفرما دارای قرارداد پیمانکار (جزء) مرتبط است و قابل حذف نیست.");
            }

            using (var check2 = new SqlCommand("SELECT COUNT(1) FROM ProjectExpenses WHERE ClientContractId = @Id", conn))
            {
                check2.Parameters.AddWithValue("@Id", id);
                if (Convert.ToInt32(check2.ExecuteScalar()) > 0)
                    throw new InvalidOperationException("این قرارداد کارفرما دارای فاکتور هزینه مرتبط است و قابل حذف نیست.");
            }

            using (var check3 = new SqlCommand("SELECT COUNT(1) FROM ClientStatements WHERE ContractId = @Id", conn))
            {
                check3.Parameters.AddWithValue("@Id", id);
                if (Convert.ToInt32(check3.ExecuteScalar()) > 0)
                    throw new InvalidOperationException("این قرارداد کارفرما دارای صورت وضعیت مرتبط است و قابل حذف نیست.");
            }

            using var cmd = new SqlCommand("DELETE FROM ClientContracts WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

        // ✅ FIX: استفاده از IsDBNull + Convert.ToDateTime به جای `as DateTime?`
        // اگر در آینده نوع ستون به datetime2 یا datetimeoffset تغییر کند، دیگر داده بی‌صدا از دست نمی‌رود.
        private ClientContract MapReaderToContract(SqlDataReader reader)
        {
            return new ClientContract
            {
                Id = Convert.ToInt32(reader["Id"]),
                PartyId = Convert.ToInt32(reader["PartyId"]),
                ContractNumber = reader["ContractNumber"]?.ToString() ?? "",
                ContractRow = reader["ContractRow"]?.ToString() ?? "",
                Subject = reader["Subject"]?.ToString() ?? "",
                Amount = reader["Amount"] != DBNull.Value ? Convert.ToDecimal(reader["Amount"]) : 0,

                ContractDate = reader["ContractDate"] != DBNull.Value
                    ? Convert.ToDateTime(reader["ContractDate"])
                    : (DateTime?)null,
                EndDate = reader["EndDate"] != DBNull.Value
                    ? Convert.ToDateTime(reader["EndDate"])
                    : (DateTime?)null,
                TerminationDate = reader["TerminationDate"] != DBNull.Value
                    ? Convert.ToDateTime(reader["TerminationDate"])
                    : (DateTime?)null,

                Status = reader["Status"]?.ToString() ?? "فعال",
                CreatedAt = reader["CreatedAt"] != DBNull.Value
                    ? Convert.ToDateTime(reader["CreatedAt"])
                    : (DateTime?)null,

                VatPercentage = reader["VatPercentage"] != DBNull.Value ? Convert.ToDecimal(reader["VatPercentage"]) : 0,
                RetentionPercentage = reader["RetentionPercentage"] != DBNull.Value ? Convert.ToDecimal(reader["RetentionPercentage"]) : 0,
                InsurancePercentage = reader["InsurancePercentage"] != DBNull.Value ? Convert.ToDecimal(reader["InsurancePercentage"]) : 0,
                AdvancePaymentPercentage = reader["AdvancePaymentPercentage"] != DBNull.Value ? Convert.ToDecimal(reader["AdvancePaymentPercentage"]) : 0,
                TotalAdvanceAmount = reader["TotalAdvanceAmount"] != DBNull.Value ? Convert.ToDecimal(reader["TotalAdvanceAmount"]) : 0,

                DisplayName = reader["DisplayName"]?.ToString() ?? ""
            };
        }

        private void AddContractParameters(SqlCommand cmd, ClientContract contract)
        {
            cmd.Parameters.AddWithValue("@PartyId", contract.PartyId);
            cmd.Parameters.AddWithValue("@ContractNumber", contract.ContractNumber ?? "");
            cmd.Parameters.AddWithValue("@ContractRow", (object?)contract.ContractRow ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Subject", contract.Subject ?? "");
            cmd.Parameters.AddWithValue("@Amount", contract.Amount);

            cmd.Parameters.AddWithValue("@ContractDate", (object?)contract.ContractDate?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EndDate", (object?)contract.EndDate?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TerminationDate", (object?)contract.TerminationDate?.Date ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@Status", contract.Status ?? "فعال");

            cmd.Parameters.AddWithValue("@VatPercentage", contract.VatPercentage);
            cmd.Parameters.AddWithValue("@RetentionPercentage", contract.RetentionPercentage);
            cmd.Parameters.AddWithValue("@InsurancePercentage", contract.InsurancePercentage);
            cmd.Parameters.AddWithValue("@AdvancePaymentPercentage", contract.AdvancePaymentPercentage);
            cmd.Parameters.AddWithValue("@TotalAdvanceAmount", contract.TotalAdvanceAmount);
        }
    }
}