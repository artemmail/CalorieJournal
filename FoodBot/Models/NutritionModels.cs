using System;

namespace FoodBot.Models
{
    public sealed record MatchedFoodRow(
        string name,
        string usda_id,
        decimal per100g_proteins_g,
        decimal per100g_fats_g,
        decimal per100g_carbs_g,
        decimal kcal_per_g
    );

    public sealed record IngredientMatch(string original, MatchedFoodRow? matched);

    public sealed record Step1Snapshot(
        string dish,
        string[] ingredients,
        decimal[] shares_percent,
        decimal weight_g,
        decimal confidence
    );

    public sealed record NutritionResult(
        string dish,
        string[] ingredients,
        decimal proteins_g,
        decimal fats_g,
        decimal carbs_g,
        decimal calories_kcal,
        decimal weight_g,
        decimal confidence
    );

    public sealed record NutritionConversation(
        Guid ThreadId,
        NutritionResult Result,
        MatchedFoodRow[] MatchedFoods,
        Step1Snapshot Step1,
        string ReasoningPrompt,
        string CalcPlanJson
    );

    // Вспомогательные DTO для парсинга ответа модели
    internal sealed class FinalOuter { public FinalPayload? final { get; set; } }

    // ? public — чтобы не было Inconsistent accessibility в интерфейсе IOpenAiClient
    public sealed class FinalPayload
    {
        public string? dish { get; set; }
        public decimal weight_g { get; set; }
        public decimal proteins_g { get; set; }
        public decimal fats_g { get; set; }
        public decimal carbs_g { get; set; }
        public decimal calories_kcal { get; set; }
        public decimal confidence { get; set; }
        public PerIng[] per_ingredient { get; set; } = Array.Empty<PerIng>();
    }

    // ? public — часть публичного графа типов возвращаемого значения
    public sealed class PerIng
    {
        public string name { get; set; } = "";
        public decimal grams { get; set; }
        public decimal per100g_proteins_g { get; set; }
        public decimal per100g_fats_g { get; set; }
        public decimal per100g_carbs_g { get; set; }
        public decimal kcal_per_g { get; set; }
    }

    /// <summary>
    /// Calculated breakdown of a dish per ingredient (grams, macros and percent of total).
    /// </summary>
    public sealed class ProductInfo
    {
        public string name { get; set; } = "";
        public decimal grams { get; set; }
        public decimal proteins_g { get; set; }
        public decimal fats_g { get; set; }
        public decimal carbs_g { get; set; }
        public decimal calories_kcal { get; set; }
        public decimal percent { get; set; }
    }
}
