using FoodBot.Data;
using FoodBot.Services;
using FoodBot.Services.OpenAI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles; // ← для корректных MIME
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using System.Text.Json.Serialization;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ===== DB + DI =====
builder.Services.AddDbContext<BotDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Sql")));

builder.Services.AddHttpClient();



builder.Services.AddSingleton<IOpenAiClient>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();

    var settings = new OpenAiSettings
    {
        ApiKey = cfg["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey missing"),
        TimeoutSeconds = int.TryParse(cfg["OpenAI:TimeoutSeconds"], out var t) ? t : 60,
        DebugLog = bool.TryParse(cfg["OpenAI:DebugLog"], out var dbg) && dbg,
        MaxRetries = Math.Clamp(int.TryParse(cfg["OpenAI:MaxRetries"], out var mr) ? mr : 7, 1, 7),
        RetryBaseDelaySeconds = Math.Clamp(int.TryParse(cfg["OpenAI:RetryBaseSeconds"], out var rbs) ? rbs : 2, 1, 60),
        ClientFactory = httpFactory
    };
    return new OpenAiClient(settings);
});

// NutritionService — передаём IOpenAiClient в конструктор (внешний интерфейс сервиса при этом не ломаем)
builder.Services.AddSingleton<NutritionService>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var ai = sp.GetRequiredService<IOpenAiClient>();
    return new NutritionService(cfg, httpFactory, env, ai);
});




builder.Services.AddSingleton<SpeechToTextService>();
builder.Services.AddScoped<TelegramReportService>();
builder.Services.AddScoped<StatsService>();
builder.Services.AddScoped<PersonalCardService>();
builder.Services.AddScoped<DietAnalysisService>();
builder.Services.AddHostedService<AnalysisQueueWorker>();

// ===== Controllers / API =====
builder.Services.AddControllers(); // AppAuthController / MealsController

// ===== CORS (Angular dev + Capacitor) =====
const string CorsPolicy = "FrontendDev";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, p =>
        p.WithOrigins(
            "http://localhost",          // Capacitor Android (WebView)
            "https://localhost",          // Capacitor Android (WebView)
            "capacitor://localhost",     // Capacitor iOS (WebView)
            "http://localhost:4200",     // ng serve
            "https://localhost:4200",
            "http://youscriptor.ru",      // твой HTTP-домен API (добавь https:// при наличии TLS)
            "https://stock-charts.ru",      // твой HTTP-домен API (добавь https:// при наличии TLS)
            "https://healthymeals.space"      // твой HTTP-домен API (добавь https:// при наличии TLS)
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .WithExposedHeaders("Content-Disposition")
        .SetPreflightMaxAge(TimeSpan.FromHours(1))
    );
});

// ===== Auth (JWT) =====
builder.Services.AddSingleton<JwtService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        var cfg = builder.Configuration;
        var keyBytes = JwtKeyHelper.GetKeyBytes(cfg["Auth:JwtKey"]!);

        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = cfg["Auth:Issuer"] ?? "foodbot",
            ValidAudience = cfg["Auth:Audience"] ?? "foodbot.app",
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });


builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        // принимать/отдавать enum как строки: "day","week","month","quarter"
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

builder.Services.AddAuthorization();

// ===== Telegram =====
var botToken = builder.Configuration["Telegram:BotToken"]
               ?? throw new InvalidOperationException("Telegram:BotToken missing");
builder.Services.AddSingleton(new TelegramBotClient(botToken));

// Общий обработчик
builder.Services.AddSingleton<UpdateHandler>();

// Long polling сервис (сам решит, активен ли он по конфигу)
builder.Services.AddHostedService<LongPollingService>();

var app = builder.Build();

if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

// ===== ПОРЯДОК ВАЖЕН =====
app.UseCors(CorsPolicy);       // CORS до аутентификации
app.UseAuthentication();
app.UseAuthorization();

// ======= СТАТИКА + SPA FALLBACK (Angular в wwwroot) =======
// (необязательно) дополнительные MIME-типы
var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".webmanifest"] = "application/manifest+json";
contentTypeProvider.Mappings[".wasm"] = "application/wasm";

// Раздаём index.html и ассеты из wwwroot
app.UseDefaultFiles(); // ищет index.html в wwwroot
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider
});

// Для index.html отключим кэш, чтобы SPA обновлялась
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path == "/" || ctx.Request.Path.Value?.EndsWith("/index.html") == true)
    {
        ctx.Response.Headers.CacheControl = "no-store, no-cache";
        ctx.Response.Headers.Pragma = "no-cache";
        ctx.Response.Headers.Expires = "0";
    }
    await next();
});

// ===== Маршруты контроллеров (AppAuthController, MealsController) =====
app.MapControllers();

// ===== Telegram webhook endpoint =====
var secret = builder.Configuration["Telegram:WebhookSecretPath"] ?? "my-secret";
app.MapPost($"/bot/{secret}", async (HttpContext http, UpdateHandler handler, ILogger<Program> logger, CancellationToken ct) =>
{
    var update = await System.Text.Json.JsonSerializer.DeserializeAsync<Update>(http.Request.Body, cancellationToken: ct);
    if (update != null)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await handler.HandleAsync(update, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling update");
            }
        });
    }
    return Results.Ok();
});

// ===== SPA fallback: все НЕ-API/НЕ-webhook запросы — на Angular index.html =====
app.MapFallbackToFile("/index.html");

// (старый пинг убрали: app.MapGet("/", () => "FoodBot is running");)

// Автонастройка webhook/polling при старте
_ = Task.Run(async () =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var bot = scope.ServiceProvider.GetRequiredService<TelegramBotClient>();
        var mode = builder.Configuration["Telegram:Mode"]?.Trim().ToLowerInvariant();
        if (mode == "webhook")
        {
            var baseUrl = builder.Configuration["Telegram:WebhookBaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                var url = $"{baseUrl.TrimEnd('/')}/bot/{secret}";
                await bot.SetWebhook(url);
                Console.WriteLine($"Webhook set: {url}");
            }
            else
            {
                Console.WriteLine("Webhook mode enabled, но Telegram:WebhookBaseUrl не задан — вебхук не выставлен автоматически.");
            }
        }
        else
        {
            await bot.DeleteWebhook(true);
            Console.WriteLine("Polling mode: webhook удалён.");
        }
    }
    catch (Exception ex) { Console.WriteLine(ex); }
});

app.Run();
