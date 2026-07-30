using Bugler.Access.Authentication;
using Bugler.Access.Contracts;
using Bugler.Access.ManageUsers;
using Bugler.Access.Outbox;
using Bugler.Access.ReadVisibility;
using Bugler.Access.ResolveMailRecipients;
using Bugler.Access.RevokeDeletedApplicationGrants;
using Bugler.Access.Users;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bugler.Access;

/// <summary>Composition entry point of the Access context (human identity and authorization).</summary>
public static class AccessModule
{
    public static IServiceCollection AddAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AccessOptions>(configuration.GetSection(AccessOptions.SectionName));
        services.AddDbContext<AccessDbContext>((provider, options) => options
            .UseNpgsql(
                provider.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "access"))
            .UseSnakeCaseNamingConvention());

        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddHttpContextAccessor();
        services.AddScoped<IReadVisibility, GrantedVisibility>();
        services.AddScoped<IMailRecipients, MailRecipientResolver>();
        services.AddScoped<IIntegrationEventHandler<ApplicationDeleted>, DeletedApplicationGrantRevoker>();

        // The dispatcher drains every IIntegrationEventOutbox; publishing stays on the concrete
        // AccessOutbox, because a second IIntegrationEventPublisher registration would silently
        // shadow Registry's wherever the interface is injected.
        services.AddScoped<AccessOutbox>();
        services.AddScoped<IIntegrationEventOutbox>(p => p.GetRequiredService<AccessOutbox>());

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "bugler.session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                // A cookie outlives the state it was minted from — re-read the User behind it.
                options.Events.OnValidatePrincipal = SessionRevalidation.ValidateAsync;
                // An API returns status codes, never login-page redirects.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("Admin", policy => policy.RequireRole(AuthEndpoints.AdminRole));

        return services;
    }

    public static IEndpointRouteBuilder MapAccess(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/auth/status", AuthEndpoints.Status).AllowAnonymous();
        endpoints.MapPost("/api/auth/setup", AuthEndpoints.Setup).AllowAnonymous()
            .Produces<CurrentUserDto>();
        endpoints.MapPost("/api/auth/login", AuthEndpoints.Login).AllowAnonymous()
            .Produces<CurrentUserDto>();
        endpoints.MapPost("/api/auth/password/change", AuthEndpoints.ChangePassword).RequireAuthorization();
        endpoints.MapPost("/api/auth/logout", AuthEndpoints.Logout).RequireAuthorization();
        endpoints.MapGet("/api/auth/me", AuthEndpoints.Me).RequireAuthorization()
            .Produces<CurrentUserDto>();

        var admin = endpoints.MapGroup("/api/users").RequireAuthorization("Admin");
        admin.MapGet("", ManageUsersEndpoints.List);
        admin.MapPost("", ManageUsersEndpoints.Create).Produces<UserDto>();
        admin.MapPost("/{id:guid}/deactivate", ManageUsersEndpoints.Deactivate);
        admin.MapPost("/{id:guid}/reactivate", ManageUsersEndpoints.Reactivate);
        admin.MapDelete("/{id:guid}", ManageUsersEndpoints.Delete);
        admin.MapPost("/{id:guid}/grants", ManageUsersEndpoints.Grant);
        admin.MapDelete("/{id:guid}/grants/{applicationId:guid}", ManageUsersEndpoints.Revoke);

        return endpoints;
    }

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AccessDbContext>()
            .Database.MigrateAsync(cancellationToken);
    }
}
