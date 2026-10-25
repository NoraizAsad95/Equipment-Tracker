using EquipmentTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Api.Controllers;

[ApiController]
[Route("api/checkouts")]
public class CheckoutsController(CheckoutService checkouts, ReturnService returns, ApprovalService approvals) : ControllerBase
{
    // The approver sends their id in X-Person-Id and their PIN in the body (AD-6).
    [HttpPost("lines/{lineId:int}/approve")]
    public async Task<IActionResult> Approve(int lineId, [FromHeader(Name = "X-Person-Id")] int? actorId, DecisionRequest request)
    {
        await approvals.DecideAsync(lineId, actorId, request, approve: true);
        return NoContent();
    }

    [HttpPost("lines/{lineId:int}/reject")]
    public async Task<IActionResult> Reject(int lineId, [FromHeader(Name = "X-Person-Id")] int? actorId, DecisionRequest request)
    {
        await approvals.DecideAsync(lineId, actorId, request, approve: false);
        return NoContent();
    }

    [HttpPost("lines/{lineId:int}/return")]
    public async Task<IActionResult> Return(int lineId, ReturnRequest request)
    {
        await returns.ReturnAsync(lineId, request);
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Create(CheckoutRequest request)
    {
        var id = await checkouts.CheckoutAsync(request);
        return Created($"/api/checkouts/{id}", new { id });
    }
}
