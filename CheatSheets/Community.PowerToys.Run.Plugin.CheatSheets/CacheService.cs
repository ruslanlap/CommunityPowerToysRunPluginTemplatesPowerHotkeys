// CacheService.cs - Caching Implementation
using System;
using System.Runtime.Caching;

namespace Community.PowerToys.Run.Plugin.CheatSheets
{
    public class CacheService : IDisposable
  {
      private readonly MemoryCache _cache;
      private bool _disposed;

      public CacheService()
      {
          _cache = new MemoryCache("CheatSheetCache");
      }

      public T Get<T>(string key) where T : class
      {
          return _cache.Get(key) as T;
      }

      public void Set<T>(string key, T value, TimeSpan expiration) where T : class
      {
          var policy = new CacheItemPolicy
          {
              AbsoluteExpiration = DateTimeOffset.Now.Add(expiration)
          };
          _cache.Set(key, value, policy);
      }

      public void Dispose()
      {
          if (!_disposed)
          {
              _cache?.Dispose();
              _disposed = true;
          }
      }
  }
}