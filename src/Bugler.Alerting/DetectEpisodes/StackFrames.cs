using System.Text.RegularExpressions;

namespace Bugler.Alerting.DetectEpisodes;

/// <summary>
/// What one recipe made of a stack trace: the frames that survived, and whether the poll had to
/// cut the stack short to read it. No frames means the recipe was not used — never a half-parse.
/// </summary>
public sealed record StackReading(IReadOnlyList<string> Frames, bool Truncated)
{
    public static readonly StackReading None = new([], false);

    public bool HasFrames => Frames.Count > 0;
}

/// <summary>
/// Strips a stack trace down to the code that threw (ADR 0033). How a stack trace is written is
/// each Runtime's own affair, so the recipe is chosen by <c>telemetry.sdk.language</c> and the
/// recipes differ more than their shared vocabulary suggests — <c>at</c> opens a frame in five of
/// them and nowhere else, Go's frames are a function line with a discarded file line under it,
/// PHP names its call after the file position, and in Ruby the path <em>is</em> the identity.
///
/// Everything that is not a frame goes: headers, <c>Caused by:</c>, <c>... N more</c>, Python's
/// echoed source lines. They carry the exception's own message, which holds hostnames, ids and
/// transaction numbers — hashing them would mint a Fingerprint per occurrence.
/// </summary>
public static partial class StackFrames
{
    /// <summary>
    /// What the poll puts in place of the middle of a stack too long to read whole. The line it
    /// lands in is the tail of one severed line glued to the head of another, so dropping that
    /// one line drops the damage on both sides of the seam.
    /// </summary>
    public const string TruncationMarker = "[...bugler-truncated...]";

    /// <summary>
    /// Reads <paramref name="stack"/> under the recipe <paramref name="runtime"/> names. An
    /// unknown or absent Runtime tries every recipe in turn and takes the first that finds
    /// anything; a Runtime Bugler has no recipe for is no worse off than one it cannot read.
    /// </summary>
    public static StackReading Read(string? stack, string? runtime)
    {
        if (string.IsNullOrWhiteSpace(stack))
        {
            return StackReading.None;
        }

        var truncated = stack.Contains(TruncationMarker, StringComparison.Ordinal);
        var lines = stack
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            // The severed line either side of the seam: half a frame is not a frame.
            .Where(line => !line.Contains(TruncationMarker, StringComparison.Ordinal))
            .ToList();

        var frames = RecipeFor(runtime) is { } recipe
            ? recipe(lines)
            : FirstRecipeThatReads(lines);

        return frames.Count == 0
            ? new StackReading([], truncated)
            : new StackReading(Normalize(frames), truncated);
    }

    private static Func<IReadOnlyList<string>, List<string>>? RecipeFor(string? runtime) =>
        runtime?.Trim().ToLowerInvariant() switch
        {
            "dotnet" or "java" or "kotlin" or "nodejs" or "webjs" => AtFamily,
            "python" => Python,
            "go" => Go,
            "php" => Php,
            "ruby" => Ruby,
            _ => null,
        };

    /// <summary>The known markers, tried in turn. Still nothing means no frames, and the caller coarsens.</summary>
    private static List<string> FirstRecipeThatReads(IReadOnlyList<string> lines)
    {
        foreach (var recipe in new Func<IReadOnlyList<string>, List<string>>[]
                 { AtFamily, Python, Go, Php, Ruby })
        {
            var frames = recipe(lines);
            if (frames.Count > 0)
            {
                return frames;
            }
        }

        return [];
    }

    /// <summary>
    /// .NET, Java, Kotlin, Node and browser JavaScript: a frame is a line opening with <c>at</c>.
    /// Nothing else qualifies, which is exactly how the header, <c>Caused by:</c>, <c>... N more</c>
    /// and <c>--- End of stack trace from previous location ---</c> all fall away. The source
    /// location goes with them — .NET's <c>in path:line N</c> tail and the <c>(File:12:3)</c>
    /// parenthetical the other four use — leaving the method that ran.
    /// </summary>
    private static List<string> AtFamily(IReadOnlyList<string> lines)
    {
        var frames = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("at ", StringComparison.Ordinal))
            {
                continue;
            }

            var frame = DotNetSourceLocation().Replace(trimmed[3..].Trim(), "");
            frame = BracketedSourceLocation().Replace(frame, "").Trim();
            if (frame.Length > 0)
            {
                frames.Add(frame);
            }
        }

        return frames;
    }

    /// <summary>
    /// Python: the <c>File "...", line N, in f</c> lines and nothing else — the source line echoed
    /// under each frame is the code as written, and two runs of one bug quote it identically only
    /// until somebody reformats the file.
    /// </summary>
    private static List<string> Python(IReadOnlyList<string> lines)
    {
        var frames = new List<string>();
        foreach (var line in lines)
        {
            if (PythonFrame().Match(line) is { Success: true } match)
            {
                frames.Add($"File \"{match.Groups[1].Value}\", line {match.Groups[2].Value}, "
                    + $"in {match.Groups[3].Value.Trim()}");
            }
        }

        return frames;
    }

    /// <summary>
    /// Go: the unindented function line is the frame; the <c>file:line +0x1a</c> under it is
    /// indented and goes, as does the <c>goroutine N [state]:</c> header and the panic message
    /// above it. The argument list goes too — Go prints the actual words of the call, so keeping
    /// them would mint a Fingerprint per occurrence.
    /// </summary>
    private static List<string> Go(IReadOnlyList<string> lines)
    {
        var frames = new List<string>();
        foreach (var line in lines)
        {
            if (line.Length == 0 || char.IsWhiteSpace(line[0]))
            {
                continue; // The file line under a frame.
            }

            var trimmed = line.TrimEnd();
            if (trimmed.StartsWith("created by ", StringComparison.Ordinal))
            {
                frames.Add(GoCreatedByTail().Replace(trimmed, ""));
                continue;
            }

            // A frame is `pkg.Func(args)`: everything before the last bracket is one unbroken
            // word. `panic: ...` and `goroutine 1 [running]:` are not, and that is the whole test.
            var open = trimmed.LastIndexOf('(');
            if (open <= 0 || !trimmed.EndsWith(')'))
            {
                continue;
            }

            var name = trimmed[..open];
            if (!name.Any(char.IsWhiteSpace))
            {
                frames.Add(name);
            }
        }

        return frames;
    }

    /// <summary>
    /// PHP: <c>#N /path/File.php(12): Class-&gt;method(args)</c> — the call is the frame, the path
    /// before it is where the call was made from and the arguments after it are values. Both go.
    /// <c>{closure}</c> and <c>{main}</c> are frames of their own.
    /// </summary>
    private static List<string> Php(IReadOnlyList<string> lines)
    {
        var frames = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!PhpFrameNumber().IsMatch(trimmed))
            {
                continue;
            }

            if (PhpCall().Match(trimmed) is { Success: true } match)
            {
                var call = match.Groups[1].Value.Trim();
                var open = call.IndexOf('(');
                frames.Add(open >= 0 ? call[..open] : call);
            }
            else if (trimmed.EndsWith("{main}", StringComparison.Ordinal))
            {
                frames.Add("{main}");
            }
        }

        return frames;
    }

    /// <summary>
    /// Ruby: anchored on <c>:in 'method'</c>, and the path in front of it is kept because it is
    /// the identity — a frame named <c>block in call</c> says nothing on its own. Whatever a gem
    /// appends past the closing quote is ignored.
    /// </summary>
    private static List<string> Ruby(IReadOnlyList<string> lines)
    {
        var frames = new List<string>();
        foreach (var line in lines)
        {
            if (RubyFrame().Match(line) is { Success: true } match)
            {
                frames.Add($"{match.Groups[1].Value.Trim()}:in '{match.Groups[2].Value}'");
            }
        }

        return frames;
    }

    /// <summary>
    /// What every recipe's frames pass through: digits emptied, so a deploy that shifted a line
    /// does not split one trouble into two, and runs of identical frames collapsed, so recursion
    /// of varying depth stays one bug rather than ten (ADR 0033).
    /// </summary>
    private static List<string> Normalize(List<string> frames)
    {
        var normalized = new List<string>(frames.Count);
        foreach (var frame in frames)
        {
            var blanked = Digits().Replace(frame, "").Trim();
            if (blanked.Length == 0)
            {
                continue;
            }

            if (normalized.Count == 0 || normalized[^1] != blanked)
            {
                normalized.Add(blanked);
            }
        }

        return normalized;
    }

    [GeneratedRegex(@"\s+in\s+.+:line\s+\d+$")]
    private static partial Regex DotNetSourceLocation();

    [GeneratedRegex(@"\s*\([^()]*:\d+[^()]*\)$")]
    private static partial Regex BracketedSourceLocation();

    [GeneratedRegex("""^\s*File "(.*)", line (\d+), in (.+)$""")]
    private static partial Regex PythonFrame();

    [GeneratedRegex(@"\s+in\s+goroutine\s+\d+$")]
    private static partial Regex GoCreatedByTail();

    [GeneratedRegex(@"^#\d+\s")]
    private static partial Regex PhpFrameNumber();

    [GeneratedRegex(@"^#\d+\s+(?:.*?\)|\[internal function\]):\s*(.+)$")]
    private static partial Regex PhpCall();

    [GeneratedRegex("""^(.*?:\d+):in ['`](.+?)'""")]
    private static partial Regex RubyFrame();

    [GeneratedRegex("[0-9]+")]
    private static partial Regex Digits();
}
