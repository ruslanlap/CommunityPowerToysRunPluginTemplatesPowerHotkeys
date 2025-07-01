// ===== 10. Services/EnhancedSearchEngine.cs =====
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Community.PowerToys.Run.Plugin.Hotkeys.Models;
using Community.PowerToys.Run.Plugin.Hotkeys.Services.Algorithms;
using Community.PowerToys.Run.Plugin.Hotkeys.Services.Caching;

namespace Community.PowerToys.Run.Plugin.Hotkeys.Services
{
    public class EnhancedSearchEngine : ISearchEngine, IDisposable
    {
        private readonly IShortcutRepository _repository;
        private readonly IFuzzyMatcher _fuzzyMatcher;
        private readonly IAbbreviationMatcher _abbreviationMatcher;
        private readonly IScoreCalculator _scoreCalculator;
        private readonly ICacheService _cache;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _searchSemaphore;

        // Cache expiration times
        private static readonly TimeSpan SearchCacheExpiration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan DataCacheExpiration = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan UsageCacheExpiration = TimeSpan.FromHours(24);

        public EnhancedSearchEngine(
            IShortcutRepository repository,
            IFuzzyMatcher fuzzyMatcher,
            IAbbreviationMatcher abbreviationMatcher,
            IScoreCalculator scoreCalculator,
            ICacheService cache,
            ILogger logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _fuzzyMatcher = fuzzyMatcher ?? throw new ArgumentNullException(nameof(fuzzyMatcher));
            _abbreviationMatcher = abbreviationMatcher ?? throw new ArgumentNullException(nameof(abbreviationMatcher));
            _scoreCalculator = scoreCalculator ?? throw new ArgumentNullException(nameof(scoreCalculator));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _searchSemaphore = new SemaphoreSlim(3, 3); // Allow up to 3 concurrent searches
        }

        public async Task<List<SearchResult>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
        {
            return await SearchShortcutsAsync(query.Term, query.AppFilter, query.Options, cancellationToken);
        }

        public async Task<List<SearchResult>> SearchShortcutsAsync(string term, string appFilter = null, SearchOptions options = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return new List<SearchResult>();
            }

            options ??= new SearchOptions();
            var searchQuery = new SearchQuery { Term = term, AppFilter = appFilter, Options = options };

            await _searchSemaphore.WaitAsync(cancellationToken);
            try
            {
                // Check cache first if enabled
                if (options.UseCache)
                {
                    var cacheKey = CacheKeys.SearchKey(term, appFilter);
                    var cachedResults = await _cache.GetAsync<List<SearchResult>>(cacheKey, cancellationToken);

                    if (cachedResults?.Any() == true)
                    {
                        _logger?.LogDebug($"Cache hit for search query: {term}");
                        foreach (var result in cachedResults)
                        {
                            result.IsFromCache = true;
                        }
                        return cachedResults.Take(options.MaxResults).ToList();
                    }
                }

                _logger?.LogDebug($"Performing search for: {term} (filter: {appFilter})");

                // Get all shortcuts
                var allShortcuts = await GetShortcutsAsync(appFilter, cancellationToken);
                if (!allShortcuts.Any())
                {
                    return new List<SearchResult>();
                }

                // Perform different types of searches
                var allResults = new List<SearchResult>();

                // 1. Exact matches (highest priority)
                var exactResults = await FindExactMatches(term, allShortcuts, searchQuery);
                allResults.AddRange(exactResults);

                // 2. Partial matches
                var partialResults = await FindPartialMatches(term, allShortcuts, searchQuery);
                allResults.AddRange(partialResults);

                // 3. Fuzzy matches (if enabled)
                if (options.EnableFuzzySearch)
                {
                    var fuzzyResults = await FindFuzzyMatches(term, allShortcuts, searchQuery, options.FuzzyThreshold);
                    allResults.AddRange(fuzzyResults);
                }

                // 4. Abbreviation matches (if enabled)
                if (options.EnableAbbreviationSearch)
                {
                    var abbreviationResults = await FindAbbreviationMatches(term, allShortcuts, searchQuery);
                    allResults.AddRange(abbreviationResults);
                }

                // Remove duplicates and sort by score
                var uniqueResults = RemoveDuplicates(allResults);
                var sortedResults = uniqueResults.OrderByDescending(r => r.Score).Take(options.MaxResults).ToList();

                // Cache results if enabled
                if (options.UseCache && sortedResults.Any())
                {
                    var cacheKey = CacheKeys.SearchKey(term, appFilter);
                    await _cache.SetAsync(cacheKey, sortedResults, SearchCacheExpiration, cancellationToken);
                }

                _logger?.LogDebug($"Search completed. Found {sortedResults.Count} results for: {term}");
                return sortedResults;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Search error for term '{term}': {ex.Message}", ex);
                return new List<SearchResult>();
            }
            finally
            {
                _searchSemaphore.Release();
            }
        }

        private async Task<List<ShortcutInfo>> GetShortcutsAsync(string appFilter, CancellationToken cancellationToken)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(appFilter))
                {
                    var shortcutsBySource = await _cache.GetOrSetAsync(
                        CacheKeys.ShortcutsBySource,
                        () => _repository.GetShortcutsBySourceAsync(cancellationToken),
                        DataCacheExpiration,
                        cancellationToken);

                    var matchingApps = shortcutsBySource.Keys
                        .Where(k => k.ToLowerInvariant().Contains(appFilter.ToLowerInvariant()))
                        .ToList();

                    return matchingApps.SelectMany(app => shortcutsBySource[app]).ToList();
                }
                else
                {
                    return await _cache.GetOrSetAsync(
                        CacheKeys.AllShortcuts,
                        () => _repository.GetAllShortcutsAsync(cancellationToken),
                        DataCacheExpiration,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error getting shortcuts: {ex.Message}", ex);
                return new List<ShortcutInfo>();
            }
        }

        private async Task<List<SearchResult>> FindExactMatches(string term, IEnumerable<ShortcutInfo> shortcuts, SearchQuery query)
        {
            return await Task.Run(() =>
            {
                var results = new List<SearchResult>();
                var termLower = term.ToLowerInvariant();

                foreach (var shortcut in shortcuts)
                {
                    var matchedFields = new List<string>();

                    if (shortcut.Shortcut?.ToLowerInvariant() == termLower)
                        matchedFields.Add("Shortcut");
                    if (shortcut.Description?.ToLowerInvariant() == termLower)
                        matchedFields.Add("Description");
                    if (shortcut.Keywords?.Any(k => k.ToLowerInvariant() == termLower) == true)
                        matchedFields.Add("Keywords");
                    if (shortcut.Aliases?.Any(a => a.ToLowerInvariant() == termLower) == true)
                        matchedFields.Add("Aliases");

                    if (matchedFields.Any())
                    {
                        var score = _scoreCalculator.CalculateScore(shortcut, query, SearchMatchType.ExactMatch);
                        results.Add(new SearchResult
                        {
                            Shortcut = shortcut,
                            Score = score,
                            MatchType = SearchMatchType.ExactMatch,
                            MatchedField = string.Join(", ", matchedFields),
                            MatchedTerms = new List<string> { term }
                        });
                    }
                }

                return results;
            });
        }

        private async Task<List<SearchResult>> FindPartialMatches(string term, IEnumerable<ShortcutInfo> shortcuts, SearchQuery query)
        {
            return await Task.Run(() =>
            {
                var results = new List<SearchResult>();
                var termLower = term.ToLowerInvariant();

                foreach (var shortcut in shortcuts)
                {
                    var matchedFields = new List<string>();
                    var matchedTerms = new List<string>();

                    if (shortcut.Shortcut?.ToLowerInvariant().Contains(termLower) == true)
                    {
                        matchedFields.Add("Shortcut");
                        matchedTerms.Add(shortcut.Shortcut);
                    }
                    if (shortcut.Description?.ToLowerInvariant().Contains(termLower) == true)
                    {
                        matchedFields.Add("Description");
                        matchedTerms.Add(shortcut.Description);
                    }
                    if (shortcut.Keywords?.Any(k => k.ToLowerInvariant().Contains(termLower)) == true)
                    {
                        matchedFields.Add("Keywords");
                        matchedTerms.AddRange(shortcut.Keywords.Where(k => k.ToLowerInvariant().Contains(termLower)));
                    }
                    if (shortcut.Aliases?.Any(a => a.ToLowerInvariant().Contains(termLower)) == true)
                    {
                        matchedFields.Add("Aliases");
                        matchedTerms.AddRange(shortcut.Aliases.Where(a => a.ToLowerInvariant().Contains(termLower)));
                    }
                    if (shortcut.Category?.ToLowerInvariant().Contains(termLower) == true)
                    {
                        matchedFields.Add("Category");
                        matchedTerms.Add(shortcut.Category);
                    }

                    if (matchedFields.Any())
                    {
                        var score = _scoreCalculator.CalculateScore(shortcut, query, SearchMatchType.PartialMatch);
                        results.Add(new SearchResult
                        {
                            Shortcut = shortcut,
                            Score = score,
                            MatchType = SearchMatchType.PartialMatch,
                            MatchedField = string.Join(", ", matchedFields),
                            MatchedTerms = matchedTerms.Distinct().ToList()
                        });
                    }
                }

                return results;
            });
        }

        private async Task<List<SearchResult>> FindFuzzyMatches(string term, IEnumerable<ShortcutInfo> shortcuts, SearchQuery query, double threshold)
        {
            return await Task.Run(() =>
            {
                var fuzzyResults = _fuzzyMatcher.FindFuzzyMatches(term, shortcuts, threshold);

                // Recalculate scores using our comprehensive scoring system
                foreach (var result in fuzzyResults)
                {
                    result.Score = _scoreCalculator.CalculateScore(result.Shortcut, query, SearchMatchType.FuzzyMatch);
                }

                return fuzzyResults;
            });
        }

        private async Task<List<SearchResult>> FindAbbreviationMatches(string term, IEnumerable<ShortcutInfo> shortcuts, SearchQuery query)
        {
            return await Task.Run(() =>
            {
                var abbreviationResults = _abbreviationMatcher.FindAbbreviationMatches(term, shortcuts);

                // Recalculate scores using our comprehensive scoring system
                foreach (var result in abbreviationResults)
                {
                    result.Score = _scoreCalculator.CalculateScore(result.Shortcut, query, SearchMatchType.AbbreviationMatch);
                }

                return abbreviationResults;
            });
        }

        private List<SearchResult> RemoveDuplicates(List<SearchResult> results)
        {
            var seen = new HashSet<string>();
            var uniqueResults = new List<SearchResult>();

            foreach (var result in results)
            {
                var key = $"{result.Shortcut.Source}_{result.Shortcut.Shortcut}_{result.Shortcut.Description}";
                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    uniqueResults.Add(result);
                }
                else
                {
                    // If we've seen this shortcut before, keep the one with higher score
                    var existingIndex = uniqueResults.FindIndex(r => 
                        $"{r.Shortcut.Source}_{r.Shortcut.Shortcut}_{r.Shortcut.Description}" == key);

                    if (existingIndex >= 0 && result.Score > uniqueResults[existingIndex].Score)
                    {
                        uniqueResults[existingIndex] = result;
                    }
                }
            }

            return uniqueResults;
        }

        public void UpdateUsageStatistics(ShortcutInfo shortcut)
        {
            try
            {
                shortcut.UsageCount++;

                // Cache the updated usage count
                var usageKey = CacheKeys.UsageKey($"{shortcut.Source}_{shortcut.Shortcut}_{shortcut.Description}");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _cache.SetAsync(usageKey, shortcut.UsageCount, UsageCacheExpiration);

                        // Invalidate search caches since usage affects scoring
                        await InvalidateSearchCaches();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Error updating usage statistics: {ex.Message}", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error in UpdateUsageStatistics: {ex.Message}", ex);
            }
        }

        public async Task InvalidateCacheAsync()
        {
            try
            {
                await _cache.ClearAsync();
                _logger?.LogInfo("Cache invalidated successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error invalidating cache: {ex.Message}", ex);
            }
        }

        public async Task WarmupCacheAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger?.LogInfo("Starting cache warmup...");

                // Preload all shortcuts
                await _cache.GetOrSetAsync(
                    CacheKeys.AllShortcuts,
                    () => _repository.GetAllShortcutsAsync(cancellationToken),
                    DataCacheExpiration,
                    cancellationToken);

                // Preload shortcuts by source
                await _cache.GetOrSetAsync(
                    CacheKeys.ShortcutsBySource,
                    () => _repository.GetShortcutsBySourceAsync(cancellationToken),
                    DataCacheExpiration,
                    cancellationToken);

                _logger?.LogInfo("Cache warmup completed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error during cache warmup: {ex.Message}", ex);
            }
        }

        private async Task InvalidateSearchCaches()
        {
            try
            {
                // Remove search result caches (they start with the search_results prefix)
                // Note: This is a simplified approach. In production, you might want to 
                // track cache keys more systematically
                await _cache.RemoveAsync(CacheKeys.SearchResults);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error invalidating search caches: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            _searchSemaphore?.Dispose();
        }
    }
}
