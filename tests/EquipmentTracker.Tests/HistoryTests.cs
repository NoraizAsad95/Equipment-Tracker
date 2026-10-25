using System.Net;
using System.Net.Http.Json;
using EquipmentTracker.Api.Data;
using EquipmentTracker.Api.Services;
using EquipmentTracker.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentTracker.Tests;

public class HistoryTests : IClassFixture<TrackerFactory>
{
    private static readonly DateOnly Today = PakistanTime.ToDate(DateTime.UtcNow);
    private readonly TrackerFactory _factory;
    private readonly HttpClient _client;

    public HistoryTests(TrackerFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        WithDb(db =>
        {
            db.HistoryEvents.ExecuteDelete();
            db.CheckoutLines.ExecuteDelete();
            db.Checkouts.ExecuteDelete();
        });
    }

    private T WithDb<T>(Func<AppDbContext, T> action)
    {
        using var scope = _factory.Services.CreateScope();
        return action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private void WithDb(Action<AppDbContext> action) => WithDb(db => { action(db); return 0; });

    private int PersonId(string name) => WithDb(db => db.People.Single(p => p.Name == name).Id);

    // Puts a past checkout straight into the database: out `outDaysAgo` days ago, back `returnedDaysAgo` days ago (or still out).
    private int AddPast(string person, string item, int outDaysAgo, int? returnedDaysAgo = null, LineStatus? status = null) =>
        WithDb(db =>
        {
            var borrowerId = db.People.Single(p => p.Name == person).Id;
            var equipmentId = db.Equipment.Single(e => e.Name == item).Id;
            var outAt = DateTime.UtcNow.AddDays(-outDaysAgo);
            var line = new CheckoutLine
            {
                EquipmentId = equipmentId,
                Quantity = 1,
                ReturnedQuantity = returnedDaysAgo == null ? 0 : 1,
                Status = status ?? (returnedDaysAgo == null ? LineStatus.Out : LineStatus.Returned)
            };
            var checkout = new Checkout { BorrowerId = borrowerId, CreatedAtUtc = outAt, DueDate = Today, Lines = { line } };
            db.Checkouts.Add(checkout);
            db.SaveChanges();

            db.HistoryEvents.Add(new HistoryEvent
            {
                Type = HistoryEventType.CheckedOut, AtUtc = outAt, ActorId = borrowerId, EquipmentId = equipmentId,
                CheckoutId = checkout.Id, CheckoutLineId = line.Id, Quantity = 1
            });
            if (returnedDaysAgo != null)
                db.HistoryEvents.Add(new HistoryEvent
                {
                    Type = HistoryEventType.Returned, AtUtc = DateTime.UtcNow.AddDays(-returnedDaysAgo.Value), ActorId = borrowerId,
                    EquipmentId = equipmentId, CheckoutId = checkout.Id, CheckoutLineId = line.Id, Quantity = 1,
                    Condition = EquipmentCondition.OK
                });
            db.SaveChanges();
            return line.Id;
        });

    private Task<HttpResponseMessage> SearchRaw(string? person, string? pin, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/history/search") { Content = JsonContent.Create(body) };
        if (person != null) request.Headers.Add("X-Person-Id", PersonId(person).ToString());
        return _client.SendAsync(request);
    }

    private async Task<List<HistoryRow>> Search(DateOnly from, DateOnly to, string person = "Tariq", string pin = "1111")
    {
        var response = await SearchRaw(person, pin, new { pin, from, to });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<HistoryRow>>())!;
    }

    private Task<HttpResponseMessage> Correct(int eventId, string? person, string? pin, string? note)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/history/{eventId}/corrections")
        {
            Content = JsonContent.Create(new { pin, note })
        };
        if (person != null) request.Headers.Add("X-Person-Id", PersonId(person).ToString());
        return _client.SendAsync(request);
    }

    // ---- Story 5.1: date-range history ----

    [Fact]
    public async Task Shows_borrower_item_quantity_and_all_three_dates_for_a_returned_item()
    {
        AddPast("Ali", "GPS Rover 1", outDaysAgo: 5, returnedDaysAgo: 2);

        var row = Assert.Single(await Search(Today.AddDays(-10), Today));

        Assert.Equal("Ali", row.Borrower);
        Assert.Equal("GPS Rover 1", row.Equipment);
        Assert.Equal(1, row.Quantity);
        Assert.Equal(Today.AddDays(-5), row.CheckedOutOn);
        Assert.Equal(Today, row.DueDate);
        Assert.Equal(Today.AddDays(-2), row.ReturnedOn);
        Assert.False(row.StillOut);
    }

    [Fact]
    public async Task Items_still_out_are_included_with_no_return_date()
    {
        AddPast("Bilal", "Laptop", outDaysAgo: 3);

        var row = Assert.Single(await Search(Today.AddDays(-10), Today));

        Assert.True(row.StillOut);
        Assert.Null(row.ReturnedOn);
    }

    [Fact]
    public async Task An_item_checked_out_long_ago_and_still_out_was_out_during_a_recent_range()
    {
        AddPast("Bilal", "Laptop", outDaysAgo: 60);

        Assert.Single(await Search(Today.AddDays(-7), Today));
    }

    [Fact]
    public async Task Only_items_that_were_out_during_the_range_are_listed()
    {
        AddPast("Ali", "GPS Rover 1", outDaysAgo: 60, returnedDaysAgo: 50); // finished before the range
        AddPast("Ali", "Total Station", outDaysAgo: 40, returnedDaysAgo: 25); // inside
        AddPast("Ali", "Laptop", outDaysAgo: 2);                              // started after the range

        var rows = await Search(Today.AddDays(-30), Today.AddDays(-10));

        Assert.Equal("Total Station", Assert.Single(rows).Equipment);
    }

    [Fact]
    public async Task Pending_and_rejected_requests_are_not_in_the_history()
    {
        AddPast("Ali", "Drone", outDaysAgo: 1, status: LineStatus.PendingApproval);
        AddPast("Ali", "Tripod", outDaysAgo: 1, status: LineStatus.Rejected);

        Assert.Empty(await Search(Today.AddDays(-5), Today));
    }

    [Fact]
    public async Task A_real_checkout_and_return_appear_with_their_events()
    {
        await _client.PostAsJsonAsync("/api/checkouts", new
        {
            borrowerId = PersonId("Imran"),
            dueDate = Today.AddDays(5),
            items = new[] { new { equipmentId = WithDb(db => db.Equipment.Single(e => e.Name == "Tripod").Id), quantity = 2 } }
        });
        var lineId = WithDb(db => db.CheckoutLines.Single().Id);
        await _client.PostAsJsonAsync($"/api/checkouts/lines/{lineId}/return", new { quantity = 2 });

        var row = Assert.Single(await Search(Today, Today));

        Assert.Equal(2, row.Quantity);
        Assert.Equal(Today, row.ReturnedOn);
        Assert.Equal(["CheckedOut", "Returned"], row.Entries.Select(e => e.Type));
        Assert.All(row.Entries, e => Assert.Equal("Imran", e.By));
    }

    [Fact]
    public async Task Office_can_read_history_too()
    {
        AddPast("Ali", "GPS Rover 1", outDaysAgo: 1);

        Assert.Single(await Search(Today.AddDays(-5), Today, "Saima", "2222"));
    }

    [Theory]
    [InlineData("Hamza", "3333")] // Staff with a PIN
    [InlineData("Ali", "1234")]   // Staff without a PIN
    [InlineData("Tariq", "0000")] // Owner, wrong PIN
    [InlineData(null, "1111")]    // nobody picked
    public async Task Everyone_else_is_refused(string? person, string pin)
    {
        var response = await SearchRaw(person, pin, new { pin, from = Today.AddDays(-5), to = Today });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Start_after_end_or_missing_dates_are_rejected()
    {
        var backwards = await SearchRaw("Tariq", "1111", new { pin = "1111", from = Today, to = Today.AddDays(-3) });
        var missing = await SearchRaw("Tariq", "1111", new { pin = "1111" });

        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task History_cannot_be_edited_or_deleted_through_any_endpoint(string method)
    {
        AddPast("Ali", "GPS Rover 1", outDaysAgo: 1);
        var id = WithDb(db => db.HistoryEvents.First().Id);

        var response = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), $"/api/history/{id}"));

        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(1, WithDb(db => db.HistoryEvents.Count()));
    }

    // ---- Story 5.2: corrections ----

    [Fact]
    public async Task Owner_can_correct_an_entry_and_the_original_is_unchanged()
    {
        var lineId = AddPast("Ali", "GPS Rover 1", outDaysAgo: 5, returnedDaysAgo: 2);
        var original = WithDb(db => db.HistoryEvents.Single(h => h.Type == HistoryEventType.Returned));

        var response = await Correct(original.Id, "Tariq", "1111", "It came back on the 3rd, not the 2nd");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = WithDb(db => db.HistoryEvents.Single(h => h.Id == original.Id));
        Assert.Equal((original.Type, original.AtUtc, original.Quantity, original.Note, original.ActorId),
                     (after.Type, after.AtUtc, after.Quantity, after.Note, after.ActorId));

        var correction = WithDb(db => db.HistoryEvents.Single(h => h.Type == HistoryEventType.Corrected));
        Assert.Equal(original.Id, correction.CorrectsEventId);
        Assert.Equal(lineId, correction.CheckoutLineId);
        Assert.Equal(PersonId("Tariq"), correction.ActorId);
        Assert.Equal("It came back on the 3rd, not the 2nd", correction.Note);
    }

    [Fact]
    public async Task The_correction_is_shown_beside_the_original_in_the_history()
    {
        AddPast("Ali", "GPS Rover 1", outDaysAgo: 5, returnedDaysAgo: 2);
        var original = WithDb(db => db.HistoryEvents.Single(h => h.Type == HistoryEventType.Returned));
        await Correct(original.Id, "Tariq", "1111", "Wrong date");

        var row = Assert.Single(await Search(Today.AddDays(-10), Today));

        Assert.Equal(3, row.Entries.Count);
        var shown = row.Entries.Single(e => e.Type == "Corrected");
        Assert.Equal(original.Id, shown.CorrectsEventId);
        Assert.Equal("Wrong date", shown.Note);
        Assert.Contains(row.Entries, e => e.Id == original.Id && e.Type == "Returned");
    }

    [Fact]
    public async Task A_correction_needs_a_note()
    {
        AddPast("Ali", "GPS Rover 1", outDaysAgo: 1);
        var id = WithDb(db => db.HistoryEvents.First().Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await Correct(id, "Tariq", "1111", "  ")).StatusCode);
        Assert.Equal(1, WithDb(db => db.HistoryEvents.Count()));
    }

    [Theory]
    [InlineData("Saima", "2222")] // Office reads history but only the Owner corrects
    [InlineData("Hamza", "3333")]
    [InlineData("Tariq", "0000")]
    [InlineData(null, "1111")]
    public async Task Only_the_owner_with_the_right_pin_can_correct(string? person, string pin)
    {
        AddPast("Ali", "GPS Rover 1", outDaysAgo: 1);
        var id = WithDb(db => db.HistoryEvents.First().Id);

        var response = await Correct(id, person, pin, "Wrong");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, WithDb(db => db.HistoryEvents.Count()));
    }

    [Fact]
    public async Task Unknown_entry_gives_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Correct(999999, "Tariq", "1111", "Wrong")).StatusCode);
    }

    [Fact]
    public async Task A_correction_cannot_itself_be_corrected()
    {
        AddPast("Ali", "GPS Rover 1", outDaysAgo: 1);
        var id = WithDb(db => db.HistoryEvents.First().Id);
        await Correct(id, "Tariq", "1111", "First");
        var correctionId = WithDb(db => db.HistoryEvents.Single(h => h.Type == HistoryEventType.Corrected).Id);

        var response = await Correct(correctionId, "Tariq", "1111", "Second");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Only_checkout_and_return_entries_can_be_corrected()
    {
        // A restore is recorded against the equipment, not against a checkout line.
        var id = WithDb(db =>
        {
            var restored = new HistoryEvent
            {
                Type = HistoryEventType.Restored, AtUtc = DateTime.UtcNow, ActorId = db.People.Single(p => p.Name == "Saima").Id,
                EquipmentId = db.Equipment.Single(e => e.Name == "Laptop").Id
            };
            db.HistoryEvents.Add(restored);
            db.SaveChanges();
            return restored.Id;
        });

        Assert.Equal(HttpStatusCode.BadRequest, (await Correct(id, "Tariq", "1111", "Wrong")).StatusCode);
    }
}
