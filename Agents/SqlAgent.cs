using Microsoft.Data.SqlClient;

namespace DnnDeploymentAgent.Agents;

public class SqlAgent
{
    public async Task<string> CreateDatabase()
    {
        string connString =
            "Server=localhost;" +
            "Integrated Security=True;" +
            "TrustServerCertificate=True";

        await using var conn =
            new SqlConnection(connString);

        await conn.OpenAsync();

        string sql =
            """
            IF DB_ID('DNNDB') IS NULL
            CREATE DATABASE DNNDB
            """;

        await using var cmd =
            new SqlCommand(sql, conn);

        await cmd.ExecuteNonQueryAsync();

        return "Database Created";
    }
}