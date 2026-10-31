using System;
using System.Collections.Concurrent;

namespace PersonnelApp.Helpers
{
    /// <summary>
    /// کش درون‌حافظه‌ای با انقضا. Thread-safe و lazy-expire.
    /// </summary>
    public static class CacheHelper
    {
        private sealed class Entry
        {
            public object Value { get; init; } = null!;
            public DateTime ExpiresAt { get; init; }
        }

        private static readonly ConcurrentDictionary<string, Entry> _store = new(StringComparer.Ordinal);
        private static int _lastSweepMinute = -1;

        public static void Set(string key, object value, int durationMinutes = 30)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (durationMinutes <= 0) durationMinutes = 30;
            _store[key] = new Entry
            {
                Value = value,
                ExpiresAt = DateTime.UtcNow.AddMinutes(durationMinutes)
            };
            SweepIfNeeded();
        }

        public static T? Get<T>(string key) where T : class
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            if (!_store.TryGetValue(key, out var entry)) return null;

            if (DateTime.UtcNow > entry.ExpiresAt)
            {
                _store.TryRemove(key, out _);
                return null;
            }
            return entry.Value as T;
        }

        public static bool TryGet<T>(string key, out T? value) where T : class
        {
            value = Get<T>(key);
            return value != null;
        }

        /// <summary>دریافت یا ساخت مقدار (کاهش تکرار الگوی Get/Set).</summary>
        public static T GetOrAdd<T>(string key, Func<T> factory, int durationMinutes = 30) where T : class
        {
            var existing = Get<T>(key);
            if (existing != null) return existing;

            var created = factory();
            if (created != null) Set(key, created, durationMinutes);
            return created;
        }

        public static void Remove(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _store.TryRemove(key, out _);
        }

        public static void ClearAll() => _store.Clear();

        /// <summary>پاک‌سازی دوره‌ای آیتم‌های منقضی (حداکثر یک‌بار در دقیقه).</summary>
        private static void SweepIfNeeded()
        {
            int minute = DateTime.UtcNow.Minute;
            if (minute == System.Threading.Volatile.Read(ref _lastSweepMinute)) return;
            System.Threading.Interlocked.Exchange(ref _lastSweepMinute, minute);

            var now = DateTime.UtcNow;
            foreach (var kv in _store)
                if (now > kv.Value.ExpiresAt)
                    _store.TryRemove(kv.Key, out _);
        }
    }
}