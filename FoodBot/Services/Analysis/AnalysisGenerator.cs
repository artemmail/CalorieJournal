using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Linq;

namespace FoodBot.Services;

public sealed class AnalysisGenerator
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _apiKey;

    public AnalysisGenerator(IHttpClientFactory httpFactory, IConfiguration cfg)
    {
        _httpFactory = httpFactory;
        _apiKey = cfg["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey missing");
    }

    public async Task<string> GenerateAsync(string body, CancellationToken ct)
    {
        const int maxAttempts = 3;
        var delay = TimeSpan.FromSeconds(2);
        var http = _httpFactory.CreateClient();
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
                msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                msg.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using var resp = await http.SendAsync(msg, ct);

                if (!resp.IsSuccessStatusCode)
                {
                    if (attempt == maxAttempts || !IsTransient(resp.StatusCode))
                    {
                        resp.EnsureSuccessStatusCode();
                    }
                    else
                    {
                        await Task.Delay(delay, ct);
                        delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2);
                        continue;
                    }
                }

                var respText = await resp.Content.ReadAsStringAsync(ct);

                using var doc = JsonDocument.Parse(respText);
                if (!doc.RootElement.TryGetProperty("output", out var output))
                {
                    return string.Empty;
                }

                var message = output.EnumerateArray()
                    .FirstOrDefault(e => e.TryGetProperty("type", out var t) && t.GetString() == "message");

                if (message.ValueKind == JsonValueKind.Undefined)
                {
                    return string.Empty;
                }

                if (!message.TryGetProperty("content", out var contentArr) || contentArr.ValueKind != JsonValueKind.Array || contentArr.GetArrayLength() == 0)
                {
                    return string.Empty;
                }

                var text = contentArr[0].GetProperty("text").GetString();

                return text ?? string.Empty;
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                lastException = ex;
                await Task.Delay(delay, ct);
                delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2);
            }
        }

        throw lastException ?? new InvalidOperationException("Failed to generate analysis response.");
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.TooManyRequests
            || (int)statusCode >= 500;
    }
}

