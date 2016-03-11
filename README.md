# Spike.AsyncValueCache

LRU cache for async values

### Introduction

Spike of a simple LRU cache for async values (aka Promises or Futures) with item individual expiration.

### Usage

Provide information on how to compute the async value (e.g. some http call)...

```c#
	Func<string, Task<int>> temperatureProvisioning = async city => await GetTemperatureAsync(city);
```

...and use this function for cache access:

```c#	
    var cities = new[] {"London", "Paris", "London", "Berlin"};
    var cache = new AsyncValueCache<string>();
    foreach (var city in cities)
    {
        var temperature = await cache.GetOrAdd(city, temperatureProvisioning);
        ...
    }
```

### Feedback
Welcome! Just raise an issue or send a pull request.