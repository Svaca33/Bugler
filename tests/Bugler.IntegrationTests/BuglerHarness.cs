using System.Net.Http.Json;
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
/// A running Bugler over a real PostgreSQL container: one seeded Application/Instance
/// with a valid API key, and an admin account signed in on <see cref="Client"/>.
/// </summary>
public sealed class BuglerHarness : IAsyncDisposable
{
    public const string AdminEmail = "admin@bugler.test";
    public const string AdminPassword = "AdminPass123!";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private WebApplicationFactory<Program> _factory = null!;

    /// <summary>Authenticated as the first Admin.</summary>
    public HttpClient Client { get; private set; } = null!;

    public string ApiKey { get; private set; } = null!;
    public Guid ApplicationId { get; private set; }
    public Guid InstanceId { get; private set; }

    public static async Task<BuglerHarness> StartAsync()
    {
        var harness = new BuglerHarness();
        await harness.InitializeAsync();
        return harness;
    }

    private async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:bugler", _postgres.GetConnectionString()));
        Client = _factory.CreateClient();

        var setup = await Client.PostAsJsonAsync(
            "/api/auth/setup",
            new { email = AdminEmail, password = AdminPassword, displayName = "Admin" });
        setup.EnsureSuccessStatusCode();

        (ApplicationId, InstanceId, ApiKey) = await SeedApplicationAsync("Eshop", "Acme production");
    }

    /// <summary>Seeds an Application with one Instance and an issued API key, bypassing the API.</summary>
    public async Task<(Guid ApplicationId, Guid InstanceId, string ApiKey)> SeedApplicationAsync(
        string applicationName, string instanceName)
    {
        var apiKey = ApiKeyMaterial.GeneratePlaintext();
        await using var scope = _factory.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();
        var application = new Application
        {
            Id = SharedKernel.ApplicationId.New(),
            Name = applicationName,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var instance = new Instance
        {
            Id = SharedKernel.InstanceId.New(),
            ApplicationId = application.Id,
            Name = instanceName,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        registry.Applications.Add(application);
        registry.Instances.Add(instance);
        registry.ApiKeys.Add(new ApiKey
        {
            Id = Guid.CreateVersion7(),
            InstanceId = instance.Id,
            KeyHash = ApiKeyMaterial.Hash(apiKey),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await registry.SaveChangesAsync();
        return (application.Id.Value, instance.Id.Value, apiKey);
    }

    /// <summary>A fresh client with its own cookie jar, not signed in.</summary>
    public HttpClient CreateAnonymousClient() => _factory.CreateClient();

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

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        return client;
    }

    private sealed record CreatedUser(Guid Id);

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
