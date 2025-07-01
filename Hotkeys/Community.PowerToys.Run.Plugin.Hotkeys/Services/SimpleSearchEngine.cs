
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Community.PowerToys.Run.Plugin.Hotkeys.Models;

namespace Community.PowerToys.Run.Plugin.Hotkeys.Services
{
    public class SimpleSearchEngine : ISearchEngine
    {
        private readonly IShortcutRepository _repository;
        private readonly ILogger _logger;

        public SimpleSearchEngine(IShortcutRepository repository, ILogger logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<List<SearchResult>> SearchShortcutsAsync(string query, string appFilter, SearchOptions options, CancellationToken cancellationToken = default)
        {
            var shortcuts = await _repository.GetAllShortcutsAsync(cancellationToken);
            var results = new List<SearchResult>();

            if (!string.IsNullOrEmpty(appFilter))
            {
                shortcuts = shortcuts.Where(s => s.Source.ToLowerInvariant().Contains(appFilter.ToLowerInvariant())).ToList();
            }

            foreach (var shortcut in shortcuts)
            {
                var score = CalculateScore(shortcut, query);
                if (score > 0)
                {
                    results.Add(new SearchResult
                    {
                        Shortcut = shortcut,
                        Score = score,
                        MatchType = SearchMatchType.PartialMatch,
                        MatchedTerms = new[] { query }
                    });
                }
            }

            return results.OrderByDescending(r => r.Score).Take(options?.MaxResults ?? 50).ToList();
        }

        public void UpdateUsageStatistics(ShortcutInfo shortcut)
        {
            shortcut.UsageCount++;
            shortcut.LastUsed = DateTime.UtcNow;
        }

        public async Task WarmupCacheAsync(CancellationToken cancellationToken = default)
        {
            // Simple warmup - just load all shortcuts
            await _repository.GetAllShortcutsAsync(cancellationToken);
        }

        private double CalculateScore(ShortcutInfo shortcut, string query)
        {
            if (string.IsNullOrEmpty(query)) return 0;

            var lowerQuery = query.ToLowerInvariant();
            var lowerShortcut = shortcut.Shortcut.ToLowerInvariant();
            var lowerDescription = shortcut.Description.ToLowerInvariant();

            // Exact match gets highest score
            if (lowerShortcut.Contains(lowerQuery) || lowerDescription.Contains(lowerQuery))
                return 100;

            // Keyword match
            if (shortcut.Keywords?.Any(k => k.ToLowerInvariant().Contains(lowerQuery)) == true)
                return 80;

            // Partial match
            var words = lowerQuery.Split(' ');
            if (words.Any(w => lowerShortcut.Contains(w) || lowerDescription.Contains(w)))
                return 60;

            return 0;
        }
    }
}
