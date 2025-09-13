using Telegram.Bot;
using Telegram.Bot.Types;

namespace FoodBot.Services;

public class LongPollingService : BackgroundService
{
    private readonly TelegramBotClient _bot;
    private readonly UpdateHandler _handler;
    private readonly IConfiguration _cfg;

    public LongPollingService(TelegramBotClient bot, UpdateHandler handler, IConfiguration cfg)
    {
        _bot = bot;
        _handler = handler;
        _cfg = cfg;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var mode = _cfg["Telegram:Mode"]?.Trim().ToLowerInvariant();
        if (mode != "polling")
            return; // выключено

        // На всякий случай — удалить вебхук (иначе getUpdates вернёт 409)
        try { await _bot.DeleteWebhook(true, stoppingToken); } catch { /* ignore */ }

        var offset = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Update[] updates = await _bot.GetUpdates(offset, timeout: 30, cancellationToken: stoppingToken);
                foreach (var u in updates)
                {
                    offset = u.Id + 1;
                    try { await _handler.HandleAsync(u, stoppingToken); }
                    catch (Exception ex) { Console.WriteLine(ex); }
                }
            }
            catch (Exception)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
