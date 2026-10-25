namespace EquipmentTracker.Api.Data;

public class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public PersonRole Role { get; set; }
    // Only people who approve, restore or manage need a PIN (AD-6).
    public string? PinHash { get; set; }
}

public class Equipment
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public TrackingMode TrackingMode { get; set; }
    // Individual items always have a total of 1.
    public int TotalQuantity { get; set; } = 1;
    public int DamagedQuantity { get; set; }
    public EquipmentCondition Condition { get; set; } = EquipmentCondition.OK;
    public string? ConditionNote { get; set; }
    public bool RequiresApproval { get; set; }
    public int? ApproverId { get; set; }
    public Person? Approver { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public class Checkout
{
    public int Id { get; set; }
    public int BorrowerId { get; set; }
    public Person? Borrower { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateOnly DueDate { get; set; }
    public List<CheckoutLine> Lines { get; set; } = [];
}

public class CheckoutLine
{
    public int Id { get; set; }
    public int CheckoutId { get; set; }
    public Checkout? Checkout { get; set; }
    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }
    public int Quantity { get; set; } = 1;
    public int ReturnedQuantity { get; set; }
    public LineStatus Status { get; set; }
    public int? DecidedById { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
}

// Append-only (AD-2): never updated or deleted.
public class HistoryEvent
{
    public int Id { get; set; }
    public HistoryEventType Type { get; set; }
    public DateTime AtUtc { get; set; }
    public int ActorId { get; set; }
    public Person? Actor { get; set; }
    public int? EquipmentId { get; set; }
    public int? CheckoutId { get; set; }
    public int? CheckoutLineId { get; set; }
    public int? Quantity { get; set; }
    public EquipmentCondition? Condition { get; set; }
    public string? Note { get; set; }
    public int? CorrectsEventId { get; set; }
}
