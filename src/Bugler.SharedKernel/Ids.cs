namespace Bugler.SharedKernel;

/// <summary>Identifies an Application — the unit at which users are granted read access.</summary>
public readonly record struct ApplicationId(Guid Value)
{
    public static ApplicationId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a Service — one registered sender of telemetry within an Application.</summary>
public readonly record struct ServiceId(Guid Value)
{
    public static ServiceId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}
