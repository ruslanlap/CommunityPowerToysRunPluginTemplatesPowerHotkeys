using System;
using System.Runtime.Caching;

namespace Community.PowerToys.Run.Plugin.CheatSheets;

/// <summary>
/// Simple wrapper around <see cref="MemoryCache"/> used by the plugin to cache HTTP responses.
/// </summary>
public sealed class CacheService : IDisposable
{
    private readonly MemoryCache _cache;
    private bool _disposed;

    public CacheService()
    {
        _cache = new MemoryCache("CheatSheetCache");
    }

    public T Get<T>(string key) where T : class
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return _cache.Get(key) as T;
    }

    public void Set<T>(string key, T value, TimeSpan expiration) where T : class
    {
        if (string.IsNullOrWhiteSpace(key) || value is null)
        {
            return;
        }

        var policy = new CacheItemPolicy
        {
            AbsoluteExpiration = DateTimeOffset.Now.Add(expiration)
        };

        _cache.Set(key, value, policy);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cache.Dispose();
        _disposed = true;
    }
}
