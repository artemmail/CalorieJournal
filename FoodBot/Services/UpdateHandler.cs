using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FoodBot.Data;
using FoodBot.Services;
using FoodBot.Models;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace FoodBot.Services;

public class UpdateHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TelegramBotClient _bot;
    private readonly IConfiguration _cfg;

    // (опционально) карта "чат → последний threadId"
    private static readonly ConcurrentDictionary<long, Guid> _threadsByChat = new();

    // ====== ПЕРЕКЛЮЧАТЕЛЬ РЕЖИМА УТОЧНЕНИЙ ======
    // Значения: FromSavedStep1 — используем сохранённый Step-1 (без повторной визионки),
    //           FromVisionStep1 — повторяем Step-1 по фото с учётом userNote,
    //           Auto — сначала Step-1 из БД, если его нет — визионка.
    private enum ClarifyStartMode { FromSavedStep1, FromVisionStep1, Auto }

    // Константа по умолчанию (можно поменять здесь в коде)
    private const ClarifyStartMode CLARIFY_START_DEFAULT = ClarifyStartMode.FromSavedStep1;

    public UpdateHandler(IServiceScopeFactory scopeFactory, TelegramBotClient bot, IConfiguration cfg)
    {
        _scopeFactory = scopeFactory;
        _bot = bot;
        _cfg = cfg;
    }

    // Читать режим из конфига (необязательно). Если не задан — берём константу.
    private ClarifyStartMode GetClarifyMode()
    {
        var s = _cfg["Clarify:StartMode"];
        return s?.ToLowerInvariant() switch
        {
            "fromvisionstep1" => ClarifyStartMode.FromVisionStep1,
            "fromsavedstep1" => ClarifyStartMode.FromSavedStep1,
            "auto" => ClarifyStartMode.Auto,
            _ => CLARIFY_START_DEFAULT
        };
    }

    public async Task HandleAsync(Update update, CancellationToken ct)
    {
        if (update?.Type != UpdateType.Message || update.Message is not { } msg)
            return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        var nutrition = scope.ServiceProvider.GetRequiredService<NutritionService>();
        var report = scope.ServiceProvider.GetRequiredService<TelegramReportService>();
        var httpf = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var stt = scope.ServiceProvider.GetRequiredService<SpeechToTextService>();

        var chatId = msg.Chat.Id;
        var userId = msg.From?.Id ?? 0;
        var username = msg.From?.Username ?? $"{msg.From?.FirstName} {msg.From?.LastName}".Trim();

        // ===== /report → Excel
        if (msg.Text != null && msg.Text.StartsWith("/report", StringComparison.OrdinalIgnoreCase))
        {
            await _bot.SendChatAction(chatId, ChatAction.UploadDocument, cancellationToken: ct);
            var (stream, filename) = await report.BuildUserReportAsync(chatId, ct);
            await _bot.SendDocument(chatId, InputFile.FromStream(stream, filename), caption: "Ваш отчёт 📊", cancellationToken: ct);
            return;
        }

        // ===== /app → инструкция по входу через старт-код из приложения
        if (msg.Text != null && msg.Text.StartsWith("/app", StringComparison.OrdinalIgnoreCase))
        {
            var botUser = _cfg["Telegram:BotUsername"] ?? "your_bot_username";
            var info =
$@"Вход через приложение:

1) В приложении нажмите <b>“Вход через бота”</b> — приложение покажет короткий код.
2) Перешлите этот код в чат боту <b>@{WebUtility.HtmlEncode(botUser)}</b> (можно просто вставить сюда).
3) Вернитесь в приложение и нажмите <b>«Обновить»</b> — вход завершится автоматически.

Если что-то не получается — код действует ограниченное время (обычно ~15 минут).";
            await _bot.SendMessage(chatId, info, parseMode: ParseMode.Html, cancellationToken: ct);
            return;
        }

        // ===== Фото блюда
        if (msg.Photo is { } photos && photos.Length > 0)
        {
            try
            {
                var ph = photos.OrderBy(p => p.FileSize).Last();
                var file = await _bot.GetFile(ph.FileId, cancellationToken: ct);

                var token = _cfg["Telegram:BotToken"]!;
                var url = $"https://api.telegram.org/file/bot{token}/{file.FilePath}";
                var client = httpf.CreateClient();
                var bytes = await client.GetByteArrayAsync(url, ct);

                await _bot.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);

                var conv = await nutrition.AnalyzeAsync(bytes, ct);

                var productsJsonEntry = conv is null ? null : ProductJsonHelper.BuildProductsJson(conv.CalcPlanJson);
                var entry = new MealEntry
                {
                    ChatId = chatId,
                    UserId = userId,
                    Username = username,
                    CreatedAtUtc = DateTimeOffset.UtcNow,

                    FileId = ph.FileId,
                    FileMime = "image/jpeg",
                    ImageBytes = bytes,

                    DishName = conv?.Result.dish,
                    IngredientsJson = conv is null ? null : JsonSerializer.Serialize(conv.Result.ingredients),
                    ProductsJson = productsJsonEntry,
                    ProteinsG = conv?.Result.proteins_g,
                    FatsG = conv?.Result.fats_g,
                    CarbsG = conv?.Result.carbs_g,
                    CaloriesKcal = conv?.Result.calories_kcal,
                    Confidence = conv?.Result.confidence,
                    WeightG = conv?.Result.weight_g,
                    Model = _cfg["OpenAI:ReasoningModel"] ?? _cfg["OpenAI:Model"],

                    Step1Json = conv is null ? null : JsonSerializer.Serialize(conv.Step1),
                    ReasoningPrompt = conv?.ReasoningPrompt
                };
                db.Meals.Add(entry);
                await db.SaveChangesAsync(ct);

                if (conv is not null)
                {
                    _threadsByChat[chatId] = conv.ThreadId;

                    var step1Html = BuildStep1PreviewHtml(conv.Step1);
                    if (!string.IsNullOrWhiteSpace(step1Html))
                        await SendHtmlSafe(_bot, chatId, step1Html, ct);

                    var promptHtml = BuildPromptPreviewHtml(conv.ReasoningPrompt);
                    if (!string.IsNullOrWhiteSpace(promptHtml))
                        await SendHtmlSafe(_bot, chatId, promptHtml, ct);

                    var r = conv.Result;
                    var compHtml = BuildProductsHtml(productsJsonEntry!);
                    var htmlFinal =
$@"<b>✅ Final nutrition (computed by AI)</b>
<b>🍽️ {WebUtility.HtmlEncode(r.dish)}</b>
Ingredients (EN): <code>{WebUtility.HtmlEncode(string.Join(", ", r.ingredients))}</code>

Serving weight: <b>{r.weight_g:F0} g</b>
P: <b>{r.proteins_g:F1} g</b>   F: <b>{r.fats_g:F1} g</b>   C: <b>{r.carbs_g:F1} g</b>
Calories: <b>{r.calories_kcal:F0}</b> kcal
Model confidence: <b>{(r.confidence * 100m):F0}%</b>{compHtml}";
                    await SendHtmlSafe(_bot, chatId, htmlFinal, ct);

                    await _bot.SendMessage(chatId,
                        "Можно уточнить текстом или голосом: “+50 g bread”, “no sauce”, “replace mayo with yogurt”, “weight 220 g”.",
                        cancellationToken: ct);
                }
                else
                {
                    await _bot.SendMessage(chatId, "Не удалось распознать блюдо. Попробуйте фото получше (чёткий крупный план).", cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                await _bot.SendMessage(chatId, "Произошла ошибка при обработке изображения.", cancellationToken: ct);
                Console.WriteLine(ex);
            }
            return;
        }

        // ===== Голосовые уточнения (voice / audio)
        if (msg.Voice is { } voice)
        {
            try
            {
                await _bot.SendChatAction(chatId, ChatAction.RecordVoice, cancellationToken: ct);

                var file = await _bot.GetFile(voice.FileId, cancellationToken: ct);
                var token = _cfg["Telegram:BotToken"]!;
                var url = $"https://api.telegram.org/file/bot{token}/{file.FilePath}";
                var client = httpf.CreateClient();
                var bytes = await client.GetByteArrayAsync(url, ct);

                var sttText = await stt.TranscribeAsync(bytes, language: "ru", fileName: "voice.ogg", contentType: "audio/ogg", ct);
                if (string.IsNullOrWhiteSpace(sttText))
                {
                    await _bot.SendMessage(chatId, "Не удалось распознать голос. Скажите ещё раз или введите текстом.", cancellationToken: ct);
                    return;
                }

                await _bot.SendMessage(chatId, $"🎙️ Уточнение с голоса: {sttText}", cancellationToken: ct);

                await ApplyClarificationTextAsync(db, nutrition, chatId, sttText, ct);
            }
            catch (Exception ex)
            {
                await _bot.SendMessage(chatId, "Ошибка при распознавании голосового сообщения.", cancellationToken: ct);
                Console.WriteLine(ex);
            }
            return;
        }

        if (msg.Audio is { } audio)
        {
            try
            {
                await _bot.SendChatAction(chatId, ChatAction.RecordVoice, cancellationToken: ct);

                var file = await _bot.GetFile(audio.FileId, cancellationToken: ct);
                var token = _cfg["Telegram:BotToken"]!;
                var url = $"https://api.telegram.org/file/bot{token}/{file.FilePath}";
                var client = httpf.CreateClient();
                var bytes = await client.GetByteArrayAsync(url, ct);

                var fileName = string.IsNullOrWhiteSpace(audio.FileName) ? "audio.mp3" : audio.FileName;
                var mime = string.IsNullOrWhiteSpace(audio.MimeType) ? "application/octet-stream" : audio.MimeType;

                var sttText = await stt.TranscribeAsync(bytes, language: "ru", fileName: fileName, contentType: mime, ct);
                if (string.IsNullOrWhiteSpace(sttText))
                {
                    await _bot.SendMessage(chatId, "Не удалось распознать аудио. Попробуйте ещё раз или введите текстом.", cancellationToken: ct);
                    return;
                }

                await _bot.SendMessage(chatId, $"🎙️ Уточнение с аудио: {sttText}", cancellationToken: ct);

                await ApplyClarificationTextAsync(db, nutrition, chatId, sttText, ct);
            }
            catch (Exception ex)
            {
                await _bot.SendMessage(chatId, "Ошибка при распознавании аудио.", cancellationToken: ct);
                Console.WriteLine(ex);
            }
            return;
        }

        // ===== Текст: это может быть КОД из приложения ИЛИ уточнение
        if (!string.IsNullOrWhiteSpace(msg.Text))
        {
            // 1) сначала пытаемся трактовать это как старт-код (линковка)
            var trimmed = msg.Text.Trim();
            if (await TryLinkStartCodeAsync(db, chatId, trimmed, ct))
                return;

            // 2) команды
            if (trimmed.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            {
                var payload = trimmed.Length > 6 ? trimmed.Substring(6).Trim() : "";
                if (!string.IsNullOrWhiteSpace(payload))
                {
                    if (await TryLinkStartCodeAsync(db, chatId, payload, ct))
                        return;
                }

                await _bot.SendMessage(chatId,
                    "Пришлите фото блюда — сначала покажу состав в процентах, затем рассчитаю нутриенты.\n" +
                    "Для входа в приложение получите код в приложении и вставьте его сюда.",
                    cancellationToken: ct);
                return;
            }

            // 3) иначе — это уточнение
            try
            {
                await _bot.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);
                await ApplyClarificationTextAsync(db, nutrition, chatId, trimmed, ct);
            }
            catch (Exception ex)
            {
                await _bot.SendMessage(chatId, "Ошибка при применении уточнения.", cancellationToken: ct);
                Console.WriteLine(ex);
            }
            return;
        }

        // На всё остальное
        await _bot.SendMessage(chatId,
            "Пришлите *фото блюда* или используйте /report. Для входа в приложение — отправьте сюда код из приложения.",
            parseMode: ParseMode.Markdown,
            cancellationToken: ct);
    }

    // ======= Линковка старт-кода (из приложения) =======
    private static readonly Regex CodePattern = new(@"^[A-Z0-9\-]{6,16}$", RegexOptions.Compiled);

    private async Task<bool> TryLinkStartCodeAsync(BotDbContext db, long chatId, string text, CancellationToken ct)
    {
        var code = text.Trim().ToUpperInvariant();
        if (!CodePattern.IsMatch(code))
            return false;

        var row = await db.StartCodes.FirstOrDefaultAsync(x => x.Code == code, ct);
        if (row is null)
            return false;

        if (row.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            await _bot.SendMessage(chatId, "Код просрочен. Попросите новый в приложении.", cancellationToken: ct);
            return true;
        }
        if (row.ConsumedAtUtc is not null)
        {
            await _bot.SendMessage(chatId, "Этот код уже использован. Попросите новый в приложении.", cancellationToken: ct);
            return true;
        }

        if (row.ChatId == chatId)
        {
            await _bot.SendMessage(chatId, "Этот код уже привязан к вашему аккаунту. Можете вернуться в приложение и нажать «Обновить».", cancellationToken: ct);
            return true;
        }

        row.ChatId = chatId; // линкуем
        await db.SaveChangesAsync(ct);

        await _bot.SendMessage(chatId,
            "✅ Код принят. Вернитесь в приложение и нажмите «Обновить».",
            cancellationToken: ct);
        return true;
    }

    // ======= Clarify helper (общий для текста и голоса) =======
    private async Task ApplyClarificationTextAsync(BotDbContext db, NutritionService nutrition, long chatId, string text, CancellationToken ct)
    {
        var mode = GetClarifyMode();

        // Берём последнюю запись с фото
        var last = await db.Meals
            .Where(m => m.ChatId == chatId && m.ImageBytes != null)
            .OrderByDescending(m => m.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        NutritionConversation? conv2 = null;

        switch (mode)
        {
            case ClarifyStartMode.FromSavedStep1:
                {
                    if (last?.Step1Json is { Length: > 0 })
                    {
                        try
                        {
                            var step1 = JsonSerializer.Deserialize<Step1Snapshot>(
                                last.Step1Json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (step1 is not null)
                                conv2 = await nutrition.ClarifyFromStep1Async(step1, text, ct);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[ClarifyFromStep1] fail, fallback to image: " + ex.Message);
                        }
                    }
                    if (conv2 is null && last?.ImageBytes is { Length: > 0 })
                        conv2 = await nutrition.AnalyzeWithNoteAsync(last.ImageBytes, text, ct);
                    break;
                }

            case ClarifyStartMode.FromVisionStep1:
                {
                    if (last?.ImageBytes is { Length: > 0 })
                        conv2 = await nutrition.AnalyzeWithNoteAsync(last.ImageBytes, text, ct);
                    if (conv2 is null && last?.Step1Json is { Length: > 0 })
                    {
                        try
                        {
                            var step1 = JsonSerializer.Deserialize<Step1Snapshot>(
                                last.Step1Json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (step1 is not null)
                                conv2 = await nutrition.ClarifyFromStep1Async(step1, text, ct);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[ClarifyFromStep1 fallback] " + ex.Message);
                        }
                    }
                    break;
                }

            case ClarifyStartMode.Auto:
            default:
                {
                    if (last?.Step1Json is { Length: > 0 })
                    {
                        try
                        {
                            var step1 = JsonSerializer.Deserialize<Step1Snapshot>(
                                last.Step1Json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (step1 is not null)
                                conv2 = await nutrition.ClarifyFromStep1Async(step1, text, ct);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[Auto ClarifyFromStep1] fail: " + ex.Message);
                            conv2 = null;
                        }
                    }
                    if (conv2 is null && last?.ImageBytes is { Length: > 0 })
                        conv2 = await nutrition.AnalyzeWithNoteAsync(last.ImageBytes, text, ct);
                    break;
                }
        }

        if (conv2 is not null)
        {
            // Обновляем запись
            last!.DishName = conv2.Result.dish;
            last.IngredientsJson = JsonSerializer.Serialize(conv2.Result.ingredients);
            var productsJson = ProductJsonHelper.BuildProductsJson(conv2.CalcPlanJson);
            last.ProductsJson = productsJson;
            last.ProteinsG = conv2.Result.proteins_g;
            last.FatsG = conv2.Result.fats_g;
            last.CarbsG = conv2.Result.carbs_g;
            last.CaloriesKcal = conv2.Result.calories_kcal;
            last.Confidence = conv2.Result.confidence;
            last.WeightG = conv2.Result.weight_g;
            last.ReasoningPrompt = conv2.ReasoningPrompt;
            last.CreatedAtUtc = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);

            // Вывод
            var step1Html = BuildStep1PreviewHtml(conv2.Step1);
            if (!string.IsNullOrWhiteSpace(step1Html))
                await SendHtmlSafe(_bot, chatId, step1Html, ct);

            var promptHtml = BuildPromptPreviewHtml(conv2.ReasoningPrompt);
            if (!string.IsNullOrWhiteSpace(promptHtml))
                await SendHtmlSafe(_bot, chatId, promptHtml, ct);

            var r = conv2.Result;
            var compHtml = BuildProductsHtml(productsJson);
            var htmlFinal =
$@"<b>✅ Final nutrition (after clarify)</b>
<b>🍽️ {WebUtility.HtmlEncode(r.dish)}</b>
Ingredients (EN): <code>{WebUtility.HtmlEncode(string.Join(", ", r.ingredients))}</code>

Serving weight: <b>{r.weight_g:F0} g</b>
P: <b>{r.proteins_g:F1} g</b>   F: <b>{r.fats_g:F1} g</b>   C: <b>{r.carbs_g:F1} g</b>
Calories: <b>{r.calories_kcal:F0}</b> kcal
Model confidence: <b>{(r.confidence * 100m):F0}%</b>{compHtml}";
            await SendHtmlSafe(_bot, chatId, htmlFinal, ct);
        }
        else
        {
            await _bot.SendMessage(chatId,
                "Не удалось применить уточнение. Пришлите фото блюда.",
                cancellationToken: ct);
        }
    }

    // ================= helpers (UI) =================

    private static string BuildStep1PreviewHtml(Step1Snapshot s1)
    {
        if (s1 is null || s1.ingredients is null || s1.shares_percent is null) return "";
        var sb = new StringBuilder();
        sb.Append("<b>🔎 Step 1 — composition from photo</b>\n");
        sb.Append("Dish: <i>").Append(WebUtility.HtmlEncode(s1.dish)).Append("</i>\n");
        sb.Append("Estimated weight: <b>").Append(s1.weight_g.ToString("F0")).Append(" g</b>, ");
        sb.Append("confidence: <b>").Append((s1.confidence * 100m).ToString("F0")).Append("%</b>\n");
        for (int i = 0; i < s1.ingredients.Length; i++)
        {
            var name = WebUtility.HtmlEncode(s1.ingredients[i] ?? "");
            var share = (i < s1.shares_percent.Length) ? s1.shares_percent[i] : 0m;
            sb.Append("• ").Append(name).Append(": <b>").Append(share.ToString("F1")).Append("%</b>\n");
        }
        return sb.ToString();
    }

    private static string BuildPromptPreviewHtml(string prompt, int maxLen = 1400)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return "";
        var trimmed = prompt.Length > maxLen ? prompt.Substring(0, maxLen) + " …" : prompt;
        var enc = WebUtility.HtmlEncode(trimmed);
        return "<b>🧠 Reasoning request (compact)</b>\n<pre>" + enc + "</pre>";
    }

    private static string BuildProductsHtml(string productsJson)
    {
        var products = ProductJsonHelper.DeserializeProducts(productsJson);
        if (products.Length == 0) return "";
        var sb = new StringBuilder();
        sb.Append("\n\n<b>📊 Composition</b>\n");
        foreach (var p in products)
        {
            sb.Append("• ")
              .Append(WebUtility.HtmlEncode(p.name))
              .Append(": <b>")
              .Append(p.grams.ToString("F0"))
              .Append(" g</b>, P ")
              .Append(p.proteins_g.ToString("F1"))
              .Append(" g F ")
              .Append(p.fats_g.ToString("F1"))
              .Append(" g C ")
              .Append(p.carbs_g.ToString("F1"))
              .Append(" g, ")
              .Append(p.calories_kcal.ToString("F0"))
              .Append(" kcal (")
              .Append(p.percent.ToString("F0"))
              .Append("%)\n");
        }
        return sb.ToString();
    }

    // Безопасная отправка HTML: если парсинг падает или слишком длинно — отправляем plain text чанками
    private async Task SendHtmlSafe(TelegramBotClient bot, long chatId, string html, CancellationToken ct)
    {
        const int MAX_HTML = 3500; // запас до лимита 4096
        if (string.IsNullOrEmpty(html))
            return;

        if (html.Length <= MAX_HTML)
        {
            try
            {
                await bot.SendMessage(chatId, html, parseMode: ParseMode.Html, cancellationToken: ct);
                return;
            }
            catch (ApiRequestException ex) when (ex.Message.Contains("can't parse entities", StringComparison.OrdinalIgnoreCase))
            { /* fallback ниже */ }
            catch
            { /* fallback ниже */ }
        }

        var plain = ToPlainText(html);
        await SendPlainInChunks(bot, chatId, plain, ct);
    }

    private static async Task SendPlainInChunks(TelegramBotClient bot, long chatId, string text, CancellationToken ct)
    {
        const int MAX = 3800;
        if (string.IsNullOrEmpty(text))
            return;

        var sb = new StringBuilder();
        foreach (var line in text.Split('\n'))
        {
            if (sb.Length + line.Length + 1 > MAX)
            {
                await bot.SendMessage(chatId, sb.ToString(), cancellationToken: ct);
                sb.Clear();
            }
            sb.AppendLine(line);
        }
        if (sb.Length > 0)
            await bot.SendMessage(chatId, sb.ToString(), cancellationToken: ct);
    }

    private static string ToPlainText(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var noTags = Regex.Replace(html, "<.*?>", string.Empty);
        return WebUtility.HtmlDecode(noTags);
    }
}
