using System.Text;
using System.Text.RegularExpressions;
using FoodBot.Models;
using FoodBot.Data;

namespace FoodBot.Services;

internal sealed class FoodMatcher
{
    // Лимиты, чтобы не раздувать промпт
    private const int MAX_TABLE_ROWS = 24;      // максимум совпавших строк
    private const int MAX_UNMATCHED = 24;       // максимум ненайденных
    private const int MAX_NAME_LEN = 40;

    private readonly FoodDb _db;
    public FoodMatcher(FoodDb db) => _db = db;

    public IngredientMatch[] MatchFoodsDetailed(IEnumerable<string> ingredients)
    {
        var res = new List<IngredientMatch>();
        foreach (var ing in ingredients)
        {
            var f = _db.FindBest(ing);
            if (f is null)
            {
                res.Add(new IngredientMatch(ing, null));
            }
            else
            {
                var row = new MatchedFoodRow(
                    name: f.name,
                    usda_id: f.usda_id ?? "",
                    per100g_proteins_g: (decimal)f.per100g_proteins_g / 1000m,
                    per100g_fats_g: (decimal)f.per100g_fats_g / 1000m,
                    per100g_carbs_g: (decimal)f.per100g_carbs_g / 1000m,
                    kcal_per_g: (decimal)f.kcal_per_g
                );
                res.Add(new IngredientMatch(ing, row));
            }
        }
        return res.ToArray();
    }

    public MatchedFoodRow[] CollapseMatchedRows(IngredientMatch[] matches)
    {
        return matches
            .Where(m => m.matched is not null)
            .Select(m => m.matched!)
            .GroupBy(r => r.name.ToLowerInvariant())
            .Select(g => g.First())
            .Take(MAX_TABLE_ROWS)
            .ToArray();
    }

    // Компактный JSON для промпта (строка)
    public string BuildFoodsJsonForPrompt(MatchedFoodRow[] rows)
    {
        // руками, чтобы гарантировать компактность и InvariantCulture
        var sb = new StringBuilder(256 + rows.Length * 64);
        sb.Append('[');
        int i = 0;
        foreach (var r in rows)
        {
            if (i++ > 0) sb.Append(',');
            var name = Trim(r.name, MAX_NAME_LEN);
            sb.Append('{');
            sb.Append("\"name\":\"").Append(E(name)).Append("\",");
            sb.Append("\"usda_id\":\"").Append(E(r.usda_id ?? "")).Append("\",");
            sb.Append("\"per100g_proteins_g\":").Append(F(r.per100g_proteins_g)).Append(',');
            sb.Append("\"per100g_fats_g\":").Append(F(r.per100g_fats_g)).Append(',');
            sb.Append("\"per100g_carbs_g\":").Append(F(r.per100g_carbs_g)).Append(',');
            sb.Append("\"kcal_per_g\":").Append(F(r.kcal_per_g));
            sb.Append('}');
            if (i >= MAX_TABLE_ROWS) break;
        }
        sb.Append(']');
        return sb.ToString();
    }

    public string BuildUnmatchedCommaList(IngredientMatch[] matches)
    {
        var list = matches.Where(m => m.matched is null)
                          .Select(m => m.original)
                          .Distinct(StringComparer.OrdinalIgnoreCase)
                          .Take(MAX_UNMATCHED)
                          .ToArray();
        return list.Length == 0 ? "" : string.Join(", ", list);
    }

    private static string Trim(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max - 1) + "…";

    private static string E(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string F(decimal d)
    {
        var x = Math.Round(d, 3, MidpointRounding.AwayFromZero);
        var s = x.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (s.Contains('.')) s = s.TrimEnd('0').TrimEnd('.');
        return s;
    }
}
