using System.Collections.Generic;
using System.Text;

namespace FoodBot.Services
{
    public static class NutritionPromptBuilder
    {
        public static Dictionary<string, decimal> ComputeGramsFromShares(string[] ingredients, decimal[] sharesPercent, decimal weight)
        {
            var dict = new Dictionary<string, decimal>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ingredients.Length; i++)
            {
                var share = (i < sharesPercent.Length) ? sharesPercent[i] : 0m;
                var g = System.Math.Round(weight * share / 100m, 1);
                dict[ingredients[i]] = g;
            }
            return dict;
        }

        public static string BuildFinalPromptFromShares(
            string dish,
            string[] ingredients,
            decimal[] sharesPercent,
            Dictionary<string, decimal> gramsTargets,
            decimal targetWeight,
            string foodsJson,
            string unmatchedList,
            string userNote)
        {
            var sbTarget = new StringBuilder();
            for (int i = 0; i < ingredients.Length; i++)
            {
                var name = ingredients[i];
                var share = i < sharesPercent.Length ? sharesPercent[i] : 0m;
                gramsTargets.TryGetValue(name, out var g);
                sbTarget.AppendLine($"- {name}: {share}%  > target {g} g");
            }

            return $@"
Clarification: {userNote}

Dish: {dish}

You will compute FINAL nutrition for the whole serving.
Use FOODS_JSON as hints only. If a food isn't present there, use reasonable analogs/knowledge.
Hard rules:
• Keep items aligned with the given INGREDIENTS (same order, do not drop items).
• Keep grams per item close to targets; total ? TARGET_WEIGHT_G (±10%).
• Seasoning caps: black pepper 0.3–2 g; salt 1–3 g; dried spices 0.5–3 g; fresh herbs 3–8 g.
• Plausible per-100g ranges (guideline, not output): P?80 g, F?100 g, C?95 g.
• Energy consistency: calories_kcal must equal 4*proteins_g + 9*carbs_g + 4*fats_g within ±3%.
• Output JSON ONLY using the schema below.

INGREDIENTS & TARGET GRAMS:
{sbTarget}

FOODS_JSON (hints; per-100g & kcal_per_g for matched items):
{foodsJson}

UNMATCHED: {(string.IsNullOrWhiteSpace(unmatchedList) ? "(none)" : unmatchedList)}

TARGET_WEIGHT_G: {targetWeight}

Return ONLY:
{{
  ""final"": {{
    ""dish"": ""string"",
    ""weight_g"": number,
    ""proteins_g"": number,
    ""fats_g"": number,
    ""carbs_g"": number,
    ""calories_kcal"": number,
    ""confidence"": number,
    ""per_ingredient"": [
      {{
        ""name"": ""string"",
        ""grams"": number,
        ""per100g_proteins_g"": number,
        ""per100g_fats_g"": number,
        ""per100g_carbs_g"": number,
        ""kcal_per_g"": number
      }}
    ]
  }}
}}";
        }
    }
}