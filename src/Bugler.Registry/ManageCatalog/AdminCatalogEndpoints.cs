using Bugler.Registry.Catalog;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Registry.ManageCatalog;

public sealed record ApplicationDto(Guid Id, string Name, DateTimeOffset CreatedAt);

public sealed record CreateApplicationRequest(string Name);

public sealed record InstanceDto(
    Guid Id, Guid ApplicationId, string Name, int? RetentionDays, DateTimeOffset CreatedAt);

public sealed record CreateInstanceRequest(Guid ApplicationId, string Name, int? RetentionDays);

public sealed record SetRetentionRequest(int? RetentionDays);

internal static class AdminCatalogEndpoints
{
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
        if (name.Length is 0 or > 200)
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

    public static async Task<IReadOnlyList<InstanceDto>> ListInstances(
        Guid applicationId, RegistryDbContext dbContext, CancellationToken cancellationToken)
    {
        var id = new ApplicationId(applicationId);
        return await dbContext.Instances
            .Where(i => i.ApplicationId == id)
            .OrderBy(i => i.Name)
            .Select(i => new InstanceDto(
                i.Id.Value, i.ApplicationId.Value, i.Name, i.RetentionDays, i.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public static async Task<IResult> CreateInstance(
        CreateInstanceRequest request, RegistryDbContext dbContext, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length is 0 or > 200)
        {
            return Results.BadRequest("Instance name must be 1-200 characters.");
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

        if (await dbContext.Instances.AnyAsync(
                i => i.ApplicationId == applicationId && i.Name == name, cancellationToken))
        {
            return Results.Conflict("An instance with this name already exists for the application.");
        }

        var instance = new Instance
        {
            Id = InstanceId.New(),
            ApplicationId = applicationId,
            Name = name,
            RetentionDays = request.RetentionDays,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.Instances.Add(instance);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new InstanceDto(
            instance.Id.Value, instance.ApplicationId.Value, instance.Name,
            instance.RetentionDays, instance.CreatedAt));
    }

    public static async Task<IResult> SetRetention(
        Guid id, SetRetentionRequest request, RegistryDbContext dbContext, CancellationToken cancellationToken)
    {
        if (request.RetentionDays is < 1)
        {
            return Results.BadRequest("Retention must be at least 1 day.");
        }

        var instanceId = new InstanceId(id);
        var instance = await dbContext.Instances
            .FirstOrDefaultAsync(i => i.Id == instanceId, cancellationToken);
        if (instance is null)
        {
            return Results.NotFound();
        }

        instance.RetentionDays = request.RetentionDays;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}
