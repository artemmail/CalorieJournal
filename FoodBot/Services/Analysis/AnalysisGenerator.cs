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
        var http = _httpFactory.CreateClient();
        using var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        msg.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var resp = await http.SendAsync(msg, ct);
        resp.EnsureSuccessStatusCode();
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
}

