using System.Net;
using Bugler.Registry;
using Bugler.Registry.Catalog;
using Bugler.SharedKernel;
using Google.Protobuf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using Testcontainers.PostgreSql;

namespace Bugler.IntegrationTests;

/// <summary>
/// Whole write path against a real PostgreSQL: authenticate, ingest via OTLP/HTTP,
/// and observe the rows the background writer persisted.
/// </summary>
public sealed class OtlpLogIngestTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _apiKey = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:bugler", _postgres.GetConnectionString()));
        _client = _factory.CreateClient();

        _apiKey = ApiKeyMaterial.GeneratePlaintext();
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
            KeyHash = ApiKeyMaterial.Hash(_apiKey),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await registry.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Export_with_valid_key_persists_log_records()
    {
        var response = await PostLogsAsync(_apiKey, "First things first", "And then the rest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bodies = await WaitForPersistedBodiesAsync(expectedCount: 2);
        Assert.Contains("First things first", bodies);
        Assert.Contains("And then the rest", bodies);
    }

    [Fact]
    public async Task Export_with_unknown_key_is_rejected()
    {
        var response = await PostLogsAsync(ApiKeyMaterial.GeneratePlaintext(), "Should never land");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpResponseMessage> PostLogsAsync(string apiKey, params string[] bodies)
    {
        var scopeLogs = new ScopeLogs();
        foreach (var body in bodies)
        {
            scopeLogs.LogRecords.Add(new LogRecord
            {
                TimeUnixNano = (ulong)(DateTime.UtcNow - DateTime.UnixEpoch).Ticks * 100,
                SeverityNumber = SeverityNumber.Info,
                Body = new AnyValue { StringValue = body },
            });
        }

        var request = new ExportLogsServiceRequest();
        var resourceLogs = new ResourceLogs
        {
            Resource = new Resource
            {
                Attributes =
                {
                    new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "web" } },
                },
            },
        };
        resourceLogs.ScopeLogs.Add(scopeLogs);
        request.ResourceLogs.Add(resourceLogs);

        var content = new ByteArrayContent(request.ToByteArray());
        content.Headers.ContentType = new("application/x-protobuf");
        var message = new HttpRequestMessage(HttpMethod.Post, "/v1/logs") { Content = content };
        message.Headers.Add("x-api-key", apiKey);
        return await _client.SendAsync(message);
    }

    private async Task<List<string>> WaitForPersistedBodiesAsync(int expectedCount)
    {
        var dataSource = _factory.Services.GetRequiredService<NpgsqlDataSource>();
        var deadline = DateTime.UtcNow.AddSeconds(15);

        while (true)
        {
            var bodies = new List<string>();
            await using (var command = dataSource.CreateCommand(
                "SELECT body FROM telemetry.log_records ORDER BY id"))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    bodies.Add(reader.GetString(0));
                }
            }

            if (bodies.Count >= expectedCount || DateTime.UtcNow > deadline)
            {
                return bodies;
            }

            await Task.Delay(200);
        }
    }
}
