using System.Net.Http.Json;
using EquipmentTracker.Api.Data;
using EquipmentTracker.Api.Services;
using EquipmentTracker.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentTracker.Tests;

public class OverdueTests : IClassFixture<TrackerFactory>
{
    private static readonly DateOnly Today = PakistanTime.ToDate(DateTime.UtcNow);
    private readonly TrackerFactory _factory;
    private readonly HttpClient _client;

    public OverdueTests(TrackerFactory factory)
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

    private void WithDb(Action<AppDbContext> action)
    {
        using var scope = _factory.Services.CreateScope();
        action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private void Lend(string item, DateOnly due, LineStatus status = LineStatus.Out, int quantity = 1, int returned = 0) =>
        WithDb(db =>
        {
            db.Checkouts.Add(new Checkout
            {
                BorrowerId = db.People.Single(p => p.Name == "Ali").Id,
                CreatedAtUtc = DateTime.UtcNow,
                DueDate = due,
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

    private async Task<Holder> HolderOf(string item)
    {
        var items = await _client.GetFromJsonAsync<List<StatusItem>>("/api/status");
        return items!.Single(i => i.Name == item).Holders.Single();
    }

    [Fact]
    public async Task Item_past_its_due_date_is_overdue()
    {
        Lend("GPS Rover 1", Today.AddDays(-1));

        Assert.True((await HolderOf("GPS Rover 1")).Overdue);
    }

    [Fact]
    public async Task Item_due_today_is_not_overdue_yet()
    {
        Lend("GPS Rover 1", Today);

        Assert.False((await HolderOf("GPS Rover 1")).Overdue);
    }

    [Fact]
    public async Task Item_due_in_the_future_is_not_overdue()
    {
        Lend("GPS Rover 1", Today.AddDays(5));

        Assert.False((await HolderOf("GPS Rover 1")).Overdue);
    }

    [Fact]
    public async Task Pending_request_is_never_overdue()
    {
        Lend("Drone", Today.AddDays(-3), LineStatus.PendingApproval);

        Assert.False((await HolderOf("Drone")).Overdue);
    }

    [Fact]
    public async Task Counted_items_not_yet_returned_stay_overdue()
    {
        // 4 tablets were borrowed, 3 came back; the last one is late.
        Lend("Field Tablet", Today.AddDays(-2), quantity: 4, returned: 3);

        var holder = await HolderOf("Field Tablet");

        Assert.True(holder.Overdue);
        Assert.Equal(1, holder.Quantity);
    }

    [Fact]
    public async Task Returned_items_are_not_listed_so_they_are_not_overdue()
    {
        Lend("GPS Rover 1", Today.AddDays(-10), LineStatus.Returned);

        var items = await _client.GetFromJsonAsync<List<StatusItem>>("/api/status");

        Assert.Empty(items!.Single(i => i.Name == "GPS Rover 1").Holders);
    }
}
