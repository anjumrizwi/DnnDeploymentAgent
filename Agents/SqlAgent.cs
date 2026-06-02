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
        var results = new List<string>();

        Console.WriteLine("Starting: Create database...");
        var r1 = await CreateDatabase();
        Console.WriteLine("CreateDatabase result: " + (string.IsNullOrWhiteSpace(r1) ? "(no output)" : r1));
        results.Add(r1);

        Console.WriteLine("Starting: Configure database...");
        var r2 = await ConfigureDatabase();
        Console.WriteLine("ConfigureDatabase result: " + (string.IsNullOrWhiteSpace(r2) ? "(no output)" : r2));
        results.Add(r2);

        Console.WriteLine("Starting: Create login...");
        var r3 = await CreateLogin();
        Console.WriteLine("CreateLogin result: " + (string.IsNullOrWhiteSpace(r3) ? "(no output)" : r3));
        results.Add(r3);

        Console.WriteLine("Starting: Create database user...");
        var r4 = await CreateDatabaseUser();
        Console.WriteLine("CreateDatabaseUser result: " + (string.IsNullOrWhiteSpace(r4) ? "(no output)" : r4));
        results.Add(r4);

        Console.WriteLine("Starting: Grant permissions...");
        var r5 = await GrantPermissions();
        Console.WriteLine("GrantPermissions result: " + (string.IsNullOrWhiteSpace(r5) ? "(no output)" : r5));
        results.Add(r5);

        Console.WriteLine("Database setup steps complete.");

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

        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Executing SQL against '{conn.DataSource}' Database='{conn.Database}'");
        Console.WriteLine("SQL:\n" + sql);

        await using var cmd = new SqlCommand(sql, conn);

        if (parameters != null)
        {
            Console.WriteLine("With parameters:");
            foreach (var (name, value) in parameters)
            {
                Console.WriteLine($"  {name} = {value}");
                cmd.Parameters.AddWithValue(name, value);
            }
        }

        try
        {
            var affected = await cmd.ExecuteNonQueryAsync();
            Console.WriteLine($"SQL executed successfully, rows affected: {affected}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR executing SQL: {ex.Message}");
            Console.Error.WriteLine(ex.ToString());
            throw;
        }
    }
}