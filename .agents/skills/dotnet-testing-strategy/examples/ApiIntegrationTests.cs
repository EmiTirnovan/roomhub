using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Features.Events.CreateEvent;
using MyApp.Features.Events.GetEventById;
using MyApp.Features.Events.PublishEvent;
using MyApp.Infrastructure.Persistence;
using Xunit;

namespace MyApp.Tests.Integration.Api;

public sealed class CampusHubApiFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace existing DbContext registration with an in-memory SQLite connection
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CampusHubDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<CampusHubDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CampusHubDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection?.Dispose();
    }
}

public sealed class EventsApiIntegrationTests : IClassFixture<CampusHubApiFactory>
{
    private readonly HttpClient _client;

    public EventsApiIntegrationTests(CampusHubApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Post_CreateEvent_WithValidData_Returns201CreatedAndPersistsEvent()
    {
        // Arrange
        var request = new CreateEventRequest(
            Title: "Modern .NET Workshop",
            Description: "Hands-on Minimal API & Domain architecture",
            ScheduledAtUtc: DateTimeOffset.UtcNow.AddDays(7),
            TicketPrice: 0m,
            Currency: "EUR",
            MaxCapacity: 80);

        // Act
        var response = await _client.PostAsJsonAsync("/api/events", request);

        // Assert: Status Code & Location header
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var payload = await response.Content.ReadFromJsonAsync<CreateEventResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Modern .NET Workshop", payload.Title);
        Assert.Equal("Draft", payload.Status);
    }

    [Fact]
    public async Task Post_CreateEvent_WithEmptyTitle_Returns400BadRequest()
    {
        // Arrange
        var request = new CreateEventRequest(
            Title: "",
            Description: "Invalid event without title",
            ScheduledAtUtc: DateTimeOffset.UtcNow.AddDays(7),
            TicketPrice: 10m,
            Currency: "EUR",
            MaxCapacity: 50);

        // Act
        var response = await _client.PostAsJsonAsync("/api/events", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_GetEventById_WhenDoesNotExist_Returns404NotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/events/{nonExistentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_PublishEvent_WhenValidDraft_Returns200OK_AndWhenRepeated_Returns409Conflict()
    {
        // Step 1: Create a draft event
        var createRequest = new CreateEventRequest(
            Title: "Publishable Summit",
            Description: "Desc",
            ScheduledAtUtc: DateTimeOffset.UtcNow.AddDays(10),
            TicketPrice: 20m,
            Currency: "EUR",
            MaxCapacity: 100);

        var createRes = await _client.PostAsJsonAsync("/api/events", createRequest);
        var created = await createRes.Content.ReadFromJsonAsync<CreateEventResponse>();
        Assert.NotNull(created);

        // Step 2: Publish event -> 200 OK
        var publishRes = await _client.PostAsync($"/api/events/{created.Id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishRes.StatusCode);

        var publishedPayload = await publishRes.Content.ReadFromJsonAsync<PublishEventResponse>();
        Assert.NotNull(publishedPayload);
        Assert.Equal("Published", publishedPayload.Status);

        // Step 3: Attempting to publish again -> 409 Conflict
        var secondPublishRes = await _client.PostAsync($"/api/events/{created.Id}/publish", null);
        Assert.Equal(HttpStatusCode.Conflict, secondPublishRes.StatusCode);
    }
}
