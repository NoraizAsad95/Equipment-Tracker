using EquipmentTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentTracker.Api.Services;

// Pin is the PIN of the person making the change (the Owner).
public record EquipmentRequest(
    string? Pin, string Name, TrackingMode TrackingMode, int TotalQuantity,
    bool RequiresApproval, int? ApproverId);

public record EquipmentDto(
    int Id, string Name, string TrackingMode, int TotalQuantity,
    bool RequiresApproval, int? ApproverId);

public class EquipmentService(AppDbContext db, AccessService access, AvailabilityService availability)
{
    public async Task<List<EquipmentDto>> ListAsync() =>
        (await db.Equipment.OrderBy(e => e.Name).ToListAsync()).Select(ToDto).ToList();

    public async Task<EquipmentDto> CreateAsync(int? actorId, EquipmentRequest request)
    {
        await access.RequireOwnerAsync(actorId, request.Pin);

        var item = new Equipment { TrackingMode = request.TrackingMode };
        await ApplyAsync(item, request);
        db.Equipment.Add(item);
        await db.SaveChangesAsync();
        return ToDto(item);
    }

    public async Task<EquipmentDto> UpdateAsync(int id, int? actorId, EquipmentRequest request)
    {
        await access.RequireOwnerAsync(actorId, request.Pin);

        var item = await db.Equipment.FindAsync(id) ?? throw DomainException.NotFound("Equipment not found.");
        if (item.TrackingMode != request.TrackingMode)
            throw DomainException.Invalid("The tracking mode of an item cannot be changed.");

        await ApplyAsync(item, request);
        await db.SaveChangesAsync();
        return ToDto(item);
    }

    // Validates the request and copies it onto the item.
    private async Task ApplyAsync(Equipment item, EquipmentRequest request)
    {
        var name = request.Name.Trim();
        if (name.Length == 0 || name.Length > 100)
            throw DomainException.Invalid("Name is required and can be at most 100 characters.");
        if (await db.Equipment.AnyAsync(e => e.Name == name && e.Id != item.Id))
            throw DomainException.Invalid($"An item named \"{name}\" already exists.");

        int total = 1;
        if (item.TrackingMode == TrackingMode.Quantity)
        {
            total = request.TotalQuantity;
            if (total < 1) throw DomainException.Invalid("Total quantity must be at least 1.");

            // Never shrink the total below the units that are damaged or currently held.
            var activeLines = await db.CheckoutLines
                .Where(l => l.EquipmentId == item.Id && (l.Status == LineStatus.Out || l.Status == LineStatus.PendingApproval))
                .ToListAsync();
            if (total < item.DamagedQuantity + availability.Held(activeLines))
                throw DomainException.Invalid("Total quantity is lower than the units that are damaged or checked out.");
        }

        int? approverId = null;
        if (request.RequiresApproval)
        {
            if (item.TrackingMode != TrackingMode.Individual)
                throw DomainException.Invalid("Approval can only be required for individually tracked items.");

            var approver = request.ApproverId == null ? null : await db.People.FindAsync(request.ApproverId.Value);
            if (approver == null) throw DomainException.Invalid("Choose an approver for this item.");
            if (approver.PinHash == null) throw DomainException.Invalid($"{approver.Name} has no PIN, so cannot approve.");
            approverId = approver.Id;
        }

        item.Name = name;
        item.TotalQuantity = total;
        item.RequiresApproval = request.RequiresApproval;
        item.ApproverId = approverId;
    }

    private static EquipmentDto ToDto(Equipment e) =>
        new(e.Id, e.Name, e.TrackingMode.ToString(), e.TotalQuantity, e.RequiresApproval, e.ApproverId);
}
