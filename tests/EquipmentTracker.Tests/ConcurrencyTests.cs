using EquipmentTracker.Api.Data;
using EquipmentTracker.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentTracker.Tests;

// Two people acting on the same item at the same moment (AD-5).
public class ConcurrencyTests : IClassFixture<TrackerFactory>
{
    private readonly TrackerFactory _factory;

    public ConcurrencyTests(TrackerFactory factory) => _factory = factory;

    [Fact]
    public void Second_writer_of_the_same_equipment_row_is_rejected()
    {
        _ = _factory.Server; // starts the app so the database exists
        using var scopeA = _factory.Services.CreateScope();
        using var scopeB = _factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();

        // Both load the drone before either saves.
        var droneA = dbA.Equipment.Single(e => e.Name == "Drone");
        var droneB = dbB.Equipment.Single(e => e.Name == "Drone");

        droneA.ConditionNote = "first";
        dbA.SaveChanges();

        droneB.ConditionNote = "second";
        Assert.Throws<DbUpdateConcurrencyException>(() => dbB.SaveChanges());
    }
}
