using System.Net.Http.Json;
using EquipmentTracker.Api.Data;
using EquipmentTracker.Api.Services;
using EquipmentTracker.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentTracker.Tests;

public class StatusTests : IClassFixture<TrackerFactory>
{
    private static readonly DateOnly Due = new(2030, 1, 15);
    private readonly TrackerFactory _factory;
    private readonly HttpClient _client;

    // xUnit makes a new instance per test, so this also resets the data between tests.
    public StatusTests(TrackerFactory factory)
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
                .SetProperty(e => e.Condition, EquipmentCondition.OK));
        });
    }

    private void WithDb(Action<AppDbContext> action)
    {
        using var scope = _factory.Services.CreateScope();
        action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private void AddLine(string person, string item, LineStatus status, int quantity = 1, int returned = 0) =>
        WithDb(db =>
        {
            db.Checkouts.Add(new Checkout
            {
                BorrowerId = db.People.Single(p => p.Name == person).Id,
                CreatedAtUtc = DateTime.UtcNow,
                DueDate = Due,
                Lines =
                {
                    new CheckoutLine
                    {
                        EquipmentId = db.Equipment.Single(e => e.Name == item).Id,
                        Quantity = quantity, ReturnedQuantity = returned, Status = status
                    }
                }
            });
            db.SaveChanges();
        });

    private async Task<List<StatusItem>> GetStatus(string? search = null) =>
        (await _client.GetFromJsonAsync<List<StatusItem>>(
            "/api/status" + (search == null ? "" : "?search=" + Uri.EscapeDataString(search))))!;

    private async Task<StatusItem> Get(string item) => (await GetStatus()).Single(i => i.Name == item);

    [Fact]
    public async Task Lists_every_item_as_available_when_nothing_is_out()
    {
        var items = await GetStatus();

        Assert.Equal(7, items.Count);
        Assert.All(items, i => Assert.Equal("Available", i.Status));
        Assert.All(items, i => Assert.Empty(i.Holders));
    }

    [Fact]
    public async Task Quantity_items_show_total_usable_and_available()
    {
        var tripod = await Get("Tripod");

        Assert.Equal((4, 4, 4, 0), (tripod.Total, tripod.Usable, tripod.Available, tripod.Damaged));
    }

    [Fact]
    public async Task Checked_out_item_shows_who_has_it_and_when_it_is_due()
    {
        AddLine("Ali", "GPS Rover 1", LineStatus.Out);

        var rover = await Get("GPS Rover 1");

        Assert.Equal("CheckedOut", rover.Status);
        var holder = Assert.Single(rover.Holders);
        Assert.Equal("Ali", holder.Person);
        Assert.Equal(Due, holder.DueDate);
        Assert.False(holder.Pending);
    }

    [Fact]
    public async Task Pending_request_shows_item_as_held()
    {
        AddLine("Ali", "Drone", LineStatus.PendingApproval);

        var drone = await Get("Drone");

        Assert.Equal("Held", drone.Status);
        Assert.True(Assert.Single(drone.Holders).Pending);
    }

    [Fact]
    public async Task Damaged_item_shows_as_damaged()
    {
        WithDb(db => db.Equipment.Where(e => e.Name == "Laptop")
            .ExecuteUpdate(s => s.SetProperty(e => e.Condition, EquipmentCondition.Damaged)));

        Assert.Equal("Damaged", (await Get("Laptop")).Status);
    }

    [Fact]
    public async Task Quantity_availability_subtracts_damaged_and_held_units()
    {
        WithDb(db => db.Equipment.Where(e => e.Name == "Field Tablet")
            .ExecuteUpdate(s => s.SetProperty(e => e.DamagedQuantity, 1)));
        AddLine("Bilal", "Field Tablet", LineStatus.Out, quantity: 2);

        var tablets = await Get("Field Tablet");

        Assert.Equal((6, 5, 3, 1), (tablets.Total, tablets.Usable, tablets.Available, tablets.Damaged));
        Assert.Equal(2, Assert.Single(tablets.Holders).Quantity);
    }

    [Fact]
    public async Task Available_quantity_is_never_negative_on_the_screen()
    {
        // 6 tablets, 2 damaged (4 usable) but 5 still out: would be -1 without the clamp.
        WithDb(db => db.Equipment.Where(e => e.Name == "Field Tablet")
            .ExecuteUpdate(s => s.SetProperty(e => e.DamagedQuantity, 2)));
        AddLine("Bilal", "Field Tablet", LineStatus.Out, quantity: 5);

        Assert.Equal(0, (await Get("Field Tablet")).Available);
    }

    [Fact]
    public async Task Partial_return_leaves_only_the_outstanding_quantity_held()
    {
        AddLine("Bilal", "Field Tablet", LineStatus.Out, quantity: 3, returned: 2);

        var tablets = await Get("Field Tablet");

        Assert.Equal(5, tablets.Available);
        Assert.Equal(1, Assert.Single(tablets.Holders).Quantity);
    }

    [Fact]
    public async Task Returned_and_rejected_lines_hold_nothing()
    {
        AddLine("Ali", "GPS Rover 1", LineStatus.Returned);
        AddLine("Ali", "Drone", LineStatus.Rejected);

        Assert.Equal("Available", (await Get("GPS Rover 1")).Status);
        Assert.Equal("Available", (await Get("Drone")).Status);
    }

    [Fact]
    public async Task Search_matches_item_names_ignoring_case()
    {
        var found = await GetStatus("rover");

        Assert.Equal(["GPS Rover 1", "GPS Rover 2 (new)"], found.Select(i => i.Name));
    }

    [Fact]
    public async Task Search_matches_the_person_holding_an_item()
    {
        AddLine("Imran", "Total Station", LineStatus.Out);

        var found = await GetStatus("imran");

        Assert.Equal("Total Station", Assert.Single(found).Name);
    }

    [Fact]
    public async Task Search_with_no_match_returns_an_empty_list()
    {
        Assert.Empty(await GetStatus("zzz"));
    }
}
