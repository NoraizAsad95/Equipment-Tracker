using System.Text.RegularExpressions;
using EquipmentTracker.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EquipmentTracker.Api.Services;

// Pin is the PIN of the Owner making the change. PersonPin is the PIN being set for this person
// (leave it empty when editing to keep the current one).
public record PersonRequest(string? Pin, string Name, PersonRole Role, string? PersonPin);

// Never contains the PIN or its hash.
public record PersonDto(int Id, string Name, string Role, bool HasPin);

public class PeopleService(AppDbContext db, AccessService access, IPasswordHasher<Person> hasher)
{
    public async Task<List<PersonDto>> ListAsync() =>
        (await db.People.OrderBy(p => p.Name).ToListAsync()).Select(ToDto).ToList();

    public async Task<PersonDto> CreateAsync(int? actorId, PersonRequest request)
    {
        await access.RequireOwnerAsync(actorId, request.Pin);

        var person = new Person();
        await ApplyAsync(person, request);
        db.People.Add(person);
        await db.SaveChangesAsync();
        return ToDto(person);
    }

    public async Task<PersonDto> UpdateAsync(int id, int? actorId, PersonRequest request)
    {
        await access.RequireOwnerAsync(actorId, request.Pin);

        var person = await db.People.FindAsync(id) ?? throw DomainException.NotFound("Person not found.");
        await ApplyAsync(person, request);
        await db.SaveChangesAsync();
        return ToDto(person);
    }

    private async Task ApplyAsync(Person person, PersonRequest request)
    {
        var name = request.Name.Trim();
        if (name.Length == 0 || name.Length > 100)
            throw DomainException.Invalid("Name is required and can be at most 100 characters.");
        if (await db.People.AnyAsync(p => p.Name == name && p.Id != person.Id))
            throw DomainException.Invalid($"A person named \"{name}\" already exists.");

        if (!string.IsNullOrEmpty(request.PersonPin))
        {
            if (!Regex.IsMatch(request.PersonPin, @"^\d{4,6}$"))
                throw DomainException.Invalid("A PIN must be 4 to 6 digits.");
            person.PinHash = hasher.HashPassword(person, request.PersonPin);
        }

        if (request.Role is PersonRole.Owner or PersonRole.Office && person.PinHash == null)
            throw DomainException.Invalid("Owner and Office people need a PIN.");

        // Do not lock everyone out of management.
        if (person.Id != 0 && person.Role == PersonRole.Owner && request.Role != PersonRole.Owner &&
            !await db.People.AnyAsync(p => p.Role == PersonRole.Owner && p.Id != person.Id))
            throw DomainException.Invalid("There must always be at least one Owner.");

        person.Name = name;
        person.Role = request.Role;
    }

    private static PersonDto ToDto(Person p) => new(p.Id, p.Name, p.Role.ToString(), p.PinHash != null);
}
