using System.Diagnostics.CodeAnalysis;

namespace Bugler.Ingestion.OtlpMapping;

/// <summary>
/// Turns what a sender said into a form storage will hold.
///
/// U+0000 is the whole of it, and it is refused twice over. A text column receives the raw byte and
/// PostgreSQL answers that it is not a valid UTF-8 sequence — a NUL is perfectly valid UTF-8, the
/// text types simply will not hold one. A jsonb column never sees the byte, because the JSON writer
/// turns it into the escape for U+0000, and jsonb refuses that too: valid JSON, untranslatable to
/// text. Escaping more carefully therefore fixes nothing.
///
/// Nothing else needs taming. Every OTLP string arrives through the protobuf decoder, so a byte
/// sequence that is not valid UTF-8 either comes back as U+FFFD already or fails the parse and the
/// Export Request is refused at the door — an unpaired surrogate cannot reach a mapper either way.
/// The remaining C0 controls are stored by text and escaped by jsonb without complaint, and a log
/// body without its tabs and newlines is a stack trace nobody can read.
///
/// Replaced rather than dropped. Dropping would silently rewrite what the sender said — a byte
/// array logged as text would come back shorter than it was sent — while U+FFFD is the character
/// Unicode defines for exactly this and keeps Bugler doing what it does everywhere else: showing
/// what the sender said instead of correcting it on their behalf (ADR 0006).
///
/// This belongs to mapping, not to writing: by the time a row reaches the writer its jsonb columns
/// are serialized text, where the escape is indistinguishable from one a sender legitimately typed.
/// </summary>
internal static class StorableText
{
    private const char Unstorable = '\0';
    private const char Replacement = '�';

    /// <summary>
    /// The value as storage can hold it. Returns the very same instance where there is nothing to
    /// tame, which is every string of all but a handful of Export Requests.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? Tame(string? value) =>
        value is null || !value.Contains(Unstorable)
            ? value
            : value.Replace(Unstorable, Replacement);
}
