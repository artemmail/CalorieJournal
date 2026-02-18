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
    public static MealListItem ToListItem(this MealEntry meal, bool updateQueued = false, int? replacesPendingRequestId = null)
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
            updateQueued,
            updateQueued,
            null,
            replacesPendingRequestId
        );
    }

    /// <summary>
    /// Convert a <see cref="PendingMeal"/> entity to a processing list item.
    /// </summary>
    public static MealListItem ToPendingListItem(this PendingMeal pending)
    {
        var createdAt = pending.DesiredMealTimeUtc ?? pending.CreatedAtUtc;
        var dishName = string.IsNullOrWhiteSpace(pending.Description)
            ? "Фото обрабатывается"
            : pending.Description.Trim();

        return new MealListItem(
            -pending.Id,
            createdAt,
            dishName,
            null,
            null,
            null,
            null,
            null,
            Array.Empty<string>(),
            Array.Empty<ProductInfo>(),
            false,
            true,
            true,
            pending.Id,
            null
        );
    }
}

