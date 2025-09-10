using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Linq;

namespace FoodBot.Services
{
    public class MealImageService
    {
        private readonly IHttpClientFactory _factory;
        private readonly ILogger<MealImageService> _log;
        private readonly string _apiKey;
        private readonly bool _enabled;
        private readonly string _model;
        private readonly string _size;

        private const string ImagesGenerationsUrl = "https://api.openai.com/v1/images/generations";
        private const string ImagesUnifiedUrl = "https://api.openai.com/v1/images";

        public MealImageService(IConfiguration cfg, IHttpClientFactory factory, ILogger<MealImageService> log = null!)
        {
            _factory = factory;
            _log = log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MealImageService>.Instance;
            _apiKey = cfg["OpenAI:ApiKey"] ?? string.Empty;
            _enabled = bool.TryParse(cfg["OpenAI:GenerateImages"], out var e) ? e : false;

            _model = cfg["OpenAI:ImageModel"] ?? "gpt-image-1";
            _size = cfg["OpenAI:ImageSize"] ?? "1024x1024"; // 256x256 | 512x512 | 1024x1024
        }

        public async Task<(byte[] bytes, string mime)> GenerateAsync(string description, CancellationToken ct)
        {
            if (!_enabled || string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(description))
                return GeneratePlaceholder(description ?? string.Empty);

            try
            {
                var http = _factory.CreateClient();
                http.Timeout = TimeSpan.FromSeconds(60);

                // 1) Основной путь: /v1/images/generations (ожидаем b64_json)
                var (ok, bytes, mime, status, body) = await TryImagesGenerations(http, description, ct);
                if (ok) return (bytes!, mime!);

                _log.LogWarning("Images/generations failed. Status={Status}. Body={Body}", status, Trunc(body, 800));

                // 2) Фолбэк: /v1/images (некоторым проектам включают этот эндпоинт)
                var (ok2, bytes2, mime2, status2, body2) = await TryImagesUnified(http, description, ct);
                if (ok2) return (bytes2!, mime2!);

                _log.LogError("Images unified failed. Status={Status}. Body={Body}", status2, Trunc(body2, 800));
                return GeneratePlaceholder(description);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Image generation crashed");
                return GeneratePlaceholder(description);
            }
        }

        private async Task<(bool ok, byte[] bytes, string mime, int status, string body)>
            TryImagesGenerations(HttpClient http, string description, CancellationToken ct)
        {
            var payload = new
            {
                model = _model,
                prompt = description,
                size = _size
                //response_format = "b64_json"
            };
            using var req = new HttpRequestMessage(HttpMethod.Post, ImagesGenerationsUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
            var status = (int)resp.StatusCode;
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return (false, null!, null!, status, body);

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("data", out var dataArr) || dataArr.GetArrayLength() == 0)
                return (false, null!, null!, status, body);

            var first = dataArr[0];
            if (first.TryGetProperty("b64_json", out var b64Prop))
            {
                var b64 = b64Prop.GetString();
                if (!string.IsNullOrWhiteSpace(b64))
                    return (true, Convert.FromBase64String(b64!), "image/png", status, body);
            }
            if (first.TryGetProperty("url", out var urlProp))
            {
                var url = urlProp.GetString();
                if (!string.IsNullOrWhiteSpace(url))
                {
                    var imgResp = await http.GetAsync(url, ct).ConfigureAwait(false);
                    if (!imgResp.IsSuccessStatusCode)
                        return (false, null!, null!, (int)imgResp.StatusCode, await imgResp.Content.ReadAsStringAsync(ct));

                    var imgBytes = await imgResp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                    var mime = imgResp.Content.Headers.ContentType?.MediaType ?? "image/png";
                    return (true, imgBytes, mime, status, body);
                }
            }
            return (false, null!, null!, status, body);
        }

        private async Task<(bool ok, byte[] bytes, string mime, int status, string body)>
            TryImagesUnified(HttpClient http, string description, CancellationToken ct)
        {
            // Некоторые аккаунты используют новый /v1/images с тем же полем prompt.
            var payload = new
            {
                model = _model,
                prompt = description,
                size = _size,
                response_format = "b64_json"
                // background = "transparent" // при необходимости
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, ImagesUnifiedUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
            var status = (int)resp.StatusCode;
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return (false, null!, null!, status, body);

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("data", out var dataArr) || dataArr.GetArrayLength() == 0)
                return (false, null!, null!, status, body);

            var first = dataArr[0];
            if (first.TryGetProperty("b64_json", out var b64Prop))
            {
                var b64 = b64Prop.GetString();
                if (!string.IsNullOrWhiteSpace(b64))
                    return (true, Convert.FromBase64String(b64!), "image/png", status, body);
            }
            if (first.TryGetProperty("url", out var urlProp))
            {
                var url = urlProp.GetString();
                if (!string.IsNullOrWhiteSpace(url))
                {
                    var imgResp = await http.GetAsync(url, ct).ConfigureAwait(false);
                    if (!imgResp.IsSuccessStatusCode)
                        return (false, null!, null!, (int)imgResp.StatusCode, await imgResp.Content.ReadAsStringAsync(ct));

                    var imgBytes = await imgResp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                    var mime = imgResp.Content.Headers.ContentType?.MediaType ?? "image/png";
                    return (true, imgBytes, mime, status, body);
                }
            }
            return (false, null!, null!, status, body);
        }

        private static string Trunc(string s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "…";

        public (byte[] bytes, string mime) GeneratePlaceholder(string text)
        {
            const int size = 512;
            using var image = new Image<Rgba32>(size, size, Color.White);

            float fontSize = 64f;
            var family = SystemFonts.Collection.Families.First();
            Font font = family.CreateFont(fontSize);

            // Отдельные options: для измерения (TextOptions) и для рисования (RichTextOptions)
            var measureOpts = new TextOptions(font)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                WrappingLength = size - 20
            };

            var drawOpts = new RichTextOptions(font)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                WrappingLength = size - 20,
                Origin = new PointF(size / 2f, size / 2f) // центр холста
            };

            // Подбор размера шрифта под квадрат size x size
            while (fontSize > 10)
            {
                var measured = TextMeasurer.MeasureSize(text, measureOpts);
                if (measured.Width <= size - 20 && measured.Height <= size - 20)
                    break;

                fontSize -= 2f;
                font = family.CreateFont(fontSize);

                // обновляем оба options
                measureOpts = new TextOptions(font)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    WrappingLength = size - 20
                };
                drawOpts = new RichTextOptions(font)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    WrappingLength = size - 20,
                    Origin = new PointF(size / 2f, size / 2f)
                };
            }

            image.Mutate(ctx => ctx.DrawText(drawOpts, text, Color.Black));

            using var ms = new System.IO.MemoryStream();
            image.SaveAsPng(ms);
            return (ms.ToArray(), "image/png");
        }

    }
}
