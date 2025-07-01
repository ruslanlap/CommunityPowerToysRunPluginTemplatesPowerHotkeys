// ===== 5. Services/Algorithms/FuzzyMatcher.cs =====
using System;
using System.Collections.Generic;
using System.Linq;
using FuzzySharp;
using FuzzySharp.SimilarityRatio;
using FuzzySharp.SimilarityRatio.Scorer.StrategySensitive;
using Community.PowerToys.Run.Plugin.Hotkeys.Models;

namespace Community.PowerToys.Run.Plugin.Hotkeys.Services.Algorithms
{
    public class FuzzyMatcher : IFuzzyMatcher
    {
        private readonly ILogger _logger;

        public FuzzyMatcher(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public double CalculateSimilarity(string query, string target)
        {
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(target))
                return 0.0;

            try
            {
                // Use weighted ratio for best results
                var ratio = Fuzz.WeightedRatio(query.ToLowerInvariant(), target.ToLowerInvariant());
                return ratio;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error calculating fuzzy similarity: {ex.Message}", ex);
                return 0.0;
            }
        }

        public bool IsMatch(string query, string target, double threshold = 60.0)
        {
            return CalculateSimilarity(query, target) >= threshold;
        }

        public List<SearchResult> FindFuzzyMatches(string query, IEnumerable<ShortcutInfo> shortcuts, double threshold = 60.0)
        {
            var results = new List<SearchResult>();

            foreach (var shortcut in shortcuts)
            {
                var bestScore = 0.0;
                var matchedField = "";
                var matchedTerms = new List<string>();

                // Check shortcut combination
                var shortcutScore = CalculateSimilarity(query, shortcut.Shortcut);
                if (shortcutScore > bestScore)
                {
                    bestScore = shortcutScore;
                    matchedField = "Shortcut";
                    matchedTerms = new List<string> { shortcut.Shortcut };
                }

                // Check description
                var descScore = CalculateSimilarity(query, shortcut.Description);
                if (descScore > bestScore)
                {
                    bestScore = descScore;
                    matchedField = "Description";
                    matchedTerms = new List<string> { shortcut.Description };
                }

                // Check keywords
                if (shortcut.Keywords?.Any() == true)
                {
                    foreach (var keyword in shortcut.Keywords)
                    {
                        var keywordScore = CalculateSimilarity(query, keyword);
                        if (keywordScore > bestScore)
                        {
                            bestScore = keywordScore;
                            matchedField = "Keywords";
                            matchedTerms = new List<string> { keyword };
                        }
                    }
                }

                // Check aliases
                if (shortcut.Aliases?.Any() == true)
                {
                    foreach (var alias in shortcut.Aliases)
                    {
                        var aliasScore = CalculateSimilarity(query, alias);
                        if (aliasScore > bestScore)
                        {
                            bestScore = aliasScore;
                            matchedField = "Aliases";
                            matchedTerms = new List<string> { alias };
                        }
                    }
                }

                if (bestScore >= threshold)
                {
                    results.Add(new SearchResult
                    {
                        Shortcut = shortcut,
                        Score = bestScore,
                        MatchType = SearchMatchType.FuzzyMatch,
                        MatchedField = matchedField,
                        MatchedTerms = matchedTerms
                    });
                }
            }

            return results.OrderByDescending(r => r.Score).ToList();
        }
    }
}