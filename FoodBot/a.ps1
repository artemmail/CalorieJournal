# refactor-nutrition.ps1
# –азносит NutritionService по файлам, делает FinalPayload/PerIng public,
# выносит OpenAI-клиент в отдельный класс с ретра€ми.
param(
  [string]$ProjectRoot = "."
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path $ProjectRoot).Path

# ѕапки (подправьте при необходимости под свой layout проекта)
$modelsDir   = Join-Path $root "Models"
$servicesDir = Join-Path $root "Services"
$openaiDir   = Join-Path $servicesDir "OpenAI"

New-Item -ItemType Directory -Force -Path $modelsDir   | Out-Null
New-Item -ItemType Directory -Force -Path $servicesDir | Out-Null
New-Item -ItemType Directory -Force -Path $openaiDir   | Out-Null

function Write-File($path, $content) {
  $dir = Split-Path $path
  if (!(Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
  $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
  [System.IO.File]::WriteAllText($path, $content, $utf8NoBom)
  Write-Host "? Wrote $path"
}

# ===== Models/NutritionModels.cs =====
$modelsContent = @'
using System;
using FoodBot.Services; // MatchedFoodRow

namespace FoodBot.Models
{
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

    // ¬спомогательные DTO дл€ парсинга ответа модели
    internal sealed class FinalOuter { public FinalPayload? final { get; set; } }

    // ? public Ч чтобы не было Inconsistent accessibility в интерфейсе IOpenAiClient
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

    // ? public Ч часть публичного графа типов возвращаемого значени€
    public sealed class PerIng
    {
        public string name { get; set; } = "";
        public decimal grams { get; set; }
        public decimal per100g_proteins_g { get; set; }
        public decimal per100g_fats_g { get; set; }
        public decimal per100g_carbs_g { get; set; }
        public decimal kcal_per_g { get; set; }
    }
}
'@
Write-File (Join-Path $modelsDir "NutritionModels.cs") $modelsContent

# ===== Services/OpenAI/IOpenAiClient.cs =====
$iopenaiContent = @'
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Models;

namespace FoodBot.Services.OpenAI
{
    public interface IOpenAiClient
    {
        Task<Step1Snapshot?> DetectFromImageAsync(string dataUrl, string? userNote, string visionModel, CancellationToken ct);
        Task<FinalPayload?> ComputeFinalAsync(IEnumerable<object> messagesHistoryWithUserPrompt, string model, CancellationToken ct);
    }

    public sealed class OpenAiSettings
    {
        public string ApiKey { get; init; } = "";
        public int TimeoutSeconds { get; init; } = 60;
        public bool DebugLog { get; init; } = false;
        public int MaxRetries { get; init; } = 7;
        public int RetryBaseDelaySeconds { get; init; } = 2;

        // предоставл€етс€ из DI
        public System.Net.Http.IHttpClientFactory? ClientFactory { get; init; }
    }
}
'@
Write-File (Join-Path $openaiDir "IOpenAiClient.cs") $iopenaiContent

# ===== Services/OpenAI/OpenAiRequestFactory.cs =====
$requestFactoryContent = @'
using System.Collections.Generic;

namespace FoodBot.Services.OpenAI
{
    public static class OpenAiRequestFactory
    {
        public static object BuildVisionStep1Request(string model, System.Collections.Generic.List<object> messages)
        {
            return new
            {
                model = model,
                messages = messages,
                response_format = new
                {
                    type = "json_schema",
                    json_schema = new
                    {
                        name = "vision_step1",
                        schema = new
                        {
                            type = "object",
                            additionalProperties = false,
                            properties = new
                            {
                                dish = new { type = "string" },
                                ingredients = new { type = "array", items = new { type = "string" } },
                                shares_percent = new { type = "array", items = new { type = "number" } },
                                weight_g = new { type = "number", minimum = 0 },
                                confidence = new { type = "number", minimum = 0, maximum = 1 }
                            },
                            required = new[] { "dish", "ingredients", "shares_percent", "weight_g", "confidence" }
                        }
                    }
                },
                temperature = 0.2
            };
        }

        public static object BuildFinalResponseFormatSchema()
        {
            return new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "final_nutrition",
                    schema = new
                    {
                        type = "object",
                        additionalProperties = false,
                        properties = new
                        {
                            final = new
                            {
                                type = "object",
                                additionalProperties = false,
                                properties = new
                                {
                                    dish = new { type = "string" },
                                    weight_g = new { type = "number", minimum = 0 },
                                    proteins_g = new { type = "number", minimum = 0 },
                                    fats_g = new { type = "number", minimum = 0 },
                                    carbs_g = new { type = "number", minimum = 0 },
                                    calories_kcal = new { type = "number", minimum = 0 },
                                    confidence = new { type = "number", minimum = 0, maximum = 1 },
                                    per_ingredient = new
                                    {
                                        type = "array",
                                        minItems = 1,
                                        items = new
                                        {
                                            type = "object",
                                            additionalProperties = false,
                                            properties = new
                                            {
                                                name = new { type = "string" },
                                                grams = new { type = "number", minimum = 0 },
                                                per100g_proteins_g = new { type = "number", minimum = 0 },
                                                per100g_fats_g = new { type = "number", minimum = 0 },
                                                per100g_carbs_g = new { type = "number", minimum = 0 },
                                                kcal_per_g = new { type = "number", minimum = 0 }
                                            },
                                            required = new[] { "name", "grams", "per100g_proteins_g", "per100g_fats_g", "per100g_carbs_g", "kcal_per_g" }
                                        }
                                    }
                                },
                                required = new[] { "dish", "weight_g", "proteins_g", "fats_g", "carbs_g", "calories_kcal", "confidence", "per_ingredient" }
                            }
                        },
                        required = new[] { "final" }
                    }
                }
            };
        }

        public static object BuildFinalRequest(string model, IEnumerable<object> messages, object responseFormatSchema)
        {
            return new
            {
                model = model,
                messages = messages,
                response_format = responseFormatSchema
            };
        }
    }
}
'@
Write-File (Join-Path $openaiDir "OpenAiRequestFactory.cs") $requestFactoryContent

# ===== Services/OpenAI/OpenAiClient.cs =====
$openaiClientContent = @'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Models;

namespace FoodBot.Services.OpenAI
{
    public sealed class OpenAiClient : IOpenAiClient
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly bool _debugLog;
        private readonly int _maxRetries;
        private readonly int _retryBaseDelaySeconds;

        public OpenAiClient(OpenAiSettings settings)
        {
            if (settings.ClientFactory is null)
                throw new ArgumentNullException(nameof(settings.ClientFactory));

            _http = settings.ClientFactory.CreateClient();
            _http.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
            _apiKey = settings.ApiKey;
            _debugLog = settings.DebugLog;
            _maxRetries = Math.Clamp(settings.MaxRetries, 1, 7);
            _retryBaseDelaySeconds = Math.Clamp(settings.RetryBaseDelaySeconds, 1, 60);
        }

        public async Task<Step1Snapshot?> DetectFromImageAsync(string dataUrl, string? userNote, string visionModel, CancellationToken ct)
        {
            var note = string.IsNullOrWhiteSpace(userNote) ? "" : $"\nUser note for adjustment: {userNote}";
            var messages = new List<object>
            {
                new { role = "system", content = "You detect dish, English-only ingredients, and composition percentages from a photo. Reply JSON only." },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new {
                            type = "text", text =
$@"From the photo, output:
Х dish (string)
Х ingredients (array of strings, English only; avoid redundant variants)
Х shares_percent (array of numbers aligned with ingredients; sum ? 100% ±2%)
Х weight_g (portion weight): estimate; round to 5 g
Х confidence in [0,1]
Rules:
Х If seasonings are visible (e.g., black pepper), keep them but typical shares are very small (?2%).
Х shares_percent and ingredients must be same length and aligned.
Return exactly this JSON:
{{""dish"":""..."",""ingredients"":[], ""shares_percent"":[], ""weight_g"":0, ""confidence"":0}}{note}"
                        },
                        new { type = "image_url", image_url = new { url = dataUrl } }
                    }
                }
            };

            var request = OpenAiRequestFactory.BuildVisionStep1Request(visionModel, messages);
            var bodyStep1 = JsonSerializer.Serialize(request);

            using var resp = await SendWithRetryAsync(
                createRequest: () =>
                {
                    var m = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                    m.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                    m.Content = new StringContent(bodyStep1, Encoding.UTF8, "application/json");
                    return m;
                },
                purpose: "vision_step1",
                ct: ct
            );

            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"OpenAI (vision step) error {(int)resp.StatusCode}: {err}");
            }

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content)) return null;

            var step = JsonSerializer.Deserialize<Step1Snapshot>(content!,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // нормализаци€ shares до суммы 100%
            if (step is not null)
            {
                var ingredients = step.ingredients ?? Array.Empty<string>();
                var shares = step.shares_percent ?? Array.Empty<decimal>();

                if (shares.Length != ingredients.Length)
                {
                    var fixedShares = new decimal[ingredients.Length];
                    var n = Math.Min(fixedShares.Length, shares.Length);
                    Array.Copy(shares, fixedShares, n);
                    shares = fixedShares;
                }

                var sum = shares.Sum();
                if (sum > 0)
                {
                    var k = 100m / sum;
                    var fixedArr = shares.Select(v => Math.Round(v * k, 1)).ToArray();
                    var diff = 100m - fixedArr.Sum();
                    if (fixedArr.Length > 0)
                        fixedArr[^1] = Math.Round(fixedArr[^1] + diff, 1);
                    shares = fixedArr;
                }

                step = step with { shares_percent = shares };
            }

            return step;
        }

        public async Task<FinalPayload?> ComputeFinalAsync(IEnumerable<object> messagesHistoryWithUserPrompt, string model, CancellationToken ct)
        {
            var responseFormat = OpenAiRequestFactory.BuildFinalResponseFormatSchema();
            var request = OpenAiRequestFactory.BuildFinalRequest(model, messagesHistoryWithUserPrompt, responseFormat);
            var body = JsonSerializer.Serialize(request);

            if (_debugLog)
                Console.WriteLine($"[OpenAI req] body head: {SafeHead(body)}");

            using var resp = await SendWithRetryAsync(
                createRequest: () =>
                {
                    var m = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                    m.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                    m.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    return m;
                },
                purpose: "final",
                ct: ct
            );

            var respText = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"[OpenAI resp] HTTP {(int)resp.StatusCode}: {SafeHead(respText)}");
                throw new InvalidOperationException($"OpenAI final error {(int)resp.StatusCode}: {respText}");
            }

            if (_debugLog) Console.WriteLine($"[OpenAI resp] head: {SafeHead(respText)}");

            using var doc = JsonDocument.Parse(respText);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content)) return null;

            var outer = JsonSerializer.Deserialize<FinalOuter>(content!,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return outer?.final;
        }

        // ===== –етрай-хелперы =====
        private async Task<HttpResponseMessage> SendWithRetryAsync(
            Func<HttpRequestMessage> createRequest,
            string purpose,
            CancellationToken ct)
        {
            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                using var req = createRequest();
                try
                {
                    if (_debugLog)
                        Console.WriteLine($"[Retry] {purpose}: attempt {attempt}/{_maxRetries}");

                    var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

                    if (resp.IsSuccessStatusCode)
                        return resp;

                    if (!ShouldRetry(resp))
                        return resp;

                    if (attempt == _maxRetries)
                        return resp;

                    var delay = ComputeRetryDelay(attempt, resp);
                    if (_debugLog)
                        Console.WriteLine($"[Retry] {purpose}: will retry after {delay.TotalSeconds:F1}s (HTTP {(int)resp.StatusCode})");
                    resp.Dispose();
                    await Task.Delay(delay, ct);
                }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested)
                {
                    if (attempt == _maxRetries) throw;

                    var delay = ComputeRetryDelay(attempt, null);
                    if (_debugLog)
                        Console.WriteLine($"[Retry] {purpose}: timeout, retry after {delay.TotalSeconds:F1}s");
                    await Task.Delay(delay, ct);
                }
                catch (HttpRequestException ex)
                {
                    if (attempt == _maxRetries) throw;

                    var delay = ComputeRetryDelay(attempt, null);
                    if (_debugLog)
                        Console.WriteLine($"[Retry] {purpose}: network error '{ex.Message}', retry after {delay.TotalSeconds:F1}s");
                    await Task.Delay(delay, ct);
                }
            }
            throw new InvalidOperationException("Unexpected retry loop exit");
        }

        private static bool ShouldRetry(HttpResponseMessage resp)
        {
            var code = (int)resp.StatusCode;
            if (code == 408 || code == 429) return true;
            if (code >= 500 && code <= 599) return true;
            return false;
        }

        private TimeSpan ComputeRetryDelay(int attempt, HttpResponseMessage? resp)
        {
            if (resp is not null && resp.Headers.TryGetValues("Retry-After", out var vals))
            {
                var v = vals.FirstOrDefault();
                if (int.TryParse(v, out var seconds))
                    return TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 120));

                if (DateTimeOffset.TryParse(v, out var when))
                {
                    var delta = when - DateTimeOffset.UtcNow;
                    if (delta > TimeSpan.Zero && delta < TimeSpan.FromMinutes(5))
                        return delta;
                }
            }

            var baseSec = _retryBaseDelaySeconds * Math.Pow(2, attempt - 1);
            var jitterMs = Random.Shared.Next(100, 500);
            var total = TimeSpan.FromSeconds(Math.Min(60, baseSec)) + TimeSpan.FromMilliseconds(jitterMs);
            return total;
        }

        private static string SafeHead(string s)
        {
            var max = Math.Min(800, s.Length);
            return s.Substring(0, max).Replace("\n", "\\n");
        }
    }
}
'@
Write-File (Join-Path $openaiDir "OpenAiClient.cs") $openaiClientContent

# ===== Services/NutritionPromptBuilder.cs =====
$promptBuilderContent = @'
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
Х Keep items aligned with the given INGREDIENTS (same order, do not drop items).
Х Keep grams per item close to targets; total ? TARGET_WEIGHT_G (±10%).
Х Seasoning caps: black pepper 0.3Ц2 g; salt 1Ц3 g; dried spices 0.5Ц3 g; fresh herbs 3Ц8 g.
Х Plausible per-100g ranges (guideline, not output): P?80 g, F?100 g, C?95 g.
Х Energy consistency: calories_kcal must equal 4*proteins_g + 9*carbs_g + 4*fats_g within ±3%.
Х Output JSON ONLY using the schema below.

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
'@
Write-File (Join-Path $servicesDir "NutritionPromptBuilder.cs") $promptBuilderContent

# ===== Services/NutritionService.cs =====
# –езервна€ копи€, если файл уже есть
$existingNutrition = Get-ChildItem -Path $root -Recurse -Filter "NutritionService.cs" -ErrorAction SilentlyContinue | Select-Object -First 1
$nutritionPath = if ($existingNutrition) { $existingNutrition.FullName } else { Join-Path $servicesDir "NutritionService.cs" }
if (Test-Path $nutritionPath) {
  $backup = "$nutritionPath.bak_" + (Get-Date -Format "yyyyMMdd_HHmmss")
  Copy-Item $nutritionPath $backup
  Write-Host "? Backup created: $backup"
}

$nutritionServiceContent = @'
using System;
using System.Collections.Concurrent;
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
        private readonly string _visionModel;        // Ўаг 1 (визуалка)
        private readonly string _reasoningModel;     // Ўаг 2 (финал считает »»)
        private readonly bool _debugLog;
        private readonly FoodMatcher _matcher;

        private readonly ConcurrentDictionary<Guid, List<object>> _threads = new();
        private readonly ConcurrentDictionary<Guid, MatchedFoodRow[]> _threadFoods = new();
        private readonly ConcurrentDictionary<Guid, string[]> _threadIngredients = new();
        private readonly ConcurrentDictionary<Guid, string> _threadImageDataUrl = new();
        private readonly ConcurrentDictionary<Guid, Step1Snapshot> _threadStep1 = new();

        // ? сохран€ем внешний интерфейс: добавили опциональный параметр ai
        public NutritionService(IConfiguration cfg, IHttpClientFactory f, IWebHostEnvironment env, IOpenAiClient? ai = null)
        {
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

            // ћодели оставл€ем в конфиге (не секретные параметры)
            _visionModel = cfg["OpenAI:Model"] ?? "gpt-4o-mini";
            _reasoningModel = cfg["OpenAI:ReasoningModel"] ?? "o4-mini";

            var foodsPath = cfg["Foods:Path"];
            if (string.IsNullOrWhiteSpace(foodsPath))
                foodsPath = System.IO.Path.Combine(env.ContentRootPath, "foods.json");

            var db = new FoodDb(foodsPath);
            _matcher = new FoodMatcher(db);
        }

        // === Public API ===
        public async Task<NutritionConversation?> AnalyzeAsync(byte[] imageBytes, CancellationToken ct)
        {
            var dataUrl = "data:image/jpeg;base64," + Convert.ToBase64String(imageBytes);
            return await AnalyzeCore(dataUrl, userNote: null, ct);
        }

        public async Task<NutritionConversation?> AnalyzeWithNoteAsync(byte[] imageBytes, string userNote, CancellationToken ct)
        {
            var dataUrl = "data:image/jpeg;base64," + Convert.ToBase64String(imageBytes);
            return await AnalyzeCore(dataUrl, userNote, ct);
        }

        public async Task<NutritionConversation?> ClarifyAsync(Guid threadId, string userNote, CancellationToken ct)
        {
            if (!_threads.TryGetValue(threadId, out var history))
                throw new InvalidOperationException("Unknown threadId. Start with AnalyzeAsync.");

            if (!_threadImageDataUrl.TryGetValue(threadId, out var dataUrl))
                throw new InvalidOperationException("Original image is not available for this thread.");

            var step1 = await _ai.DetectFromImageAsync(dataUrl, userNote, _visionModel, ct);
            if (step1 is null || step1.ingredients.Length == 0) return null;

            _threadStep1[threadId] = step1;
            _threadIngredients[threadId] = step1.ingredients;

            var matches = _matcher.MatchFoodsDetailed(step1.ingredients);
            var matched = _matcher.CollapseMatchedRows(matches);
            _threadFoods[threadId] = matched;

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

        private async Task<NutritionConversation?> AnalyzeCore(string dataUrl, string? userNote, CancellationToken ct)
        {
            var threadId = Guid.NewGuid();
            _threadImageDataUrl[threadId] = dataUrl;

            var step1 = await _ai.DetectFromImageAsync(dataUrl, userNote, _visionModel, ct);
            if (step1 is null || step1.ingredients.Length == 0) return null;

            _threadStep1[threadId] = step1;
            _threadIngredients[threadId] = step1.ingredients;

            var matches = _matcher.MatchFoodsDetailed(step1.ingredients);
            var matchedRows = _matcher.CollapseMatchedRows(matches);
            _threadFoods[threadId] = matchedRows;

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
            _threads[threadId] = msgs;

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
'@
Write-File $nutritionPath $nutritionServiceContent

Write-Host "`nDone."
Write-Host "ƒалее: 1) внесите правки в Program.cs (см. ниже), 2) dotnet build"
