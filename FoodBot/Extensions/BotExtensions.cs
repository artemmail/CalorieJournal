using FoodBot.Data;
using FoodBot.Services;
using FoodBot.Services.OpenAI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using System.Text.Json.Serialization;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FoodBot;

public static class BotExtensions
{
    private const string CorsPolicy = "FrontendDev";

    public static IServiceCollection AddBotServices(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddDbContext<BotDbContext>(opt =>
            opt.UseSqlServer(cfg.GetConnectionString("Sql")));

        services.AddHttpClient();
        services.AddMemoryCache();

        services.AddSingleton<INutritionSessionService, NutritionSessionService>();

        services.AddSingleton<IOpenAiClient>(sp =>
        {
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

        services.AddSingleton<NutritionService>(sp =>
        {
            var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
            var env = sp.GetRequiredService<IWebHostEnvironment>();
            var sessions = sp.GetRequiredService<INutritionSessionService>();
            var ai = sp.GetRequiredService<IOpenAiClient>();
            return new NutritionService(cfg, httpFactory, env, sessions, ai);
        });

        services.AddSingleton<SpeechToTextService>();
        services.AddScoped<TelegramReportService>();
        services.AddScoped<PdfReportService>();
        services.AddScoped<StatsService>();
        services.AddScoped<PersonalCardService>();
        services.AddScoped<ReportDataLoader>();
        services.AddScoped<AnalysisPromptBuilder>();
        services.AddScoped<AnalysisGenerator>();
        services.AddScoped<DietAnalysisService>();
        services.AddScoped<AnalysisPdfService>();
          services.AddScoped<IMealRepository, MealRepository>();
          services.AddScoped<IMealService, MealService>();
          services.AddScoped<IAppAuthService, AppAuthService>();
        services.AddSingleton<MealImageService>();
          services.AddHostedService<AnalysisQueueWorker>();
          services.AddHostedService<PhotoQueueWorker>();
        services.AddHostedService<TextMealQueueWorker>();
          services.AddHostedService<PeriodPdfJobWorker>();
          services.AddHostedService<AnalysisPdfJobWorker>();

        services
            .AddControllers()
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            });

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicy, p =>
                p.WithOrigins(
                    "http://localhost",
                    "https://localhost",
                    "capacitor://localhost",
                    "http://localhost:4200",
                    "https://localhost:4200",
                    "http://youscriptor.ru",
                    "https://stock-charts.ru",
                    "https://healthymeals.space"
                )
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders("Content-Disposition")
                .SetPreflightMaxAge(TimeSpan.FromHours(1))
            );
        });

        var botToken = cfg["Telegram:BotToken"]
                       ?? throw new InvalidOperationException("Telegram:BotToken missing");
        services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botToken));
        services.AddSingleton(sp => (TelegramBotClient)sp.GetRequiredService<ITelegramBotClient>());
        services.AddSingleton<UpdateHandler>();
        services.AddHostedService<LongPollingService>();

        return services;
    }

    public static IServiceCollection AddBotAuth(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddSingleton<JwtService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
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

        services.AddAuthorization();

        return services;
    }

    public static WebApplication UseBotEndpoints(this WebApplication app)
    {
        if (app.Environment.IsProduction())
        {
            app.UseHttpsRedirection();
        }

        app.UseCors(CorsPolicy);
        app.UseAuthentication();
        app.UseAuthorization();

        var contentTypeProvider = new FileExtensionContentTypeProvider();
        contentTypeProvider.Mappings[".webmanifest"] = "application/manifest+json";
        contentTypeProvider.Mappings[".wasm"] = "application/wasm";

        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = contentTypeProvider
        });

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

        app.MapControllers();

        var secret = app.Configuration["Telegram:WebhookSecretPath"] ?? "my-secret";
        app.MapPost($"/bot/{secret}", async (HttpContext http, UpdateHandler handler, ILogger<Program> logger, CancellationToken ct) =>
        {
            Update? update;
            try
            {
                update = await JsonSerializer.DeserializeAsync<Update>(http.Request.Body, cancellationToken: ct);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Invalid update payload");

                return Results.BadRequest("Invalid update payload");
            }

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

        app.MapFallbackToFile("/index.html");

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = app.Services.CreateScope();
                var bot = scope.ServiceProvider.GetRequiredService<TelegramBotClient>();
                var mode = app.Configuration["Telegram:Mode"]?.Trim().ToLowerInvariant();
                if (mode == "webhook")
                {
                    var baseUrl = app.Configuration["Telegram:WebhookBaseUrl"];
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

        return app;
    }
}

