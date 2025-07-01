using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ManagedCommon;
using PowerToysRun.ShortcutFinder.Plugin.Helpers;
using Wox.Plugin;
using Community.PowerToys.Run.Plugin.Hotkeys.Models;

namespace Community.PowerToys.Run.Plugin.Hotkeys.Services
{
    public class QueryProcessor : IQueryProcessor
    {
        private readonly IShortcutRepository _repository;
        private readonly ILogger _logger;
        private readonly PluginInitContext _context;
        private readonly ISearchEngine _searchEngine;

        public QueryProcessor(IShortcutRepository repository, ILogger logger, PluginInitContext context, ISearchEngine searchEngine)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _searchEngine = searchEngine ?? throw new ArgumentNullException(nameof(searchEngine));
        }

        public async Task<List<Result>> ProcessQueryAsync(Query query, string iconPath, CancellationToken cancellationToken = default)
        {
            var search = query.Search?.Trim();

            if (string.IsNullOrWhiteSpace(search))
            {
                return GetHelpResults(iconPath);
            }

            var parsedQuery = ParseQuery(search);

            try
            {
                return parsedQuery.Command.ToLowerInvariant() switch
                {
                    "list" => await GetAppShortcutsAsync(parsedQuery.AppFilter ?? parsedQuery.SearchTerm, iconPath, cancellationToken),
                    "apps" => await GetAvailableAppsAsync(iconPath, cancellationToken),
                    "search" or _ => await SearchShortcutsAsync(parsedQuery.SearchTerm, parsedQuery.AppFilter, search, iconPath, cancellationToken)
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error processing query '{search}': {ex.Message}", ex);
                return GetErrorResults(search, iconPath, ex.Message);
            }
        }

        private ParsedQuery ParseQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new ParsedQuery("search", null, "");

            query = query.Trim();

            if (query.Equals("apps", StringComparison.OrdinalIgnoreCase))
                return new ParsedQuery("apps", null, null);

            if (query.StartsWith("list:", StringComparison.OrdinalIgnoreCase))
            {
                string app = query.Substring(5).Trim();
                return new ParsedQuery("list", app, app);
            }

            var parts = query.Split(new[] { '/' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 2)
            {
                return new ParsedQuery("search", parts[1].Trim(), parts[0].Trim());
            }
            else if (query.StartsWith("/"))
            {
                string appFilter = query.Substring(1).Trim();
                return new ParsedQuery("list", appFilter, appFilter);
            }

            return new ParsedQuery("search", null, query);
        }

        // === ОНОВЛЕНА ВЕРСІЯ ===
        private async Task<List<Result>> SearchShortcutsAsync(string searchTerm, string appFilter, string originalQuery, string iconPath, CancellationToken cancellationToken)
        {
            var searchOptions = new SearchOptions
            {
                EnableFuzzySearch = true,
                EnableAbbreviationSearch = true,
                UseCache = true,
                MaxResults = 50,
                FuzzyThreshold = 60.0,
                BoostRecentlyUsed = true,
                BoostPopularApps = true
            };

            var searchResults = await _searchEngine.SearchShortcutsAsync(searchTerm, appFilter, searchOptions, cancellationToken);
            var results = new List<Result>();

            foreach (var searchResult in searchResults)
            {
                var shortcut = searchResult.Shortcut;

                // Create enhanced subtitle with match information
                var subtitle = FormatEnhancedSubTitle(shortcut, appFilter, searchResult);

                results.Add(new Result
                {
                    QueryTextDisplay = originalQuery,
                    IcoPath = iconPath,
                    Title = FormatTitle(shortcut),
                    SubTitle = subtitle,
                    ToolTipData = CreateEnhancedToolTip(shortcut, searchResult),
                    Score = (int)searchResult.Score,
                    Action = _ => CopyShortcutActionWithStats(shortcut),
                    ContextData = shortcut
                });
            }

            if (results.Count == 0)
            {
                results.Add(CreateNoResultsFound(searchTerm, appFilter, originalQuery, iconPath));
            }

            return results;
        }

        private string FormatEnhancedSubTitle(ShortcutInfo shortcut, string appFilter, SearchResult searchResult)
        {
            var subtitle = $"{shortcut.Source} | {shortcut.Category ?? "General"}";

            // Add match type indicator
            var matchIndicator = searchResult.MatchType switch
            {
                SearchMatchType.ExactMatch => "🎯",
                SearchMatchType.FuzzyMatch => "🔍",
                SearchMatchType.AbbreviationMatch => "📝",
                SearchMatchType.PartialMatch => "⭐",
                SearchMatchType.KeywordMatch => "🏷️",
                SearchMatchType.CategoryMatch => "📁",
                _ => ""
            };

            if (!string.IsNullOrEmpty(matchIndicator))
                subtitle = $"{matchIndicator} {subtitle}";

            // Add cache indicator
            if (searchResult.IsFromCache)
                subtitle += " ⚡";

            // Add usage count if significant
            if (shortcut.UsageCount > 0)
                subtitle += $" • Used {shortcut.UsageCount}x";

            // Add app filter indicator
            if (!string.IsNullOrWhiteSpace(appFilter))
                subtitle = $"📍 {subtitle} (filtered by {appFilter})";

            return subtitle;
        }

        private ToolTipData CreateEnhancedToolTip(ShortcutInfo shortcut, SearchResult searchResult)
        {
            var details = $"{shortcut.Shortcut}\n\n" +
                          $"Source: {shortcut.Source}\n" +
                          $"Category: {shortcut.Category}\n" +
                          $"Match Type: {searchResult.MatchType}\n" +
                          $"Score: {searchResult.Score:F1}";

            if (searchResult.MatchedTerms?.Any() == true)
            {
                details += $"\nMatched: {string.Join(", ", searchResult.MatchedTerms)}";
            }

            if (shortcut.UsageCount > 0)
            {
                details += $"\nUsage Count: {shortcut.UsageCount}";
            }

            if (!string.IsNullOrWhiteSpace(shortcut.Platform))
            {
                details += $"\nPlatform: {shortcut.Platform}";
            }

            if (!string.IsNullOrWhiteSpace(shortcut.Difficulty))
            {
                details += $"\nDifficulty: {shortcut.Difficulty}";
            }

            if (shortcut.IsGlobal)
            {
                details += "\nGlobal: Yes";
            }

            return new ToolTipData(shortcut.Description, details);
        }

        private bool CopyShortcutActionWithStats(ShortcutInfo shortcut)
        {
            try
            {
                ClipboardHelper.SetClipboard(shortcut.Shortcut);

                // Update usage statistics
                _searchEngine.UpdateUsageStatistics(shortcut);

                _context.API.ShowMsg("Hotkey Copied", $"'{shortcut.Shortcut}' copied to clipboard", string.Empty);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to copy shortcut: {ex.Message}", ex);
                _context.API.ShowMsg("Error", "Failed to copy shortcut to clipboard", string.Empty);
                return false;
            }
        }

        // Далі стандартні методи (GetAvailableAppsAsync, GetAppShortcutsAsync, інші) залишаються без змін...

        private async Task<List<Result>> GetAvailableAppsAsync(string iconPath, CancellationToken cancellationToken)
        {
            var shortcutsBySource = await _repository.GetShortcutsBySourceAsync(cancellationToken);
            var results = new List<Result>();

            foreach (var app in shortcutsBySource.Keys.OrderBy(k => k))
            {
                var count = shortcutsBySource[app].Count;
                results.Add(new Result
                {
                    IcoPath = iconPath,
                    Title = $"{app} ({count} shortcuts)",
                    SubTitle = $"Click to see all {app} shortcuts",
                    Action = _ =>
                    {
                        _context.API.ChangeQuery($"hk list:{app}", true);
                        return false;
                    },
                    ContextData = app
                });
            }

            return results;
        }

        private async Task<List<Result>> GetAppShortcutsAsync(string appName, string iconPath, CancellationToken cancellationToken)
        {
            var shortcutsBySource = await _repository.GetShortcutsBySourceAsync(cancellationToken);
            var results = new List<Result>();

            var matchingApps = shortcutsBySource.Keys
                .Where(k => k.ToLowerInvariant().Contains(appName.ToLowerInvariant()))
                .ToList();

            foreach (var app in matchingApps)
            {
                var shortcuts = shortcutsBySource[app];

                foreach (var shortcut in shortcuts.OrderBy(s => s.Category).ThenBy(s => s.Description))
                {
                    results.Add(new Result
                    {
                        IcoPath = iconPath,
                        Title = FormatTitle(shortcut),
                        SubTitle = FormatSubTitle(shortcut),
                        ToolTipData = new ToolTipData(shortcut.Description, $"{shortcut.Shortcut}\n\nCategory: {shortcut.Category}"),
                        Action = _ => CopyShortcutActionWithStats(shortcut),
                        ContextData = shortcut
                    });
                }
            }

            if (results.Count == 0)
            {
                results.Add(new Result
                {
                    IcoPath = iconPath,
                    Title = $"No app found matching '{appName}'",
                    SubTitle = "Type 'apps' to see all available applications",
                    Action = _ =>
                    {
                        _context.API.ChangeQuery("hk apps", true);
                        return false;
                    }
                });
            }

            return results;
        }

        private List<Result> GetHelpResults(string iconPath)
        {
            return new List<Result>
            {
                new Result
                {
                    IcoPath = iconPath,
                    Title = "Search hotkeys by keyword",
                    SubTitle = "Example: 'copy', 'paste', 'ctrl+c'",
                    Action = _ => true
                },
                new Result
                {
                    IcoPath = iconPath,
                    Title = "List all available apps",
                    SubTitle = "Type: apps",
                    Action = _ =>
                    {
                        _context.API.ChangeQuery("hk apps", true);
                        return false;
                    }
                },
                new Result
                {
                    IcoPath = iconPath,
                    Title = "List shortcuts for specific app",
                    SubTitle = "Type: list:appname (e.g., 'list:chrome')",
                    Action = _ => true
                }
            };
        }

        private List<Result> GetErrorResults(string query, string iconPath, string errorMessage)
        {
            return new List<Result>
            {
                new Result
                {
                    IcoPath = iconPath,
                    Title = "Error processing query",
                    SubTitle = $"Query: '{query}' - {errorMessage}",
                    Action = _ => true
                }
            };
        }

        private Result CreateNoResultsFound(string searchTerm, string appFilter, string originalQuery, string iconPath)
        {
            string message = !string.IsNullOrWhiteSpace(appFilter)
                ? $"No hotkeys found for '{searchTerm}' in {appFilter}"
                : $"No hotkeys found for '{searchTerm}'";

            string suggestion = !string.IsNullOrWhiteSpace(appFilter)
                ? $"Try removing /{appFilter} filter or check app name"
                : "Try: 'apps' to see available apps, or 'search /appname' to filter by app";

            return new Result
            {
                QueryTextDisplay = originalQuery,
                IcoPath = iconPath,
                Title = message,
                SubTitle = suggestion,
                Action = _ => true,
                ContextData = originalQuery
            };
        }

        private static string FormatTitle(ShortcutInfo shortcut)
        {
            return $"{shortcut.Shortcut} - {shortcut.Description}";
        }

        private static string FormatSubTitle(ShortcutInfo shortcut, string appFilter = null)
        {
            string subtitle = $"{shortcut.Source} | {shortcut.Category ?? "General"}";

            if (!string.IsNullOrWhiteSpace(appFilter))
            {
                subtitle = $"📍 {subtitle} (filtered by {appFilter})";
            }

            return subtitle;
        }
    }
}
