using System.Net;
using System.Net.Http.Json;
using EquipmentTracker.Api.Data;
using EquipmentTracker.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentTracker.Tests;

public class CheckoutTests : IClassFixture<TrackerFactory>
{
    private static readonly string Due = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd");
    private readonly TrackerFactory _factory;
    private readonly HttpClient _client;

    public CheckoutTests(TrackerFactory factory)
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

    private T WithDb<T>(Func<AppDbContext, T> action)
    {
        using var scope = _factory.Services.CreateScope();
        return action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private void WithDb(Action<AppDbContext> action) => WithDb(db => { action(db); return 0; });

    private int PersonId(string name) => WithDb(db => db.People.Single(p => p.Name == name).Id);
    private int ItemId(string name) => WithDb(db => db.Equipment.Single(e => e.Name == name).Id);

    private Task<HttpResponseMessage> Checkout(string person, string due, params (string item, int qty)[] items) =>
        _client.PostAsJsonAsync("/api/checkouts", new
        {
            borrowerId = PersonId(person),
            dueDate = due,
            items = items.Select(i => new { equipmentId = ItemId(i.item), quantity = i.qty })
        });

    private int ActiveLines(string item) => WithDb(db => db.CheckoutLines.Count(l =>
        l.Equipment!.Name == item && (l.Status == LineStatus.Out || l.Status == LineStatus.PendingApproval)));

    [Fact]
    public async Task Checking_out_an_item_marks_it_out_and_records_history()
    {
        var response = await Checkout("Ali", Due, ("GPS Rover 1", 1));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, ActiveLines("GPS Rover 1"));
        var history = WithDb(db => db.HistoryEvents.Single());
        Assert.Equal(HistoryEventType.CheckedOut, history.Type);
        Assert.Equal(PersonId("Ali"), history.ActorId);
    }

    [Fact]
    public async Task A_kit_is_one_checkout_with_several_items()
    {
        var response = await Checkout("Bilal", Due, ("Total Station", 1), ("Tripod", 2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, WithDb(db => db.Checkouts.Count()));
        Assert.Equal(2, WithDb(db => db.CheckoutLines.Count()));
        Assert.Equal(2, WithDb(db => db.HistoryEvents.Count()));
    }

    [Fact]
    public async Task Same_individual_item_cannot_be_checked_out_twice()
    {
        await Checkout("Ali", Due, ("GPS Rover 1", 1));

        var second = await Checkout("Bilal", Due, ("GPS Rover 1", 1));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(1, ActiveLines("GPS Rover 1"));
    }

    [Fact]
    public async Task Two_people_checking_out_at_the_same_moment_only_one_wins()
    {
        var results = await Task.WhenAll(
            Checkout("Ali", Due, ("Laptop", 1)),
            Checkout("Bilal", Due, ("Laptop", 1)));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, ActiveLines("Laptop"));
    }

    [Fact]
    public async Task Counted_item_cannot_go_below_zero_available()
    {
        await Checkout("Ali", Due, ("Tripod", 3));

        var tooMany = await Checkout("Bilal", Due, ("Tripod", 2));

        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        var lastOne = await Checkout("Bilal", Due, ("Tripod", 1));
        Assert.Equal(HttpStatusCode.Created, lastOne.StatusCode);
    }

    [Fact]
    public async Task Damaged_item_cannot_be_checked_out()
    {
        WithDb(db => db.Equipment.Where(e => e.Name == "Laptop")
            .ExecuteUpdate(s => s.SetProperty(e => e.Condition, EquipmentCondition.Damaged)));

        var response = await Checkout("Ali", Due, ("Laptop", 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Due_date_in_the_past_is_rejected()
    {
        var response = await Checkout("Ali", "2000-01-01", ("Laptop", 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, WithDb(db => db.Checkouts.Count()));
    }

    [Fact]
    public async Task If_one_item_in_a_kit_is_unavailable_nothing_is_checked_out()
    {
        await Checkout("Ali", Due, ("Laptop", 1));

        var kit = await Checkout("Bilal", Due, ("Total Station", 1), ("Laptop", 1));

        Assert.Equal(HttpStatusCode.Conflict, kit.StatusCode);
        Assert.Equal(0, ActiveLines("Total Station"));
    }

    [Fact]
    public async Task Item_that_needs_approval_is_requested_not_checked_out()
    {
        var response = await Checkout("Ali", Due, ("Drone", 1));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(LineStatus.PendingApproval, WithDb(db => db.CheckoutLines.Single().Status));
        Assert.Equal(HistoryEventType.ApprovalRequested, WithDb(db => db.HistoryEvents.Single().Type));
    }

    [Fact]
    public async Task A_pending_request_holds_the_item_for_everyone_else()
    {
        await Checkout("Ali", Due, ("Drone", 1));

        var second = await Checkout("Bilal", Due, ("Drone", 1));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(1, ActiveLines("Drone"));
    }

    [Fact]
    public async Task In_a_kit_only_the_approval_item_waits()
    {
        await Checkout("Ali", Due, ("Drone", 1), ("Laptop", 1));

        var lines = WithDb(db => db.CheckoutLines.Include(l => l.Equipment).ToList());
        Assert.Equal(LineStatus.PendingApproval, lines.Single(l => l.Equipment!.Name == "Drone").Status);
        Assert.Equal(LineStatus.Out, lines.Single(l => l.Equipment!.Name == "Laptop").Status);
    }

    [Fact]
    public async Task Empty_checkout_is_rejected()
    {
        var response = await Checkout("Ali", Due);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
