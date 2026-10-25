using System.Net;
using System.Net.Http.Json;
using EquipmentTracker.Api.Data;
using EquipmentTracker.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentTracker.Tests;

public class ReturnTests : IClassFixture<TrackerFactory>
{
    private readonly TrackerFactory _factory;
    private readonly HttpClient _client;

    public ReturnTests(TrackerFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        WithDb(db =>
        {
            db.HistoryEvents.ExecuteDelete();
            db.CheckoutLines.ExecuteDelete();
            db.Checkouts.ExecuteDelete();
            db.Equipment.ExecuteUpdate(s => s
                .SetProperty(e => e.DamagedQuantity, 0)
                .SetProperty(e => e.ConditionNote, (string?)null)
                .SetProperty(e => e.Condition, EquipmentCondition.OK));
        });
    }

    private T WithDb<T>(Func<AppDbContext, T> action)
    {
        using var scope = _factory.Services.CreateScope();
        return action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private void WithDb(Action<AppDbContext> action) => WithDb(db => { action(db); return 0; });

    // Puts items straight into the database as checked out, and returns the line ids.
    private List<int> Lend(string person, params (string item, int qty)[] items) => WithDb(db =>
    {
        var checkout = new Checkout
        {
            BorrowerId = db.People.Single(p => p.Name == person).Id,
            CreatedAtUtc = DateTime.UtcNow,
            DueDate = new DateOnly(2030, 1, 15)
        };
        foreach (var (item, qty) in items)
            checkout.Lines.Add(new CheckoutLine
            {
                EquipmentId = db.Equipment.Single(e => e.Name == item).Id,
                Quantity = qty, Status = LineStatus.Out
            });
        db.Checkouts.Add(checkout);
        db.SaveChanges();
        return checkout.Lines.Select(l => l.Id).ToList();
    });

    private Task<HttpResponseMessage> Return(int lineId, int quantity = 1, int damaged = 0, string? note = null) =>
        _client.PostAsJsonAsync($"/api/checkouts/lines/{lineId}/return",
            new { quantity, damagedQuantity = damaged, note });

    private Equipment Item(string name) => WithDb(db => db.Equipment.Single(e => e.Name == name));
    private CheckoutLine Line(int id) => WithDb(db => db.CheckoutLines.Single(l => l.Id == id));

    [Fact]
    public async Task Returning_a_single_item_in_good_condition_frees_it()
    {
        var line = Lend("Ali", ("GPS Rover 1", 1))[0];

        var response = await Return(line);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(LineStatus.Returned, Line(line).Status);
        Assert.Equal(EquipmentCondition.OK, Item("GPS Rover 1").Condition);
        var history = WithDb(db => db.HistoryEvents.Single());
        Assert.Equal(HistoryEventType.Returned, history.Type);
        Assert.Equal(EquipmentCondition.OK, history.Condition);
    }

    [Fact]
    public async Task Returning_a_single_item_as_damaged_blocks_it_and_keeps_the_note()
    {
        var line = Lend("Ali", ("Laptop", 1))[0];

        await Return(line, damaged: 1, note: "Cracked screen");

        var laptop = Item("Laptop");
        Assert.Equal(EquipmentCondition.Damaged, laptop.Condition);
        Assert.Equal("Cracked screen", laptop.ConditionNote);
        Assert.Equal(LineStatus.Returned, Line(line).Status);
        Assert.Equal("Cracked screen", WithDb(db => db.HistoryEvents.Single()).Note);
    }

    [Fact]
    public async Task Partial_return_of_counted_items_leaves_the_rest_out()
    {
        var line = Lend("Bilal", ("Field Tablet", 4))[0];

        await Return(line, quantity: 3);

        var after = Line(line);
        Assert.Equal(3, after.ReturnedQuantity);
        Assert.Equal(LineStatus.Out, after.Status);

        await Return(line, quantity: 1);
        Assert.Equal(LineStatus.Returned, Line(line).Status);
    }

    [Fact]
    public async Task Damaged_counted_units_reduce_the_usable_total()
    {
        var line = Lend("Bilal", ("Field Tablet", 2))[0];

        await Return(line, quantity: 2, damaged: 1, note: "Water damage");

        var tablets = Item("Field Tablet");
        Assert.Equal(1, tablets.DamagedQuantity);
        Assert.Equal(6, tablets.TotalQuantity);
        Assert.StartsWith("1 damaged.", WithDb(db => db.HistoryEvents.Single()).Note);
    }

    [Fact]
    public async Task A_kit_can_come_back_partly_and_the_missing_item_stays_out()
    {
        var lines = Lend("Imran", ("Total Station", 1), ("Tripod", 2));

        await Return(lines[1], quantity: 2); // only the tripods come back

        Assert.Equal(LineStatus.Returned, Line(lines[1]).Status);
        Assert.Equal(LineStatus.Out, Line(lines[0]).Status);
    }

    [Fact]
    public async Task Cannot_return_more_than_is_outstanding()
    {
        var line = Lend("Bilal", ("Tripod", 2))[0];

        var response = await Return(line, quantity: 3);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, Line(line).ReturnedQuantity);
    }

    [Fact]
    public async Task Damaged_quantity_cannot_exceed_the_quantity_returned()
    {
        var line = Lend("Bilal", ("Tripod", 2))[0];

        var response = await Return(line, quantity: 1, damaged: 2);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, Item("Tripod").DamagedQuantity);
    }

    [Fact]
    public async Task A_line_cannot_be_returned_twice()
    {
        var line = Lend("Ali", ("GPS Rover 1", 1))[0];
        await Return(line);

        var again = await Return(line);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(1, WithDb(db => db.HistoryEvents.Count()));
    }

    [Fact]
    public async Task Unknown_line_gives_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Return(999999)).StatusCode);
    }

    [Fact]
    public async Task Returning_makes_the_item_available_again_on_the_status_screen()
    {
        var line = Lend("Ali", ("GPS Rover 1", 1))[0];
        await Return(line);

        var items = await _client.GetFromJsonAsync<List<EquipmentTracker.Api.Services.StatusItem>>("/api/status");

        Assert.Equal("Available", items!.Single(i => i.Name == "GPS Rover 1").Status);
    }
}
