using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoomHub.Api.Features.Bookings.CreateBooking;
using RoomHub.Api.Features.Bookings.GetBooking;
using RoomHub.Api.Features.Rooms.CreateRoom;
using RoomHub.Api.Features.Rooms.GetRoom;

namespace RoomHub.Api.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly TestTimeProvider _timeProvider = new(new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero));

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace system time provider with controllable test provider
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TimeProvider));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton<TimeProvider>(_timeProvider);
            });
        });
    }

    [Fact]
    public async Task CreateRoom_Returns201WithLocationAndCamelCaseBody()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = "Conference Room Alpha",
            capacity = 12
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var room = await response.Content.ReadFromJsonAsync<CreateRoomResponse>();
        Assert.NotNull(room);
        Assert.NotEqual(Guid.Empty, room.Id);
        Assert.Equal("Conference Room Alpha", room.Name);
        Assert.Equal(12, room.Capacity);
    }

    [Fact]
    public async Task GetRoom_WhenMissing_Returns404ProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/rooms/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateBooking_Returns201WithLocationAndStatus()
    {
        var client = _factory.CreateClient();

        // 1. Create room
        var roomResp = await client.PostAsJsonAsync("/api/rooms", new { name = "Room Beta", capacity = 8 });
        var room = await roomResp.Content.ReadFromJsonAsync<CreateRoomResponse>();
        Assert.NotNull(room);

        // 2. Create booking
        var bookingResp = await client.PostAsJsonAsync($"/api/rooms/{room.Id}/bookings", new
        {
            bookedBy = "Emi",
            startUtc = "2026-11-20T10:00:00Z",
            endUtc = "2026-11-20T12:00:00Z",
            attendees = 4
        });

        Assert.Equal(HttpStatusCode.Created, bookingResp.StatusCode);
        Assert.NotNull(bookingResp.Headers.Location);

        var booking = await bookingResp.Content.ReadFromJsonAsync<CreateBookingResponse>();
        Assert.NotNull(booking);
        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(room.Id, booking.RoomId);
        Assert.Equal("Active", booking.Status);

        // 3. Get booking
        var getResp = await client.GetAsync($"/api/bookings/{booking.Id}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var details = await getResp.Content.ReadFromJsonAsync<GetBookingResponse>();
        Assert.NotNull(details);
        Assert.Equal("Active", details.Status);
        Assert.Equal("Emi", details.BookedBy);

        // 4. Cancel booking
        var cancelResp = await client.PostAsync($"/api/bookings/{booking.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, cancelResp.StatusCode);

        // 5. Cancel again -> 409 Conflict
        var cancelAgainResp = await client.PostAsync($"/api/bookings/{booking.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, cancelAgainResp.StatusCode);
        Assert.Equal("application/problem+json", cancelAgainResp.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateBooking_WhenRoomMissing_Returns404BeforeValidatingBody()
    {
        var client = _factory.CreateClient();

        // Completely invalid payload
        var response = await client.PostAsJsonAsync($"/api/rooms/{Guid.NewGuid()}/bookings", new
        {
            bookedBy = "",
            startUtc = "2020-01-01T00:00:00Z",
            endUtc = "2020-01-01T00:00:00Z",
            attendees = -5
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
