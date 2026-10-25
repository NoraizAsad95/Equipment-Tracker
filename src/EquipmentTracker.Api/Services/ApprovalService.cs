using EquipmentTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentTracker.Api.Services;

// Pin = the approver's PIN. Reason is optional and only used when rejecting.
public record DecisionRequest(string? Pin, string? Reason = null);

public class ApprovalService(AppDbContext db, AccessService access, TimeProvider time)
{
    // Approving turns a pending request into a real checkout (Out). Rejecting releases the hold.
    public async Task DecideAsync(int lineId, int? actorId, DecisionRequest request, bool approve)
    {
        var line = await db.CheckoutLines
            .Include(l => l.Equipment)
            .SingleOrDefaultAsync(l => l.Id == lineId)
            ?? throw DomainException.NotFound("Request not found.");

        var approver = await access.RequireApproverAsync(actorId, request.Pin, line.Equipment!);

        if (line.Status != LineStatus.PendingApproval)
            throw DomainException.Conflict("This request has already been decided.");

        var now = time.GetUtcNow().UtcDateTime;
        line.Status = approve ? LineStatus.Out : LineStatus.Rejected;
        line.DecidedById = approver.Id;
        line.DecidedAtUtc = now;
        if (!approve) line.RejectionReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        // History is append-only (AD-2): who decided, what, and when.
        db.HistoryEvents.Add(new HistoryEvent
        {
            Type = approve ? HistoryEventType.Approved : HistoryEventType.Rejected,
            AtUtc = now,
            ActorId = approver.Id,
            EquipmentId = line.EquipmentId,
            CheckoutId = line.CheckoutId,
            CheckoutLineId = line.Id,
            Quantity = line.Quantity,
            Note = line.RejectionReason
        });

        await db.SaveChangesAsync(); // one save = one transaction
    }
}
