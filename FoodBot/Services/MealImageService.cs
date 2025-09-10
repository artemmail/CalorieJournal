using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace FoodBot.Services;

public class MealImageService
{
    private readonly IHttpClientFactory _factory;
    private readonly string _apiKey;
    private readonly bool _enabled;

    public MealImageService(IConfiguration cfg, IHttpClientFactory factory)
    {
        _factory = factory;
        _apiKey = cfg["OpenAI:ApiKey"] ?? string.Empty;
        _enabled = bool.TryParse(cfg["OpenAI:GenerateImages"], out var e) && e;
    }

    public async Task<(byte[] bytes, string mime)> GenerateAsync(string description, CancellationToken ct)
    {
        if (!_enabled)
            return GeneratePlaceholder();
        try
        {
            var http = _factory.CreateClient();
            var body = JsonSerializer.Serialize(new
            {
                model = "gpt-image-1",
                prompt = description,
                size = "512x512"
            });
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            req.Headers.Add("OpenAI-Beta", "image-v1");
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                return GeneratePlaceholder();
            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var b64 = doc.RootElement.GetProperty("data")[0].GetProperty("b64_json").GetString();
            if (string.IsNullOrWhiteSpace(b64))
                return GeneratePlaceholder();
            return (Convert.FromBase64String(b64), "image/png");
        }
        catch
        {
            return GeneratePlaceholder();
        }
    }

    public (byte[] bytes, string mime) GeneratePlaceholder()
    {
        const string b64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAAAAAA6fptVAAAAC0lEQVR42mP8/x8AAwMCAOJqS58AAAAASUVORK5CYII=";
        return (Convert.FromBase64String(b64), "image/png");
    }
}
