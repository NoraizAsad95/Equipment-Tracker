using EquipmentTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Api.Controllers;

[ApiController]
[Route("api/people")]
public class PeopleController(PeopleService people) : ControllerBase
{
    // Open to everyone: the name pickers need it. Never includes PINs.
    [HttpGet]
    public async Task<List<PersonDto>> List() => await people.ListAsync();

    // Owner only: send your id in X-Person-Id and your PIN in the body (AD-6).
    [HttpPost]
    public async Task<IActionResult> Create([FromHeader(Name = "X-Person-Id")] int? actorId, PersonRequest request)
    {
        var created = await people.CreateAsync(actorId, request);
        return Created($"/api/people/{created.Id}", created);
    }

    [HttpPut("{id:int}")]
    public async Task<PersonDto> Update(int id, [FromHeader(Name = "X-Person-Id")] int? actorId, PersonRequest request) =>
        await people.UpdateAsync(id, actorId, request);
}
