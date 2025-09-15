using System;
using System.Reflection;
using FoodBot.Models;
using FoodBot.Services;
using FoodBot.Services.Reports;
using Xunit;

public class WeekPeriodTests
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.CreateCustomTimeZone("UTC+3", TimeSpan.FromHours(3), "UTC+3", "UTC+3");

    private static (DateTimeOffset startUtc, string periodHuman, string recScopeHint, DateTime startLocal) InvokeGetPeriodStart(Type type)
    {
        var method = type.GetMethod("GetPeriodStart", BindingFlags.NonPublic | BindingFlags.Static)!;
        var nowLocal = new DateTimeOffset(2025, 9, 15, 12, 0, 0, Tz.BaseUtcOffset);
        var result = method.Invoke(null, new object[] { nowLocal, AnalysisPeriod.Week, Tz });
        return ((DateTimeOffset startUtc, string periodHuman, string recScopeHint, DateTime startLocal))result!;
    }

    [Fact]
    public void ReportDataLoader_UsesLast7Days()
    {
        var tuple = InvokeGetPeriodStart(typeof(ReportDataLoader));
        Assert.Equal(new DateTime(2025, 9, 9, 0, 0, 0), tuple.startLocal);
        Assert.Equal("за последние 7 дней", tuple.periodHuman);
        Assert.Equal(new DateTimeOffset(2025, 9, 8, 21, 0, 0, TimeSpan.Zero), tuple.startUtc);
    }

    [Fact]
    public void DietAnalysisService_UsesLast7Days()
    {
        var tuple = InvokeGetPeriodStart(typeof(DietAnalysisService));
        Assert.Equal(new DateTime(2025, 9, 9, 0, 0, 0), tuple.startLocal);
        Assert.Equal("за последние 7 дней", tuple.periodHuman);
        Assert.Equal(new DateTimeOffset(2025, 9, 8, 21, 0, 0, TimeSpan.Zero), tuple.startUtc);
    }
}
