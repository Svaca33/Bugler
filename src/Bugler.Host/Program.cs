using Bugler.Ingestion;
using Bugler.Registry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNpgsqlDataSource(
    builder.Configuration.GetConnectionString("bugler")
    ?? throw new InvalidOperationException("Connection string 'bugler' is missing."));

builder.Services.AddRegistry();
builder.Services.AddIngestion(builder.Configuration);

var app = builder.Build();

await RegistryModule.MigrateAsync(app.Services);
await IngestionModule.MigrateAsync(app.Services);

app.MapGet("/health", () => Results.Text("OK"));
app.MapIngestion();

app.Run();

public partial class Program;
