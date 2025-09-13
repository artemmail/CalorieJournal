using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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
        var content = doc.RootElement.GetProperty("output")[0]
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        return content ?? string.Empty;
    }
}

