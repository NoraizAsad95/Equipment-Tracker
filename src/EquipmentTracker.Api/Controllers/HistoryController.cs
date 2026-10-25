using EquipmentTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Api.Controllers;

// There is deliberately no PUT or DELETE here: history cannot be edited or removed.
[ApiController]
[Route("api/history")]
public class HistoryController(HistoryService history) : ControllerBase
{
    // POST so the PIN travels in the body like every other protected call (AD-6). It only reads.
    [HttpPost("search")]
    public async Task<List<HistoryRow>> Search([FromHeader(Name = "X-Person-Id")] int? actorId, HistoryQuery query) =>
        await history.SearchAsync(actorId, query);

    // Owner only.
    [HttpPost("{eventId:int}/corrections")]
    public async Task<IActionResult> Correct(int eventId, [FromHeader(Name = "X-Person-Id")] int? actorId, CorrectionRequest request)
    {
        await history.CorrectAsync(eventId, actorId, request);
        return NoContent();
    }
}
