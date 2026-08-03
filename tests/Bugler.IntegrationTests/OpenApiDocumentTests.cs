using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace Bugler.IntegrationTests;

/// <summary>
/// The OpenAPI document describes the whole API, admin paths included, so it answers from behind
/// the same door as the API itself: any signed-in user, nobody anonymous. Development alone stays
/// anonymous — that is what <c>bun run regen-api</c> reads — and both halves of that bargain are
/// asserted here, so a refactor can neither quietly revoke the exemption nor quietly widen it.
/// </summary>
public sealed class OpenApiDocumentTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() =>
        _harness = await BuglerHarness.StartAsync(builder => builder.UseEnvironment("Production"));

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Outside_Development_the_document_refuses_the_anonymous()
    {
        var response = await _harness.CreateAnonymousClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Any signed-in user, deliberately not an Admin: the SPA bundle every user downloads
    /// already carries the generated shape of the API, so the document guards nothing beyond it.</summary>
    [Fact]
    public async Task Any_signed_in_user_reads_the_document()
    {
        var reader = await _harness.CreateUserClientAsync("reader@bugler.test", "ReaderPass123!");

        var response = await reader.GetAsync("/openapi/v1.json");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.TryGetProperty("openapi", out _));
    }

    [Fact]
    public async Task Development_still_hands_the_document_to_the_anonymous()
    {
        await using var development = await BuglerHarness.StartAsync();

        var response = await development.CreateAnonymousClient().GetAsync("/openapi/v1.json");

        response.EnsureSuccessStatusCode();
    }
}
