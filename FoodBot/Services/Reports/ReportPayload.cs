using System.Text.Json.Serialization;

namespace FoodBot.Services.Reports;

/// <summary>
/// Root payload object returned for report generation.
/// </summary>
public sealed class ReportPayload
{
    /// <summary>Information about the client's timezone.</summary>
    [JsonPropertyName("timezone")]
    public TimezoneInfoPayload Timezone { get; init; } = new();

    /// <summary>Period details for the generated report.</summary>
    [JsonPropertyName("period")]
    public PeriodInfoPayload Period { get; init; } = new();

    /// <summary>Current timestamp information.</summary>
    [JsonPropertyName("now")]
    public NowInfoPayload Now { get; init; } = new();

    /// <summary>Information about the client.</summary>
    [JsonPropertyName("client")]
    public ClientInfoPayload Client { get; init; } = new();

    /// <summary>List of meals consumed during the period.</summary>
    [JsonPropertyName("meals")]
    public List<MealEntry> Meals { get; init; } = new();

    /// <summary>Aggregated totals across all meals.</summary>
    [JsonPropertyName("totals")]
    public Totals Totals { get; init; } = new();

    /// <summary>Grouping information for meals.</summary>
    [JsonPropertyName("grouping")]
    public Grouping Grouping { get; init; } = new();

    /// <summary>Additional context for building a daily plan.</summary>
    [JsonPropertyName("dailyPlanContext")]
    public DailyPlanContext DailyPlanContext { get; init; } = new();
}

/// <summary>Represents a single meal in the report.</summary>
public sealed class MealEntry
{
    /// <summary>Name of the consumed dish.</summary>
    [JsonPropertyName("dish")]
    public string Dish { get; init; } = string.Empty;

    /// <summary>Local date of the meal formatted as yyyy-MM-dd.</summary>
    [JsonPropertyName("localDate")]
    public string LocalDate { get; init; } = string.Empty;

    /// <summary>Local time of the meal formatted as HH:mm.</summary>
    [JsonPropertyName("localTime")]
    public string LocalTime { get; init; } = string.Empty;

    /// <summary>Combined local date and time of the meal.</summary>
    [JsonPropertyName("localDateTimeIso")]
    public string LocalDateTimeIso { get; init; } = string.Empty;

    /// <summary>Calories of the meal in Kcal.</summary>
    [JsonPropertyName("calories")]
    public decimal Calories { get; init; }

    /// <summary>Proteins amount in grams.</summary>
    [JsonPropertyName("proteins")]
    public decimal Proteins { get; init; }

    /// <summary>Fats amount in grams.</summary>
    [JsonPropertyName("fats")]
    public decimal Fats { get; init; }

    /// <summary>Carbohydrates amount in grams.</summary>
    [JsonPropertyName("carbs")]
    public decimal Carbs { get; init; }
}

/// <summary>Aggregated totals for the report period.</summary>
public sealed class Totals
{
    /// <summary>Total calories across all meals.</summary>
    [JsonPropertyName("calories")]
    public decimal Calories { get; init; }

    /// <summary>Total proteins in grams.</summary>
    [JsonPropertyName("proteins")]
    public decimal Proteins { get; init; }

    /// <summary>Total fats in grams.</summary>
    [JsonPropertyName("fats")]
    public decimal Fats { get; init; }

    /// <summary>Total carbohydrates in grams.</summary>
    [JsonPropertyName("carbs")]
    public decimal Carbs { get; init; }

    /// <summary>Total number of meals recorded.</summary>
    [JsonPropertyName("mealsCount")]
    public int MealsCount { get; init; }
}

/// <summary>Grouping information for analytics.</summary>
public sealed class Grouping
{
    /// <summary>Grouping of meals by hour.</summary>
    [JsonPropertyName("byHour")]
    public List<GroupByHour> ByHour { get; init; } = new();

    /// <summary>Grouping of meals by day.</summary>
    [JsonPropertyName("byDay")]
    public List<GroupByDay> ByDay { get; init; } = new();
}

/// <summary>Represents meals grouped by hour.</summary>
public sealed class GroupByHour
{
    /// <summary>Hour of the day in 24-hour format.</summary>
    [JsonPropertyName("hour")]
    public int Hour { get; init; }

    /// <summary>Number of meals in the specified hour.</summary>
    [JsonPropertyName("cnt")]
    public int Count { get; init; }

    /// <summary>Total calories consumed in the hour.</summary>
    [JsonPropertyName("kcal")]
    public decimal Kcal { get; init; }
}

/// <summary>Represents meals grouped by day.</summary>
public sealed class GroupByDay
{
    /// <summary>Date of the group formatted as yyyy-MM-dd.</summary>
    [JsonPropertyName("date")]
    public string Date { get; init; } = string.Empty;

    /// <summary>Number of meals for the day.</summary>
    [JsonPropertyName("meals")]
    public int Meals { get; init; }

    /// <summary>Total calories for the day.</summary>
    [JsonPropertyName("kcal")]
    public decimal Kcal { get; init; }

    /// <summary>Total proteins for the day.</summary>
    [JsonPropertyName("prot")]
    public decimal Proteins { get; init; }

    /// <summary>Total fats for the day.</summary>
    [JsonPropertyName("fat")]
    public decimal Fats { get; init; }

    /// <summary>Total carbohydrates for the day.</summary>
    [JsonPropertyName("carb")]
    public decimal Carbs { get; init; }
}

/// <summary>Timezone details.</summary>
public sealed class TimezoneInfoPayload
{
    /// <summary>Timezone identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Human readable timezone label.</summary>
    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    /// <summary>UTC offset formatted as <c>hh:mm</c>.</summary>
    [JsonPropertyName("utcOffset")]
    public string UtcOffset { get; init; } = string.Empty;
}

/// <summary>Represents the report period metadata.</summary>
public sealed class PeriodInfoPayload
{
    /// <summary>Kind of the reporting period (e.g., Day, Week).</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>Human readable label for the period.</summary>
    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    /// <summary>Local start of the period.</summary>
    [JsonPropertyName("startLocal")]
    public string StartLocal { get; init; } = string.Empty;

    /// <summary>Local end of the period.</summary>
    [JsonPropertyName("endLocal")]
    public string EndLocal { get; init; } = string.Empty;

    /// <summary>UTC start of the period.</summary>
    [JsonPropertyName("startUtc")]
    public string StartUtc { get; init; } = string.Empty;

    /// <summary>UTC end of the period.</summary>
    [JsonPropertyName("endUtc")]
    public string EndUtc { get; init; } = string.Empty;
}

/// <summary>Current timestamp details.</summary>
public sealed class NowInfoPayload
{
    /// <summary>Current local timestamp.</summary>
    [JsonPropertyName("local")]
    public string Local { get; init; } = string.Empty;

    /// <summary>Current local hour.</summary>
    [JsonPropertyName("localHour")]
    public string LocalHour { get; init; } = string.Empty;

    /// <summary>Current local date.</summary>
    [JsonPropertyName("localDate")]
    public string LocalDate { get; init; } = string.Empty;
}

/// <summary>Information about the client receiving the report.</summary>
public sealed class ClientInfoPayload
{
    /// <summary>Client's name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Client's age.</summary>
    [JsonPropertyName("age")]
    public int? Age { get; init; }

    /// <summary>Client's height in centimeters.</summary>
    [JsonPropertyName("heightCm")]
    public int? HeightCm { get; init; }

    /// <summary>Client's weight in kilograms.</summary>
    [JsonPropertyName("weightKg")]
    public decimal? WeightKg { get; init; }

    /// <summary>Client's gender.</summary>
    [JsonPropertyName("gender")]
    public string? Gender { get; init; }

    /// <summary>Client's daily calorie expenditure or plan.</summary>
    [JsonPropertyName("dailyCalories")]
    public int? DailyCalories { get; init; }

    /// <summary>Diet goals provided by the client.</summary>
    [JsonPropertyName("goals")]
    public string? Goals { get; init; }

    /// <summary>Medical restrictions supplied by the client.</summary>
    [JsonPropertyName("restrictions")]
    public string? Restrictions { get; init; }
}

/// <summary>Context used for generating daily plan recommendations.</summary>
public sealed class DailyPlanContext
{
    /// <summary>Indicates whether the current report is for a single day.</summary>
    [JsonPropertyName("isDaily")]
    public bool IsDaily { get; init; }

    /// <summary>Remaining hours in the day formatted as HH:mm.</summary>
    [JsonPropertyName("remainingHourGrid")]
    public List<string>? RemainingHourGrid { get; init; }

    /// <summary>Local time of the last meal.</summary>
    [JsonPropertyName("lastMealLocalTime")]
    public string? LastMealLocalTime { get; init; }

    /// <summary>Hours elapsed since the last meal.</summary>
    [JsonPropertyName("hoursSinceLastMeal")]
    public double? HoursSinceLastMeal { get; init; }
}

