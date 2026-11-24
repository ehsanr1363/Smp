using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Smp.Models;

namespace Smp.Data;

public class ContractSummaryRepository
{
    private readonly string _connectionString;

    public ContractSummaryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public List<ContractSummary> GetAllSummaries(string searchText = "")
    {
        var list = new List<ContractSummary>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        // ✅ اضافه شدن فیلتر Status برای چک‌ها
        string query = @"
                SELECT 
                    c.Id AS ContractId,
                    c.ContractNumber,
                    CASE WHEN p.PartyType = N'حقیقی' THEN p.FirstName + N' ' + p.LastName ELSE p.CompanyName END AS ContractorName,
                    c.Amount AS TotalAmount,
                    ISNULL((SELECT SUM(GrossAmount) FROM ContractorStatements WHERE ContractId = c.Id), 0) AS TotalWorkDone,
                    ISNULL((SELECT SUM(NetPayableAmount) FROM ContractorStatements WHERE ContractId = c.Id), 0) AS TotalNetPayable,
                    (
                        ISNULL((SELECT SUM(Amount) FROM ContractorPayments WHERE ContractId = c.Id), 0) +
                        ISNULL((SELECT SUM(Amount) FROM CashTransactions WHERE ContractorContractId = c.Id AND LTRIM(RTRIM(Direction)) = N'پرداختی'), 0) +
                        ISNULL((SELECT SUM(Amount) FROM CashChecks WHERE ContractorContractId = c.Id AND CheckType = N'پرداختی' AND Status NOT IN (N'باطل شده', N'برگشتی')), 0)
                    ) AS TotalPaid
                FROM ContractorContracts c
                INNER JOIN Parties p ON c.PartyId = p.Id
                WHERE 1=1";

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query += " AND (c.ContractNumber LIKE @search OR p.FirstName LIKE @search OR p.LastName LIKE @search OR p.CompanyName LIKE @search)";
        }

        query += " ORDER BY c.Id DESC";

        using var cmd = new SqlCommand(query, conn);
        if (!string.IsNullOrWhiteSpace(searchText))
            cmd.Parameters.AddWithValue("@search", $"%{searchText}%");

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new ContractSummary
            {
                ContractId = reader.GetInt32(0),
                ContractNumber = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ContractorName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                TotalAmount = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader[3]),
                TotalWorkDone = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader[4]),
                TotalNetPayable = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader[5]),
                TotalPaid = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader[6])
            });
        }
        return list;
    }
}
