using System.Text.RegularExpressions;

namespace TalkType.Desktop;

internal static partial class TranscriptCleaner
{
    public static string Clean(string text)
    {
        var cleaned = StandaloneFillers().Replace(text, " ");
        cleaned = RepeatedFillers().Replace(cleaned, "$1");
        cleaned = SpaceBeforePunctuation().Replace(cleaned, "$1");
        cleaned = RepeatedWhitespace().Replace(cleaned, " ").Trim();
        return cleaned;
    }

    [GeneratedRegex(@"(?i)(?<![\p{L}\p{N}])(?:um+|uh+|erm+|hmm+)(?:\s*,)?(?![\p{L}\p{N}])")]
    private static partial Regex StandaloneFillers();
    [GeneratedRegex(@"(?i)\b(like|you know)(?:\s+\1){1,}\b")]
    private static partial Regex RepeatedFillers();
    [GeneratedRegex(@"\s+([,.!?;:])")]
    private static partial Regex SpaceBeforePunctuation();
    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex RepeatedWhitespace();
}
