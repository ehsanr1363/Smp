using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Smp.Models;

namespace Smp.Data;

public class ProjectExpenseRepository
{
    private readonly string _connectionString;

    public ProjectExpenseRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private SqlConnection GetOpenConnection()
    {
        var conn = new SqlConnection(_connectionString);
        try
        {
            conn.Open();
            return conn;
        }
        catch (Exception ex)
        {
            conn.Dispose();
            throw new Exception("خطا در اتصال به دیتابیس برای مدیریت هزینه‌های فاکتوری.\n\n" + ex.Message, ex);
        }
    }

    public List<ProjectExpense> GetByClientContractId(int clientContractId)
    {
        var list = new List<ProjectExpense>();
        using var conn = GetOpenConnection();

        string query = @"
                SELECT e.Id, e.ClientContractId, e.PartyId, e.InvoiceNumber, e.InvoiceDate, e.Amount, e.Description, e.CreatedAt, e.CostType, e.ClientStatementId,
                       COALESCE(
                           CASE WHEN p.PartyType = N'حقیقی' THEN p.FirstName + N' ' + p.LastName ELSE p.CompanyName END,
                           t.Name
                       ) AS PartyName
                FROM ProjectExpenses e
                LEFT JOIN Parties p ON e.PartyId = p.Id
                LEFT JOIN Tafsilis t ON e.PartyId = t.Id AND p.Id IS NULL
                WHERE e.ClientContractId = @cid
                ORDER BY e.InvoiceDate DESC";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@cid", clientContractId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new ProjectExpense
            {
                Id = reader.GetInt32(0),
                ClientContractId = reader.GetInt32(1),
                PartyId = reader.IsDBNull(2) ? (int?)null : Convert.ToInt32(reader[2]),
                InvoiceNumber = reader.IsDBNull(3) ? "" : reader.GetString(3),
                InvoiceDate = Convert.ToDateTime(reader[4]),
                Amount = Convert.ToDecimal(reader[5]),
                Description = reader.IsDBNull(6) ? "" : reader.GetString(6),
                CreatedAt = Convert.ToDateTime(reader[7]),
                CostType = reader.IsDBNull(8) ? null : reader.GetString(8),
                ClientStatementId = reader.IsDBNull(9) ? (int?)null : Convert.ToInt32(reader[9]), // ✅
                PartyName = reader.IsDBNull(10) ? "" : reader.GetString(10)
            });
        }
        return list;
    }

    public ProjectExpense GetById(int id)
    {
        using var conn = GetOpenConnection();

        using var cmd = new SqlCommand(@"
                SELECT e.Id, e.ClientContractId, e.PartyId, e.InvoiceNumber, e.InvoiceDate, e.Amount, e.Description, e.CreatedAt, e.CostType, e.ClientStatementId,
                       COALESCE(
                           CASE WHEN p.PartyType = N'حقیقی' THEN p.FirstName + N' ' + p.LastName ELSE p.CompanyName END,
                           t.Name
                       ) AS PartyName
                FROM ProjectExpenses e
                LEFT JOIN Parties p ON e.PartyId = p.Id
                LEFT JOIN Tafsilis t ON e.PartyId = t.Id AND p.Id IS NULL
                WHERE e.Id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new ProjectExpense
            {
                Id = reader.GetInt32(0),
                ClientContractId = reader.GetInt32(1),
                PartyId = reader.IsDBNull(2) ? (int?)null : Convert.ToInt32(reader[2]),
                InvoiceNumber = reader.IsDBNull(3) ? "" : reader.GetString(3),
                InvoiceDate = Convert.ToDateTime(reader[4]),
                Amount = Convert.ToDecimal(reader[5]),
                Description = reader.IsDBNull(6) ? "" : reader.GetString(6),
                CreatedAt = Convert.ToDateTime(reader[7]),
                CostType = reader.IsDBNull(8) ? null : reader.GetString(8),
                ClientStatementId = reader.IsDBNull(9) ? (int?)null : Convert.ToInt32(reader[9]), // ✅
                PartyName = reader.IsDBNull(10) ? "" : reader.GetString(10)
            };
        }
        return null!;
    }

    public void Add(ProjectExpense e)
    {
        using var conn = GetOpenConnection();

        using var cmd = new SqlCommand(@"
                INSERT INTO ProjectExpenses (ClientContractId, PartyId, InvoiceNumber, InvoiceDate, Amount, Description, CostType, CreatedAt)
                VALUES (@cid, @pid, @num, @date, @amount, @desc, @costType, @created)", conn);

        cmd.Parameters.AddWithValue("@cid", e.ClientContractId);
        cmd.Parameters.AddWithValue("@pid", (object?)e.PartyId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@num", e.InvoiceNumber ?? "");
        cmd.Parameters.AddWithValue("@date", e.InvoiceDate);
        cmd.Parameters.AddWithValue("@amount", e.Amount);
        cmd.Parameters.AddWithValue("@desc", e.Description ?? "");
        cmd.Parameters.AddWithValue("@costType", (object?)e.CostType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created", DateTime.Now);

        cmd.ExecuteNonQuery();
        Smp.Helpers.AuditHelper.Log("ثبت", "هزینه پروژه", $"شناسه {e.Id}، مبلغ {e.Amount:N0}");
    }

    public void Update(ProjectExpense e)
    {
        using var conn = GetOpenConnection();

        using var cmd = new SqlCommand(@"
                UPDATE ProjectExpenses SET
                    PartyId = @pid, InvoiceNumber = @num, InvoiceDate = @date,
                    Amount = @amount, Description = @desc, CostType = @costType
                WHERE Id = @id", conn);

        cmd.Parameters.AddWithValue("@pid", (object?)e.PartyId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@num", e.InvoiceNumber ?? "");
        cmd.Parameters.AddWithValue("@date", e.InvoiceDate);
        cmd.Parameters.AddWithValue("@amount", e.Amount);
        cmd.Parameters.AddWithValue("@desc", e.Description ?? "");
        cmd.Parameters.AddWithValue("@costType", (object?)e.CostType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", e.Id);

        cmd.ExecuteNonQuery();
        Smp.Helpers.AuditHelper.Log("ویرایش", "هزینه پروژه", $"شناسه {e.Id}، مبلغ {e.Amount:N0}");
    }

    public void Delete(int id)
    {
        using var conn = GetOpenConnection();
        using var cmd = new SqlCommand("DELETE FROM ProjectExpenses WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
        Smp.Helpers.AuditHelper.Log("حذف", "هزینه پروژه", $"شناسه {id}");
    }
}
