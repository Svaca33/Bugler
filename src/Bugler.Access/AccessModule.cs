using Bugler.Access.Authentication;
using Bugler.Access.Contracts;
using Bugler.Access.ManageApplicationFocus;
using Bugler.Access.ManageMachineDelegations;
using Bugler.Access.ManageUsers;
using Bugler.Access.Outbox;
using Bugler.Access.ReadApplicationFocus;
using Bugler.Access.ReadVisibility;
using Bugler.Access.ResetPassword;
using Bugler.Access.ResolveMachineActor;
using Bugler.Access.ResolveMachineDelegationNames;
using Bugler.Access.ResolveMailRecipients;
using Bugler.Access.ResolveUserNames;
using Bugler.Access.RevokeDeletedApplicationGrants;
using Bugler.Access.Users;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Authentication;
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
    /// <summary>
    /// The authorization the machine door stands behind: a live Machine Delegation, and nothing else will
    /// do. Named here because Access owns what proving an identity means; the Host mounts it.
    /// </summary>
    public const string HoldsMachineDelegationPolicy = "HoldsMachineDelegation";

    public static IServiceCollection AddAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AccessOptions>(configuration.GetSection(AccessOptions.SectionName));
        // Second bind onto the same options: PublicBaseUrl describes the server, so it is
        // configured once and read by whoever puts links in messages (ADR 0011).
        services.Configure<AccessOptions>(configuration.GetSection(AccessOptions.ServerSectionName));
        services.AddDbContext<AccessDbContext>((provider, options) => options
            .UseNpgsql(
                provider.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "access"))
            .UseSnakeCaseNamingConvention());

        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        // The one thing Access configures about the hasher. Everything else about Identity V3 —
        // PBKDF2-HMAC-SHA512, the constant-time comparison, the format marker that lets an older
        // hash still verify — is the framework's and is right as it stands.
        services.Configure<PasswordHasherOptions>(
            options => options.IterationCount = Passwords.IterationCount);
        // One set of Attempt Budgets for the whole server, held for its lifetime: what they count
        // is how often an address may ask, which is not a fact about any one request (ADR 0021).
        services.AddSingleton<AttemptBudgets>();
        services.AddHttpContextAccessor();
        services.AddScoped<IReadVisibility, GrantedVisibility>();
        services.AddScoped<IReadApplicationFocus, FocusedApplications>();
        services.AddScoped<IMailRecipients, MailRecipientResolver>();
        services.AddScoped<IUserNames, UserNameResolver>();
        services.AddScoped<IMachineActor, MachineActorResolver>();
        services.AddScoped<IMachineDelegationNames, MachineDelegationNameResolver>();
        services.AddScoped<IIntegrationEventHandler<ApplicationDeleted>, DeletedApplicationGrantRevoker>();

        // The dispatcher drains every IIntegrationEventOutbox; publishing stays on the concrete
        // AccessOutbox, because a second IIntegrationEventPublisher registration would silently
        // shadow Registry's wherever the interface is injected.
        services.AddScoped<AccessOutbox>();
        services.AddScoped<IIntegrationEventOutbox>(p => p.GetRequiredService<AccessOutbox>());

        // Bugler sees plain HTTP behind the proxy that terminates TLS, so the request cannot say
        // whether the Session cookie needs protecting. The address the server declares itself at
        // can, and does (ADR 0019).
        var cookieSecurity = SessionCookieSecurity.Read(configuration);
        services.AddSingleton(cookieSecurity);
        services.AddHostedService<SessionCookieAnnouncement>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = cookieSecurity.CookieName;
                options.Cookie.SecurePolicy = cookieSecurity.SecurePolicy;
                // Spelled out rather than left to the default, because the __Host- prefix the name
                // may carry is refused by the browser without it.
                options.Cookie.Path = "/";
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
            })
            // A second way for the same person to prove who they are, never a second identity
            // (ADR 0029). It is not the default scheme: only the policy below names it, so a
            // Machine Delegation Secret presented to the app's REST API authenticates nothing.
            .AddScheme<AuthenticationSchemeOptions, MachineDelegationAuthenticationHandler>(
                MachineDelegationAuthenticationHandler.SchemeName, _ => { });

        services.AddAuthorizationBuilder()
            .AddPolicy("Admin", policy => policy.RequireRole(AuthEndpoints.AdminRole))
            // Capabilities name the deed, not the doer (ADR 0015). Both spellings coexist while
            // the older endpoints still ask for the role directly.
            .AddPolicy(Capabilities.ConfigureAlerting, policy => policy.RequireRole(AuthEndpoints.AdminRole))
            .AddPolicy(Capabilities.InspectStorage, policy => policy.RequireRole(AuthEndpoints.AdminRole))
            .AddPolicy(Capabilities.InspectMachineDelegations, policy => policy.RequireRole(AuthEndpoints.AdminRole))
            .AddPolicy(Capabilities.ConfigureMcp, policy => policy.RequireRole(AuthEndpoints.AdminRole))
            .AddPolicy(Capabilities.DeleteKindsOfTrouble, policy => policy.RequireRole(AuthEndpoints.AdminRole))
            // What the machine door asks for, and the only policy that names the Machine Delegation
            // scheme: a Session cookie arriving there authenticates nothing either.
            .AddPolicy(HoldsMachineDelegationPolicy, policy =>
            {
                policy.AuthenticationSchemes.Add(MachineDelegationAuthenticationHandler.SchemeName);
                policy.RequireAuthenticatedUser();
            });

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
        endpoints.MapPost("/api/auth/language", AuthEndpoints.SetLanguage).RequireAuthorization();

        // A person's own Focus, a row per click. Not under /api/users: nobody sets anybody else's.
        endpoints.MapPut("/api/auth/focus/{applicationId:guid}", ApplicationFocusEndpoints.Attend)
            .RequireAuthorization();
        endpoints.MapDelete("/api/auth/focus/{applicationId:guid}", ApplicationFocusEndpoints.Ignore)
            .RequireAuthorization();
        endpoints.MapPost("/api/auth/password/forgot", ResetPasswordEndpoints.Forgot).AllowAnonymous();
        endpoints.MapPost("/api/auth/password/reset", ResetPasswordEndpoints.Reset).AllowAnonymous();
        endpoints.MapPost("/api/auth/logout", AuthEndpoints.Logout).RequireAuthorization();
        endpoints.MapGet("/api/auth/me", AuthEndpoints.Me).RequireAuthorization()
            .Produces<CurrentUserDto>();

        // A person's own Machine Delegations. Issuing one needs no approval: it cannot reach past what its
        // holder already reads (ADR 0029).
        var delegations = endpoints.MapGroup("/api/machine-delegations").RequireAuthorization();
        delegations.MapGet("", MachineDelegationEndpoints.ListOwn).Produces<IReadOnlyList<MachineDelegationDto>>();
        delegations.MapPost("", MachineDelegationEndpoints.Issue)
            .Produces<IssuedMachineDelegationDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
        delegations.MapDelete("/{id:guid}", MachineDelegationEndpoints.RevokeOwn);

        var heldMachineDelegations = endpoints.MapGroup("/api/admin/machine-delegations")
            .RequireAuthorization(Capabilities.InspectMachineDelegations);
        heldMachineDelegations.MapGet("", MachineDelegationEndpoints.ListHeld).Produces<IReadOnlyList<HeldMachineDelegationDto>>();
        heldMachineDelegations.MapDelete("/{id:guid}", MachineDelegationEndpoints.RevokeHeld);

        var admin = endpoints.MapGroup("/api/users").RequireAuthorization("Admin");
        admin.MapGet("", ManageUsersEndpoints.List);
        admin.MapPost("", ManageUsersEndpoints.Create).Produces<UserDto>();
        admin.MapPost("/{id:guid}/deactivate", ManageUsersEndpoints.Deactivate);
        admin.MapPost("/{id:guid}/reactivate", ManageUsersEndpoints.Reactivate);
        admin.MapDelete("/{id:guid}", ManageUsersEndpoints.Delete);
        admin.MapPost("/{id:guid}/reset-ticket", ManageUsersEndpoints.IssueResetTicket)
            .Produces<IssuedResetTicketDto>();
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
