using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Bugler.Alerting.Settings;

namespace Bugler.Alerting.DetectEpisodes;

/// <summary>
/// What a Match offers the recipe: everything the poll reads about one Log Record that could tell
/// one kind of trouble from another. A pure input — the recipe never asks the database anything.
/// </summary>
public sealed record FingerprintEvidence(
    /// <summary>The sender's message template: Serilog's `message_template.text` or MEL's `{OriginalFormat}`.</summary>
    string? Template,
    string? EventName,
    string? Body,
    string? ExceptionType,
    string? ExceptionStack,
    /// <summary>The Runtime as its sender declares it (`telemetry.sdk.language`) — the only thing that says how a stack is read.</summary>
    string? Runtime,
    /// <summary>The value of the Application's named attribute, where this Match carried one.</summary>
    string? NamedAttributeValue = null);

/// <summary>
/// One reading of a Match: the opaque Fingerprint, the readable Title, which rung of the ladder
/// produced them, and whether the stack had to be cut short to be read.
/// </summary>
public sealed record FingerprintReading(
    string Fingerprint,
    string Title,
    FingerprintRung Rung,
    bool StackTruncated);

/// <summary>
/// Distills the kind of trouble a Log Record announces (see CONTEXT.md: Fingerprint) down the
/// ladder of ADR 0033: the named attribute, then the code that threw, then the kind of failure,
/// then what was said. The Application's Fingerprint Rule says which rung to start on; what is
/// not understood falls a rung and the Episode records that it did.
///
/// The answer is a hash, deliberately: a .NET stack trace opens with the exception's own message
/// and a Java one repeats it in every `Caused by:` line, so anything legible enough to be an
/// identity is legible enough to carry a hostname. What a person reads is the Title.
/// </summary>
public static partial class Fingerprint
{
    /// <summary>What the column holds — the Fingerprint's own length is fixed, but legacy rows fill it.</summary>
    public const int MaxLength = 300;

    /// <summary>The readable name takes over the length the old legible Fingerprint had.</summary>
    public const int MaxTitleLength = 300;

    /// <summary>
    /// The version of the recipe below. Stamped on every Episode, because improving a normaliser
    /// re-partitions the space of Fingerprints exactly as changing the Rule does, and both must be
    /// legible after the fact. Legacy rows carry 0 and are never re-fingerprinted.
    /// </summary>
    public const int RecipeVersion = 1;

    /// <summary>
    /// The one kind of trouble the Health Check Watch knows. Reserved rather than derived: a
    /// Service that answers 503 and one that refuses the connection are the same trouble — it
    /// is not answering — so both feed one Episode instead of flapping between two.
    /// </summary>
    public const string HealthCheckFailing = "(health check failing)";

    private const string NoBody = "(no body)";

    public static FingerprintReading Of(FingerprintEvidence evidence, FingerprintRule rule)
    {
        // Above every rung: an attribute the Application named, whose value — where the Match
        // carries one — is the whole answer. A sender that already knows how its troubles group
        // says so, and nothing Bugler distills can beat that.
        if (Trimmed(evidence.NamedAttributeValue) is { } named)
        {
            return new FingerprintReading(
                Hash("attribute", named), Cap(named), FingerprintRung.NamedAttribute, false);
        }

        var said = WhatWasSaid(evidence);
        var type = Trimmed(evidence.ExceptionType);

        if (rule == FingerprintRule.ThrowingCode)
        {
            var stack = StackFrames.Read(evidence.ExceptionStack, evidence.Runtime);
            if (stack.HasFrames)
            {
                return new FingerprintReading(
                    Hash("stack", type ?? "", string.Join("\n", stack.Frames)),
                    Title(type, said),
                    FingerprintRung.Stack,
                    stack.Truncated);
            }

            // No recipe, no frames, no stack: coarsen one rung, and let the Episode say so.
            return Failure(type, said, stack.Truncated);
        }

        return rule == FingerprintRule.KindOfFailure
            ? Failure(type, said, false)
            : new FingerprintReading(
                Hash("message", said), Cap(said), FingerprintRung.Message, false);
    }

    /// <summary>
    /// The kind of failure: the exception type with what was said. With no type there is nothing
    /// left of this rung that the one below does not already say, so it falls through — one
    /// answer, one rung, never two names for the same distillation.
    /// </summary>
    private static FingerprintReading Failure(string? type, string said, bool stackTruncated) =>
        type is null
            ? new FingerprintReading(
                Hash("message", said), Cap(said), FingerprintRung.Message, stackTruncated)
            : new FingerprintReading(
                Hash("failure", type, said), Title(type, said), FingerprintRung.Failure, stackTruncated);

    /// <summary>
    /// What the Match said, in the sender's own words where they travel along: the message
    /// template — Serilog's and MEL's alike — then the semantic event name, then the body with
    /// its variable parts blanked, which is all a sender that declares nothing leaves to go on.
    /// </summary>
    private static string WhatWasSaid(FingerprintEvidence evidence) =>
        Trimmed(evidence.Template)
        ?? Trimmed(evidence.EventName)
        ?? (Trimmed(evidence.Body) is { } body ? Normalize(body) : NoBody);

    /// <summary>
    /// The readable name (see CONTEXT.md: Title). The exception's short name reads better than
    /// its namespace and costs nothing: the Title is never an identity, so two troubles sharing
    /// one is allowed and expected.
    /// </summary>
    private static string Title(string? type, string said) =>
        type is null ? Cap(said) : Cap($"{ShortName(type)}: {said}");

    private static string ShortName(string type)
    {
        var lastDot = type.LastIndexOf('.');
        var name = lastDot >= 0 && lastDot < type.Length - 1 ? type[(lastDot + 1)..] : type;
        return name.Length == 0 ? type : name;
    }

    /// <summary>
    /// The rung's name travels into the hash: two rungs that happened to distill the same words
    /// are still two different questions, and a Rule change must re-partition rather than collide.
    /// The parts are joined on a NUL, which no part can hold, so one part's tail sliding into the
    /// next one's head can never produce the same material twice.
    /// </summary>
    private static string Hash(params string[] parts)
    {
        var material = string.Join((char)0, parts);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexStringLower(digest.AsSpan(0, 16));
    }

    private static string Normalize(string body)
    {
        var result = UuidPattern().Replace(body, "<id>");
        result = LongHexPattern().Replace(result, "<hex>");
        return DigitsPattern().Replace(result, "<n>");
    }

    private static string Cap(string value) =>
        value.Length <= MaxTitleLength ? value : value[..MaxTitleLength];

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex UuidPattern();

    [GeneratedRegex("[0-9a-fA-F]{8,}")]
    private static partial Regex LongHexPattern();

    [GeneratedRegex("[0-9]+")]
    private static partial Regex DigitsPattern();
}
