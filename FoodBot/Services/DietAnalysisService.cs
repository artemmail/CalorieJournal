using System;
using System.Linq;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using FoodBot.Data;
using FoodBot.Models;
using System.Text;

namespace FoodBot.Services;

public sealed class DietAnalysisService
{
    private readonly BotDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _apiKey;

    public DietAnalysisService(BotDbContext db, IHttpClientFactory httpFactory, IConfiguration cfg)
    {
        _db = db;
        _httpFactory = httpFactory;
        _apiKey = cfg["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey missing");
    }

    public async Task<AnalysisReport> GetDailyAsync(long chatId, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var existing = await _db.AnalysisReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ChatId == chatId && r.ReportDate == today, ct);
        if (existing != null)
        {
            if (!existing.IsProcessing)
                return existing;
            return existing; // still processing
        }

        var hasMealsToday = await _db.Meals
            .AsNoTracking()
            .AnyAsync(m => m.ChatId == chatId && m.CreatedAtUtc.Date == today, ct);
        if (!hasMealsToday)
        {
            var last = await _db.AnalysisReports
                .AsNoTracking()
                .Where(r => r.ChatId == chatId && !r.IsProcessing)
                .OrderByDescending(r => r.ReportDate)
                .FirstOrDefaultAsync(ct);
            if (last != null)
                return last;
        }

        var rec = new AnalysisReport
        {
            ChatId = chatId,
            ReportDate = today,
            IsProcessing = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        _db.AnalysisReports.Add(rec);
        await _db.SaveChangesAsync(ct);

        try
        {
            var markdown = await GenerateReportAsync(chatId, AnalysisPeriod.Day, ct);
            rec.Markdown = markdown;
            rec.IsProcessing = false;
            rec.CreatedAtUtc = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch(Exception e)
        {
            rec.IsProcessing = false;
            await _db.SaveChangesAsync(ct);
            throw;
        }

        return rec;
    }

    public async Task<string> GetPlanAsync(long chatId, AnalysisPeriod period, CancellationToken ct)
    {
        if (period == AnalysisPeriod.Day)
            throw new ArgumentException("Day period is handled by GetDailyAsync", nameof(period));
        return await GenerateReportAsync(chatId, period, ct);
    }

    private async Task<string> GenerateReportAsync(long chatId, AnalysisPeriod period, CancellationToken ct)
    {
        var card = await _db.PersonalCards.AsNoTracking().FirstOrDefaultAsync(x => x.ChatId == chatId, ct);
        var from = DateTimeOffset.UtcNow.AddDays(-90);
        var meals = await _db.Meals.AsNoTracking()
            .Where(m => m.ChatId == chatId && m.CreatedAtUtc >= from)
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new { m.DishName, m.CreatedAtUtc, m.CaloriesKcal, m.ProteinsG, m.FatsG, m.CarbsG })
            .ToListAsync(ct);

        var mealHistory = meals.Select(m => new
        {
            time = m.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            dish = m.DishName,
            calories = m.CaloriesKcal ?? 0,
            proteins = m.ProteinsG ?? 0,
            fats = m.FatsG ?? 0,
            carbs = m.CarbsG ?? 0
        }).ToList();

        var data = new
        {
            clientInfo = new
            {
                birthYear = card?.BirthYear,
                goals = card?.DietGoals,
                restrictions = card?.MedicalRestrictions
            },
            mealHistory
        };

        var periodPrompt = period switch
        {
            AnalysisPeriod.Day => "rest of the day",
            AnalysisPeriod.Week => "upcoming week",
            AnalysisPeriod.Month => "upcoming month",
            AnalysisPeriod.Quarter => "upcoming quarter",
            _ => "period"
        };
        var prompt = $"Give dietologist recommendations for the {periodPrompt} based on the goals and restrictions. Use markdown with tables.";

        var reqObj = new
        {
            model = "gpt-4o-mini",
            input = new object[]
            {
                new { role = "system", content = "You are a helpful dietologist." },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "input_text", text = prompt },
                        // OpenAI responses API does not support an "input_json" type.
                        // Serialize the structured data and send it as plain text instead.
                        new { type = "input_text", text = JsonSerializer.Serialize(data) }
                    }
                }
            }
        };
        var body = JsonSerializer.Serialize(reqObj);
        var http = _httpFactory.CreateClient();
        using var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        msg.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var resp = await http.SendAsync(msg, ct);
        var respText = await resp.Content.ReadAsStringAsync(ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(respText);
        var content = doc.RootElement.GetProperty("output")[0]
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetProperty("value")
            .GetString();
        return content ?? string.Empty;
    }
}
