using System.Net.Http.Json;
using Bugler.Access.ManageUsers;
using Bugler.Registry;
using Bugler.Registry.Catalog;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bugler.IntegrationTests;

/// <summary>
/// A running Bugler over a real PostgreSQL container: one seeded Application/Service
/// with a valid API key, and an admin account signed in on <see cref="Client"/>.
/// </summary>
public sealed class BuglerHarness : IAsyncDisposable
{
    public const string AdminEmail = "admin@bugler.test";
    public const string AdminPassword = "AdminPass123!";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private WebApplicationFactory<Program> _factory = null!;
    private WebApplicationFactoryClientOptions _clientOptions = new();

    /// <summary>Authenticated as the first Admin.</summary>
    public HttpClient Client { get; private set; } = null!;

    public string ApiKey { get; private set; } = null!;
    public Guid ApplicationId { get; private set; }
    public Guid ServiceId { get; private set; }

    /// <param name="publicBaseUrl">
    /// The address this Bugler is to believe it answers at. It reaches the server as
    /// <c>Server:PublicBaseUrl</c> <em>and</em> becomes the base address of every client the harness
    /// hands out, because the two cannot disagree: a server told it lives at an https address mints
    /// a Secure Session cookie, and <see cref="System.Net.CookieContainer"/> will not send one back
    /// over <c>http://</c>. Left null, the server keeps its configured address and clients keep the
    /// default <c>http://localhost</c>.
    /// </param>
    public static async Task<BuglerHarness> StartAsync(
        Action<IWebHostBuilder>? configure = null,
        string? publicBaseUrl = null)
    {
        var harness = new BuglerHarness();
        await harness.InitializeAsync(configure, publicBaseUrl);
        return harness;
    }

    private async Task InitializeAsync(Action<IWebHostBuilder>? configure, string? publicBaseUrl)
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:bugler", _postgres.GetConnectionString());
            // Tests drive the alerting units directly; a live scheduler would race them. Its
            // startup tick still runs once, harmlessly, before any test seeds telemetry.
            builder.UseSetting("Alerting:PollIntervalSeconds", "3600");
            if (publicBaseUrl is not null)
            {
                builder.UseSetting("Server:PublicBaseUrl", publicBaseUrl);
            }

            configure?.Invoke(builder);
        });

        if (publicBaseUrl is not null)
        {
            _clientOptions.BaseAddress = new Uri(new Uri(publicBaseUrl).GetLeftPart(UriPartial.Authority));
        }

        Client = _factory.CreateClient(_clientOptions);

        var setup = await Client.PostAsJsonAsync(
            "/api/auth/setup",
            new { email = AdminEmail, password = AdminPassword, displayName = "Admin" });
        setup.EnsureSuccessStatusCode();

        (ApplicationId, ServiceId, ApiKey) = await SeedApplicationAsync("Eshop", "acme", "prod", "web");
    }

    /// <summary>Seeds an Application with one Service and an issued API key, bypassing the API.</summary>
    public async Task<(Guid ApplicationId, Guid ServiceId, string ApiKey)> SeedApplicationAsync(
        string applicationName, string serviceNamespace, string environment, string serviceName)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();
        var application = new Application
        {
            Id = SharedKernel.ApplicationId.New(),
            Name = applicationName,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        registry.Applications.Add(application);
        var (serviceId, apiKey) = AddService(registry, application.Id, serviceNamespace, environment, serviceName);
        await registry.SaveChangesAsync();
        return (application.Id.Value, serviceId, apiKey);
    }

    /// <summary>Seeds one more Service under an existing Application — a second sender of the same deployment.</summary>
    public async Task<(Guid ServiceId, string ApiKey)> SeedServiceAsync(
        Guid applicationId, string serviceNamespace, string environment, string serviceName)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();
        var seeded = AddService(
            registry, new SharedKernel.ApplicationId(applicationId), serviceNamespace, environment, serviceName);
        await registry.SaveChangesAsync();
        return seeded;
    }

    private static (Guid ServiceId, string ApiKey) AddService(
        RegistryDbContext registry,
        SharedKernel.ApplicationId applicationId,
        string serviceNamespace,
        string environment,
        string serviceName)
    {
        var apiKey = ApiKeyMaterial.GeneratePlaintext();
        var service = new Service
        {
            Id = SharedKernel.ServiceId.New(),
            ApplicationId = applicationId,
            Namespace = serviceNamespace,
            Environment = environment,
            Name = serviceName,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        registry.Services.Add(service);
        registry.ApiKeys.Add(new ApiKey
        {
            Id = Guid.CreateVersion7(),
            ServiceId = service.Id,
            KeyHash = ApiKeyMaterial.Hash(apiKey),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        return (service.Id.Value, apiKey);
    }

    /// <summary>A fresh client with its own cookie jar, not signed in.</summary>
    public HttpClient CreateAnonymousClient() => _factory.CreateClient(_clientOptions);

    /// <summary>Creates a non-admin user with the given grants and returns a client signed in as them.</summary>
    public async Task<HttpClient> CreateUserClientAsync(
        string email, string password, params Guid[] grantedApplicationIds)
    {
        var created = await Client.PostAsJsonAsync(
            "/api/users", new { email, password, displayName = (string?)null, isAdmin = false });
        created.EnsureSuccessStatusCode();
        var user = await created.Content.ReadFromJsonAsync<CreatedUser>();

        foreach (var applicationId in grantedApplicationIds)
        {
            var grant = await Client.PostAsJsonAsync($"/api/users/{user!.Id}/grants", new { applicationId });
            grant.EnsureSuccessStatusCode();
        }

        var client = _factory.CreateClient(_clientOptions);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        return client;
    }

    private sealed record CreatedUser(Guid Id);

    /// <summary>Looks a User up the way an Admin does — through the list the admin UI renders.</summary>
    public async Task<Guid> FindUserIdAsync(string email)
    {
        var users = await Client.GetFromJsonAsync<List<UserDto>>("/api/users");
        return users!.Single(u => u.Email == email).Id;
    }

    /// <summary>Deactivates a User through the admin API, the way an Admin would from the UI.</summary>
    public async Task DeactivateUserAsync(string email)
    {
        var response = await Client.PostAsync(
            $"/api/users/{await FindUserIdAsync(email)}/deactivate", content: null);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Polls the given scalar-list query until it yields at least expectedCount rows.</summary>
    public async Task<List<string>> WaitForRowsAsync(string sql, int expectedCount)
    {
        var dataSource = _factory.Services.GetRequiredService<NpgsqlDataSource>();
        var deadline = DateTime.UtcNow.AddSeconds(15);

        while (true)
        {
            var values = new List<string>();
            await using (var command = dataSource.CreateCommand(sql))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    values.Add(reader.GetString(0));
                }
            }

            if (values.Count >= expectedCount || DateTime.UtcNow > deadline)
            {
                return values;
            }

            await Task.Delay(200);
        }
    }

    /// <summary>Polls a scalar count query until it reads exactly the expected value, or gives up.</summary>
    public async Task<long> WaitForCountAsync(string sql, long expected)
    {
        var dataSource = _factory.Services.GetRequiredService<NpgsqlDataSource>();
        var deadline = DateTime.UtcNow.AddSeconds(15);

        while (true)
        {
            await using var command = dataSource.CreateCommand(sql);
            var count = Convert.ToInt64(await command.ExecuteScalarAsync());

            if (count == expected || DateTime.UtcNow > deadline)
            {
                return count;
            }

            await Task.Delay(200);
        }
    }

    public async Task ExecuteSqlAsync(string sql)
    {
        var dataSource = _factory.Services.GetRequiredService<NpgsqlDataSource>();
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    public T GetRequiredService<T>() where T : notnull => _factory.Services.GetRequiredService<T>();

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
