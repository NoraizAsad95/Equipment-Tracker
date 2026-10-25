using EquipmentTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentTracker.Api.Services;

public record HistoryQuery(string? Pin, DateOnly From, DateOnly To);
public record CorrectionRequest(string? Pin, string? Note);

// One thing that happened (checked out, returned, corrected...). Corrections show beside the entry they correct.
public record HistoryEntry(int Id, string Type, DateOnly On, string By, int? Quantity, string? Condition, string? Note, int? CorrectsEventId);

// One row per item taken out. ReturnedOn is empty while any of it is still out.
public record HistoryRow(
    int LineId, string Borrower, string Equipment, int Quantity, int ReturnedQuantity,
    DateOnly CheckedOutOn, DateOnly DueDate, DateOnly? ReturnedOn, bool StillOut, List<HistoryEntry> Entries);

public class HistoryService(AppDbContext db, AccessService access, TimeProvider time)
{
    // Lists everything that was out at some point between From and To (inclusive), including items still out.
    public async Task<List<HistoryRow>> SearchAsync(int? actorId, HistoryQuery query)
    {
        await access.RequireOfficeOrOwnerAsync(actorId, query.Pin);

        if (query.From == default || query.To == default) throw DomainException.Invalid("Pick a start and an end date.");
        if (query.From > query.To) throw DomainException.Invalid("The start date cannot be after the end date.");

        // Pending and rejected requests were never out, so they are not part of this list.
        var lines = await db.CheckoutLines
            .Where(l => l.Status == LineStatus.Out || l.Status == LineStatus.Returned)
            .Include(l => l.Checkout!).ThenInclude(c => c.Borrower)
            .Include(l => l.Equipment)
            .ToListAsync();

        var events = (await db.HistoryEvents
            .Where(h => h.CheckoutLineId != null)
            .Include(h => h.Actor)
            .OrderBy(h => h.AtUtc).ThenBy(h => h.Id)
            .ToListAsync()).ToLookup(h => h.CheckoutLineId!.Value);

        var rows = new List<HistoryRow>();
        foreach (var l in lines)
        {
            var lineEvents = events[l.Id].ToList();
            // For an approved item the clock starts at approval, not at the request.
            var checkedOutOn = PakistanTime.ToDate(l.DecidedAtUtc ?? l.Checkout!.CreatedAtUtc);
            var stillOut = l.Status == LineStatus.Out;
            var lastReturn = lineEvents.LastOrDefault(h => h.Type == HistoryEventType.Returned);
            DateOnly? returnedOn = stillOut ? null : PakistanTime.ToDate(lastReturn?.AtUtc ?? l.Checkout!.CreatedAtUtc);

            var wasOutInRange = checkedOutOn <= query.To && (stillOut || returnedOn >= query.From);
            if (!wasOutInRange) continue;

            rows.Add(new HistoryRow(
                l.Id, l.Checkout!.Borrower!.Name, l.Equipment!.Name, l.Quantity, l.ReturnedQuantity,
                checkedOutOn, l.Checkout.DueDate, returnedOn, stillOut,
                lineEvents.Select(h => new HistoryEntry(
                    h.Id, h.Type.ToString(), PakistanTime.ToDate(h.AtUtc), h.Actor!.Name,
                    h.Quantity, h.Condition?.ToString(), h.Note, h.CorrectsEventId)).ToList()));
        }

        return rows.OrderBy(r => r.CheckedOutOn).ThenBy(r => r.LineId).ToList();
    }

    // History is never edited or deleted (FR-27): a correction is a new entry that points at the original.
    public async Task CorrectAsync(int eventId, int? actorId, CorrectionRequest request)
    {
        var owner = await access.RequireOwnerAsync(actorId, request.Pin);

        if (string.IsNullOrWhiteSpace(request.Note)) throw DomainException.Invalid("Say what was wrong.");

        var original = await db.HistoryEvents.FindAsync(eventId)
            ?? throw DomainException.NotFound("History entry not found.");
        if (original.CheckoutLineId == null)
            throw DomainException.Invalid("Only checkout and return entries can be corrected.");
        if (original.Type == HistoryEventType.Corrected)
            throw DomainException.Invalid("Correct the original entry, not a correction.");

        db.HistoryEvents.Add(new HistoryEvent
        {
            Type = HistoryEventType.Corrected,
            AtUtc = time.GetUtcNow().UtcDateTime,
            ActorId = owner.Id,
            EquipmentId = original.EquipmentId,
            CheckoutId = original.CheckoutId,
            CheckoutLineId = original.CheckoutLineId, // same line, so it shows beside the original
            CorrectsEventId = original.Id,
            Note = request.Note.Trim()
        });
        await db.SaveChangesAsync();
    }
}
