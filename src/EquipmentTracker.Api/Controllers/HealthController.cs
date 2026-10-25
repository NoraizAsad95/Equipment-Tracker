using EquipmentTracker.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(new { status = "ok", database = await db.Database.CanConnectAsync(ct) });
}
