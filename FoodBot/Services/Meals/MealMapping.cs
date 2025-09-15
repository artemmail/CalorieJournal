using System;
using System.Text.Json;
using FoodBot.Data;
using FoodBot.Models;

namespace FoodBot.Services;

/// <summary>
/// Helper extensions for converting meal entities to API contracts.
/// </summary>
public static class MealMapping
{
    /// <summary>
    /// Convert a <see cref="MealEntry"/> entity to a <see cref="MealListItem"/>.
    /// </summary>
    public static MealListItem ToListItem(this MealEntry meal)
    {
        var ingredients = string.IsNullOrWhiteSpace(meal.IngredientsJson)
            ? Array.Empty<string>()
            : (JsonSerializer.Deserialize<string[]>(meal.IngredientsJson!) ?? Array.Empty<string>());

        return new MealListItem(
            meal.Id,
            meal.CreatedAtUtc,
            meal.DishName,
            meal.WeightG,
            meal.CaloriesKcal,
            meal.ProteinsG,
            meal.FatsG,
            meal.CarbsG,
            ingredients,
            ProductJsonHelper.DeserializeProducts(meal.ProductsJson),
            meal.ImageBytes != null && meal.ImageBytes.Length > 0,
            false
        );
    }
}

