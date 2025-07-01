// ===== 4. Services/Algorithms/IFuzzyMatcher.cs =====
using System.Collections.Generic;
using Community.PowerToys.Run.Plugin.Hotkeys.Models;

namespace Community.PowerToys.Run.Plugin.Hotkeys.Services.Algorithms
{
    public interface IFuzzyMatcher
    {
        double CalculateSimilarity(string query, string target);
        bool IsMatch(string query, string target, double threshold = 60.0);
        List<SearchResult> FindFuzzyMatches(string query, IEnumerable<ShortcutInfo> shortcuts, double threshold = 60.0);
    }

    public interface IAbbreviationMatcher
    {
        bool IsAbbreviationMatch(string abbreviation, string fullText);
        List<SearchResult> FindAbbreviationMatches(string query, IEnumerable<ShortcutInfo> shortcuts);
        string GenerateAbbreviation(string text);
    }

    public interface IScoreCalculator
    {
        double CalculateScore(ShortcutInfo shortcut, SearchQuery query, SearchMatchType matchType);
        double CalculateRelevanceScore(ShortcutInfo shortcut, string query, string appFilter = null);
        double CalculateUsageBoost(ShortcutInfo shortcut);
        double CalculateRecencyBoost(ShortcutInfo shortcut);
    }
}