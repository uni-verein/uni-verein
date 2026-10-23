using System;
using System.Linq;
using F23.StringSimilarity;

namespace UniVerein.Api.Helper;

public static class NameSearchHelper
{
    public const double MinScore = 0.75;
    private static readonly JaroWinkler JaroWinkler = new();

    // Scores how well the search term matches any of the candidates. A substring hit (case-insensitive) anywhere
    // in a candidate scores 1.0, otherwise the best Jaro-Winkler similarity is used so typos still match.
    public static double Score(string searchTerm, params string?[] candidates)
    {
        string needle = searchTerm.Trim();
        string[] values = candidates.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).ToArray();
        if (needle.Length == 0 || values.Length == 0)
            return 0;

        if (values.Any(x => x.Contains(needle, StringComparison.OrdinalIgnoreCase)))
            return 1;

        string lowerNeedle = needle.ToLowerInvariant();
        return values.Max(x => JaroWinkler.Similarity(x.ToLowerInvariant(), lowerNeedle));
    }

    public static bool IsMatch(double score) => score > MinScore;
}
