using Bugler.Access.Contracts;
using Bugler.Access.ReadVisibility;
using Microsoft.Extensions.DependencyInjection;

namespace Bugler.Access;

/// <summary>Composition entry point of the Access context (human identity and authorization).</summary>
public static class AccessModule
{
    public static IServiceCollection AddAccess(this IServiceCollection services)
    {
        services.AddScoped<IReadVisibility, UnrestrictedVisibility>();
        return services;
    }
}
