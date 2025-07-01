
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Community.PowerToys.Run.Plugin.Hotkeys.Services
{
    public class SimpleCacheService : ICacheService
    {
        private readonly ConcurrentDictionary<string, object> _cache = new();
        private readonly ConcurrentDictionary<string, DateTime> _expiry = new();

        public async Task<T> GetAsync<T>(string key)
        {
            if (_cache.TryGetValue(key, out var value) && _expiry.TryGetValue(key, out var expiry))
            {
                if (DateTime.UtcNow < expiry)
                {
                    return (T)value;
                }
                else
                {
                    _cache.TryRemove(key, out _);
                    _expiry.TryRemove(key, out _);
                }
            }
            return default(T);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            _cache[key] = value;
            _expiry[key] = DateTime.UtcNow.Add(expiration ?? TimeSpan.FromHours(1));
        }

        public async Task RemoveAsync(string key)
        {
            _cache.TryRemove(key, out _);
            _expiry.TryRemove(key, out _);
        }

        public async Task ClearAsync()
        {
            _cache.Clear();
            _expiry.Clear();
        }
    }
}
