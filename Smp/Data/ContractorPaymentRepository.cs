using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Smp.Models;

namespace Smp.Data;

/// <summary>
/// پرداخت‌های «قدیمی» پیمانکار (جدول ContractorPayments).
/// پرداخت‌های جدید از بخش نقدینگی (CashTransactions / CashChecks) ثبت می‌شوند؛
/// هر سه منبع در مانده‌ی قرارداد جمع می‌شوند (ContractSummaryRepository).
/// </summary>
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
                INSERT INTO ContractorPayments (ContractId, ClientContractId, PaymentDate, Amount, PaymentType, CostType, Description, CreatedAt)
                VALUES (@cid, @clientId, @date, @amount, @type, @costType, @desc, @created);
                SELECT SCOPE_IDENTITY();", conn);

        AddParameters(cmd, p);
        cmd.Parameters.AddWithValue("@created", DateTime.Now);

        var result = cmd.ExecuteScalar();
        if (result is not null && result != DBNull.Value)
            p.Id = Convert.ToInt32(result);
        Smp.Helpers.AuditHelper.Log("ثبت", "پرداخت پیمانکار", $"شناسه {p.Id}، مبلغ {p.Amount:N0}");
    }

    // ✅ قبلاً امکان اصلاح پرداخت اشتباه وجود نداشت
    public void Update(ContractorPayment p)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        using var cmd = new SqlCommand(@"
                UPDATE ContractorPayments SET
                    ClientContractId = @clientId, PaymentDate = @date, Amount = @amount,
                    PaymentType = @type, CostType = @costType, Description = @desc
                WHERE Id = @id", conn);

        AddParameters(cmd, p);
        cmd.Parameters.AddWithValue("@id", p.Id);
        cmd.ExecuteNonQuery();
        Smp.Helpers.AuditHelper.Log("ویرایش", "پرداخت پیمانکار", $"شناسه {p.Id}، مبلغ {p.Amount:N0}");
    }

    public void Delete(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("DELETE FROM ContractorPayments WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
        Smp.Helpers.AuditHelper.Log("حذف", "پرداخت پیمانکار", $"شناسه {id}");
    }

    public ContractorPayment? GetById(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        using var cmd = new SqlCommand("SELECT * FROM ContractorPayments WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public List<ContractorPayment> GetByContractId(int contractId)
    {
        var list = new List<ContractorPayment>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        using var cmd = new SqlCommand("SELECT * FROM ContractorPayments WHERE ContractId = @cid ORDER BY PaymentDate DESC, Id DESC", conn);
        cmd.Parameters.AddWithValue("@cid", contractId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(Map(reader));
        return list;
    }

    private static void AddParameters(SqlCommand cmd, ContractorPayment p)
    {
        cmd.Parameters.AddWithValue("@cid", p.ContractId);
        cmd.Parameters.AddWithValue("@clientId", (object?)p.ClientContractId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@date", p.PaymentDate);
        cmd.Parameters.AddWithValue("@amount", p.Amount);
        cmd.Parameters.AddWithValue("@type", p.PaymentType ?? "");
        cmd.Parameters.AddWithValue("@costType", (object?)p.CostType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@desc", p.Description ?? "");
    }

    // ✅ خواندن بر اساس نام ستون (نه ترتیب ستون‌ها) تا با تغییر ساختار جدول نشکند
    private static ContractorPayment Map(SqlDataReader r)
    {
        return new ContractorPayment
        {
            Id = Convert.ToInt32(r["Id"]),
            ContractId = Convert.ToInt32(r["ContractId"]),
            ClientContractId = HasColumn(r, "ClientContractId") && r["ClientContractId"] != DBNull.Value ? Convert.ToInt32(r["ClientContractId"]) : null,
            PaymentDate = Convert.ToDateTime(r["PaymentDate"]),
            Amount = Convert.ToDecimal(r["Amount"]),
            PaymentType = r["PaymentType"] == DBNull.Value ? "" : r["PaymentType"].ToString() ?? "",
            CostType = HasColumn(r, "CostType") && r["CostType"] != DBNull.Value ? r["CostType"].ToString() : null,
            Description = r["Description"] == DBNull.Value ? "" : r["Description"].ToString() ?? "",
            CreatedAt = r["CreatedAt"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(r["CreatedAt"])
        };
    }

    private static bool HasColumn(SqlDataReader r, string name)
    {
        for (int i = 0; i < r.FieldCount; i++)
            if (string.Equals(r.GetName(i), name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
