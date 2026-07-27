using Testcontainers.PostgreSql;

namespace Bugler.IntegrationTests;

/// <summary>
/// Proves the Testcontainers plumbing works end to end. Real storage tests
/// (COPY ingest, JSONB queries, partitions) will build on this fixture.
/// </summary>
public sealed class PostgresSmokeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Container_starts_and_answers_queries()
    {
        var result = await _postgres.ExecScriptAsync("SELECT 1;");

        Assert.Equal(0, result.ExitCode);
    }
}
