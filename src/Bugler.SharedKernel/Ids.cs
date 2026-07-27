namespace Bugler.SharedKernel;

/// <summary>Identifies an Application — the unit at which users are granted read access.</summary>
public readonly record struct ApplicationId(Guid Value)
{
    public static ApplicationId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies an Instance — a single client deployment of an Application.</summary>
public readonly record struct InstanceId(Guid Value)
{
    public static InstanceId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}
