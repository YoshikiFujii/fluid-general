using fluid_general.Models;
using fluid_general.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace fluid_general.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class RosterConfigsController : ControllerBase
    {
        private readonly IDataService _dataService;

        public RosterConfigsController(IDataService dataService)
        {
            _dataService = dataService;
        }

        [HttpGet("{rosterName}")]
        public async Task<IActionResult> GetConfig(string rosterName)
        {
            var config = await _dataService.GetRosterConfigAsync(rosterName);
            if (config == null) return NotFound();
            return Ok(config);
        }

        [HttpPost]
        public async Task<IActionResult> SaveConfig([FromBody] RosterConfig config)
        {
            await _dataService.UpdateRosterConfigAsync(config);
            return Ok();
        }
    }
}
