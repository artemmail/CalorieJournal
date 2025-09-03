using System;
using FoodBot.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FoodBot.Services
{
    /// <summary>
    /// Manages nutrition analysis sessions using a cache store.
    /// Allows switching to persistent storage in the future.
    /// </summary>
    public class NutritionSessionService : INutritionSessionService
    {
        private readonly IMemoryCache _cache;
        private readonly MemoryCacheEntryOptions _options;

        public NutritionSessionService(IMemoryCache cache)
        {
            _cache = cache;
            _options = new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromHours(1)
            };
        }

        public NutritionSession Create(string imageDataUrl)
        {
            var session = new NutritionSession { ImageDataUrl = imageDataUrl };
            _cache.Set(session.Id, session, _options);
            return session;
        }

        public bool TryGet(Guid id, out NutritionSession session)
        {
            return _cache.TryGetValue(id, out session!);
        }
    }
}
