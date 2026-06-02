using Microsoft.Data.SqlClient;

namespace DnnDeploymentAgent.Agents;

public class SqlAgent
{
    private const string DatabaseName = "DNNDB";
    private const string LoginName = "DNNUser";
    private const string LoginPassword = "Change_Me_Strong#2024!";

    private static readonly string MasterConnString =
        "Server=localhost;" +
        "Integrated Security=True;" +
        "TrustServerCertificate=True";

    private static string DatabaseConnString =>
        MasterConnString + $";Database={DatabaseName}";

    // ── Entry point ──────────────────────────────────────────────────────────

    public async Task<string> SetupForDnn()
    {
        var results = new List<string>
        {
            await CreateDatabase(),
            await ConfigureDatabase(),
            await CreateLogin(),
            await CreateDatabaseUser(),
            await GrantPermissions()
        };
        return string.Join(Environment.NewLine, results);
    }

    // ── Step 1: Create database ──────────────────────────────────────────────

    public async Task<string> CreateDatabase()
    {
        // CREATE DATABASE cannot run inside a transaction,
        // so we set AutoCommit by leaving TransactionScope out.
        const string sql = """
            IF DB_ID(N'DNNDB') IS NULL
                CREATE DATABASE DNNDB;
            """;

        await ExecuteAsync(MasterConnString, sql);
        return $"Database '{DatabaseName}' ready";
    }

    // ── Step 2: Configure DB options DNN requires ────────────────────────────
    //  • RECOVERY SIMPLE  – fine for dev/single-server; change to FULL for prod
    //  • compatibility level 130+ (SQL 2016) keeps JSON/STRING_SPLIT available
    //  • ALLOW_SNAPSHOT_ISOLATION ON – reduces deadlocks under DNN's read pattern

    private async Task<string> ConfigureDatabase()
    {
        // Dynamic SQL for the db name is safe here: it is our own constant,
        // not user input. ALTER DATABASE does not accept parameters.
        string sql = $"""
            USE master;

            IF DATABASEPROPERTYEX(N'{DatabaseName}', 'Recovery') <> 'SIMPLE'
                ALTER DATABASE [{DatabaseName}] SET RECOVERY SIMPLE;

            IF DATABASEPROPERTYEX(N'{DatabaseName}', 'IsAutoShrink') = 1
                ALTER DATABASE [{DatabaseName}] SET AUTO_SHRINK OFF;

            IF DATABASEPROPERTYEX(N'{DatabaseName}', 'CompatibilityLevel') < 130
                ALTER DATABASE [{DatabaseName}] SET COMPATIBILITY_LEVEL = 130;

            IF DATABASEPROPERTYEX(N'{DatabaseName}', 'IsSnapshotIsolationAllowed') = 0
                ALTER DATABASE [{DatabaseName}] SET ALLOW_SNAPSHOT_ISOLATION ON;
            """;

        await ExecuteAsync(MasterConnString, sql);
        return $"Database '{DatabaseName}' configured";
    }

    // ── Step 3: Create SQL Server login ─────────────────────────────────────

    private async Task<string> CreateLogin()
    {
        // MUST_CHANGE forces a password reset on first connect —
        // remove it if the account is used only by the app pool.
        string sql = $"""
            IF NOT EXISTS (
                SELECT 1 FROM sys.server_principals
                WHERE name = N'{LoginName}')
            BEGIN
                CREATE LOGIN [{LoginName}]
                    WITH PASSWORD   = N'{LoginPassword}',
                         CHECK_POLICY = ON,
                         CHECK_EXPIRATION = OFF;
            END
            """;

        await ExecuteAsync(MasterConnString, sql);
        return $"Login '{LoginName}' ready";
    }

    // ── Step 4: Create database user mapped to the login ────────────────────

    private async Task<string> CreateDatabaseUser()
    {
        string sql = $"""
            USE [{DatabaseName}];

            IF NOT EXISTS (
                SELECT 1 FROM sys.database_principals
                WHERE name = N'{LoginName}')
            BEGIN
                CREATE USER [{LoginName}] FOR LOGIN [{LoginName}];
            END
            """;

        await ExecuteAsync(MasterConnString, sql);
        return $"Database user '{LoginName}' ready";
    }

    // ── Step 5: Grant permissions ────────────────────────────────────────────
    //  DNN's installer needs db_owner to create tables/procs/indexes.
    //  After installation, db_owner can be replaced with a custom role
    //  granting only SELECT, INSERT, UPDATE, DELETE, EXECUTE.

    private async Task<string> GrantPermissions()
    {
        string sql = $"""
            USE [{DatabaseName}];

            IF IS_ROLEMEMBER('db_owner', N'{LoginName}') = 0
                ALTER ROLE db_owner ADD MEMBER [{LoginName}];
            """;

        await ExecuteAsync(MasterConnString, sql);
        return $"Permissions granted to '{LoginName}'";
    }

    // ── Shared helper ────────────────────────────────────────────────────────

    private static async Task ExecuteAsync(
        string connString,
        string sql,
        IReadOnlyDictionary<string, object>? parameters = null)
    {
        await using var conn = new SqlConnection(connString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(sql, conn);

        if (parameters != null)
            foreach (var (name, value) in parameters)
                cmd.Parameters.AddWithValue(name, value);

        await cmd.ExecuteNonQueryAsync();
    }
}