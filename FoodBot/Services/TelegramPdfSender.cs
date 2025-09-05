using System.IO;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FoodBot.Services;

public class TelegramPdfSender
{
    private readonly ITelegramBotClient _bot;

    public TelegramPdfSender(ITelegramBotClient bot)
    {
        _bot = bot;
    }

    public Task SendAsync(long chatId, Stream pdf, string fileName, CancellationToken ct)
    {
        pdf.Position = 0;
        return _bot.SendDocument(chatId, InputFile.FromStream(pdf, fileName), cancellationToken: ct);
    }
}

