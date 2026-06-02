namespace DnnDeploymentAgent.Agents;

public class ManagerAgent
{
    private readonly GitAgent _git;
    private readonly SqlAgent _sql;
    private readonly IisAgent _iis;

    public ManagerAgent(
        GitAgent git,
        SqlAgent sql,
        IisAgent iis)
    {
        _git = git;
        _sql = sql;
        _iis = iis;
    }

    public async Task<string> Deploy()
    {
        var steps = new (string Name, Func<Task<string>> Action)[]
        {
            ("Clone repository",  _git.CloneRepository),
            ("Setup database",    _sql.SetupForDnn),
            ("Setup IIS",         _iis.SetupForDnn),
        };

        var results = new List<string>();

        foreach (var (name, action) in steps)
        {
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Starting: {name}");

            try
            {
                string result = await action();
                results.Add($"[OK] {name}: {result}");
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Completed: {name}");
            }
            catch (Exception ex)
            {
                string failure = $"[FAIL] {name}: {ex.Message}";
                results.Add(failure);
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Failed: {name}");

                // Abort — later steps depend on earlier ones:
                // IIS needs the DB, the DB needs the repo config, etc.
                results.Add("Deployment aborted — subsequent steps skipped.");
                break;
            }
        }

        return string.Join(Environment.NewLine, results);
    }
}