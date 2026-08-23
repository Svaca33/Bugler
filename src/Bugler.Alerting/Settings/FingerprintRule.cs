using System.Text.Json.Serialization;

namespace Bugler.Alerting.Settings;

/// <summary>
/// How an Application's Fingerprints are distilled (see CONTEXT.md: Fingerprint Rule), coarsening
/// downwards. Application-wide and no Service may override it, because an Episode reaches across
/// Services (ADR 0034) and the two ends must agree on what "the same trouble" means.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<FingerprintRule>))]
public enum FingerprintRule : short
{
    /// <summary>By the code that threw: the stack trace's frames, hashed with the exception type.</summary>
    ThrowingCode = 1,

    /// <summary>By the kind of failure: the exception type and the template, ignoring the stack.</summary>
    KindOfFailure = 2,

    /// <summary>By what was said: the sender's message template, or the body with its values blanked.</summary>
    WhatWasSaid = 3,
}
