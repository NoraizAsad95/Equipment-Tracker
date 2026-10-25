using System.Net;
using System.Net.Http.Json;
using EquipmentTracker.Api.Data;
using EquipmentTracker.Api.Services;
using EquipmentTracker.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentTracker.Tests;

public class ApprovalTests : IClassFixture<TrackerFactory>
{
    private readonly TrackerFactory _factory;
    private readonly HttpClient _client;

    public ApprovalTests(TrackerFactory factory)
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

    // Ali asks for the Drone (it needs Hamza's approval); returns the pending line's id.
    private async Task<int> RequestDrone()
    {
        var response = await _client.PostAsJsonAsync("/api/checkouts", new
        {
            borrowerId = PersonId("Ali"),
            dueDate = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-dd"),
            items = new[] { new { equipmentId = WithDb(db => db.Equipment.Single(e => e.Name == "Drone").Id), quantity = 1 } }
        });
        response.EnsureSuccessStatusCode();
        return WithDb(db => db.CheckoutLines.Single().Id);
    }

    private Task<HttpResponseMessage> Decide(int lineId, string action, string? person, string? pin, string? reason = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/checkouts/lines/{lineId}/{action}")
        {
            Content = JsonContent.Create(new { pin, reason })
        };
        if (person != null) request.Headers.Add("X-Person-Id", PersonId(person).ToString());
        return _client.SendAsync(request);
    }

    private CheckoutLine Line(int id) => WithDb(db => db.CheckoutLines.Single(l => l.Id == id));

    [Fact]
    public async Task Approver_can_approve_and_the_item_goes_out()
    {
        var line = await RequestDrone();

        var response = await Decide(line, "approve", "Hamza", "3333");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = Line(line);
        Assert.Equal(LineStatus.Out, after.Status);
        Assert.Equal(PersonId("Hamza"), after.DecidedById);
        Assert.NotNull(after.DecidedAtUtc);
        var approved = WithDb(db => db.HistoryEvents.Single(h => h.Type == HistoryEventType.Approved));
        Assert.Equal(PersonId("Hamza"), approved.ActorId);
    }

    [Fact]
    public async Task Approved_item_shows_as_checked_out_and_can_be_returned()
    {
        var line = await RequestDrone();
        await Decide(line, "approve", "Hamza", "3333");

        var items = await _client.GetFromJsonAsync<List<StatusItem>>("/api/status");
        Assert.Equal("CheckedOut", items!.Single(i => i.Name == "Drone").Status);

        var returned = await _client.PostAsJsonAsync($"/api/checkouts/lines/{line}/return", new { quantity = 1 });
        Assert.Equal(HttpStatusCode.NoContent, returned.StatusCode);
    }

    [Fact]
    public async Task Rejecting_records_the_reason_and_frees_the_item()
    {
        var line = await RequestDrone();

        var response = await Decide(line, "reject", "Hamza", "3333", "Needed for a site visit");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = Line(line);
        Assert.Equal(LineStatus.Rejected, after.Status);
        Assert.Equal("Needed for a site visit", after.RejectionReason);
        var rejected = WithDb(db => db.HistoryEvents.Single(h => h.Type == HistoryEventType.Rejected));
        Assert.Equal("Needed for a site visit", rejected.Note);

        var items = await _client.GetFromJsonAsync<List<StatusItem>>("/api/status");
        Assert.Equal("Available", items!.Single(i => i.Name == "Drone").Status);
    }

    [Fact]
    public async Task Rejection_reason_is_optional()
    {
        var line = await RequestDrone();

        var response = await Decide(line, "reject", "Hamza", "3333");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(Line(line).RejectionReason);
    }

    [Fact]
    public async Task Wrong_pin_is_refused_and_nothing_changes()
    {
        var line = await RequestDrone();

        var response = await Decide(line, "approve", "Hamza", "0000");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(LineStatus.PendingApproval, Line(line).Status);
    }

    [Fact]
    public async Task Someone_who_is_not_the_approver_is_refused_even_with_a_correct_pin()
    {
        var line = await RequestDrone();

        // Tariq is the Owner with a valid PIN, but Hamza is this item's approver.
        var response = await Decide(line, "approve", "Tariq", "1111");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(LineStatus.PendingApproval, Line(line).Status);
    }

    [Fact]
    public async Task Missing_person_or_pin_is_refused()
    {
        var line = await RequestDrone();

        Assert.Equal(HttpStatusCode.Forbidden, (await Decide(line, "approve", null, "3333")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Decide(line, "approve", "Hamza", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Decide(line, "reject", "Ali", "1234")).StatusCode);
        Assert.Equal(LineStatus.PendingApproval, Line(line).Status);
    }

    [Fact]
    public async Task A_request_can_only_be_decided_once()
    {
        var line = await RequestDrone();
        await Decide(line, "approve", "Hamza", "3333");

        var again = await Decide(line, "reject", "Hamza", "3333");

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(LineStatus.Out, Line(line).Status);
        Assert.Equal(2, WithDb(db => db.HistoryEvents.Count())); // requested + approved only
    }

    [Fact]
    public async Task Unknown_request_gives_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Decide(999999, "approve", "Hamza", "3333")).StatusCode);
    }
}
