using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Data;               // FoodDb
using FoodBot.Models;
using FoodBot.Services.OpenAI;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace FoodBot.Services
{
    public class NutritionService
    {
        private readonly IOpenAiClient _ai;
        private readonly string _visionModel;        // Шаг 1 (визуалка)
        private readonly string _reasoningModel;     // Шаг 2 (финал считает ИИ)
        private readonly bool _debugLog;
        private readonly FoodMatcher _matcher;
        private readonly INutritionSessionService _sessions;

        // ? сохраняем внешний интерфейс: добавили опциональный параметр ai
        public NutritionService(
            IConfiguration cfg,
            IHttpClientFactory f,
            IWebHostEnvironment env,
            INutritionSessionService sessions,
            IOpenAiClient? ai = null)
        {
            _sessions = sessions;
            if (ai is not null)
            {
                _ai = ai; // настройки OpenAI инкапсулированы в клиенте
                _debugLog = false; // при желании можно лог перенести целиком в клиент
            }
            else
            {
                // fallback, если IOpenAiClient не зарегистрирован (совместимость)
                var settings = new OpenAiSettings
                {
                    ApiKey = cfg["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey missing"),
                    TimeoutSeconds = int.TryParse(cfg["OpenAI:TimeoutSeconds"], out var t) ? t : 60,
                    DebugLog = bool.TryParse(cfg["OpenAI:DebugLog"], out var dbg) && dbg,
                    MaxRetries = Math.Clamp(int.TryParse(cfg["OpenAI:MaxRetries"], out var mr) ? mr : 7, 1, 7),
                    RetryBaseDelaySeconds = Math.Clamp(int.TryParse(cfg["OpenAI:RetryBaseSeconds"], out var rbs) ? rbs : 2, 1, 60),
                    ClientFactory = f
                };
                _ai = new OpenAiClient(settings);
                _debugLog = settings.DebugLog;
            }

            // Модели оставляем в конфиге (не секретные параметры)
            _visionModel = cfg["OpenAI:Model"] ?? "gpt-4o-mini";
            _reasoningModel = cfg["OpenAI:ReasoningModel"] ?? "gpt-4o-mini";

            var foodsPath = cfg["Foods:Path"];
            if (string.IsNullOrWhiteSpace(foodsPath))
                foodsPath = System.IO.Path.Combine(env.ContentRootPath, "foods.json");

            var db = new FoodDb(foodsPath);
            _matcher = new FoodMatcher(db);
        }

        // === Public API ===
        public async Task<NutritionConversation?> AnalyzeAsync(
            byte[] imageBytes,
            Func<int, Task>? progress = null,
            CancellationToken ct = default)
        {
            var dataUrl = "data:image/jpeg;base64," + Convert.ToBase64String(imageBytes);
            return await AnalyzeCore(dataUrl, userNote: null, progress, ct);
        }

        public async Task<NutritionConversation?> AnalyzeWithNoteAsync(
            byte[] imageBytes,
            string userNote,
            Func<int, Task>? progress = null,
            CancellationToken ct = default)
        {
            var dataUrl = "data:image/jpeg;base64," + Convert.ToBase64String(imageBytes);
            return await AnalyzeCore(dataUrl, userNote, progress, ct);
        }

        public async Task<NutritionConversation?> ClarifyAsync(Guid threadId, string userNote, CancellationToken ct)
        {
            if (!_sessions.TryGet(threadId, out var session))
                throw new InvalidOperationException("Unknown threadId. Start with AnalyzeAsync.");

            var history = session.History;
            var dataUrl = session.ImageDataUrl;

            var step1 = await _ai.DetectFromImageAsync(dataUrl, userNote, _visionModel, ct);
            if (step1 is null || step1.ingredients.Length == 0) return null;

            session.Step1 = step1;
            session.Ingredients = step1.ingredients;

            var matches = _matcher.MatchFoodsDetailed(step1.ingredients);
            var matched = _matcher.CollapseMatchedRows(matches);
            session.MatchedFoods = matched;

            var foodsJson = _matcher.BuildFoodsJsonForPrompt(matched);
            var unmatched = _matcher.BuildUnmatchedCommaList(matches);

            var targetWeight = step1.weight_g > 0 ? step1.weight_g : 100m;
            var gramsTargets = NutritionPromptBuilder.ComputeGramsFromShares(step1.ingredients, step1.shares_percent, targetWeight);

            var finalPrompt = NutritionPromptBuilder.BuildFinalPromptFromShares(
                dish: step1.dish,
                ingredients: step1.ingredients,
                sharesPercent: step1.shares_percent,
                gramsTargets: gramsTargets,
                targetWeight: targetWeight,
                foodsJson: foodsJson,
                unmatchedList: unmatched,
                userNote: userNote
            );

            history.Add(new { role = "user", content = "[re-step1] " + JsonSerializer.Serialize(step1) });
            history.Add(new { role = "user", content = finalPrompt });

            if (_debugLog)
            {
                Console.WriteLine($"[Clarify Final by AI] Prompt chars={finalPrompt.Length}");
                Console.WriteLine($"[Clarify Final by AI] Prompt head:\n{SafeHead(finalPrompt)}");
            }

            var finalAi = await _ai.ComputeFinalAsync(history, model: _reasoningModel, ct: ct);
            if (finalAi is null)
            {
                if (_debugLog) Console.WriteLine("[Clarify Final by AI] ? Model returned null.");
                return null;
            }

            history.Add(new { role = "assistant", content = JsonSerializer.Serialize(finalAi) });

            var final = new NutritionResult(
                dish: finalAi.dish ?? step1.dish,
                ingredients: step1.ingredients,
                proteins_g: Math.Round(finalAi.proteins_g, 1),
                fats_g: Math.Round(finalAi.fats_g, 1),
                carbs_g: Math.Round(finalAi.carbs_g, 1),
                calories_kcal: Math.Round(finalAi.calories_kcal, 0),
                weight_g: Math.Round(finalAi.weight_g > 0 ? finalAi.weight_g : targetWeight, 0),
                confidence: Math.Clamp(finalAi.confidence, 0m, 1m)
            );

            var finalJson = JsonSerializer.Serialize(finalAi, new JsonSerializerOptions { PropertyNamingPolicy = null });

            return new NutritionConversation(
                threadId,
                final,
                matched,
                step1,
                finalPrompt,
                finalJson
            );
        }

        public async Task<NutritionConversation?> ClarifyFromStep1Async(
            Step1Snapshot step1,
            string userNote,
            CancellationToken ct)
        {
            var matches = _matcher.MatchFoodsDetailed(step1.ingredients);
            var matched = _matcher.CollapseMatchedRows(matches);
            var foodsJson = _matcher.BuildFoodsJsonForPrompt(matched);
            var unmatched = _matcher.BuildUnmatchedCommaList(matches);

            var targetWeight = step1.weight_g > 0 ? step1.weight_g : 100m;
            var gramsTargets = NutritionPromptBuilder.ComputeGramsFromShares(step1.ingredients, step1.shares_percent, targetWeight);

            var finalPrompt = NutritionPromptBuilder.BuildFinalPromptFromShares(
                dish: step1.dish,
                ingredients: step1.ingredients,
                sharesPercent: step1.shares_percent,
                gramsTargets: gramsTargets,
                targetWeight: targetWeight,
                foodsJson: foodsJson,
                unmatchedList: unmatched,
                userNote: userNote
            );

            var msgs = new List<object>
            {
                new { role = "system", content = "You are a meticulous nutrition analyst. Use FOODS_JSON as hints only. Produce final macros yourself. Reply JSON only." },
                new { role = "assistant", content = JsonSerializer.Serialize(new {
                    dish = step1.dish, ingredients = step1.ingredients, shares_percent = step1.shares_percent, weight_g = step1.weight_g, confidence = step1.confidence
                })},
                new { role = "user", content = finalPrompt }
            };

            var finalAi = await _ai.ComputeFinalAsync(msgs, model: _reasoningModel, ct: ct);
            if (finalAi is null) return null;

            var final = new NutritionResult(
                dish: finalAi.dish ?? step1.dish,
                ingredients: step1.ingredients,
                proteins_g: Math.Round(finalAi.proteins_g, 1),
                fats_g: Math.Round(finalAi.fats_g, 1),
                carbs_g: Math.Round(finalAi.carbs_g, 1),
                calories_kcal: Math.Round(finalAi.calories_kcal, 0),
                weight_g: Math.Round(finalAi.weight_g > 0 ? finalAi.weight_g : targetWeight, 0),
                confidence: Math.Clamp(finalAi.confidence, 0m, 1m)
            );

            var finalJson = JsonSerializer.Serialize(finalAi, new JsonSerializerOptions { PropertyNamingPolicy = null });

            return new NutritionConversation(
                ThreadId: Guid.NewGuid(),
                Result: final,
                MatchedFoods: matched,
                Step1: step1,
                ReasoningPrompt: finalPrompt,
                CalcPlanJson: finalJson
            );
        }

        private async Task<NutritionConversation?> AnalyzeCore(
            string dataUrl,
            string? userNote,
            Func<int, Task>? progress,
            CancellationToken ct)
        {
            var session = _sessions.Create(dataUrl);

            if (progress is not null)
                await progress(1); // start vision step

            var step1 = await _ai.DetectFromImageAsync(dataUrl, userNote, _visionModel, ct);
            if (step1 is null || step1.ingredients.Length == 0) return null;

            if (progress is not null)
                await progress(2); // start final compute

            session.Step1 = step1;
            session.Ingredients = step1.ingredients;

            var matches = _matcher.MatchFoodsDetailed(step1.ingredients);
            var matchedRows = _matcher.CollapseMatchedRows(matches);
            session.MatchedFoods = matchedRows;

            var foodsJson = _matcher.BuildFoodsJsonForPrompt(matchedRows);
            var unmatched = _matcher.BuildUnmatchedCommaList(matches);

            var targetWeight = step1.weight_g > 0 ? step1.weight_g : 100m;
            var gramsTargets = NutritionPromptBuilder.ComputeGramsFromShares(step1.ingredients, step1.shares_percent, targetWeight);

            var finalPrompt = NutritionPromptBuilder.BuildFinalPromptFromShares(
                dish: step1.dish,
                ingredients: step1.ingredients,
                sharesPercent: step1.shares_percent,
                gramsTargets: gramsTargets,
                targetWeight: targetWeight,
                foodsJson: foodsJson,
                unmatchedList: unmatched,
                userNote: string.IsNullOrWhiteSpace(userNote) ? "Initial compute from composition shares" : userNote!
            );

            if (_debugLog)
            {
                Console.WriteLine($"[Final by AI] Prompt chars={finalPrompt.Length}, matched_rows={matchedRows.Length}");
                Console.WriteLine($"[Final by AI] Prompt head:\n{SafeHead(finalPrompt)}");
            }

            var msgs = new List<object>
            {
                new { role = "system", content = "You are a meticulous nutrition analyst. Use FOODS_JSON as hints only. Produce final macros yourself. Reply JSON only." },
                new { role = "assistant", content = JsonSerializer.Serialize(new {
                    dish = step1.dish, ingredients = step1.ingredients, shares_percent = step1.shares_percent, weight_g = step1.weight_g, confidence = step1.confidence
                })},
                new { role = "user", content = finalPrompt }
            };

            var finalAi = await _ai.ComputeFinalAsync(msgs, model: _reasoningModel, ct: ct);
            if (finalAi is null)
            {
                if (_debugLog) Console.WriteLine("[Final by AI] ? Model returned null.");
                return null;
            }

            msgs.Add(new { role = "assistant", content = JsonSerializer.Serialize(finalAi) });
            session.History.AddRange(msgs);

            var final = new NutritionResult(
                dish: finalAi.dish ?? step1.dish,
                ingredients: step1.ingredients,
                proteins_g: Math.Round(finalAi.proteins_g, 1),
                fats_g: Math.Round(finalAi.fats_g, 1),
                carbs_g: Math.Round(finalAi.carbs_g, 1),
                calories_kcal: Math.Round(finalAi.calories_kcal, 0),
                weight_g: Math.Round(finalAi.weight_g > 0 ? finalAi.weight_g : targetWeight, 0),
                confidence: Math.Clamp(finalAi.confidence, 0m, 1m)
            );

            var finalJson = JsonSerializer.Serialize(finalAi, new JsonSerializerOptions { PropertyNamingPolicy = null });

            return new NutritionConversation(
                session.Id,
                final,
                matchedRows,
                step1,
                finalPrompt,
                finalJson
            );
        }

        private static string SafeHead(string s)
        {
            var max = Math.Min(800, s.Length);
            return s.Substring(0, max).Replace("\n", "\\n");
        }
    }
}