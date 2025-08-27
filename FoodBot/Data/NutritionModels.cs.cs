using System;

namespace FoodBot.Services;

// Финальный ответ пользователю (детерминированный подсчёт)
public record NutritionResult(
    string dish,
    string[] ingredients,
    decimal proteins_g,
    decimal fats_g,
    decimal carbs_g,
    decimal calories_kcal,
    decimal weight_g,
    decimal confidence
);

// Совпадение из foods.json (per 100 g + kcal_per_g)
public record MatchedFoodRow(
    string name,
    string usda_id,
    decimal per100g_proteins_g,
    decimal per100g_fats_g,
    decimal per100g_carbs_g,
    decimal kcal_per_g
);

// Отображение исходного ингредиента на строку БД
public record IngredientMatch(string original, MatchedFoodRow? matched);

// Снимок шага 1 (визуалка): состав в процентах
public record Step1Snapshot(
    string dish,
    string[] ingredients,
    decimal[] shares_percent,
    decimal weight_g,
    decimal confidence
);

// Контекст для бота: итог + matched + промежуточки + reasoning
public record NutritionConversation(
    Guid ThreadId,
    NutritionResult Result,
    MatchedFoodRow[] MatchedFoods,
    Step1Snapshot Step1,
    string ReasoningPrompt,
    string CalcPlanJson
);
