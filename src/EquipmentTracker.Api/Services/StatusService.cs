using EquipmentTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentTracker.Api.Services;

// Someone who holds (or has requested) an item.
public record Holder(string Person, int Quantity, bool Pending, DateOnly Since, DateOnly DueDate, int LineId, bool Overdue);

// One row on the current screen. The quantity fields are null for Individual items.
public record StatusItem(
    int Id, string Name, string TrackingMode, string Status,
    int? Total, int? Usable, int? Available, int? Damaged,
    string? ConditionNote, List<Holder> Holders);

public class StatusService(AppDbContext db, AvailabilityService availability, TimeProvider time)
{
    public async Task<List<StatusItem>> GetAsync(string? search)
    {
        var today = PakistanTime.ToDate(time.GetUtcNow().UtcDateTime);
        var equipment = await db.Equipment.OrderBy(e => e.Name).ToListAsync();

        var activeLines = await db.CheckoutLines
            .Where(l => l.Status == LineStatus.Out || l.Status == LineStatus.PendingApproval)
            .Include(l => l.Checkout!).ThenInclude(c => c.Borrower)
            .ToListAsync();

        var items = new List<StatusItem>();
        foreach (var e in equipment)
        {
            var lines = activeLines.Where(l => l.EquipmentId == e.Id).ToList();
            var holders = lines.Select(l => new Holder(
                l.Checkout!.Borrower!.Name,
                l.Quantity - l.ReturnedQuantity,
                l.Status == LineStatus.PendingApproval,
                PakistanTime.ToDate(l.Checkout.CreatedAtUtc),
                l.Checkout.DueDate,
                l.Id,
                // Overdue is only shown, never acted on (D4). A pending request is not out yet; due today is not late.
                l.Status == LineStatus.Out && l.Checkout.DueDate < today)).ToList();

            if (e.TrackingMode == TrackingMode.Individual)
            {
                items.Add(new StatusItem(e.Id, e.Name, "Individual", availability.IndividualStatus(e, lines),
                    null, null, null, null, e.ConditionNote, holders));
            }
            else
            {
                var available = availability.Available(e, lines);
                items.Add(new StatusItem(e.Id, e.Name, "Quantity", available > 0 ? "Available" : "Unavailable",
                    e.TotalQuantity, availability.Usable(e), available, e.DamagedQuantity, e.ConditionNote, holders));
            }
        }

        if (string.IsNullOrWhiteSpace(search)) return items;

        // Match on item name or on the name of anyone holding it.
        var text = search.Trim();
        return items.Where(i =>
            i.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
            i.Holders.Any(h => h.Person.Contains(text, StringComparison.OrdinalIgnoreCase))).ToList();
    }
}
