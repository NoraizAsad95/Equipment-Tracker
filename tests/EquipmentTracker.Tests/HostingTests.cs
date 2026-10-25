using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EquipmentTracker.Tests.Infrastructure;

namespace EquipmentTracker.Tests;

public class HostingTests : IClassFixture<TrackerFactory>
{
    private readonly HttpClient _client;

    public HostingTests(TrackerFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Root_serves_the_mobile_friendly_page()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Equipment Tracker", html);
        Assert.Contains("name=\"viewport\"", html);
    }

    [Fact]
    public async Task Health_reports_database_reachable()
    {
        var json = await _client.GetFromJsonAsync<JsonElement>("/api/health");

        Assert.Equal("ok", json.GetProperty("status").GetString());
        Assert.True(json.GetProperty("database").GetBoolean());
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    public async Task Domain_exceptions_become_problem_details_with_the_same_status(int status)
    {
        var response = await _client.GetAsync($"/api/test-errors/{status}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(status, json.GetProperty("status").GetInt32());
        Assert.Equal("probe message", json.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Unknown_api_route_returns_problem_details()
    {
        var response = await _client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
