using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace Spike.AsyncValueCache
{
    public class AsyncValueCache : IEnumerable<AsyncValueCache.Item>, IDisposable
    {
        private readonly ConcurrentDictionary<string, Item> _data;
        private readonly TimeSpan _defaultExpiration;
        private readonly int _maxItems;
        private readonly Timer _timer;

        public AsyncValueCache(int maxItems = 1000, TimeSpan? defaultExpiration = null,
            TimeSpan? automaticExpirationHandlerInterval = null)
        {
            _data = new ConcurrentDictionary<string, Item>();
            _maxItems = maxItems;
            _defaultExpiration = defaultExpiration ?? TimeSpan.FromDays(365);
            if (automaticExpirationHandlerInterval.HasValue)
            {
                _timer = new Timer(automaticExpirationHandlerInterval.Value.TotalMilliseconds);
                _timer.Elapsed += (state, args) => HandleExpiration();
                _timer.Start();
            }
        }

        public int Count
        {
            get { return _data.Count; }
        }

        public void Dispose()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
            }
        }

        public IEnumerator<Item> GetEnumerator()
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

            foreach (var item in this.Where(x => x.ExpirationDate <= now).ToList())
            {
                Item removedItem;
                _data.TryRemove(item.Key, out removedItem);
            }

            if (Count > _maxItems)
            {
                var itemsToRemove = this.OrderByDescending(x => x.LastAccessDate)
                    .Skip(_maxItems)
                    .ToList();

                foreach (var item in itemsToRemove)
                {
                    Item removedItem;
                    _data.TryRemove(item.Key, out removedItem);
                }
            }
        }

        public void Clear()
        {
            _data.Clear();
        }

        public async Task<TValue> GetOrAdd<TValue>(string key, Func<string, Task<TValue>> asyncValueProvisioning,
            TimeSpan? expiration = null)
        {
            Func<string, Task<object>> asyncValueFactory = async currentKey => await asyncValueProvisioning(currentKey);
            var item = _data.GetOrAdd(key,
                currentKey =>
                    new Item(currentKey, async () => await asyncValueFactory(currentKey),
                        DateTime.UtcNow.Add(expiration ?? _defaultExpiration)));
            var value = await item.LazyAsyncValue.Value;
            item.LastAccessDate = DateTime.UtcNow;
            return (TValue) value;
        }

        public class Item
        {
            public Item(string key, Func<Task<object>> asyncValueFactory, DateTime expirationDate)
            {
                Key = key;
                LazyAsyncValue = new Lazy<Task<object>>(asyncValueFactory);
                ExpirationDate = expirationDate;
                LastAccessDate = DateTime.UtcNow;
            }

            public string Key { get; }

            public Lazy<Task<object>> LazyAsyncValue { get; }

            public DateTime ExpirationDate { get; }

            public DateTime LastAccessDate { get; set; }
        }
    }
}