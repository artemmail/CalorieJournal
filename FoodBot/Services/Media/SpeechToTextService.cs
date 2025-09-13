using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace FoodBot.Services
{
    /// <summary>
    /// Простая обёртка над OpenAI Whisper для распознавания голосовых заметок.
    /// По умолчанию использует модель "whisper-1".
    /// </summary>
    public sealed class SpeechToTextService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _model;

        public SpeechToTextService(IHttpClientFactory f, IConfiguration cfg)
        {
            _http = f.CreateClient();
            _http.Timeout = TimeSpan.FromSeconds(int.TryParse(cfg["OpenAI:TimeoutSeconds"], out var t) ? t : 60);

            _apiKey = cfg["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey missing");
            _model = cfg["OpenAI:TranscribeModel"] ?? "whisper-1";
        }

        /// <param name="audioBytes">Сырые байты аудио (ogg/opus, mp3 и т.п.)</param>
        /// <param name="language">Код языка ISO (например, "ru" или "en"). Можно null — автоопределение.</param>
        public async Task<string?> TranscribeAsync(byte[] audioBytes, string? language, string? fileName, string? contentType, CancellationToken ct)
        {
            const int maxAttempts = 3;
            Exception? lastError = null;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions");
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                    var form = new MultipartFormDataContent();

                    var file = new ByteArrayContent(audioBytes);
                    file.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
                    form.Add(file, "file", string.IsNullOrWhiteSpace(fileName) ? "audio.ogg" : fileName);

                    form.Add(new StringContent(_model), "model");
                    form.Add(new StringContent("json"), "response_format");
                    if (!string.IsNullOrWhiteSpace(language))
                        form.Add(new StringContent(language), "language");

                    req.Content = form;

                    using var resp = await _http.SendAsync(req, ct);
                    var text = await resp.Content.ReadAsStringAsync(ct);

                    if (!resp.IsSuccessStatusCode)
                        throw new HttpRequestException($"OpenAI STT error {(int)resp.StatusCode}: {text}");

                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.TryGetProperty("text", out var te))
                        return te.GetString();

                    return null;
                }
                catch (HttpRequestException ex)
                {
                    lastError = ex;
                }
                catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
                {
                    lastError = ex;
                }

                if (lastError != null)
                {
                    if (attempt == maxAttempts - 1)
                        throw new InvalidOperationException(lastError.Message, lastError);

                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    await Task.Delay(delay, ct);
                }
            }

            throw new InvalidOperationException(lastError?.Message ?? "unknown error", lastError);
        }
    }
}
