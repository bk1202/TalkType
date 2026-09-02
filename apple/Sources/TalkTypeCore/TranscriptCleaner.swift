import Foundation

public enum TranscriptCleaner {
    public static func clean(_ input: String) -> String {
        var result = input
        result = replacing(#"(?i)(?<![\p{L}\p{N}])(?:um+|uh+|erm+|hmm+)(?:\s*,)?(?![\p{L}\p{N}])"#, in: result, with: " ")
        result = replacing(#"\s+([,.!?;:])"#, in: result, with: "$1")
        result = replacing(#"\s{2,}"#, in: result, with: " ")
        return result.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private static func replacing(_ pattern: String, in input: String, with template: String) -> String {
        guard let expression = try? NSRegularExpression(pattern: pattern) else { return input }
        let range = NSRange(input.startIndex..<input.endIndex, in: input)
        return expression.stringByReplacingMatches(in: input, range: range, withTemplate: template)
    }
}
