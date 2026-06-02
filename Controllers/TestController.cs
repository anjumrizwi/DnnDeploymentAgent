using DnnDeploymentAgent.Agents;
using Microsoft.AspNetCore.Mvc;

namespace DnnDeploymentAgent.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly GitAgent _gitAgent;

        public TestController(
            GitAgent gitAgent)
        {
            _gitAgent = gitAgent;
        }

        //[HttpGet]
        //public async Task<string> Test()
        //{
        //    return await _gitAgent.CloneRepository();
        //}

        [HttpGet]
        public IActionResult Get()
        {
            Console.WriteLine($"[{DateTime.UtcNow:O}] API: /api/test GET called");
            return Ok("DNN Deployment Agent Running");
        }

        [HttpGet("status")]
        public IActionResult Status()
        {
            Console.WriteLine($"[{DateTime.UtcNow:O}] API: /api/test/status called");
            var status = _gitAgent.GetStatus();
            Console.WriteLine($"GitAgent status: {status}");
            return Ok(status);
        }
    }
}
