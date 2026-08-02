using Bugler.SharedKernel;

namespace Bugler.Alerting.Settings;

/// <summary>One Service the Health Check Watch has an address for — everything a sweep needs about it.</summary>
public sealed record WatchedService(ServiceId ServiceId, ApplicationId ApplicationId, string Url);
