using Bugler.Access;
using Bugler.Access.Contracts;
using Bugler.Ai;
using Bugler.Alerting;
using Bugler.Exploration;
using Bugler.Host;
using Bugler.Host.IntegrationEvents;
using Bugler.Ingestion;
using Bugler.Mail;
using Bugler.Registry;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

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
builder.Services.AddAi(builder.Configuration);

// The Host's own store of deployment settings, and the stored SMTP settings taking over the
// transport's source: saved-in-the-UI wins over Mail:Smtp until reset (ADR 0014).
builder.Services.AddDbContext<ServerDbContext>((provider, options) => options
    .UseNpgsql(
        provider.GetRequiredService<NpgsqlDataSource>(),
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "server"))
    .UseSnakeCaseNamingConvention());
builder.Services.Replace(ServiceDescriptor.Singleton<ISmtpSettingsSource, StoredSmtpSettingsSource>());
builder.Services.Replace(ServiceDescriptor.Singleton<IAiSettingsSource, StoredAiSettingsSource>());

// The language the server speaks by default, and the language each answer leaves in: the
// requester's own where Accept-Language names a supported one, the server's otherwise (ADR 0024).
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IServerLanguage, StoredServerLanguageSource>();
builder.Services.AddScoped<IRequestLanguage, RequestLanguage>();

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
await ServerDbContext.MigrateAsync(app.Services);

// The static UI belongs to the app surface only — and so does the policy it is served under, which
// only a browser enforces and no OTLP sender would ever read (ADR 0022). The branch rejoins the
// pipeline, so the headers reach the REST answers and the SPA fallback too, not just these files.
bool OnAppSurface(HttpContext context) =>
    !surfaceByPort.TryGetValue(context.Connection.LocalPort, out var surface) || surface == Surface.App;
app.UseWhen(OnAppSurface, spa =>
{
    spa.UseSecurityHeaders();
    // Before routing, so no endpoint on this surface can be added without it (ADR 0025), and
    // after the headers, so the refusal is dressed like every other answer.
    spa.UseSameOriginMutations();
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

// The document describes the whole API, admin paths included, so it sits behind the same door:
// any signed-in user. Development alone stays anonymous so `bun run regen-api` can read it
// without a session; an unrecognized environment locks rather than leaks.
var openApiDocument = appSurface.MapOpenApi();
if (!app.Environment.IsDevelopment())
{
    openApiDocument.RequireAuthorization();
}

// Deployment diagnostics and settings, like /health: whether this server can send mail, and
// where to. Admin-only, because the SMTP settings are the server's, not any application's.
var mailAdmin = appSurface.MapGroup("/api/admin/mail").RequireAuthorization("Admin");
mailAdmin.MapPost("/test", SendTestMail.Handle)
    .Produces<TestMailResult>()
    // The refusal is the useful answer here, so it belongs in the document like any other.
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status502BadGateway);
mailAdmin.MapGet("/settings", MailSettingsEndpoints.Get)
    .Produces<MailSettingsDto>();
mailAdmin.MapPut("/settings", MailSettingsEndpoints.Save)
    .Produces<MailSettingsDto>()
    .ProducesProblem(StatusCodes.Status400BadRequest);
mailAdmin.MapDelete("/settings", MailSettingsEndpoints.Reset)
    .Produces<MailSettingsDto>();

// The AI settings sit beside the SMTP ones on the same terms (ADR 0027): the server's, not any
// application's — which application's telemetry may be shown to the provider is Registry's
// AI Consent, a different question (ADR 0028).
var aiAdmin = appSurface.MapGroup("/api/admin/ai").RequireAuthorization("Admin");
aiAdmin.MapPost("/test", AskTestCompletion.Handle)
    .Produces<TestCompletionResult>()
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status502BadGateway);
aiAdmin.MapGet("/settings", AiSettingsEndpoints.Get)
    .Produces<AiSettingsDto>();
aiAdmin.MapPut("/settings", AiSettingsEndpoints.Save)
    .Produces<AiSettingsDto>()
    .ProducesProblem(StatusCodes.Status400BadRequest);
aiAdmin.MapDelete("/settings", AiSettingsEndpoints.Reset)
    .Produces<AiSettingsDto>();

// The server's language sits beside the SMTP settings: both are facts of the deployment, owned
// by the Host and edited on the same admin screen.
var serverAdmin = appSurface.MapGroup("/api/admin/server").RequireAuthorization("Admin");
serverAdmin.MapGet("/language", ServerLanguageEndpoints.Get)
    .Produces<ServerLanguageDto>();
serverAdmin.MapPut("/language", ServerLanguageEndpoints.Save)
    .Produces<ServerLanguageDto>()
    .ProducesProblem(StatusCodes.Status400BadRequest);

// What the stored telemetry costs, from the context that stores it (ADR 0017): each Service's
// Footprint and Ingest Rate beside the Effective Retention its purge works from. The group is
// mounted here because the capability name is Access's and the handler is Ingestion's, and only
// the Host may know both.
appSurface.MapGroup("/api/admin/storage")
    .RequireAuthorization(Capabilities.InspectStorage)
    .MapStorageReport();

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
