using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Features.Events;
using MyApp.Features.Events.CreateEvent;
using MyApp.Features.Events.GetEventById;
using MyApp.Features.Events.PublishEvent;

namespace MyApp;

public static class ProgramExtensions
{
    // 1. Dependency Injection: Handlers are registered with Scoped lifetime
    public static IServiceCollection AddEventFeatureHandlers(this IServiceCollection services)
    {
        services.AddScoped<CreateEventHandler>();
        services.AddScoped<GetEventByIdHandler>();
        services.AddScoped<PublishEventHandler>();

        return services;
    }

    // 2. Program.cs usage demonstration:
    /*
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddPersistence(builder.Configuration);
    builder.Services.AddEventFeatureHandlers();

    var app = builder.Build();

    // Map feature route groups
    app.MapEventsEndpoints();

    app.Run();
    */
}
