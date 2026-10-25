using Microsoft.AspNetCore.Identity;

namespace EquipmentTracker.Api.Data;

public static class SeedData
{
    // Demo PINs (hashed before storing). Only Owner, Office and approvers need one.
    public const string TariqPin = "1111";
    public const string SaimaPin = "2222";
    public const string HamzaPin = "3333";

    public static void Seed(AppDbContext db, IPasswordHasher<Person> hasher)
    {
        if (db.People.Any() || db.Equipment.Any()) return;

        Person Make(string name, PersonRole role, string? pin = null)
        {
            var person = new Person { Name = name, Role = role };
            if (pin != null) person.PinHash = hasher.HashPassword(person, pin);
            return person;
        }

        var tariq = Make("Tariq", PersonRole.Owner, TariqPin);
        var saima = Make("Saima", PersonRole.Office, SaimaPin);
        var hamza = Make("Hamza", PersonRole.Staff, HamzaPin);
        db.People.AddRange(tariq, saima, hamza,
            Make("Bilal", PersonRole.Staff),
            Make("Ali", PersonRole.Staff),
            Make("Imran", PersonRole.Staff));
        db.SaveChanges();

        db.Equipment.AddRange(
            new Equipment { Name = "Drone", RequiresApproval = true, ApproverId = hamza.Id },
            new Equipment { Name = "GPS Rover 1" },
            new Equipment { Name = "GPS Rover 2 (new)" },
            new Equipment { Name = "Total Station" },
            new Equipment { Name = "Laptop" },
            new Equipment { Name = "Tripod", TrackingMode = TrackingMode.Quantity, TotalQuantity = 4 },
            new Equipment { Name = "Field Tablet", TrackingMode = TrackingMode.Quantity, TotalQuantity = 6 });
        db.SaveChanges();
    }
}
