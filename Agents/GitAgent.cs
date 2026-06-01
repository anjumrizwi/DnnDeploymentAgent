using System.Diagnostics;

namespace DnnDeploymentAgent.Agents;

public class GitAgent
{
    public async Task<string> CloneRepository()
    {
        string repo =
            "https://github.com/dnnsoftware/Dnn.Platform.git";

        string target =
            @"C:\Deployments\DNN";

        Process process = new();

        process.StartInfo.FileName = "git";

        process.StartInfo.Arguments =
            $"clone {repo} {target}";

        process.Start();

        await process.WaitForExitAsync();

        return "Repository cloned";
    }

    public string GetStatus()
    {
        return "Git Agent Ready";
    }
}