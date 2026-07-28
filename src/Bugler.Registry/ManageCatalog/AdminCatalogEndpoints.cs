using Bugler.Registry.Catalog;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Registry.ManageCatalog;

public sealed record ApplicationDto(Guid Id, string Name, DateTimeOffset CreatedAt);

public sealed record CreateApplicationRequest(string Name);

public sealed record ServiceDto(
    Guid Id,
    Guid ApplicationId,
    string Namespace,
    string Environment,
    string Name,
    int? RetentionDays,
    DateTimeOffset CreatedAt);

public sealed record CreateServiceRequest(
    Guid ApplicationId, string Namespace, string Environment, string Name, int? RetentionDays);

public sealed record SetRetentionRequest(int? RetentionDays);

internal static class AdminCatalogEndpoints
{
    private const int MaxFacetLength = 200;

    public static async Task<IReadOnlyList<ApplicationDto>> ListApplications(
        RegistryDbContext dbContext, CancellationToken cancellationToken) =>
        await dbContext.Applications
            .OrderBy(a => a.Name)
            .Select(a => new ApplicationDto(a.Id.Value, a.Name, a.CreatedAt))
            .ToListAsync(cancellationToken);

    public static async Task<IResult> CreateApplication(
        CreateApplicationRequest request, RegistryDbContext dbContext, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length is 0 or > MaxFacetLength)
        {
            return Results.BadRequest("Application name must be 1-200 characters.");
        }

        if (await dbContext.Applications.AnyAsync(a => a.Name == name, cancellationToken))
        {
            return Results.Conflict("An application with this name already exists.");
        }

        var application = new Application
        {
            Id = ApplicationId.New(),
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.Applications.Add(application);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new ApplicationDto(application.Id.Value, application.Name, application.CreatedAt));
    }

    public static async Task<IReadOnlyList<ServiceDto>> ListServices(
        Guid applicationId, RegistryDbContext dbContext, CancellationToken cancellationToken)
    {
        var id = new ApplicationId(applicationId);
        return await dbContext.Services
            .Where(s => s.ApplicationId == id)
            .OrderBy(s => s.Namespace).ThenBy(s => s.Environment).ThenBy(s => s.Name)
            .Select(s => new ServiceDto(
                s.Id.Value, s.ApplicationId.Value, s.Namespace, s.Environment, s.Name,
                s.RetentionDays, s.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public static async Task<IResult> CreateService(
        CreateServiceRequest request, RegistryDbContext dbContext, CancellationToken cancellationToken)
    {
        var serviceNamespace = request.Namespace.Trim();
        var environment = request.Environment.Trim();
        var name = request.Name.Trim();

        if (!IsValidFacet(serviceNamespace) || !IsValidFacet(environment) || !IsValidFacet(name))
        {
            return Results.BadRequest("Namespace, environment and name must each be 1-200 characters.");
        }

        if (request.RetentionDays is < 1)
        {
            return Results.BadRequest("Retention must be at least 1 day.");
        }

        var applicationId = new ApplicationId(request.ApplicationId);
        if (!await dbContext.Applications.AnyAsync(a => a.Id == applicationId, cancellationToken))
        {
            return Results.NotFound("Unknown application.");
        }

        if (await dbContext.Services.AnyAsync(
                s => s.ApplicationId == applicationId &&
                     s.Namespace == serviceNamespace &&
                     s.Environment == environment &&
                     s.Name == name,
                cancellationToken))
        {
            return Results.Conflict("This service is already registered for the application.");
        }

        var service = new Service
        {
            Id = ServiceId.New(),
            ApplicationId = applicationId,
            Namespace = serviceNamespace,
            Environment = environment,
            Name = name,
            RetentionDays = request.RetentionDays,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.Services.Add(service);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new ServiceDto(
            service.Id.Value, service.ApplicationId.Value, service.Namespace, service.Environment,
            service.Name, service.RetentionDays, service.CreatedAt));
    }

    /// <summary>
    /// Deletes an Application with every Service registered under it. API keys go with the
    /// Services by cascade; telemetry and read grants follow from the recorded events.
    /// </summary>
    public static async Task<IResult> DeleteApplication(
        Guid id,
        RegistryDbContext dbContext,
        IIntegrationEventPublisher publisher,
        IOutboxSignal signal,
        CancellationToken cancellationToken)
    {
        var applicationId = new ApplicationId(id);
        var serviceIds = await dbContext.Services
            .Where(s => s.ApplicationId == applicationId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var removed = await dbContext.Applications
            .Where(a => a.Id == applicationId)
            .ExecuteDeleteAsync(cancellationToken);
        if (removed == 0)
        {
            return Results.NotFound();
        }

        // One event for all of them: the erasure is a single statement and a single retry unit.
        if (serviceIds.Count > 0)
        {
            await publisher.EnqueueAsync(new ServicesDeleted(serviceIds), cancellationToken);
        }

        await publisher.EnqueueAsync(new ApplicationDeleted(applicationId), cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        signal.Nudge(); // Only now — before the commit the dispatcher would find nothing.
        return Results.NoContent();
    }

    /// <summary>Deletes a Service, its API keys (cascade) and — through the event — everything it ever sent.</summary>
    public static async Task<IResult> DeleteService(
        Guid id,
        RegistryDbContext dbContext,
        IIntegrationEventPublisher publisher,
        IOutboxSignal signal,
        CancellationToken cancellationToken)
    {
        var serviceId = new ServiceId(id);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var removed = await dbContext.Services
            .Where(s => s.Id == serviceId)
            .ExecuteDeleteAsync(cancellationToken);
        if (removed == 0)
        {
            return Results.NotFound();
        }

        await publisher.EnqueueAsync(new ServicesDeleted([serviceId]), cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        signal.Nudge();
        return Results.NoContent();
    }

    public static async Task<IResult> SetRetention(
        Guid id, SetRetentionRequest request, RegistryDbContext dbContext, CancellationToken cancellationToken)
    {
        if (request.RetentionDays is < 1)
        {
            return Results.BadRequest("Retention must be at least 1 day.");
        }

        var serviceId = new ServiceId(id);
        var service = await dbContext.Services
            .FirstOrDefaultAsync(s => s.Id == serviceId, cancellationToken);
        if (service is null)
        {
            return Results.NotFound();
        }

        service.RetentionDays = request.RetentionDays;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static bool IsValidFacet(string value) => value.Length is > 0 and <= MaxFacetLength;
}
