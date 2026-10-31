using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using PersonnelApp.Models;

namespace PersonnelApp.Data
{
    public class ContractorContractRepository
    {
        private readonly string _connectionString;

        public ContractorContractRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<ContractorContract> GetAll(string searchText = "")
        {
            var list = new List<ContractorContract>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string query = @"
                SELECT c.Id, c.PartyId, c.ContractNumber, c.WorkshopCode, c.ContractRow, c.Subject, c.Amount, c.ContractDate, c.EndDate, c.CreatedAt,
                       p.PartyType, p.FirstName, p.LastName, p.NationalCode, p.CompanyName, p.NationalId,
                       c.GuaranteePercentage, c.RetentionPercentage, c.InsurancePercentage, c.AdvancePaymentPercentage, c.TotalAdvanceAmount,
                       c.VatPercentage, c.ClientContractId, cc.ContractNumber AS ClientContractNumber
                FROM ContractorContracts c
                INNER JOIN Parties p ON c.PartyId = p.Id
                LEFT JOIN ClientContracts cc ON c.ClientContractId = cc.Id
                WHERE 1=1";

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                query += " AND (c.ContractNumber LIKE @search OR c.Subject LIKE @search OR p.FirstName LIKE @search OR p.LastName LIKE @search OR p.CompanyName LIKE @search)";
            }

            query += " ORDER BY c.Id DESC";

            using var cmd = new SqlCommand(query, conn);
            if (!string.IsNullOrWhiteSpace(searchText))
                cmd.Parameters.AddWithValue("@search", $"%{searchText}%");

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(MapReader(reader));
            }
            return list;
        }

        public ContractorContract? GetById(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string query = @"
                SELECT c.Id, c.PartyId, c.ContractNumber, c.WorkshopCode, c.ContractRow, c.Subject, c.Amount, c.ContractDate, c.EndDate, c.CreatedAt,
                       p.PartyType, p.FirstName, p.LastName, p.NationalCode, p.CompanyName, p.NationalId,
                       c.GuaranteePercentage, c.RetentionPercentage, c.InsurancePercentage, c.AdvancePaymentPercentage, c.TotalAdvanceAmount,
                       c.VatPercentage, c.ClientContractId, cc.ContractNumber AS ClientContractNumber
                FROM ContractorContracts c
                INNER JOIN Parties p ON c.PartyId = p.Id
                LEFT JOIN ClientContracts cc ON c.ClientContractId = cc.Id
                WHERE c.Id = @Id";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return MapReader(reader);
            }
            return null;
        }

        public List<ContractorContract> GetByClientContractId(int clientContractId)
        {
            var list = new List<ContractorContract>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            string query = @"
                SELECT c.Id, c.PartyId, c.ContractNumber, c.WorkshopCode, c.ContractRow, c.Subject, c.Amount, c.ContractDate, c.EndDate, c.CreatedAt,
                       p.PartyType, p.FirstName, p.LastName, p.NationalCode, p.CompanyName, p.NationalId,
                       c.GuaranteePercentage, c.RetentionPercentage, c.InsurancePercentage, c.AdvancePaymentPercentage, c.TotalAdvanceAmount,
                       c.VatPercentage, c.ClientContractId, cc.ContractNumber AS ClientContractNumber
                FROM ContractorContracts c
                INNER JOIN Parties p ON c.PartyId = p.Id
                LEFT JOIN ClientContracts cc ON c.ClientContractId = cc.Id
                WHERE c.ClientContractId = @cid
                ORDER BY c.Id DESC";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@cid", clientContractId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(MapReader(reader));
            }
            return list;
        }

        // ✅ FIX P1: SCOPE_IDENTITY برای دریافت Id واقعی
        public void Add(ContractorContract c)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(@"
                INSERT INTO ContractorContracts (PartyId, ContractNumber, WorkshopCode, ContractRow, Subject, Amount, ContractDate, EndDate, CreatedAt, 
                                                 GuaranteePercentage, RetentionPercentage, InsurancePercentage, AdvancePaymentPercentage, TotalAdvanceAmount, VatPercentage, ClientContractId)
                VALUES (@pid, @num, @wc, @cr, @sub, @amt, @cd, @ed, @created, 
                        @guarantee, @retention, @insurance, @advance, @totalAdvance, @vat, @clientId);
                SELECT SCOPE_IDENTITY();", conn);

            cmd.Parameters.AddWithValue("@pid", c.PartyId);
            cmd.Parameters.AddWithValue("@num", c.ContractNumber ?? "");
            cmd.Parameters.AddWithValue("@wc", c.WorkshopCode ?? "");
            cmd.Parameters.AddWithValue("@cr", c.ContractRow ?? "");
            cmd.Parameters.AddWithValue("@sub", c.Subject ?? "");
            cmd.Parameters.AddWithValue("@amt", c.Amount);

            cmd.Parameters.AddWithValue("@cd", (object?)c.ContractDate?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ed", (object?)c.EndDate?.Date ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@created", DateTime.Now.Date);
            cmd.Parameters.AddWithValue("@guarantee", c.GuaranteePercentage);
            cmd.Parameters.AddWithValue("@retention", c.RetentionPercentage);
            cmd.Parameters.AddWithValue("@insurance", c.InsurancePercentage);
            cmd.Parameters.AddWithValue("@advance", c.AdvancePaymentPercentage);
            cmd.Parameters.AddWithValue("@totalAdvance", c.TotalAdvanceAmount);
            cmd.Parameters.AddWithValue("@vat", c.VatPercentage);
            cmd.Parameters.AddWithValue("@clientId", (object?)c.ClientContractId ?? DBNull.Value);

            var result = cmd.ExecuteScalar();
            if (result != null && result != DBNull.Value)
                c.Id = Convert.ToInt32(result);
        }

        public void Update(ContractorContract c)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(@"
                UPDATE ContractorContracts SET
                    PartyId = @pid, ContractNumber = @num, WorkshopCode = @wc, ContractRow = @cr,
                    Subject = @sub, Amount = @amt, ContractDate = @cd, EndDate = @ed,
                    GuaranteePercentage = @guarantee, RetentionPercentage = @retention, 
                    InsurancePercentage = @insurance, AdvancePaymentPercentage = @advance, TotalAdvanceAmount = @totalAdvance,
                    VatPercentage = @vat, ClientContractId = @clientId
                WHERE Id = @rowId", conn);

            cmd.Parameters.AddWithValue("@pid", c.PartyId);
            cmd.Parameters.AddWithValue("@num", c.ContractNumber ?? "");
            cmd.Parameters.AddWithValue("@wc", c.WorkshopCode ?? "");
            cmd.Parameters.AddWithValue("@cr", c.ContractRow ?? "");
            cmd.Parameters.AddWithValue("@sub", c.Subject ?? "");
            cmd.Parameters.AddWithValue("@amt", c.Amount);

            cmd.Parameters.AddWithValue("@cd", (object?)c.ContractDate?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ed", (object?)c.EndDate?.Date ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@guarantee", c.GuaranteePercentage);
            cmd.Parameters.AddWithValue("@retention", c.RetentionPercentage);
            cmd.Parameters.AddWithValue("@insurance", c.InsurancePercentage);
            cmd.Parameters.AddWithValue("@advance", c.AdvancePaymentPercentage);
            cmd.Parameters.AddWithValue("@totalAdvance", c.TotalAdvanceAmount);
            cmd.Parameters.AddWithValue("@vat", c.VatPercentage);
            cmd.Parameters.AddWithValue("@clientId", (object?)c.ClientContractId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@rowId", c.Id);

            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using (var check1 = new SqlCommand("SELECT COUNT(1) FROM ContractorStatements WHERE ContractId = @Id", conn))
            {
                check1.Parameters.AddWithValue("@Id", id);
                if (Convert.ToInt32(check1.ExecuteScalar()) > 0)
                    throw new InvalidOperationException("این قرارداد دارای صورت وضعیت ثبت‌شده است و قابل حذف نیست.");
            }

            using (var check2 = new SqlCommand("SELECT COUNT(1) FROM ContractorPayments WHERE ContractId = @Id", conn))
            {
                check2.Parameters.AddWithValue("@Id", id);
                if (Convert.ToInt32(check2.ExecuteScalar()) > 0)
                    throw new InvalidOperationException("این قرارداد دارای سابقه پرداخت است و قابل حذف نیست.");
            }

            using (var check3 = new SqlCommand("SELECT COUNT(1) FROM ContractorGuarantees WHERE ContractId = @Id", conn))
            {
                check3.Parameters.AddWithValue("@Id", id);
                if (Convert.ToInt32(check3.ExecuteScalar()) > 0)
                    throw new InvalidOperationException("این قرارداد دارای ضمانت‌نامه ثبت‌شده است و قابل حذف نیست.");
            }

            using var cmd = new SqlCommand("DELETE FROM ContractorContracts WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private ContractorContract MapReader(SqlDataReader reader)
        {
            return new ContractorContract
            {
                Id = reader.GetInt32(0),
                PartyId = reader.GetInt32(1),
                ContractNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                WorkshopCode = reader.IsDBNull(3) ? "" : reader.GetString(3),
                ContractRow = reader.IsDBNull(4) ? "" : reader.GetString(4),
                Subject = reader.IsDBNull(5) ? "" : reader.GetString(5),
                Amount = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader[6]),

                ContractDate = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                EndDate = reader.IsDBNull(8) ? null : reader.GetDateTime(8),

                CreatedAt = reader.IsDBNull(9) ? DateTime.Now : Convert.ToDateTime(reader[9]),

                PartyType = reader.IsDBNull(10) ? "" : reader.GetString(10),
                FirstName = reader.IsDBNull(11) ? "" : reader.GetString(11),
                LastName = reader.IsDBNull(12) ? "" : reader.GetString(12),
                NationalCode = reader.IsDBNull(13) ? "" : reader.GetString(13),
                CompanyName = reader.IsDBNull(14) ? "" : reader.GetString(14),
                NationalId = reader.IsDBNull(15) ? "" : reader.GetString(15),

                GuaranteePercentage = reader.IsDBNull(16) ? 0 : Convert.ToDecimal(reader[16]),
                RetentionPercentage = reader.IsDBNull(17) ? 0 : Convert.ToDecimal(reader[17]),
                InsurancePercentage = reader.IsDBNull(18) ? 0 : Convert.ToDecimal(reader[18]),
                AdvancePaymentPercentage = reader.IsDBNull(19) ? 0 : Convert.ToDecimal(reader[19]),
                TotalAdvanceAmount = reader.IsDBNull(20) ? 0 : Convert.ToDecimal(reader[20]),
                VatPercentage = reader.IsDBNull(21) ? 0 : Convert.ToDecimal(reader[21]),

                ClientContractId = reader.IsDBNull(22) ? (int?)null : Convert.ToInt32(reader[22]),
                ClientContractNumber = reader.IsDBNull(23) ? "" : reader.GetString(23)
            };
        }
    }
}