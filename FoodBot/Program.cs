using FoodBot;
using FoodBot.Logging;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Keep file logging enabled, but do not crash startup when the directory
    // is unavailable on a different machine/user profile.
    var fallbackLogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FoodBot",
        "logs");
    var configuredLogDir = builder.Configuration["Logging:File:Directory"];
    var fileLogDir = string.IsNullOrWhiteSpace(configuredLogDir) ? fallbackLogDir : configuredLogDir;

    try
    {
        builder.Logging.AddProvider(new FileLoggerProvider(fileLogDir));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[startup] File logger disabled ({fileLogDir}): {ex.Message}");
    }

    builder.Services
        .AddBotServices(builder.Configuration)
        .AddBotAuth(builder.Configuration);

    var app = builder.Build();

    app.UseBotEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Console.Error.WriteLine("[startup] Fatal application error:");
    Console.Error.WriteLine(ex);
    throw;
}
