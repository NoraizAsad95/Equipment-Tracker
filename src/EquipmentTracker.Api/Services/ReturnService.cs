using EquipmentTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentTracker.Api.Services;

// Quantity = how many are coming back now. DamagedQuantity = how many of those are damaged
// (for a single item: 1 means it is damaged, 0 means it is fine).
public record ReturnRequest(int Quantity = 1, int DamagedQuantity = 0, string? Note = null);

public class ReturnService(AppDbContext db, TimeProvider time)
{
    // Each line is returned on its own, so a kit can come back partly: the missing item stays checked out.
    public async Task ReturnAsync(int lineId, ReturnRequest request)
    {
        var line = await db.CheckoutLines
            .Include(l => l.Checkout)
            .Include(l => l.Equipment)
            .SingleOrDefaultAsync(l => l.Id == lineId)
            ?? throw DomainException.NotFound("Checkout line not found.");

        if (line.Status != LineStatus.Out)
            throw DomainException.Conflict("This item is not currently checked out.");

        var equipment = line.Equipment!;
        var outstanding = line.Quantity - line.ReturnedQuantity;

        if (request.Quantity < 1 || request.Quantity > outstanding)
            throw DomainException.Invalid($"Return between 1 and {outstanding} of {equipment.Name}.");
        if (request.DamagedQuantity < 0 || request.DamagedQuantity > request.Quantity)
            throw DomainException.Invalid("Damaged quantity cannot be more than the quantity returned.");

        line.ReturnedQuantity += request.Quantity;
        if (line.ReturnedQuantity == line.Quantity) line.Status = LineStatus.Returned;

        var damaged = request.DamagedQuantity > 0;
        if (damaged)
        {
            // A damaged single item is blocked until restored (Story 4.1); damaged counted units leave "usable".
            if (equipment.TrackingMode == TrackingMode.Individual) equipment.Condition = EquipmentCondition.Damaged;
            else equipment.DamagedQuantity += request.DamagedQuantity;
            equipment.ConditionNote = request.Note;
        }
        // Touch the row so concurrent changes to the same item give a 409 (AD-5).
        db.Entry(equipment).Property(e => e.Name).IsModified = true;

        var note = request.Note;
        if (damaged && equipment.TrackingMode == TrackingMode.Quantity)
            note = $"{request.DamagedQuantity} damaged. {note}".Trim();

        // History is append-only (AD-2). The person who borrowed it is recorded as returning it.
        db.HistoryEvents.Add(new HistoryEvent
        {
            Type = HistoryEventType.Returned,
            AtUtc = time.GetUtcNow().UtcDateTime,
            ActorId = line.Checkout!.BorrowerId,
            EquipmentId = equipment.Id,
            CheckoutId = line.CheckoutId,
            CheckoutLineId = line.Id,
            Quantity = request.Quantity,
            Condition = damaged ? EquipmentCondition.Damaged : EquipmentCondition.OK,
            Note = note
        });

        await db.SaveChangesAsync(); // one save = one transaction
    }
}
