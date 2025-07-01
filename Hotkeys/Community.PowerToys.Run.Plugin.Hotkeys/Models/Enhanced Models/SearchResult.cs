using System;
using System.Collections.Generic;

namespace Community.PowerToys.Run.Plugin.Hotkeys.Models
{
    public class SearchResult
    {
        public ShortcutInfo Shortcut { get; set; }
        public double Score { get; set; }
        public SearchMatchType MatchType { get; set; }
        public List<string> MatchedTerms { get; set; } = new List<string>();
        public string MatchedField { get; set; }
        public bool IsFromCache { get; set; }
        public DateTime SearchTime { get; set; } = DateTime.UtcNow;
    }

    public enum SearchMatchType
    {
        ExactMatch,
        FuzzyMatch,
        AbbreviationMatch,
        PartialMatch,
        KeywordMatch,
        CategoryMatch
    }

    public class SearchQuery
    {
        public string Term { get; set; }
        public string AppFilter { get; set; }
        public SearchOptions Options { get; set; } = new SearchOptions();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class SearchOptions
    {
        public bool EnableFuzzySearch { get; set; } = true;
        public bool EnableAbbreviationSearch { get; set; } = true;
        public bool UseCache { get; set; } = true;
        public int MaxResults { get; set; } = 50;
        public double FuzzyThreshold { get; set; } = 60.0; // Minimum fuzzy score (0-100)
        public bool BoostRecentlyUsed { get; set; } = true;
        public bool BoostPopularApps { get; set; } = true;
    }
}