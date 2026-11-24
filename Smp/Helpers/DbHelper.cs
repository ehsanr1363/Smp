using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Smp.Helpers;

/// <summary>
/// کمکی‌های ساده‌ی ADO.NET روی SQL Server. برای فرم‌ها و کدهای رابط کاربری
/// نسخه‌های Async را ترجیح دهید تا نخ UI قفل نشود.
/// </summary>
public static class DbHelper
{
    // ───────────────────────── همگام (Sync) ─────────────────────────

    public static DataTable ExecuteQuery(string connectionString, string query, params SqlParameter[] parameters)
    {
        var dt = new DataTable();
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(query, conn);
        if (parameters != null) cmd.Parameters.AddRange(parameters);
        using var reader = cmd.ExecuteReader();
        dt.Load(reader);
        return dt;
    }

    public static int ExecuteNonQuery(string connectionString, string query, params SqlParameter[] parameters)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(query, conn);
        if (parameters != null) cmd.Parameters.AddRange(parameters);
        return cmd.ExecuteNonQuery();
    }

    public static object? ExecuteScalar(string connectionString, string query, params SqlParameter[] parameters)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(query, conn);
        if (parameters != null) cmd.Parameters.AddRange(parameters);
        return cmd.ExecuteScalar();
    }

    // ───────────────────────── ناهمگام (Async) ─────────────────────────

    public static Task<DataTable> ExecuteQueryAsync(string connectionString, string query, params SqlParameter[] parameters)
        => ExecuteQueryAsync(connectionString, query, CancellationToken.None, parameters);

    public static async Task<DataTable> ExecuteQueryAsync(string connectionString, string query, CancellationToken ct, params SqlParameter[] parameters)
    {
        var dt = new DataTable();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new SqlCommand(query, conn);
        if (parameters != null) cmd.Parameters.AddRange(parameters);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        dt.Load(reader);
        return dt;
    }

    public static Task<int> ExecuteNonQueryAsync(string connectionString, string query, params SqlParameter[] parameters)
        => ExecuteNonQueryAsync(connectionString, query, CancellationToken.None, parameters);

    public static async Task<int> ExecuteNonQueryAsync(string connectionString, string query, CancellationToken ct, params SqlParameter[] parameters)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new SqlCommand(query, conn);
        if (parameters != null) cmd.Parameters.AddRange(parameters);
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public static Task<object?> ExecuteScalarAsync(string connectionString, string query, params SqlParameter[] parameters)
        => ExecuteScalarAsync(connectionString, query, CancellationToken.None, parameters);

    public static async Task<object?> ExecuteScalarAsync(string connectionString, string query, CancellationToken ct, params SqlParameter[] parameters)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new SqlCommand(query, conn);
        if (parameters != null) cmd.Parameters.AddRange(parameters);
        return await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }
}
