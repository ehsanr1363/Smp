using Microsoft.Data.SqlClient; // 👈 تغییر از Sqlite به SqlClient
using System.Data;

namespace PersonnelApp.Helpers
{
    public static class DbHelper
    {
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
    }
}