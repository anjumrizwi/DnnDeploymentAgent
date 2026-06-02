using DnnDeploymentAgent.Agents;
using Microsoft.AspNetCore.Mvc;

namespace DnnDeploymentAgent.Controllers;

[ApiController]
[Route("api/manager")]
public class ManagerController : ControllerBase
{
    private readonly ManagerAgent _managerAgent;

    public ManagerController(
        ManagerAgent managerAgent)
    {
        _managerAgent = managerAgent;
    }

    [HttpGet("run")]
    public async Task<IActionResult> Run()
    {
        Console.WriteLine($"[{DateTime.UtcNow:O}] API: /api/manager/run called");
        var result = await _managerAgent.Deploy();
        Console.WriteLine($"[{DateTime.UtcNow:O}] API result length: {result?.Length ?? 0}");
        return Ok(result);
    }
}