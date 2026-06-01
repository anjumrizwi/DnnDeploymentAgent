using System.Diagnostics;

namespace DnnDeploymentAgent.Agents;

public class IisAgent
{
    public async Task<string> CreateSite()
    {
        string command =
            """
            Import-Module WebAdministration;
            New-WebAppPool -Name DNNPool
            """;

        Process.Start(
            "powershell",
            command);

        return "IIS Configured";
    }
}