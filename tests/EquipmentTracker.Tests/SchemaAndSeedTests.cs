using EquipmentTracker.Api.Data;
using EquipmentTracker.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentTracker.Tests;

public class SchemaAndSeedTests : IClassFixture<TrackerFactory>
{
    private readonly TrackerFactory _factory;

    public SchemaAndSeedTests(TrackerFactory factory) => _factory = factory;

    private T WithDb<T>(Func<AppDbContext, T> query)
    {
        _ = _factory.Server; // starts the app, which migrates and seeds
        using var scope = _factory.Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public void Startup_migrates_and_seeds_people_with_roles()
    {
        var people = WithDb(db => db.People.ToDictionary(p => p.Name));

        Assert.Equal(6, people.Count);
        Assert.Equal(PersonRole.Owner, people["Tariq"].Role);
        Assert.Equal(PersonRole.Office, people["Saima"].Role);
        Assert.Equal(PersonRole.Staff, people["Hamza"].Role);
    }

    [Fact]
    public void Seeded_pins_are_stored_hashed_and_verify()
    {
        var hamza = WithDb(db => db.People.Single(p => p.Name == "Hamza"));
        var bilal = WithDb(db => db.People.Single(p => p.Name == "Bilal"));
        var hasher = new PasswordHasher<Person>();

        Assert.NotNull(hamza.PinHash);
        Assert.NotEqual(SeedData.HamzaPin, hamza.PinHash);
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(hamza, hamza.PinHash!, SeedData.HamzaPin));
        Assert.Null(bilal.PinHash);
    }

    [Fact]
    public void Drone_requires_approval_from_Hamza_and_new_rover_does_not()
    {
        var (drone, rover2, hamzaId) = WithDb(db => (
            db.Equipment.Single(e => e.Name == "Drone"),
            db.Equipment.Single(e => e.Name == "GPS Rover 2 (new)"),
            db.People.Single(p => p.Name == "Hamza").Id));

        Assert.True(drone.RequiresApproval);
        Assert.Equal(hamzaId, drone.ApproverId);
        Assert.False(rover2.RequiresApproval);
        Assert.Null(rover2.ApproverId);
    }

    [Fact]
    public void Quantity_items_have_counts_and_individual_items_have_one_unit()
    {
        var equipment = WithDb(db => db.Equipment.ToDictionary(e => e.Name));

        Assert.Equal(TrackingMode.Quantity, equipment["Tripod"].TrackingMode);
        Assert.Equal(4, equipment["Tripod"].TotalQuantity);
        Assert.Equal(TrackingMode.Quantity, equipment["Field Tablet"].TrackingMode);
        Assert.Equal(6, equipment["Field Tablet"].TotalQuantity);
        Assert.Equal(TrackingMode.Individual, equipment["Drone"].TrackingMode);
        Assert.Equal(1, equipment["Drone"].TotalQuantity);
        Assert.All(equipment.Values, e => Assert.Equal(EquipmentCondition.OK, e.Condition));
    }

    [Fact]
    public void Equipment_has_a_row_version_for_optimistic_concurrency()
    {
        var drone = WithDb(db => db.Equipment.Single(e => e.Name == "Drone"));

        Assert.Equal(8, drone.RowVersion.Length);
    }

    [Fact]
    public void Enums_are_stored_as_strings()
    {
        var mode = WithDb(db => db.Database
            .SqlQueryRaw<string>("SELECT TrackingMode AS [Value] FROM Equipment WHERE Name = 'Tripod'")
            .Single());

        Assert.Equal("Quantity", mode);
    }
}
