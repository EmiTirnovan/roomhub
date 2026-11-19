using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyApp.Features.Events.CreateEvent;
using MyApp.Features.Events.GetEventById;
using MyApp.Features.Events.PublishEvent;

namespace MyApp.Features.Events;

public static class EventsEndpoints
{
    public static IEndpointRouteBuilder MapEventsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Route grouping via MapGroup with route prefix and OpenAPI tags
        var group = endpoints.MapGroup("/api/events")
            .WithTags("Events");

        // Register each vertical slice endpoint onto the route group
        group.MapCreateEventEndpoint();
        group.MapGetEventByIdEndpoint();
        group.MapPublishEventEndpoint();

        return endpoints;
    }
}
