using EquipmentTracker.Api.Data;
using EquipmentTracker.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentTracker.Tests.Infrastructure;

/// <summary>Runs the real app against its own throwaway LocalDB database, dropped on dispose.</summary>
public class TrackerFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"EquipmentTracker_Test_{Guid.NewGuid():N}";
    private bool _dropped;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default",
            $"Server=(localdb)\\MSSQLLocalDB;Database={_dbName};Trusted_Connection=True;TrustServerCertificate=True");
        // Lets tests reach the error-shape controller below.
        builder.ConfigureServices(s => s.AddControllers().AddApplicationPart(typeof(ErrorProbeController).Assembly));
    }

    protected override void Dispose(bool disposing)
    {
        // The base class re-enters Dispose while tearing down, by which point services are gone.
        if (disposing && !_dropped)
        {
            _dropped = true;
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeleted();
        }
        base.Dispose(disposing);
    }
}

/// <summary>Test-only endpoint that throws a DomainException with the requested status.</summary>
[ApiController]
[Route("api/test-errors")]
public class ErrorProbeController : ControllerBase
{
    [HttpGet("{status:int}")]
    public IActionResult Get(int status) => throw new DomainException(status, "probe message");
}
