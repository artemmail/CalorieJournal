using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FoodBot.Services
{
    /// <summary>
    /// Загружает foods.json, нормализует единицы измерения:
    ///  - proteins/fat/carbohydrates в исходном JSON: мг/кг (ppm) → преобразуем в г/100 г (× 0.0001)
    ///  - calories: ккал/г → оставляем как есть (kcal_per_g)
    /// Также предоставляет поиск лучшего совпадения FindBest по названию.
    /// </summary>
    public sealed class FoodDb
    {
        /// <summary>
        /// Нормализованная запись продукта (г/100 г и ккал/г).
        /// </summary>
        public sealed record FoodRow(
            string name,
            decimal per100g_proteins_g,
            decimal per100g_fats_g,
            decimal per100g_carbs_g,
            decimal kcal_per_g,
            string usda_id
        );

        /// <summary>
        /// Сырые поля как в foods.json.
        /// </summary>
        private sealed class RawFood
        {
            public string? name { get; set; }
            public decimal? fat { get; set; }              // mg/kg (ppm)
            public decimal? calories { get; set; }         // kcal/g
            public decimal? proteins { get; set; }         // mg/kg (ppm)
            public decimal? carbohydrates { get; set; }    // mg/kg (ppm)
            public decimal? serving { get; set; }          // grams (опционально)
            public string? usda_id { get; set; }           // опционально
            // nutrients ... (игнорируем на этапе нормализации макро)
        }

        public FoodRow[] Rows { get; }

        // Быстрый индекс по нормализованному имени
        private readonly Dictionary<string, FoodRow> _byNormName;

        public FoodDb(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("foods.json path is empty.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("foods.json not found", path);

            var json = File.ReadAllText(path);
            var raw = JsonSerializer.Deserialize<List<RawFood>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            Rows = raw
                .Where(r => !string.IsNullOrWhiteSpace(r.name))
                .Select(MapToRow)
                .ToArray();

            _byNormName = Rows
                .GroupBy(r => Norm(r.name))
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Диагностика согласованности калорий (не влияет на работу)
            foreach (var r in Rows)
            {
                var kcal100_from_macros = 4m * r.per100g_proteins_g + 9m * r.per100g_fats_g + 4m * r.per100g_carbs_g;
                var kcal100_from_field = r.kcal_per_g * 100m;

                if (kcal100_from_field > 0)
                {
                    var diffAbs = Math.Abs(kcal100_from_macros - kcal100_from_field);
                    var diffRel = diffAbs / kcal100_from_field;
                    if (diffRel > 0.35m)
                    {
                        Console.WriteLine($"[foods.json warn] {r.name}: macros vs kcal mismatch: " +
                                          $"{kcal100_from_macros:F1} vs {kcal100_from_field:F1} kcal/100g (Δ={(diffRel * 100m):F0}%)");
                    }
                }
            }
        }

        private static FoodRow MapToRow(RawFood r)
        {
            // Конверсия: мг/кг → г/100 г  (× 0.0001)
            decimal toPer100g(decimal? mgPerKg) => Math.Round((mgPerKg ?? 0m) * 0.0001m, 3);

            var p100 = toPer100g(r.proteins);
            var f100 = toPer100g(r.fat);
            var c100 = toPer100g(r.carbohydrates);
            var kcalPerG = r.calories ?? 0m;

            p100 = ClampNonNegative(p100);
            f100 = ClampNonNegative(f100);
            c100 = ClampNonNegative(c100);
            kcalPerG = ClampNonNegative(kcalPerG);

            return new FoodRow(
                name: r.name!.Trim(),
                per100g_proteins_g: p100,
                per100g_fats_g: f100,
                per100g_carbs_g: c100,
                kcal_per_g: kcalPerG,
                usda_id: (r.usda_id ?? "").Trim()
            );
        }

        private static decimal ClampNonNegative(decimal v) => v < 0 ? 0 : v;

        /// <summary>
        /// Найти лучшее совпадение по названию. Возвращает null, если ничего подходящего.
        /// </summary>
        public FoodRow? FindBest(string query) => FindBest(query, out _);

        /// <summary>
        /// Найти лучшее совпадение по названию и вернуть score сходства в диапазоне [0..1].
        /// </summary>
        public FoodRow? FindBest(string query, out double score)
        {
            score = 0;
            if (string.IsNullOrWhiteSpace(query) || Rows.Length == 0) return null;

            var key = Norm(query);

            // 1) Сначала пробуем точное совпадение по нормализованному имени
            if (_byNormName.TryGetValue(key, out var exact))
            {
                score = 1.0;
                return exact;
            }

            // 2) Иначе — семантическое «похожее» по токенам (Jaccard)
            FoodRow? best = null;
            double bestScore = 0;

            foreach (var row in Rows)
            {
                var s = TokenJaccard(key, Norm(row.name));
                if (s > bestScore)
                {
                    bestScore = s;
                    best = row;
                }
            }

            // Порог можно подстроить; 0.45–0.6 обычно хорошо
            if (best is not null && bestScore >= 0.45)
            {
                score = bestScore;
                return best;
            }

            return null;
        }

        // ===== утилиты нормализации/сходства =====

        private static string Norm(string s)
        {
            s = s.ToLowerInvariant();
            s = Regex.Replace(s, @"[^a-z0-9\s\-]", "");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }

        private static double TokenJaccard(string a, string b)
        {
            var A = a.Split(new[] { ' ', '-', '/', ',' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var B = b.Split(new[] { ' ', '-', '/', ',' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            if (A.Count == 0 || B.Count == 0) return 0;
            var inter = A.Intersect(B).Count();
            var union = A.Union(B).Count();
            return union == 0 ? 0 : (double)inter / union;
        }
    }
}
