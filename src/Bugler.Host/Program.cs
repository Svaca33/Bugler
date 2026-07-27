using Bugler.Access;
using Bugler.Exploration;
using Bugler.Ingestion;
using Bugler.Registry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNpgsqlDataSource(
    builder.Configuration.GetConnectionString("bugler")
    ?? throw new InvalidOperationException("Connection string 'bugler' is missing."));

builder.Services.AddRegistry(builder.Configuration);
builder.Services.AddIngestion(builder.Configuration);
builder.Services.AddAccess();
builder.Services.AddExploration();
builder.Services.AddOpenApi();

var app = builder.Build();

await RegistryModule.MigrateAsync(app.Services);
await IngestionModule.MigrateAsync(app.Services);
await AccessModule.MigrateAsync(app.Services);

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Text("OK"));
app.MapOpenApi();
app.MapIngestion();
app.MapExploration();
app.MapAccess();
app.MapRegistry();

// Serve the SPA for client-side routes when the frontend build is bundled in.
var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (File.Exists(Path.Combine(webRoot, "index.html")))
{
    app.MapFallbackToFile("index.html");
}

app.Run();

public partial class Program;
