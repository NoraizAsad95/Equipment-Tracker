using EquipmentTracker.Api.Data;
using EquipmentTracker.Api.Services;

namespace EquipmentTracker.Tests;

// Pure rule tests: no database needed.
public class AvailabilityServiceTests
{
    private readonly AvailabilityService _availability = new();

    private static Equipment Tablets(int total = 6, int damaged = 0) =>
        new() { Name = "Field Tablet", TrackingMode = TrackingMode.Quantity, TotalQuantity = total, DamagedQuantity = damaged };

    private static Equipment Rover() => new() { Name = "GPS Rover 1" };

    private static CheckoutLine Line(LineStatus status, int quantity = 1, int returned = 0) =>
        new() { Status = status, Quantity = quantity, ReturnedQuantity = returned };

    // ---- Available quantity never goes below zero ----

    [Fact]
    public void Available_is_zero_when_all_usable_units_are_held()
    {
        // 6 tablets, 1 damaged, 5 held => 0
        Assert.Equal(0, _availability.Available(Tablets(damaged: 1), [Line(LineStatus.Out, 5)]));
    }

    [Fact]
    public void Available_is_clamped_to_zero_when_more_is_held_than_usable()
    {
        // 6 tablets, 2 later reported damaged while 5 are still out => would be -1
        Assert.Equal(0, _availability.Available(Tablets(damaged: 2), [Line(LineStatus.Out, 5)]));
    }

    [Fact]
    public void Pending_requests_hold_their_full_quantity()
    {
        Assert.Equal(3, _availability.Available(Tablets(), [Line(LineStatus.PendingApproval, 3)]));
    }

    // ---- An individual item can only be with one person at a time ----

    [Fact]
    public void Free_individual_item_can_be_taken()
    {
        _availability.EnsureCanTake(Rover(), []);
    }

    [Theory]
    [InlineData(LineStatus.Out)]
    [InlineData(LineStatus.PendingApproval)]
    public void Individual_item_already_out_or_held_cannot_be_taken_again(LineStatus existing)
    {
        var error = Assert.Throws<DomainException>(() => _availability.EnsureCanTake(Rover(), [Line(existing)]));

        Assert.Equal(409, error.StatusCode);
    }

    [Fact]
    public void Damaged_individual_item_cannot_be_taken()
    {
        var damaged = Rover();
        damaged.Condition = EquipmentCondition.Damaged;

        var error = Assert.Throws<DomainException>(() => _availability.EnsureCanTake(damaged, []));

        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public void Individual_item_must_be_taken_one_at_a_time()
    {
        Assert.Throws<DomainException>(() => _availability.EnsureCanTake(Rover(), [], quantity: 2));
    }

    // ---- Quantity items ----

    [Fact]
    public void Quantity_item_can_be_taken_up_to_the_available_amount()
    {
        _availability.EnsureCanTake(Tablets(), [Line(LineStatus.Out, 2)], quantity: 4);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(0)]
    public void Quantity_item_cannot_be_taken_beyond_available_or_below_one(int quantity)
    {
        var error = Assert.Throws<DomainException>(
            () => _availability.EnsureCanTake(Tablets(), [Line(LineStatus.Out, 2)], quantity));

        Assert.Equal(400, error.StatusCode);
    }
}
