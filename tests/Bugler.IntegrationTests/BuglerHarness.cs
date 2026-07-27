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
/// A running Bugler over a real PostgreSQL container, with one seeded
/// Application/Instance and a valid API key ready for exports.
/// </summary>
public sealed class BuglerHarness : IAsyncDisposable
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private WebApplicationFactory<Program> _factory = null!;

    public HttpClient Client { get; private set; } = null!;
    public string ApiKey { get; private set; } = null!;

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

        ApiKey = ApiKeyMaterial.GeneratePlaintext();
        await using var scope = _factory.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();
        var application = new Application { Id = ApplicationId.New(), Name = "Eshop", CreatedAt = DateTimeOffset.UtcNow };
        var instance = new Instance
        {
            Id = InstanceId.New(),
            ApplicationId = application.Id,
            Name = "Acme production",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        registry.Applications.Add(application);
        registry.Instances.Add(instance);
        registry.ApiKeys.Add(new ApiKey
        {
            Id = Guid.CreateVersion7(),
            InstanceId = instance.Id,
            KeyHash = ApiKeyMaterial.Hash(ApiKey),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await registry.SaveChangesAsync();
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

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
