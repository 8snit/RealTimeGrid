using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Spike.AsyncValueCache.Tests
{
    [TestFixture]
    public class SmokeTests
    {
        public static IEnumerable<int> MaxItems()
        {
            return Enumerable.Range(1, 5).Select(i => (int) Math.Pow(2, i));
        }

        private async Task<int> GetTemperatureAsync(string city)
        {
            await Task.Delay(100);
            return city.Length;
        }

        [Test]
        public async Task TestDemo()
        {
            Func<string, Task<int>> temperatureProvisioning = async city => await GetTemperatureAsync(city);

            var cities = new[] {"London", "Paris", "London", "Berlin"};
            var cache = new AsyncValueCache<string>();
            foreach (var city in cities)
            {
                var temperature = await cache.GetOrAdd(city, temperatureProvisioning);
                Assert.AreEqual(city.Length, temperature);
                await Task.Delay(1);
            }
            Assert.IsTrue(cache["London"].ExpirationDate < cache["Paris"].ExpirationDate, "cache item created before");
            Assert.IsTrue(cache["London"].LastAccessDate > cache["Paris"].LastAccessDate, "cache item accessed again");
        }

        [Test]
        public async Task TestExpiration()
        {
            var expiration = TimeSpan.FromSeconds(1);
            var cache = new AsyncValueCache<char>(defaultExpiration: expiration);

            Func<char, Task<char>> asyncValueProvisioning = async character =>
            {
                await Task.Delay(200);
                return character;
            };

            await cache.GetOrAdd('x', asyncValueProvisioning);
            var lastAccessDate1 = cache['x'].LastAccessDate;
            var expirationDate1 = cache['x'].ExpirationDate;

            cache.HandleExpiration(); // not expired

            await cache.GetOrAdd('x', asyncValueProvisioning);
            var lastAccessDate2 = cache['x'].LastAccessDate;
            var expirationDate2 = cache['x'].ExpirationDate;

            Assert.AreEqual(expirationDate1, expirationDate2);
            Assert.AreNotEqual(lastAccessDate1, lastAccessDate2);

            await Task.Delay(expiration);
            cache.HandleExpiration(); // expired

            await cache.GetOrAdd('x', asyncValueProvisioning);
            var lastAccessDate3 = cache['x'].LastAccessDate;
            var expirationDate3 = cache['x'].ExpirationDate;

            Assert.AreNotEqual(expirationDate2, expirationDate3);
            Assert.AreNotEqual(lastAccessDate2, lastAccessDate3);

            for (var i = 0; i < 10; i++)
            {
                await cache.GetOrAdd('x', asyncValueProvisioning);
                await Task.Delay(1);
            }

            cache.HandleExpiration(); // not expired 

            var lastAccessDate4 = cache['x'].LastAccessDate;
            var expirationDate4 = cache['x'].ExpirationDate;

            Assert.AreEqual(expirationDate3, expirationDate4);
            Assert.AreNotEqual(lastAccessDate3, lastAccessDate4);
        }

        [Test]
        public async Task TestIndividualItemExpiration()
        {
            var cache = new AsyncValueCache<int>();

            var counter = 0;
            Func<int, Task<int>> asyncValueProvisioning = _ => Task.FromResult(Interlocked.Increment(ref counter));

            Assert.AreEqual(1, await cache.GetOrAdd(1, asyncValueProvisioning, TimeSpan.FromMilliseconds(10)),
                "1 as new item is created");
            Assert.AreEqual(2, await cache.GetOrAdd(2, asyncValueProvisioning, TimeSpan.FromMilliseconds(50)),
                "2 as new item is created");

            await Task.Delay(30); // 2 expired
            cache.HandleExpiration();

            Assert.AreEqual(3, await cache.GetOrAdd(1, asyncValueProvisioning), "3 as new item is created");
            Assert.AreEqual(2, await cache.GetOrAdd(2, asyncValueProvisioning), "2 as cached item is used");
        }

        [Test, TestCaseSource(nameof(MaxItems))]
        public async Task TestLRU(int maxItems)
        {
            var cache = new AsyncValueCache<string>(maxItems);

            var counter = 0;
            Func<string, Task<int>> asyncValueProvisioning = _ => Task.FromResult(Interlocked.Increment(ref counter));

            for (var i = 1; i <= maxItems + 1; i++)
            {
                var key = Convert.ToString(i);
                Assert.AreEqual(i, await cache.GetOrAdd(key, asyncValueProvisioning), key + " as new item is created");
                Assert.AreEqual(i, await cache.GetOrAdd(key, asyncValueProvisioning), key + " as cached item is used");
                await Task.Delay(30);
            }

            cache.HandleExpiration();

            Assert.AreEqual(maxItems, await cache.GetOrAdd(Convert.ToString(maxItems), asyncValueProvisioning),
                maxItems + " as cached item is used");
            Assert.AreEqual(maxItems + 1, await cache.GetOrAdd(Convert.ToString(maxItems + 1), asyncValueProvisioning),
                maxItems + 1 + " as cached item is used");
            Assert.AreNotEqual(1, await cache.GetOrAdd(Convert.ToString(1), asyncValueProvisioning),
                "not " + 1 + " as new item is created");
        }
    }
}