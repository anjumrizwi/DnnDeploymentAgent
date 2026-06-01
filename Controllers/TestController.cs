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
            return Ok("DNN Deployment Agent Running");
        }

        [HttpGet("status")]
        public IActionResult Status()
        {
            return Ok(_gitAgent.GetStatus());
        }
    }
}
