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
        await _git.CloneRepository();

        await _sql.CreateDatabase();

        await _iis.CreateSite();

        return "Deployment Completed";
    }
}