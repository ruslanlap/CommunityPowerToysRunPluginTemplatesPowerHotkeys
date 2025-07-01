// ===== 8. Services/Caching/ICacheService.cs =====
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Community.PowerToys.Run.Plugin.Hotkeys.Services.Caching
{
    public interface ICacheService
    {
        Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default);
        Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default);
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);
        Task ClearAsync(CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan expiration, CancellationToken cancellationToken = default);
    }

    public class CacheKeys
    {
        public const string SearchResults = "search_results";
        public const string ShortcutsBySource = "shortcuts_by_source";
        public const string AllShortcuts = "all_shortcuts";
        public const string UsageStatistics = "usage_statistics";
        public const string AbbreviationCache = "abbreviation_cache";

        public static string SearchKey(string query, string appFilter = null)
        {
            var key = $"{SearchResults}:{query.ToLowerInvariant()}";
            if (!string.IsNullOrWhiteSpace(appFilter))
                key += $":{appFilter.ToLowerInvariant()}";
            return key;
        }

        public static string UsageKey(string shortcutKey) => $"{UsageStatistics}:{shortcutKey}";
        public static string AbbreviationKey(string term) => $"{AbbreviationCache}:{term.ToLowerInvariant()}";
    }
}