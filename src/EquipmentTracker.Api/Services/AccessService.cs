using EquipmentTracker.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace EquipmentTracker.Api.Services;

// Checks who is acting and whether their PIN is right (AD-6). There are no sessions:
// every protected request carries the person id (header) and their PIN (body).
public class AccessService(AppDbContext db, IPasswordHasher<Person> hasher)
{
    public async Task<Person> RequireOwnerAsync(int? personId, string? pin)
    {
        var person = await VerifyPinAsync(personId, pin);
        if (person.Role != PersonRole.Owner) throw Denied();
        return person;
    }

    // Restoring damaged equipment and reading history are for the Office or the Owner (FR-21, FR-26).
    public async Task<Person> RequireOfficeOrOwnerAsync(int? personId, string? pin)
    {
        var person = await VerifyPinAsync(personId, pin);
        if (person.Role == PersonRole.Staff) throw Denied();
        return person;
    }

    // Only the person named as the item's approver, with the right PIN, may decide (D3).
    public async Task<Person> RequireApproverAsync(int? personId, string? pin, Equipment equipment)
    {
        var person = await VerifyPinAsync(personId, pin);
        if (equipment.ApproverId != person.Id) throw Denied();
        return person;
    }

    private async Task<Person> VerifyPinAsync(int? personId, string? pin)
    {
        if (personId == null || string.IsNullOrEmpty(pin)) throw Denied();

        var person = await db.People.FindAsync(personId.Value);
        if (person?.PinHash == null) throw Denied();

        var result = hasher.VerifyHashedPassword(person, person.PinHash, pin);
        if (result == PasswordVerificationResult.Failed) throw Denied();
        return person;
    }

    // One message for every failure, so it does not reveal which part was wrong.
    private static DomainException Denied() =>
        DomainException.Forbidden("Wrong person or PIN, or you are not allowed to do this.");
}
