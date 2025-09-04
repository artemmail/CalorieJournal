using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FoodBot.Data;
using FoodBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

public class PdfReportServiceTests
{
    [Fact]
    public async Task BuildAsync_GeneratesPdfWithRussianCharacters()
    {
        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var ctx = new BotDbContext(options);
        ctx.PersonalCards.Add(new PersonalCard
        {
            ChatId = 1,
            Name = "Иван Иванов",
            BirthYear = 1990,
            DietGoals = "Сброс веса",
            MedicalRestrictions = "Нет"
        });
        ctx.Meals.Add(new MealEntry
        {
            Id = 1,
            ChatId = 1,
            UserId = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            FileId = "file",
            DishName = "Борщ"
        });
        await ctx.SaveChangesAsync();

        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PdfReport:LaTeXPath"] = "pdflatex"
            })
            .Build();

        var service = new PdfReportService(ctx, cfg);
        var (stream, fileName) = await service.BuildAsync(1, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

        Assert.False(string.IsNullOrEmpty(fileName));
        Assert.True(stream.Length > 0);
    }
}
