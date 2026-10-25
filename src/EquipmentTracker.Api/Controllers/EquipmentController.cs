using EquipmentTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Api.Controllers;

[ApiController]
[Route("api/equipment")]
public class EquipmentController(EquipmentService equipment, RestoreService restores) : ControllerBase
{
    // Office or Owner: send your id in X-Person-Id and your PIN in the body (AD-6).
    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id, [FromHeader(Name = "X-Person-Id")] int? actorId, RestoreRequest request)
    {
        await restores.RestoreAsync(id, actorId, request);
        return NoContent();
    }

    [HttpGet]
    public async Task<List<EquipmentDto>> List() => await equipment.ListAsync();

    // Owner only: send your id in X-Person-Id and your PIN in the body (AD-6).
    [HttpPost]
    public async Task<IActionResult> Create([FromHeader(Name = "X-Person-Id")] int? actorId, EquipmentRequest request)
    {
        var created = await equipment.CreateAsync(actorId, request);
        return Created($"/api/equipment/{created.Id}", created);
    }

    [HttpPut("{id:int}")]
    public async Task<EquipmentDto> Update(int id, [FromHeader(Name = "X-Person-Id")] int? actorId, EquipmentRequest request) =>
        await equipment.UpdateAsync(id, actorId, request);
}
