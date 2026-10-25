using EquipmentTracker.Api.Data;

namespace EquipmentTracker.Api.Services;

// Pin = the PIN of the Office person or Owner restoring. Quantity = how many damaged units to restore (ignored for a single item).
public record RestoreRequest(string? Pin, int Quantity = 1, string? Note = null);

public class RestoreService(AppDbContext db, AccessService access, TimeProvider time)
{
    // Only restores equipment that is damaged. Nothing else about the item can be changed here (D6).
    public async Task RestoreAsync(int equipmentId, int? actorId, RestoreRequest request)
    {
        var equipment = await db.Equipment.FindAsync(equipmentId)
            ?? throw DomainException.NotFound("Equipment not found.");

        var person = await access.RequireOfficeOrOwnerAsync(actorId, request.Pin);

        var quantity = 1;
        if (equipment.TrackingMode == TrackingMode.Individual)
        {
            if (equipment.Condition != EquipmentCondition.Damaged)
                throw DomainException.Invalid($"{equipment.Name} is not damaged.");
            equipment.Condition = EquipmentCondition.OK;
            equipment.ConditionNote = null;
        }
        else
        {
            quantity = request.Quantity;
            if (equipment.DamagedQuantity == 0)
                throw DomainException.Invalid($"No {equipment.Name} are damaged.");
            if (quantity < 1 || quantity > equipment.DamagedQuantity)
                throw DomainException.Invalid($"Restore between 1 and {equipment.DamagedQuantity} of {equipment.Name}.");
            equipment.DamagedQuantity -= quantity;
            if (equipment.DamagedQuantity == 0) equipment.ConditionNote = null;
        }

        // History is append-only (AD-2).
        db.HistoryEvents.Add(new HistoryEvent
        {
            Type = HistoryEventType.Restored,
            AtUtc = time.GetUtcNow().UtcDateTime,
            ActorId = person.Id,
            EquipmentId = equipment.Id,
            Quantity = quantity,
            Condition = EquipmentCondition.OK,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        });

        await db.SaveChangesAsync(); // one save = one transaction; RowVersion gives a 409 on a clash (AD-5)
    }
}
