// ===== 9. Services/Caching/EasyCacheService.cs =====
using System;
using System.Threading;
using System.Threading.Tasks;
using EasyCaching.Core;
using Community.PowerToys.Run.Plugin.Hotkeys.Services.Caching;

namespace Community.PowerToys.Run.Plugin.Hotkeys.Services.Caching
{
    public class EasyCacheService : ICacheService
    {
        private readonly IEasyCachingProvider _cache;
        private readonly ILogger _logger;

        public EasyCacheService(IEasyCachingProvider cache, ILogger logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _cache.GetAsync<T>(key, cancellationToken);
                return result.HasValue ? result.Value : default(T);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Cache get error for key {key}: {ex.Message}", ex);
                return default(T);
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default)
        {
            try
            {
                await _cache.SetAsync(key, value, expiration, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Cache set error for key {key}: {ex.Message}", ex);
            }
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                await _cache.RemoveAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Cache remove error for key {key}: {ex.Message}", ex);
            }
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _cache.FlushAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Cache clear error: {ex.Message}", ex);
            }
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _cache.ExistsAsync(key, cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Cache exists check error for key {key}: {ex.Message}", ex);
                return false;
            }
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan expiration, CancellationToken cancellationToken = default)
        {
            try
            {
                var cached = await GetAsync<T>(key, cancellationToken);
                if (cached != null && !cached.Equals(default(T)))
                {
                    return cached;
                }

                var value = await factory();
                await SetAsync(key, value, expiration, cancellationToken);
                return value;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Cache get-or-set error for key {key}: {ex.Message}", ex);
                return await factory(); // Fallback to direct execution
            }
        }
    }
}