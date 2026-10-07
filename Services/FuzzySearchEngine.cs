using System.Globalization;
using System.Text;
using SpotlightNative.Models;

namespace SpotlightNative.Services;

public sealed class FuzzySearchEngine
{
    private readonly IReadOnlyList<SearchResult> _candidates;
    private readonly UsageStore? _usage;
    private readonly int _maxEditDistance;
    private readonly bool _enabled;

    public FuzzySearchEngine(
        IEnumerable<SearchResult> candidates,
        UsageStore? usage = null,
        int maxEditDistance = 2,
        bool enabled = true)
    {
        _candidates = candidates.ToArray();
        _usage = usage;
        _maxEditDistance = Math.Max(0, maxEditDistance);
        _enabled = enabled;
    }

    public IReadOnlyList<SearchResult> Search(string query, int maxResults = 20)
    {
        var normalizedQuery = Normalize(query);
        if (normalizedQuery.Length < 1) return [];
        var queryTokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matches = new List<(SearchResult Result, int Score, int[] Indices)>();
        foreach (var candidate in _candidates)
        {
            var scored = Score(candidate, normalizedQuery, queryTokens);
            if (scored is not null) matches.Add((candidate, scored.Value.Score, scored.Value.Indices));
        }

        return matches
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Result.Title.Length)
            .Take(Math.Max(0, maxResults))
            .Select(match =>
            {
                match.Result.FuzzyScore = match.Score;
                match.Result.MatchedIndices = match.Indices;
                return match.Result;
            })
            .ToArray();
    }

    private (int Score, int[] Indices)? Score(SearchResult result, string query, string[] queryTokens)
    {
        var title = Normalize(result.Title);
        var path = Normalize(result.Path ?? "");
        var titleScore = ScoreText(title, query, queryTokens, out var titleIndices);
        var pathScore = string.IsNullOrEmpty(path) ? null : ScoreText(path, query, queryTokens, out _);
        if (titleScore is null && pathScore is null) return null;
        var score = Math.Max(titleScore?.Score ?? 0, (pathScore?.Score ?? 0) / 4) +
                    (int)Math.Round(_usage?.GetBoost(result.Path) ?? 0);
        return (score, titleScore?.Indices ?? []);
    }

    private (int Score, int[] Indices)? ScoreText(string text, string query, string[] queryTokens, out int[] indices)
    {
        indices = [];
        if (text.Length == 0) return null;
        var score = 0;
        var allIndices = new List<int>();
        foreach (var token in queryTokens)
        {
            var tokenScore = ScoreToken(text, token, out var tokenIndices);
            if (tokenScore is null) return null;
            score += tokenScore.Value.Score;
            allIndices.AddRange(tokenIndices);
        }
        indices = allIndices.Distinct().OrderBy(index => index).ToArray();
        return (score, indices);
    }

    private (int Score, int[] Indices)? ScoreToken(string text, string token, out int[] indices)
    {
        indices = [];
        var exact = text.Equals(token, StringComparison.Ordinal) ? 1000 : 0;
        var prefix = text.StartsWith(token, StringComparison.Ordinal) ? 760 : 0;
        var wordStart = IsWordStart(text, token) ? 600 : 0;
        var substring = text.Contains(token, StringComparison.Ordinal) ? 420 : 0;
        if (exact > 0 || prefix > 0 || wordStart > 0 || substring > 0)
        {
            var position = text.IndexOf(token, StringComparison.Ordinal);
            indices = Enumerable.Range(Math.Max(0, position), token.Length).ToArray();
            return (exact + prefix + wordStart + substring + BoundaryBonus(text, indices) - text.Length, indices);
        }

        if (!_enabled || token.Length < 1) return null;
        var distance = DamerauLevenshtein(token, text);
        var allowed = token.Length <= 2 ? 0 : token.Length <= 5 ? 1 : 2;
        allowed = Math.Min(allowed, _maxEditDistance);
        var subsequence = FindSubsequence(text, token);
        if (subsequence is null && distance > allowed) return null;
        indices = subsequence ?? [];
        var gaps = indices.Length == 0 ? text.Length : indices[^1] - indices[0] + 1 - indices.Length;
        return (260 + (subsequence is not null ? 100 : 0) - gaps * 4 - distance * 45 - text.Length, indices);
    }

    private static bool IsWordStart(string text, string token)
    {
        for (var i = 0; i <= text.Length - token.Length; i++)
            if ((i == 0 || !char.IsLetterOrDigit(text[i - 1])) &&
                text.AsSpan(i).StartsWith(token, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static int BoundaryBonus(string text, IReadOnlyList<int> indices) =>
        indices.Count == 0 ? 0 : indices.Count(i => i == 0 || !char.IsLetterOrDigit(text[i - 1])) * 18;

    private static int[]? FindSubsequence(string text, string token)
    {
        var indices = new int[token.Length];
        var textIndex = 0;
        for (var tokenIndex = 0; tokenIndex < token.Length; tokenIndex++)
        {
            var found = text.IndexOf(token[tokenIndex], textIndex);
            if (found < 0) return null;
            indices[tokenIndex] = found;
            textIndex = found + 1;
        }
        return indices;
    }

    private static int DamerauLevenshtein(string left, string right)
    {
        right = right.Length > left.Length + 2 ? right[..(left.Length + 2)] : right;
        var row = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var previous = row[0];
            row[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var current = row[j];
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), previous + cost);
                if (i > 1 && j > 1 && left[i - 1] == right[j - 2] && left[i - 2] == right[j - 1])
                    row[j] = Math.Min(row[j], row[j - 2] + 1);
                previous = current;
            }
        }
        return row[^1];
    }

    public static string Normalize(string value)
    {
        var form = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(form.Length);
        foreach (var character in form)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(character));
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
