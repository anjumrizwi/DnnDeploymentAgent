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

        Console.WriteLine($"Starting git clone: {repo} -> {target}");
        Console.WriteLine($"Running: git {process.StartInfo.Arguments}");

        process.Start();

        await process.WaitForExitAsync();

        Console.WriteLine($"Git process exited with code: {process.ExitCode}");

        if (process.ExitCode != 0)
        {
            Console.Error.WriteLine("Git clone failed");
            return "ERROR: Git clone failed";
        }

        return "Repository cloned";
    }

    public string GetStatus()
    {
        var status = "Git Agent Ready";
        Console.WriteLine($"Status: {status}");
        return status;
    }
}