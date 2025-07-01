// ===== 7. Services/Algorithms/ScoreCalculator.cs =====
using System;
using System.Collections.Generic;
using System.Linq;
using Community.PowerToys.Run.Plugin.Hotkeys.Models;

namespace Community.PowerToys.Run.Plugin.Hotkeys.Services.Algorithms
{
    public class ScoreCalculator : IScoreCalculator
    {
        private readonly ILogger _logger;
        private static readonly Dictionary<string, int> PopularApps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            {"chrome", 100}, {"firefox", 95}, {"edge", 90}, {"safari", 85},
            {"vscode", 100}, {"visual studio", 95}, {"sublime", 85}, {"atom", 80},
            {"word", 95}, {"excel", 95}, {"powerpoint", 90}, {"outlook", 90},
            {"photoshop", 90}, {"illustrator", 85}, {"premiere", 80}, {"after effects", 75},
            {"windows", 100}, {"explorer", 95}, {"notepad", 85}, {"calculator", 80},
            {"teams", 90}, {"slack", 85}, {"discord", 80}, {"zoom", 85},
            {"spotify", 85}, {"vlc", 80}, {"media player", 75}
        };

        public ScoreCalculator(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public double CalculateScore(ShortcutInfo shortcut, SearchQuery query, SearchMatchType matchType)
        {
            double baseScore = GetBaseScoreForMatchType(matchType);

            // Calculate relevance based on query
            double relevanceScore = CalculateRelevanceScore(shortcut, query.Term, query.AppFilter);

            // Apply various boosts
            double usageBoost = query.Options.BoostRecentlyUsed ? CalculateUsageBoost(shortcut) : 0;
            double recencyBoost = query.Options.BoostRecentlyUsed ? CalculateRecencyBoost(shortcut) : 0;
            double popularityBoost = query.Options.BoostPopularApps ? CalculatePopularityBoost(shortcut) : 0;
            double contextBoost = CalculateContextBoost(shortcut, query);

            // Combine scores with weights
            double finalScore = (baseScore * 0.4) + 
                              (relevanceScore * 0.3) + 
                              (usageBoost * 0.1) + 
                              (recencyBoost * 0.1) + 
                              (popularityBoost * 0.05) + 
                              (contextBoost * 0.05);

            return Math.Min(100.0, Math.Max(0.0, finalScore));
        }

        private double GetBaseScoreForMatchType(SearchMatchType matchType)
        {
            return matchType switch
            {
                SearchMatchType.ExactMatch => 100.0,
                SearchMatchType.FuzzyMatch => 80.0,
                SearchMatchType.AbbreviationMatch => 75.0,
                SearchMatchType.PartialMatch => 70.0,
                SearchMatchType.KeywordMatch => 65.0,
                SearchMatchType.CategoryMatch => 60.0,
                _ => 50.0
            };
        }

        public double CalculateRelevanceScore(ShortcutInfo shortcut, string query, string appFilter = null)
        {
            if (string.IsNullOrWhiteSpace(query)) return 0.0;

            double score = 0.0;
            string q = query.ToLowerInvariant();

            // App filter bonus
            if (!string.IsNullOrWhiteSpace(appFilter))
            {
                string filter = appFilter.ToLowerInvariant();
                if (shortcut.Source?.ToLowerInvariant() == filter)
                    score += 30.0;
                else if (shortcut.Source?.ToLowerInvariant().Contains(filter) == true)
                    score += 15.0;
            }

            // Shortcut field scoring
            if (shortcut.Shortcut?.ToLowerInvariant() == q)
                score += 50.0;
            else if (shortcut.Shortcut?.ToLowerInvariant().Contains(q) == true)
                score += 30.0;

            // Description field scoring
            if (shortcut.Description?.ToLowerInvariant() == q)
                score += 45.0;
            else if (shortcut.Description?.ToLowerInvariant().StartsWith(q) == true)
                score += 35.0;
            else if (shortcut.Description?.ToLowerInvariant().Contains(q) == true)
                score += 20.0;

            // Keywords scoring
            if (shortcut.Keywords?.Any(k => k.ToLowerInvariant() == q) == true)
                score += 40.0;
            else if (shortcut.Keywords?.Any(k => k.ToLowerInvariant().Contains(q)) == true)
                score += 25.0;

            // Aliases scoring
            if (shortcut.Aliases?.Any(a => a.ToLowerInvariant() == q) == true)
                score += 35.0;
            else if (shortcut.Aliases?.Any(a => a.ToLowerInvariant().Contains(q)) == true)
                score += 20.0;

            // Category scoring
            if (shortcut.Category?.ToLowerInvariant().Contains(q) == true)
                score += 10.0;

            return Math.Min(100.0, score);
        }

        public double CalculateUsageBoost(ShortcutInfo shortcut)
        {
            if (shortcut.UsageCount == 0) return 0.0;

            // Logarithmic scaling for usage count
            double usageScore = Math.Log10(shortcut.UsageCount + 1) * 10.0;
            return Math.Min(20.0, usageScore);
        }

        public double CalculateRecencyBoost(ShortcutInfo shortcut)
        {
            // This would require tracking last used time - placeholder implementation
            // In a real implementation, you'd store LastUsed timestamp in ShortcutInfo
            return 0.0; // TODO: Implement when LastUsed field is added
        }

        private double CalculatePopularityBoost(ShortcutInfo shortcut)
        {
            if (PopularApps.TryGetValue(shortcut.Source ?? "", out int popularityScore))
            {
                return popularityScore / 10.0; // Convert to 0-10 scale
            }
            return 0.0;
        }

        private double CalculateContextBoost(ShortcutInfo shortcut, SearchQuery query)
        {
            double boost = 0.0;

            // Global shortcuts are more valuable
            if (shortcut.IsGlobal)
                boost += 5.0;

            // Beginner-friendly shortcuts get small boost for discoverability
            if (shortcut.Difficulty == "Beginner")
                boost += 2.0;

            // Platform-specific boost (assuming Windows context)
            if (shortcut.Platform == "Windows")
                boost += 3.0;

            return boost;
        }
    }
}