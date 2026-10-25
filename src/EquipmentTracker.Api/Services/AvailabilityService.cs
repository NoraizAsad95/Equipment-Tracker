using EquipmentTracker.Api.Data;

namespace EquipmentTracker.Api.Services;

// The only place that decides what is available (AD-3).
// "Active" lines are the ones that currently hold equipment: Out or PendingApproval.
public class AvailabilityService
{
    // Units not damaged.
    public int Usable(Equipment e) => e.TotalQuantity - e.DamagedQuantity;

    // Units currently held by active lines. A pending request holds its full quantity.
    public int Held(IEnumerable<CheckoutLine> activeLines) =>
        activeLines.Sum(l => l.Status == LineStatus.Out ? l.Quantity - l.ReturnedQuantity : l.Quantity);

    // For Quantity items: usable units that nobody holds. Never below zero, even if units were
    // reported damaged while others were still out.
    public int Available(Equipment e, IEnumerable<CheckoutLine> activeLines) =>
        Math.Max(0, Usable(e) - Held(activeLines));

    // Throws if the item cannot be taken right now. Checkout and approval requests must call this (FR-10).
    public void EnsureCanTake(Equipment e, IEnumerable<CheckoutLine> activeLines, int quantity = 1)
    {
        var lines = activeLines.ToList();

        if (e.TrackingMode == TrackingMode.Individual)
        {
            if (quantity != 1) throw DomainException.Invalid($"{e.Name} is a single item; quantity must be 1.");
            if (e.Condition == EquipmentCondition.Damaged)
                throw DomainException.Invalid($"{e.Name} is damaged and cannot be checked out.");
            // One person at a time: any Out or PendingApproval line blocks everyone else.
            if (lines.Count > 0)
                throw DomainException.Conflict($"{e.Name} is already checked out or held by someone else.");
            return;
        }

        if (quantity < 1) throw DomainException.Invalid("Quantity must be at least 1.");
        var available = Available(e, lines);
        if (quantity > available)
            throw DomainException.Invalid($"Only {available} of {e.Name} available.");
    }

    // For Individual items: Available, Held, CheckedOut or Damaged.
    public string IndividualStatus(Equipment e, IEnumerable<CheckoutLine> activeLines)
    {
        var lines = activeLines.ToList();
        if (lines.Any(l => l.Status == LineStatus.Out)) return "CheckedOut";
        if (lines.Any(l => l.Status == LineStatus.PendingApproval)) return "Held";
        if (e.Condition == EquipmentCondition.Damaged) return "Damaged";
        return "Available";
    }
}
