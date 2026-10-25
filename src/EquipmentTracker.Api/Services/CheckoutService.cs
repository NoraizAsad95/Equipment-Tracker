using EquipmentTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentTracker.Api.Services;

public record CheckoutItem(int EquipmentId, int Quantity = 1);
public record CheckoutRequest(int BorrowerId, DateOnly DueDate, List<CheckoutItem> Items);

public class CheckoutService(AppDbContext db, AvailabilityService availability, TimeProvider time)
{
    // One checkout can hold several items (a "kit" is just a multi-item checkout).
    // Borrowing is name-pick only, so there is no PIN here (AD-6).
    public async Task<int> CheckoutAsync(CheckoutRequest request)
    {
        var items = request.Items ?? [];
        if (items.Count == 0) throw DomainException.Invalid("Pick at least one item.");
        if (items.Select(i => i.EquipmentId).Distinct().Count() != items.Count)
            throw DomainException.Invalid("Each item can only be listed once.");

        var today = PakistanTime.ToDate(time.GetUtcNow().UtcDateTime);
        if (request.DueDate < today) throw DomainException.Invalid("The due date cannot be in the past.");

        var borrower = await db.People.FindAsync(request.BorrowerId)
            ?? throw DomainException.NotFound("Person not found.");

        var checkout = new Checkout { BorrowerId = borrower.Id, CreatedAtUtc = time.GetUtcNow().UtcDateTime, DueDate = request.DueDate };

        foreach (var item in items)
        {
            var equipment = await db.Equipment.FindAsync(item.EquipmentId)
                ?? throw DomainException.NotFound("Equipment not found.");

            var activeLines = await db.CheckoutLines
                .Where(l => l.EquipmentId == equipment.Id && (l.Status == LineStatus.Out || l.Status == LineStatus.PendingApproval))
                .ToListAsync();

            availability.EnsureCanTake(equipment, activeLines, item.Quantity);

            // Touch the equipment row so its rowversion changes: if someone else checked the
            // same item out at the same moment, our save fails with a 409 instead of double-booking.
            db.Entry(equipment).Property(e => e.Name).IsModified = true;

            // Items that need approval are only requested: the line is held until the approver decides (FR-14).
            var status = equipment.RequiresApproval ? LineStatus.PendingApproval : LineStatus.Out;
            checkout.Lines.Add(new CheckoutLine { EquipmentId = equipment.Id, Quantity = item.Quantity, Status = status });
        }

        // The checkout and its history are saved together or not at all.
        await using var transaction = await db.Database.BeginTransactionAsync();

        db.Checkouts.Add(checkout);
        await db.SaveChangesAsync(); // gives the checkout and its lines their ids

        // History is append-only (AD-2).
        foreach (var line in checkout.Lines)
        {
            db.HistoryEvents.Add(new HistoryEvent
            {
                Type = line.Status == LineStatus.Out ? HistoryEventType.CheckedOut : HistoryEventType.ApprovalRequested,
                AtUtc = checkout.CreatedAtUtc,
                ActorId = borrower.Id,
                EquipmentId = line.EquipmentId,
                CheckoutId = checkout.Id,
                CheckoutLineId = line.Id,
                Quantity = line.Quantity
            });
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return checkout.Id;
    }
}
