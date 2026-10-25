using System.Net;
using System.Net.Http.Json;
using EquipmentTracker.Api.Data;
using EquipmentTracker.Api.Services;
using EquipmentTracker.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentTracker.Tests;

public class RestoreTests : IClassFixture<TrackerFactory>
{
    private readonly TrackerFactory _factory;
    private readonly HttpClient _client;

    public RestoreTests(TrackerFactory factory)
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

    private int PersonId(string name) => WithDb(db => db.People.Single(p => p.Name == name).Id);
    private int ItemId(string name) => WithDb(db => db.Equipment.Single(e => e.Name == name).Id);
    private Equipment Item(string name) => WithDb(db => db.Equipment.Single(e => e.Name == name));

    private void Damage(string item, int damagedUnits = 0) =>
        WithDb(db => db.Equipment.Where(e => e.Name == item).ExecuteUpdate(s => s
            .SetProperty(e => e.Condition, damagedUnits == 0 ? EquipmentCondition.Damaged : EquipmentCondition.OK)
            .SetProperty(e => e.DamagedQuantity, damagedUnits)
            .SetProperty(e => e.ConditionNote, "Cracked")));

    private Task<HttpResponseMessage> Restore(string item, string? person, string? pin, int quantity = 1, string? note = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/equipment/{ItemId(item)}/restore")
        {
            Content = JsonContent.Create(new { pin, quantity, note })
        };
        if (person != null) request.Headers.Add("X-Person-Id", PersonId(person).ToString());
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task Office_person_can_restore_a_damaged_single_item()
    {
        Damage("Laptop");

        var response = await Restore("Laptop", "Saima", "2222", note: "Screen replaced");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var laptop = Item("Laptop");
        Assert.Equal(EquipmentCondition.OK, laptop.Condition);
        Assert.Null(laptop.ConditionNote);
        var history = WithDb(db => db.HistoryEvents.Single());
        Assert.Equal(HistoryEventType.Restored, history.Type);
        Assert.Equal(PersonId("Saima"), history.ActorId);
        Assert.Equal("Screen replaced", history.Note);
    }

    [Fact]
    public async Task Restored_item_can_be_checked_out_again()
    {
        Damage("Laptop");
        await Restore("Laptop", "Saima", "2222");

        var checkout = await _client.PostAsJsonAsync("/api/checkouts", new
        {
            borrowerId = PersonId("Ali"),
            dueDate = DateTime.UtcNow.AddDays(5).ToString("yyyy-MM-dd"),
            items = new[] { new { equipmentId = ItemId("Laptop"), quantity = 1 } }
        });

        Assert.Equal(HttpStatusCode.Created, checkout.StatusCode);
    }

    [Fact]
    public async Task Counted_items_can_be_restored_a_few_at_a_time()
    {
        Damage("Field Tablet", damagedUnits: 3);

        await Restore("Field Tablet", "Saima", "2222", quantity: 2);

        var tablets = Item("Field Tablet");
        Assert.Equal(1, tablets.DamagedQuantity);
        Assert.Equal(6, tablets.TotalQuantity);
        Assert.NotNull(tablets.ConditionNote); // one unit is still damaged

        await Restore("Field Tablet", "Saima", "2222", quantity: 1);

        tablets = Item("Field Tablet");
        Assert.Equal(0, tablets.DamagedQuantity);
        Assert.Null(tablets.ConditionNote);
        Assert.Equal(2, WithDb(db => db.HistoryEvents.Count()));
    }

    [Fact]
    public async Task Restoring_makes_the_units_available_again_on_the_screen()
    {
        Damage("Field Tablet", damagedUnits: 2);
        await Restore("Field Tablet", "Saima", "2222", quantity: 2);

        var items = await _client.GetFromJsonAsync<List<StatusItem>>("/api/status");

        Assert.Equal(6, items!.Single(i => i.Name == "Field Tablet").Available);
    }

    [Fact]
    public async Task Wrong_pin_is_refused_and_nothing_changes()
    {
        Damage("Laptop");

        var response = await Restore("Laptop", "Saima", "0000");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(EquipmentCondition.Damaged, Item("Laptop").Condition);
        Assert.Equal(0, WithDb(db => db.HistoryEvents.Count()));
    }

    [Fact]
    public async Task The_owner_can_also_restore()
    {
        Damage("Laptop");

        var response = await Restore("Laptop", "Tariq", "1111");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(EquipmentCondition.OK, Item("Laptop").Condition);
    }

    [Theory]
    [InlineData("Hamza", "3333")] // Staff approver
    [InlineData("Ali", "1234")]   // Staff without a PIN
    public async Task Staff_cannot_restore(string person, string pin)
    {
        Damage("Laptop");

        var response = await Restore("Laptop", person, pin);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(EquipmentCondition.Damaged, Item("Laptop").Condition);
    }

    [Fact]
    public async Task Missing_person_is_refused()
    {
        Damage("Laptop");

        Assert.Equal(HttpStatusCode.Forbidden, (await Restore("Laptop", null, "2222")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Restore("Laptop", "Saima", null)).StatusCode);
    }

    [Fact]
    public async Task Equipment_that_is_not_damaged_cannot_be_restored()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Restore("Laptop", "Saima", "2222")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Restore("Field Tablet", "Saima", "2222")).StatusCode);
        Assert.Equal(0, WithDb(db => db.HistoryEvents.Count()));
    }

    [Fact]
    public async Task Cannot_restore_more_units_than_are_damaged()
    {
        Damage("Field Tablet", damagedUnits: 1);

        var response = await Restore("Field Tablet", "Saima", "2222", quantity: 2);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1, Item("Field Tablet").DamagedQuantity);
    }

    [Fact]
    public async Task Unknown_equipment_gives_404()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/equipment/999999/restore")
        {
            Content = JsonContent.Create(new { pin = "2222", quantity = 1 })
        };
        request.Headers.Add("X-Person-Id", PersonId("Saima").ToString());

        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(request)).StatusCode);
    }
}
