using System;
using FoodBot.Models;

namespace FoodBot.Services
{
    public interface INutritionSessionService
    {
        NutritionSession Create(string imageDataUrl);
        bool TryGet(Guid id, out NutritionSession session);
    }
}
