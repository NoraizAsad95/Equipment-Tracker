using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EquipmentTracker.Api.Data;
using EquipmentTracker.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentTracker.Tests;

public class ManagementTests : IClassFixture<TrackerFactory>
{
    private readonly TrackerFactory _factory;
    private readonly HttpClient _client;

    public ManagementTests(TrackerFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ---- helpers ----

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..14];

    private T WithDb<T>(Func<AppDbContext, T> query)
    {
        using var scope = _factory.Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private int IdOf(string person) => WithDb(db => db.People.Single(p => p.Name == person).Id);

    private async Task<HttpResponseMessage> Send(HttpMethod method, string url, object? body, string actor = "Tariq")
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Person-Id", IdOf(actor).ToString());
        if (body != null)
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return await _client.SendAsync(request);
    }

    private static object Equip(string name, string mode = "Individual", int total = 1,
        bool approval = false, int? approverId = null, string pin = SeedData.TariqPin) =>
        new { pin, name, trackingMode = mode, totalQuantity = total, requiresApproval = approval, approverId };

    private static object Person(string name, string role = "Staff", string? personPin = null, string pin = SeedData.TariqPin) =>
        new { pin, name, role, personPin };

    private async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<string> Detail(HttpResponseMessage response) =>
        (await Json(response)).GetProperty("detail").GetString()!;

    private int EquipmentCount() => WithDb(db => db.Equipment.Count());
    private int PeopleCount() => WithDb(db => db.People.Count());

    // ---- equipment: what the Owner can do ----

    [Fact]
    public async Task Owner_adds_an_individual_item_and_it_appears_in_the_list()
    {
        var name = Unique("Total Stn");

        var response = await Send(HttpMethod.Post, "/api/equipment", Equip(name));
        var list = await _client.GetFromJsonAsync<JsonElement>("/api/equipment");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains(list.EnumerateArray(), e => e.GetProperty("name").GetString() == name);
    }

    [Fact]
    public async Task Owner_adds_a_counted_item_with_its_total()
    {
        var response = await Send(HttpMethod.Post, "/api/equipment", Equip(Unique("Prism"), "Quantity", total: 8));
        var json = await Json(response);

        Assert.Equal("Quantity", json.GetProperty("trackingMode").GetString());
        Assert.Equal(8, json.GetProperty("totalQuantity").GetInt32());
    }

    [Fact]
    public async Task Individual_items_always_have_a_total_of_one()
    {
        var response = await Send(HttpMethod.Post, "/api/equipment", Equip(Unique("Level"), total: 5));

        Assert.Equal(1, (await Json(response)).GetProperty("totalQuantity").GetInt32());
    }

    [Fact]
    public async Task Owner_can_require_approval_with_a_named_approver()
    {
        var hamza = IdOf("Hamza");

        var response = await Send(HttpMethod.Post, "/api/equipment", Equip(Unique("Rover 3"), approval: true, approverId: hamza));
        var json = await Json(response);

        Assert.True(json.GetProperty("requiresApproval").GetBoolean());
        Assert.Equal(hamza, json.GetProperty("approverId").GetInt32());
    }

    [Fact]
    public async Task Turning_approval_off_clears_the_approver()
    {
        var created = await Json(await Send(HttpMethod.Post, "/api/equipment",
            Equip(Unique("Rover 4"), approval: true, approverId: IdOf("Hamza"))));
        var id = created.GetProperty("id").GetInt32();

        var response = await Send(HttpMethod.Put, $"/api/equipment/{id}", Equip(created.GetProperty("name").GetString()!));
        var json = await Json(response);

        Assert.False(json.GetProperty("requiresApproval").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("approverId").ValueKind);
    }

    [Fact]
    public async Task Owner_can_rename_and_change_approval_on_the_new_rover_without_touching_the_old_one()
    {
        var oldRover = WithDb(db => db.Equipment.Single(e => e.Name == "GPS Rover 1"));
        var newRover = WithDb(db => db.Equipment.Single(e => e.Name == "GPS Rover 2 (new)"));

        var response = await Send(HttpMethod.Put, $"/api/equipment/{newRover.Id}",
            Equip("GPS Rover 2 (new)", approval: true, approverId: IdOf("Tariq")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(WithDb(db => db.Equipment.Single(e => e.Id == oldRover.Id)).RequiresApproval);
        Assert.True(WithDb(db => db.Equipment.Single(e => e.Id == newRover.Id)).RequiresApproval);
    }

    // ---- equipment: rules ----

    [Fact]
    public async Task Approver_must_have_a_pin()
    {
        var response = await Send(HttpMethod.Post, "/api/equipment", Equip(Unique("Rover 5"), approval: true, approverId: IdOf("Bilal")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("no PIN", await Detail(response));
    }

    [Fact]
    public async Task Approval_needs_an_approver()
    {
        var response = await Send(HttpMethod.Post, "/api/equipment", Equip(Unique("Rover 6"), approval: true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Approval_cannot_be_required_for_counted_items()
    {
        var response = await Send(HttpMethod.Post, "/api/equipment",
            Equip(Unique("Cones"), "Quantity", total: 5, approval: true, approverId: IdOf("Hamza")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_item_names_are_rejected_ignoring_case()
    {
        var response = await Send(HttpMethod.Post, "/api/equipment", Equip("drone"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Counted_item_needs_a_total_of_at_least_one()
    {
        var response = await Send(HttpMethod.Post, "/api/equipment", Equip(Unique("Pole"), "Quantity", total: 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Tracking_mode_cannot_be_changed_after_creation()
    {
        var drone = WithDb(db => db.Equipment.Single(e => e.Name == "Drone"));

        var response = await Send(HttpMethod.Put, $"/api/equipment/{drone.Id}", Equip("Drone", "Quantity", total: 3));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Total_cannot_drop_below_the_units_that_are_checked_out()
    {
        var name = Unique("Batteries");
        var id = (await Json(await Send(HttpMethod.Post, "/api/equipment", Equip(name, "Quantity", total: 6)))).GetProperty("id").GetInt32();
        WithDb(db =>
        {
            db.Checkouts.Add(new Checkout
            {
                BorrowerId = db.People.Single(p => p.Name == "Ali").Id,
                CreatedAtUtc = DateTime.UtcNow,
                DueDate = new DateOnly(2030, 1, 1),
                Lines = { new CheckoutLine { EquipmentId = id, Quantity = 4, Status = LineStatus.Out } }
            });
            return db.SaveChanges();
        });

        var tooLow = await Send(HttpMethod.Put, $"/api/equipment/{id}", Equip(name, "Quantity", total: 3));
        var enough = await Send(HttpMethod.Put, $"/api/equipment/{id}", Equip(name, "Quantity", total: 4));

        Assert.Equal(HttpStatusCode.BadRequest, tooLow.StatusCode);
        Assert.Equal(HttpStatusCode.OK, enough.StatusCode);
    }

    [Fact]
    public async Task Editing_unknown_equipment_returns_404()
    {
        var response = await Send(HttpMethod.Put, "/api/equipment/999999", Equip("Ghost"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- access: who is NOT allowed ----

    [Fact]
    public async Task Wrong_pin_is_refused_and_nothing_changes()
    {
        var before = EquipmentCount();

        var response = await Send(HttpMethod.Post, "/api/equipment", Equip(Unique("Nope"), pin: "9999"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, EquipmentCount());
    }

    [Theory]
    [InlineData("Saima", SeedData.SaimaPin)]   // Office
    [InlineData("Hamza", SeedData.HamzaPin)]   // Staff approver
    [InlineData("Bilal", "1234")]              // Staff with no PIN at all
    public async Task Only_the_owner_can_manage_even_with_a_correct_pin(string actor, string pin)
    {
        var equipmentBefore = EquipmentCount();
        var peopleBefore = PeopleCount();

        var addEquipment = await Send(HttpMethod.Post, "/api/equipment", Equip(Unique("Nope"), pin: pin), actor);
        var addPerson = await Send(HttpMethod.Post, "/api/people", Person(Unique("Nobody"), pin: pin), actor);

        Assert.Equal(HttpStatusCode.Forbidden, addEquipment.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, addPerson.StatusCode);
        Assert.Equal(equipmentBefore, EquipmentCount());
        Assert.Equal(peopleBefore, PeopleCount());
    }

    [Fact]
    public async Task Missing_person_header_or_pin_is_refused()
    {
        var noHeader = await _client.PostAsJsonAsync("/api/equipment", Equip(Unique("Nope")));
        var noPin = await Send(HttpMethod.Post, "/api/equipment", new { name = Unique("Nope"), trackingMode = "Individual", totalQuantity = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, noHeader.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, noPin.StatusCode);
    }

    [Fact]
    public async Task A_refused_edit_leaves_the_item_unchanged()
    {
        var drone = WithDb(db => db.Equipment.Single(e => e.Name == "Drone"));

        var response = await Send(HttpMethod.Put, $"/api/equipment/{drone.Id}", Equip("Renamed Drone", approval: true, approverId: IdOf("Hamza")), actor: "Saima");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Drone", WithDb(db => db.Equipment.Single(e => e.Id == drone.Id)).Name);
    }

    // ---- people ----

    [Fact]
    public async Task Owner_adds_a_staff_member_without_a_pin()
    {
        var name = Unique("Kamran");

        var response = await Send(HttpMethod.Post, "/api/people", Person(name));
        var json = await Json(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Staff", json.GetProperty("role").GetString());
        Assert.False(json.GetProperty("hasPin").GetBoolean());
    }

    [Fact]
    public async Task Office_and_owner_people_must_have_a_pin()
    {
        var office = await Send(HttpMethod.Post, "/api/people", Person(Unique("Clerk"), "Office"));
        var owner = await Send(HttpMethod.Post, "/api/people", Person(Unique("Boss"), "Owner"));

        Assert.Equal(HttpStatusCode.BadRequest, office.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, owner.StatusCode);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("1234567")]
    [InlineData("abcd")]
    public async Task Pin_must_be_four_to_six_digits(string badPin)
    {
        var response = await Send(HttpMethod.Post, "/api/people", Person(Unique("Zed"), personPin: badPin));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_person_names_are_rejected()
    {
        var response = await Send(HttpMethod.Post, "/api/people", Person("saima"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Pins_are_stored_hashed_and_never_returned()
    {
        var name = Unique("Faisal");

        var created = await Send(HttpMethod.Post, "/api/people", Person(name, "Staff", "4321"));
        var createdBody = await created.Content.ReadAsStringAsync();
        var listBody = await _client.GetStringAsync("/api/people");

        var stored = WithDb(db => db.People.Single(p => p.Name == name));
        Assert.NotEqual("4321", stored.PinHash);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<Person>().VerifyHashedPassword(stored, stored.PinHash!, "4321"));
        Assert.DoesNotContain(stored.PinHash!, createdBody);
        Assert.DoesNotContain(stored.PinHash!, listBody);
        Assert.DoesNotContain("4321", listBody);
        Assert.DoesNotContain("pinHash", listBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Blank_pin_on_edit_keeps_the_current_one_and_a_new_pin_replaces_it()
    {
        var name = Unique("Owner2");
        var id = (await Json(await Send(HttpMethod.Post, "/api/people", Person(name, "Owner", "5555")))).GetProperty("id").GetInt32();
        var originalHash = WithDb(db => db.People.Single(p => p.Id == id)).PinHash;

        // Edit without a new PIN: hash is untouched and the old PIN still works.
        await Send(HttpMethod.Put, $"/api/people/{id}", Person(name, "Owner", null, pin: "5555"), actor: name);
        Assert.Equal(originalHash, WithDb(db => db.People.Single(p => p.Id == id)).PinHash);

        // Change the PIN: the old one stops working, the new one works.
        await Send(HttpMethod.Put, $"/api/people/{id}", Person(name, "Owner", "6666", pin: "5555"), actor: name);
        var oldPin = await Send(HttpMethod.Post, "/api/equipment", Equip(Unique("A"), pin: "5555"), actor: name);
        var newPin = await Send(HttpMethod.Post, "/api/equipment", Equip(Unique("B"), pin: "6666"), actor: name);

        Assert.Equal(HttpStatusCode.Forbidden, oldPin.StatusCode);
        Assert.Equal(HttpStatusCode.Created, newPin.StatusCode);
    }

    [Fact]
    public async Task The_last_owner_cannot_be_demoted()
    {
        // Own database, so other tests that add owners cannot affect this one.
        using var factory = new TrackerFactory();
        var client = factory.CreateClient();
        int tariq;
        using (var scope = factory.Services.CreateScope())
            tariq = scope.ServiceProvider.GetRequiredService<AppDbContext>().People.Single(p => p.Name == "Tariq").Id;

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/people/{tariq}");
        request.Headers.Add("X-Person-Id", tariq.ToString());
        request.Content = new StringContent(JsonSerializer.Serialize(Person("Tariq", "Staff")), Encoding.UTF8, "application/json");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var check = factory.Services.CreateScope();
        Assert.Equal(PersonRole.Owner, check.ServiceProvider.GetRequiredService<AppDbContext>().People.Single(p => p.Id == tariq).Role);
    }

    [Fact]
    public async Task Editing_unknown_person_returns_404()
    {
        var response = await Send(HttpMethod.Put, "/api/people/999999", Person("Ghost"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
