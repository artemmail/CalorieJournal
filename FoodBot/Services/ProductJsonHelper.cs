using System;
using System.Linq;
using System.Text.Json;
using FoodBot.Models;

namespace FoodBot.Services;

public static class ProductJsonHelper
{
    public static string BuildProductsJson(string? calcPlanJson)
    {
        if (string.IsNullOrWhiteSpace(calcPlanJson)) return "[]";
        try
        {
            var final = JsonSerializer.Deserialize<FinalPayload>(calcPlanJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (final?.per_ingredient == null) return "[]";
            var total = final.weight_g > 0 ? final.weight_g : final.per_ingredient.Sum(p => p.grams);
            var parts = final.per_ingredient.Select(p => new ProductInfo
            {
                name = p.name,
                grams = Math.Round(p.grams, 2),
                proteins_g = Math.Round(p.per100g_proteins_g * p.grams / 100m, 2),
                fats_g = Math.Round(p.per100g_fats_g * p.grams / 100m, 2),
                carbs_g = Math.Round(p.per100g_carbs_g * p.grams / 100m, 2),
                calories_kcal = Math.Round(p.kcal_per_g * p.grams, 2),
                percent = total > 0 ? Math.Round(p.grams / total * 100m, 2) : 0m
            }).ToArray();
            return JsonSerializer.Serialize(parts);
        }
        catch
        {
            return "[]";
        }
    }

    public static ProductInfo[] DeserializeProducts(string? productsJson)
    {
        if (string.IsNullOrWhiteSpace(productsJson)) return Array.Empty<ProductInfo>();
        try
        {
            return JsonSerializer.Deserialize<ProductInfo[]>(productsJson,
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? Array.Empty<ProductInfo>();
        }
        catch
        {
            return Array.Empty<ProductInfo>();
        }
    }
}
