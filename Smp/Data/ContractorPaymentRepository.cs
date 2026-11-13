using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using SMP.Models;

namespace SMP.Data
{
    public class ContractorPaymentRepository
    {
        private readonly string _connectionString;

        public ContractorPaymentRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void Add(ContractorPayment p)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand(@"
                INSERT INTO ContractorPayments (ContractId, PaymentDate, Amount, PaymentType, Description, CreatedAt)
                VALUES (@cid, @date, @amount, @type, @desc, @created)", conn);

            cmd.Parameters.AddWithValue("@cid", p.ContractId);
            cmd.Parameters.AddWithValue("@date", p.PaymentDate);
            cmd.Parameters.AddWithValue("@amount", p.Amount);
            cmd.Parameters.AddWithValue("@type", p.PaymentType ?? "");
            cmd.Parameters.AddWithValue("@desc", p.Description ?? "");
            cmd.Parameters.AddWithValue("@created", DateTime.Now);

            cmd.ExecuteNonQuery();
        }

        public List<ContractorPayment> GetByContractId(int contractId)
        {
            var list = new List<ContractorPayment>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand("SELECT * FROM ContractorPayments WHERE ContractId = @cid ORDER BY PaymentDate DESC", conn);
            cmd.Parameters.AddWithValue("@cid", contractId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ContractorPayment
                {
                    Id = reader.GetInt32(0),
                    ContractId = reader.GetInt32(1),
                    PaymentDate = Convert.ToDateTime(reader[2]),
                    Amount = Convert.ToDecimal(reader[3]),
                    PaymentType = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Description = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    CreatedAt = Convert.ToDateTime(reader[6])
                });
            }
            return list;
        }
    }
}