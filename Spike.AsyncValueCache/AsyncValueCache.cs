using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace Spike.AsyncValueCache
{
    public class AsyncValueCache<TKey> : IEnumerable<AsyncValueCache<TKey>.Entry>, IDisposable
    {
        private readonly ConcurrentDictionary<TKey, Entry> _data;
        private readonly TimeSpan _defaultExpiration;
        private readonly int _maxItems;
        private readonly Timer _timer;

        public AsyncValueCache(int maxItems = 1000, TimeSpan? defaultExpiration = null,
            TimeSpan? automaticExpirationHandlerInterval = null)
        {
            _data = new ConcurrentDictionary<TKey, Entry>();
            _maxItems = maxItems;
            _defaultExpiration = defaultExpiration ?? TimeSpan.FromDays(365);
            if (automaticExpirationHandlerInterval.HasValue)
            {
                _timer = new Timer(automaticExpirationHandlerInterval.Value.TotalMilliseconds);
                _timer.Elapsed += (state, args) => HandleExpiration();
                _timer.Start();
            }
        }

        public int Count => _data.Count;

        public Entry this[TKey key]
        {
            get
            {
                Entry entry;
                return !_data.TryGetValue(key, out entry) ? null : entry;
            }
        }

        public void Dispose()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
            }
        }

        public IEnumerator<Entry> GetEnumerator()
        {
            return _data.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void HandleExpiration()
        {
            var now = DateTime.UtcNow;

            foreach (var entry in this.Where(x => x.ExpirationDate <= now).ToList())
            {
                Entry removedItem;
                _data.TryRemove(entry.Key, out removedItem);
            }

            if (Count > _maxItems)
            {
                var itemsToRemove = this.OrderByDescending(x => x.LastAccessDate)
                    .Skip(_maxItems)
                    .ToList();

                foreach (var entry in itemsToRemove)
                {
                    Entry removedItem;
                    _data.TryRemove(entry.Key, out removedItem);
                }
            }
        }

        public void Clear()
        {
            _data.Clear();
        }

        public async Task<TValue> GetOrAdd<TValue>(TKey key, Func<TKey, Task<TValue>> asyncValueProvisioning,
            TimeSpan? expiration = null)
        {
            Func<TKey, Task<object>> asyncValueFactory = async currentKey => await asyncValueProvisioning(currentKey);
            var entry = _data.GetOrAdd(key,
                currentKey =>
                    new Entry(currentKey, async () => await asyncValueFactory(currentKey),
                        DateTime.UtcNow.Add(expiration ?? _defaultExpiration)));
            var value = await entry.LazyAsyncValue.Value;
            entry.LastAccessDate = DateTime.UtcNow;
            return (TValue) value;
        }

        public class Entry
        {
            public Entry(TKey key, Func<Task<object>> asyncValueFactory, DateTime expirationDate)
            {
                Key = key;
                LazyAsyncValue = new Lazy<Task<object>>(asyncValueFactory);
                ExpirationDate = expirationDate;
                LastAccessDate = DateTime.UtcNow;
            }

            public TKey Key { get; }

            public Lazy<Task<object>> LazyAsyncValue { get; }

            public DateTime ExpirationDate { get; }

            public DateTime LastAccessDate { get; set; }
        }
    }
}