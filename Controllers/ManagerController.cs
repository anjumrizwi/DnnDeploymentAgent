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
        var result =
            await _managerAgent.Deploy();

        return Ok(result);
    }
}