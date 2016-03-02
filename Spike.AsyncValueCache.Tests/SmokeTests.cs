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

        [Test]
        public async Task TestItemExpiration()
        {
            var cache = new AsyncValueCache();

            var counter = 0;
            Func<string, Task<int>> asyncValueProvisioning = _ => Task.FromResult(Interlocked.Increment(ref counter));

            Assert.AreEqual(1,
                await cache.GetOrAdd(Convert.ToString(1), asyncValueProvisioning, TimeSpan.FromMilliseconds(10)),
                "1 as new item is created");
            Assert.AreEqual(2,
                await cache.GetOrAdd(Convert.ToString(2), asyncValueProvisioning, TimeSpan.FromMilliseconds(50)),
                "2 as new item is created");

            await Task.Delay(30);
            cache.HandleExpiration();

            Assert.AreEqual(3, await cache.GetOrAdd(Convert.ToString(1), asyncValueProvisioning),
                "3 as new item is created");
            Assert.AreEqual(2, await cache.GetOrAdd(Convert.ToString(2), asyncValueProvisioning),
                "2 as cached item is used");
        }

        [Test, TestCaseSource(nameof(MaxItems))]
        public async Task TestLRU(int maxItems)
        {
            var cache = new AsyncValueCache(maxItems);

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