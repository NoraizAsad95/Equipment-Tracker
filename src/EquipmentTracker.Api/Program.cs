using System.Text.Json.Serialization;
using EquipmentTracker.Api.Data;
using EquipmentTracker.Api.Middleware;
using EquipmentTracker.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseSqlServer(sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher<Person>, PasswordHasher<Person>>();
builder.Services.AddSingleton<AvailabilityService>();
builder.Services.AddScoped<StatusService>();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddScoped<ReturnService>();
builder.Services.AddScoped<ApprovalService>();
builder.Services.AddScoped<RestoreService>();
builder.Services.AddScoped<HistoryService>();
builder.Services.AddScoped<AccessService>();
builder.Services.AddScoped<EquipmentService>();
builder.Services.AddScoped<PeopleService>();
// Lets requests use enum names like "Individual" or "Owner".
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    SeedData.Seed(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<Person>>());
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.Run();

public partial class Program;
