using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FluentAzure.Sources;

/// <summary>
/// Thread-safe cache for Key Vault secrets with TTL support.
/// </summary>
internal class KeyVaultSecretCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ILogger? _logger;
    private readonly TimeSpan _defaultTtl;
    private readonly object _cleanupLock = new();
    private DateTime _lastCleanup = DateTime.UtcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyVaultSecretCache"/> class.
    /// </summary>
    /// <param name="defaultTtl">The default TTL for cached entries.</param>
    /// <param name="logger">Optional logger for cache operations.</param>
    public KeyVaultSecretCache(TimeSpan defaultTtl, ILogger? logger = null)
    {
        _defaultTtl = defaultTtl;
        _logger = logger;
    }

    /// <summary>
    /// Gets a cached secret value if it exists and hasn't expired.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The cached value if found and valid.</param>
    /// <returns>True if a valid cached value was found; otherwise, false.</returns>
    public bool TryGetValue(string key, out string? value)
    {
        value = null;

        if (!_cache.TryGetValue(key, out var entry))
        {
            return false;
        }

        if (DateTime.UtcNow > entry.ExpiresAt)
        {
            // Entry has expired, remove it
            _cache.TryRemove(key, out _);
            _logger?.LogDebug("Cache entry for key '{Key}' has expired and was removed", key);
            return false;
        }

        value = entry.Value;
        entry.LastAccessed = DateTime.UtcNow;
        _logger?.LogDebug("Cache hit for key '{Key}'", key);
        return true;
    }

    /// <summary>
    /// Adds or updates a cached secret value.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="ttl">The TTL for this entry. If null, uses the default TTL.</param>
    public void Set(string key, string value, TimeSpan? ttl = null)
    {
        var effectiveTtl = ttl ?? _defaultTtl;

        // Don't cache if TTL is zero or negative
        if (effectiveTtl <= TimeSpan.Zero)
        {
            return;
        }

        var entry = new CacheEntry
        {
            Value = value,
            ExpiresAt = DateTime.UtcNow.Add(effectiveTtl),
            LastAccessed = DateTime.UtcNow
        };

        _cache.AddOrUpdate(key, entry, (_, _) => entry);
        _logger?.LogDebug("Cached value for key '{Key}' with TTL {TTL}", key, effectiveTtl);

        // Periodically clean up expired entries
        TryCleanupExpiredEntries();
    }

    /// <summary>
    /// Removes a cached entry.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    /// <returns>True if the entry was found and removed; otherwise, false.</returns>
    public bool Remove(string key)
    {
        var removed = _cache.TryRemove(key, out _);
        if (removed)
        {
            _logger?.LogDebug("Removed cache entry for key '{Key}'", key);
        }
        return removed;
    }

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    public void Clear()
    {
        var count = _cache.Count;
        _cache.Clear();
        _logger?.LogDebug("Cleared {Count} cache entries", count);
    }

    /// <summary>
    /// Gets the number of cached entries.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Gets all cached keys.
    /// </summary>
    public IEnumerable<string> Keys => _cache.Keys;

    /// <summary>
    /// Checks if a key exists in the cache (regardless of expiration).
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <returns>True if the key exists in the cache; otherwise, false.</returns>
    public bool ContainsKey(string key) => _cache.ContainsKey(key);

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    /// <returns>A dictionary containing cache statistics.</returns>
    public Dictionary<string, object> GetStatistics()
    {
        var now = DateTime.UtcNow;
        var entries = _cache.Values.ToList();
        var expired = entries.Count(e => now > e.ExpiresAt);
        var valid = entries.Count - expired;

        return new Dictionary<string, object>
        {
            ["TotalEntries"] = entries.Count,
            ["ValidEntries"] = valid,
            ["ExpiredEntries"] = expired,
            ["CacheHitRate"] = CalculateHitRate(entries)
        };
    }

    private double CalculateHitRate(List<CacheEntry> entries)
    {
        if (entries.Count == 0) return 0.0;

        var recentAccesses = entries
            .Where(e => DateTime.UtcNow.Subtract(e.LastAccessed) < TimeSpan.FromMinutes(10))
            .Count();

        return entries.Count > 0 ? (double)recentAccesses / entries.Count : 0.0;
    }

    private void TryCleanupExpiredEntries()
    {
        // Only cleanup every 5 minutes to avoid overhead
        if (DateTime.UtcNow.Subtract(_lastCleanup) < TimeSpan.FromMinutes(5))
        {
            return;
        }

        lock (_cleanupLock)
        {
            if (DateTime.UtcNow.Subtract(_lastCleanup) < TimeSpan.FromMinutes(5))
            {
                return;
            }

            var now = DateTime.UtcNow;
            var expiredKeys = _cache
                .Where(kvp => now > kvp.Value.ExpiresAt)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            _lastCleanup = now;

            if (expiredKeys.Count > 0)
            {
                _logger?.LogDebug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
            }
        }
    }

    /// <summary>
    /// Represents a cached entry with expiration and access tracking.
    /// </summary>
    private class CacheEntry
    {
        public string Value { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime LastAccessed { get; set; }
    }
}
