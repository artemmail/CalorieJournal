using FoodBot;
using FoodBot.Logging;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Prefer logs near the deployed app (e.g. C:\fb\logs on production IIS).
    var fallbackLogDir = Path.Combine(builder.Environment.ContentRootPath, "logs");
    var localProfileLogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FoodBot",
        "logs");
    var configuredLogDir = builder.Configuration["Logging:File:Directory"];
    var fileLogDir = string.IsNullOrWhiteSpace(configuredLogDir) ? fallbackLogDir : configuredLogDir;

    try
    {
        Directory.CreateDirectory(fileLogDir);
        builder.Logging.AddProvider(new FileLoggerProvider(fileLogDir));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[startup] File logger failed ({fileLogDir}): {ex.Message}");
        try
        {
            Directory.CreateDirectory(localProfileLogDir);
            builder.Logging.AddProvider(new FileLoggerProvider(localProfileLogDir));
            Console.Error.WriteLine($"[startup] File logger fallback enabled ({localProfileLogDir})");
        }
        catch (Exception ex2)
        {
            Console.Error.WriteLine($"[startup] File logger disabled ({localProfileLogDir}): {ex2.Message}");
        }
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
