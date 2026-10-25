using EquipmentTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Api.Controllers;

[ApiController]
[Route("api/status")]
public class StatusController(StatusService status) : ControllerBase
{
    // The current screen. Open to everyone, no PIN (FR-24).
    [HttpGet]
    public async Task<List<StatusItem>> Get([FromQuery] string? search) => await status.GetAsync(search);
}
