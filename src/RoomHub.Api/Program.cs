using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RoomHub.Api.Common;
using RoomHub.Api.Domain.Repositories;
using RoomHub.Api.Features;
using RoomHub.Api.Features.Bookings.CancelBooking;
using RoomHub.Api.Features.Bookings.CreateBooking;
using RoomHub.Api.Features.Bookings.GetBooking;
using RoomHub.Api.Features.Rooms.CreateRoom;
using RoomHub.Api.Features.Rooms.GetRoom;
using RoomHub.Api.Infrastructure.Persistence;
using RoomHub.Api.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// TimeProvider for deterministic time operations
builder.Services.AddSingleton(TimeProvider.System);

// Persistence: EF Core with SQLite (Scoped DbContext)
builder.Services.AddDbContext<RoomHubDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=roomhub.db";
    options.UseSqlite(connectionString);
});

// Unit of Work: Scoped, shares the exact same DbContext instance
builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<RoomHubDbContext>());

// Repositories: Scoped
builder.Services.AddScoped<IRoomRepository, SqliteRoomRepository>();
builder.Services.AddScoped<IBookingRepository, SqliteBookingRepository>();

// CQRS Handlers
builder.Services.AddScoped<ICommandHandler<CreateRoomCommand, CreateRoomResponse>, CreateRoomHandler>();
builder.Services.AddScoped<IQueryHandler<GetRoomQuery, GetRoomResponse>, GetRoomHandler>();
builder.Services.AddScoped<ICommandHandler<CreateBookingCommand, CreateBookingResponse>, CreateBookingHandler>();
builder.Services.AddScoped<IQueryHandler<GetBookingQuery, GetBookingResponse>, GetBookingHandler>();
builder.Services.AddScoped<ICommandHandler<CancelBookingCommand>, CancelBookingHandler>();

// JSON Configuration
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

// Ensure SQLite database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RoomHubDbContext>();
    db.Database.EnsureCreated();
}

app.MapGet("/", () => "RoomHub API is running.");
app.MapRoomsEndpoints();
app.MapBookingsEndpoints();

app.Run();

// Marker class for WebApplicationFactory in integration tests
public partial class Program { }
