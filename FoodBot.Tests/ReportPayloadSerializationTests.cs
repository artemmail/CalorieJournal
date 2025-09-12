using System.Collections.Generic;
using System.Text.Json;
using FoodBot.Services.Reports;
using Xunit;

public class ReportPayloadSerializationTests
{
    [Fact]
    public void MealEntry_SerializesWithExpectedPropertyNames()
    {
        var meal = new MealEntry
        {
            Dish = "Soup",
            LocalDate = "2024-01-01",
            LocalTime = "12:00",
            LocalDateTimeIso = "2024-01-01 12:00",
            Calories = 10,
            Proteins = 1,
            Fats = 2,
            Carbs = 3
        };

        var json = JsonSerializer.Serialize(meal);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("Soup", root.GetProperty("dish").GetString());
        Assert.Equal("2024-01-01", root.GetProperty("localDate").GetString());
        Assert.Equal("12:00", root.GetProperty("localTime").GetString());
        Assert.Equal("2024-01-01 12:00", root.GetProperty("localDateTimeIso").GetString());
        Assert.Equal(10m, root.GetProperty("calories").GetDecimal());
        Assert.Equal(1m, root.GetProperty("proteins").GetDecimal());
        Assert.Equal(2m, root.GetProperty("fats").GetDecimal());
        Assert.Equal(3m, root.GetProperty("carbs").GetDecimal());
    }

    [Fact]
    public void Totals_SerializesWithExpectedPropertyNames()
    {
        var totals = new Totals
        {
            Calories = 100,
            Proteins = 10,
            Fats = 5,
            Carbs = 20,
            MealsCount = 3
        };

        var json = JsonSerializer.Serialize(totals);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(100m, root.GetProperty("calories").GetDecimal());
        Assert.Equal(10m, root.GetProperty("proteins").GetDecimal());
        Assert.Equal(5m, root.GetProperty("fats").GetDecimal());
        Assert.Equal(20m, root.GetProperty("carbs").GetDecimal());
        Assert.Equal(3, root.GetProperty("mealsCount").GetInt32());
    }

    [Fact]
    public void Grouping_SerializesWithExpectedPropertyNames()
    {
        var grouping = new Grouping
        {
            ByHour = new List<GroupByHour> { new GroupByHour { Hour = 9, Count = 1, Kcal = 100 } },
            ByDay = new List<GroupByDay>
            {
                new GroupByDay
                {
                    Date = "2024-01-01",
                    Meals = 2,
                    Kcal = 200,
                    Proteins = 20,
                    Fats = 10,
                    Carbs = 30
                }
            }
        };

        var json = JsonSerializer.Serialize(grouping);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var hour = root.GetProperty("byHour")[0];
        Assert.Equal(9, hour.GetProperty("hour").GetInt32());
        Assert.Equal(1, hour.GetProperty("cnt").GetInt32());
        Assert.Equal(100m, hour.GetProperty("kcal").GetDecimal());

        var day = root.GetProperty("byDay")[0];
        Assert.Equal("2024-01-01", day.GetProperty("date").GetString());
        Assert.Equal(2, day.GetProperty("meals").GetInt32());
        Assert.Equal(200m, day.GetProperty("kcal").GetDecimal());
        Assert.Equal(20m, day.GetProperty("prot").GetDecimal());
        Assert.Equal(10m, day.GetProperty("fat").GetDecimal());
        Assert.Equal(30m, day.GetProperty("carb").GetDecimal());
    }

    [Fact]
    public void ReportPayload_SerializesWithExpectedRootProperties()
    {
        var payload = new ReportPayload
        {
            Timezone = new TimezoneInfoPayload { Id = "tz", Label = "label", UtcOffset = "+03:00" },
            Period = new PeriodInfoPayload { Kind = "Day", Label = "Today", StartLocal = "sl", EndLocal = "el", StartUtc = "su", EndUtc = "eu" },
            Now = new NowInfoPayload { Local = "now", LocalHour = "12", LocalDate = "2024-01-01" },
            Client = new ClientInfoPayload { Name = "A", Age = 30, Goals = "G", Restrictions = "R" },
            Meals = new List<MealEntry>(),
            Totals = new Totals(),
            Grouping = new Grouping(),
            DailyPlanContext = new DailyPlanContext { IsDaily = true }
        };

        var json = JsonSerializer.Serialize(payload);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("timezone", out _));
        Assert.True(root.TryGetProperty("period", out _));
        Assert.True(root.TryGetProperty("now", out _));
        Assert.True(root.TryGetProperty("client", out _));
        Assert.True(root.TryGetProperty("meals", out _));
        Assert.True(root.TryGetProperty("totals", out _));
        Assert.True(root.TryGetProperty("grouping", out _));
        Assert.True(root.TryGetProperty("dailyPlanContext", out _));
    }
}

