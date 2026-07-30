using Bugler.Access;
using Bugler.Alerting;
using Bugler.Exploration;
using Bugler.Host;
using Bugler.Host.IntegrationEvents;
using Bugler.Ingestion;
using Bugler.Mail;
using Bugler.Registry;
using Bugler.SharedKernel;

var builder = WebApplication.CreateBuilder(args);

// Which Kestrel listener serves which surface (App / OtlpGrpc / OtlpHttp).
var surfaceByPort = ListenerSurfaces.FromConfiguration(builder.Configuration);

builder.Services.AddNpgsqlDataSource(
    builder.Configuration.GetConnectionString("bugler")
    ?? throw new InvalidOperationException("Connection string 'bugler' is missing."));

builder.Services.AddSingleton<HealthProbe>();

// The only place that knows which context listens to another context's facts.
builder.Services.AddSingleton<OutboxSignal>();
builder.Services.AddSingleton<IOutboxSignal>(p => p.GetRequiredService<OutboxSignal>());
builder.Services.AddHostedService<OutboxDispatcher>();

builder.Services.AddMail(builder.Configuration);
builder.Services.AddRegistry(builder.Configuration);
builder.Services.AddIngestion(builder.Configuration);
builder.Services.AddAccess(builder.Configuration);
builder.Services.AddAlerting(builder.Configuration);
builder.Services.AddExploration();
builder.Services.AddOpenApi();

var app = builder.Build();

await RegistryModule.MigrateAsync(app.Services);
await IngestionModule.MigrateAsync(app.Services);
await AccessModule.MigrateAsync(app.Services);
await AlertingModule.MigrateAsync(app.Services);

// The static UI belongs to the app surface only.
bool OnAppSurface(HttpContext context) =>
    !surfaceByPort.TryGetValue(context.Connection.LocalPort, out var surface) || surface == Surface.App;
app.UseWhen(OnAppSurface, spa =>
{
    spa.UseDefaultFiles();
    spa.UseStaticFiles();
});

app.UseListenerSurfaces(surfaceByPort);

app.UseAuthentication();
app.UseAuthorization();

// Untagged: probed on every surface. Answers for the database behind it, not just for the
// process listening in front of it (see HealthProbe).
app.MapGet("/health", async (HealthProbe probe, CancellationToken cancellationToken) =>
    await probe.IsHealthyAsync(cancellationToken)
        ? Results.Text("OK")
        : Results.Text("Database unreachable", statusCode: StatusCodes.Status503ServiceUnavailable));

var appSurface = app.MapGroup("").ServedOn(Surface.App);
appSurface.MapOpenApi();
appSurface.MapExploration();
appSurface.MapAccess();
appSurface.MapAlerting();
appSurface.MapRegistry();

app.MapGroup("").ServedOn(Surface.OtlpGrpc).MapOtlpGrpcIngestion();
app.MapGroup("").ServedOn(Surface.OtlpHttp).MapOtlpHttpIngestion();

// Serve the SPA for client-side routes when the frontend build is bundled in.
var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (File.Exists(Path.Combine(webRoot, "index.html")))
{
    appSurface.MapFallbackToFile("index.html");
}

app.Run();

public partial class Program;
