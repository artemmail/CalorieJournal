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
• dish (string)
• ingredients (array of strings, English only; avoid redundant variants)
• shares_percent (array of numbers aligned with ingredients; sum ? 100% ±2%)
• weight_g (portion weight): estimate; round to 5 g
• confidence in [0,1]
Rules:
• If seasonings are visible (e.g., black pepper), keep them but typical shares are very small (?2%).
• shares_percent and ingredients must be same length and aligned.
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

            // нормализация shares до суммы 100%
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

        public async Task<Step1Snapshot?> DetectFromTextAsync(string description, string model, CancellationToken ct)
        {
            var messages = new List<object>
            {
                new { role = "system", content = "You infer dish, English-only ingredients, composition shares and portion weight from a meal description. Reply JSON only." },
                new { role = "user", content = $@"Description: {description}

Return JSON exactly as:
{{""dish"":""..."",""ingredients"":[],""shares_percent"":[],""weight_g"":0,""confidence"":0}}
Rules:
• If grams or weights are mentioned, use them to compute shares_percent and weight_g.
• If not, assume realistic average grams yourself." }
            };

            var request = OpenAiRequestFactory.BuildVisionStep1Request(model, messages);
            var bodyStep1 = JsonSerializer.Serialize(request);

            using var resp = await SendWithRetryAsync(
                createRequest: () =>
                {
                    var m = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                    m.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                    m.Content = new StringContent(bodyStep1, Encoding.UTF8, "application/json");
                    return m;
                },
                purpose: "text_step1",
                ct: ct
            );

            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"OpenAI (text step) error {(int)resp.StatusCode}: {err}");
            }

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content)) return null;

            var step = JsonSerializer.Deserialize<Step1Snapshot>(content!,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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

        // ===== Ретрай-хелперы =====
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