// ===== 3. Services/ISearchEngine.cs =====
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Community.PowerToys.Run.Plugin.Hotkeys.Models;

namespace Community.PowerToys.Run.Plugin.Hotkeys.Services
{
    public interface ISearchEngine
    {
        Task<List<SearchResult>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default);
        Task<List<SearchResult>> SearchShortcutsAsync(string term, string appFilter = null, SearchOptions options = null, CancellationToken cancellationToken = default);
        void UpdateUsageStatistics(ShortcutInfo shortcut);
        Task InvalidateCacheAsync();
        Task WarmupCacheAsync(CancellationToken cancellationToken = default);
    }
}