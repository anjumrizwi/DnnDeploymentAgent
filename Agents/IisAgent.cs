using System.Diagnostics;

namespace DnnDeploymentAgent.Agents;

public class IisAgent
{
    private const string SiteName = "DNN";
    private const string PoolName = "DNNPool";
    private const string SitePath = @"C:\inetpub\wwwroot\DNN";
    private const int SitePort = 80;

    public async Task<string> SetupForDnn()
    {
        var results = new List<string>();

        Console.WriteLine("Starting: Enable Windows features...");
        var res1 = await EnableWindowsFeatures();
        Console.WriteLine("EnableWindowsFeatures result: " + (string.IsNullOrWhiteSpace(res1) ? "(no output)" : res1));
        results.Add(res1);

        Console.WriteLine("Starting: Create application pool...");
        var res2 = await CreateAppPool();
        Console.WriteLine("CreateAppPool result: " + (string.IsNullOrWhiteSpace(res2) ? "(no output)" : res2));
        results.Add(res2);

        Console.WriteLine("Starting: Create website...");
        var res3 = await CreateWebsite();
        Console.WriteLine("CreateWebsite result: " + (string.IsNullOrWhiteSpace(res3) ? "(no output)" : res3));
        results.Add(res3);

        Console.WriteLine("Starting: Set folder permissions...");
        var res4 = await SetFolderPermissions();
        Console.WriteLine("SetFolderPermissions result: " + (string.IsNullOrWhiteSpace(res4) ? "(no output)" : res4));
        results.Add(res4);

        Console.WriteLine("All setup steps complete.");

        return string.Join(Environment.NewLine, results);
    }

    private Task<string> EnableWindowsFeatures()
    {
        // Plain raw literal — no interpolation needed, no PS tokens to escape.
        const string script = """
            $features = @(
                'IIS-WebServerRole',
                'IIS-WebServer',
                'IIS-CommonHttpFeatures',
                'IIS-StaticContent',
                'IIS-DefaultDocument',
                'IIS-DirectoryBrowsing',
                'IIS-HttpErrors',
                'IIS-ApplicationDevelopment',
                'IIS-ASPNET45',
                'IIS-NetFxExtensibility45',
                'IIS-ISAPIExtensions',
                'IIS-ISAPIFilter',
                'IIS-HttpLogging',
                'IIS-RequestFiltering',
                'IIS-HttpCompressionStatic',
                'IIS-ManagementConsole',
                'NetFx4-AdvSrvs',
                'NetFx4Extended-ASPNET45'
            )
            foreach ($f in $features) {
                Enable-WindowsOptionalFeature -Online -FeatureName $f -All -NoRestart | Out-Null
            }
            Write-Output 'Windows features enabled'
            """;

        return RunPowerShell(script);
    }

    private Task<string> CreateAppPool()
    {
        // Inject the C# constant as a PS variable at the top of the script
        // so the rest of the script uses no C# interpolation at all.
        string script = $$"""
            Import-Module WebAdministration
            $poolName = '{{PoolName}}'

            if (Test-Path "IIS:\AppPools\$poolName") {
                Write-Output 'App pool already exists - skipping'
            } else {
                New-WebAppPool -Name $poolName
                Set-ItemProperty "IIS:\AppPools\$poolName" managedRuntimeVersion 'v4.0'
                Set-ItemProperty "IIS:\AppPools\$poolName" managedPipelineMode  'Integrated'
                Set-ItemProperty "IIS:\AppPools\$poolName" enable32BitAppOnWin64 $false
                Clear-ItemProperty "IIS:\AppPools\$poolName" recycling.periodicRestart.time
                $pool = Get-Item "IIS:\AppPools\$poolName"
                $pool.recycling.periodicRestart.schedule.Add('03:00:00') | Out-Null
                $pool | Set-Item
                Write-Output 'App pool created'
            }
            """;

        return RunPowerShell(script);
    }

    private Task<string> CreateWebsite()
    {
        // $$""" gives us {{ }} for literal braces; C# constants injected once at the top.
        string script = $$"""
            Import-Module WebAdministration
            $siteName = '{{SiteName}}'
            $sitePath = '{{SitePath}}'
            $poolName = '{{PoolName}}'
            $sitePort = {{SitePort}}

            if (-not (Test-Path $sitePath)) {
                New-Item -ItemType Directory -Path $sitePath | Out-Null
            }

            if (Test-Path "IIS:\Sites\$siteName") {
                Write-Output 'Website already exists - skipping'
            } else {
                New-Website -Name $siteName `
                            -PhysicalPath $sitePath `
                            -ApplicationPool $poolName `
                            -Port $sitePort `
                            -Force
                Write-Output 'Website created'
            }
            """;

        return RunPowerShell(script);
    }

    private Task<string> SetFolderPermissions()
    {
        string script = $$"""
            $path = '{{SitePath}}'
            $accounts = @('IIS_IUSRS', 'IUSR')

            foreach ($account in $accounts) {
                $acl  = Get-Acl $path
                $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
                            $account, 'Modify',
                            'ContainerInherit,ObjectInherit', 'None', 'Allow')
                $acl.SetAccessRule($rule)
                Set-Acl -Path $path -AclObject $acl
            }

            Write-Output 'Folder permissions applied'
            """;

        return RunPowerShell(script);
    }

    private static Task<string> RunPowerShell(string script)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // Feed the script via stdin — avoids ALL quoting/escaping on the command line.
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add("-"); // read from stdin

        using var process = new Process { StartInfo = psi };
        var output = new System.Text.StringBuilder();
        var errors = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) errors.AppendLine(e.Data); };

        Console.WriteLine("Executing PowerShell script:");
        Console.WriteLine(script);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.StandardInput.WriteLine(script);
        process.StandardInput.Close();

        process.WaitForExit();

        Console.WriteLine("PowerShell exited with code: " + process.ExitCode);
        if (output.Length > 0)
        {
            Console.WriteLine("PowerShell output:\n" + output.ToString().Trim());
        }

        if (errors.Length > 0)
        {
            Console.Error.WriteLine("PowerShell errors:\n" + errors.ToString().Trim());
        }

        return Task.FromResult(
            errors.Length > 0
                ? $"ERROR: {errors}"
                : output.ToString().Trim());
    }
}