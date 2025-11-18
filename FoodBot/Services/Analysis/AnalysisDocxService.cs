using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FoodBot.Data;
using FoodBot.Services.Reports;
using Microsoft.Extensions.Logging;

namespace FoodBot.Services;

public sealed class AnalysisDocxService
{
    private readonly ILogger<AnalysisDocxService> _log;

    public AnalysisDocxService(ILogger<AnalysisDocxService> log)
        => _log = log;

    public Task<(MemoryStream Stream, string FileName)> BuildAsync(AnalysisReport1 report, CancellationToken ct = default)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (string.IsNullOrWhiteSpace(report.RequestJson))
            throw new InvalidOperationException("Report payload is missing.");

        ct.ThrowIfCancellationRequested();

        var payload = ExtractPayload(report.RequestJson);
        if (payload == null)
            throw new InvalidOperationException("Unable to extract report payload for DOCX export.");

        var stream = new MemoryStream();

        try
        {
            using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
            {
                var mainPart = document.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());
                var body = mainPart.Document.Body ?? throw new InvalidOperationException("Document body missing");

                AppendTitle(body, string.IsNullOrWhiteSpace(report.Name) ? "Анализ питания" : report.Name!);
                AppendParagraph(body, $"Сформирован: {payload.Now.Local}");
                AppendParagraph(body, $"Период: {payload.Period.Label} ({payload.Period.StartLocal} — {payload.Period.EndLocal})");
                AppendParagraph(body, $"Часовой пояс: {payload.Timezone.Label} (UTC{payload.Timezone.UtcOffset})");

                var clientRows = BuildClientRows(payload);
                if (clientRows.Count > 0)
                {
                    AppendSectionTitle(body, "Карточка клиента");
                    body.AppendChild(BuildKeyValueTable(clientRows));
                }

                AppendSectionTitle(body, "Сводка по нутриентам");
                body.AppendChild(BuildTotalsTable(payload.Totals));

                AppendSectionTitle(body, "Приёмы пищи");
                if (payload.Meals.Count == 0)
                {
                    AppendParagraph(body, "За выбранный период нет данных.");
                }
                else
                {
                    body.AppendChild(BuildMealsTable(payload));
                }

                mainPart.Document.Save();
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to build analysis DOCX for report {ReportId}", report.Id);
            stream.Dispose();
            throw;
        }

        stream.Position = 0;
        var fileName = MakeSafeFileName(string.IsNullOrWhiteSpace(report.Name) ? "analysis" : report.Name!) + ".docx";
        return Task.FromResult((stream, fileName));
    }

    private static List<(string Key, string Value)> BuildClientRows(ReportPayload payload)
    {
        var rows = new List<(string Key, string Value)>();
        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                rows.Add((key, value));
        }

        Add("ФИО", payload.Client.Name);
        if (payload.Client.Age.HasValue) Add("Возраст", payload.Client.Age.Value.ToString(CultureInfo.InvariantCulture));
        if (payload.Client.HeightCm.HasValue) Add("Рост", $"{payload.Client.HeightCm.Value} см");
        if (payload.Client.WeightKg.HasValue) Add("Вес", $"{payload.Client.WeightKg.Value:0.#} кг");
        Add("Пол", payload.Client.Gender);
        if (payload.Client.DailyCalories.HasValue) Add("Калорийность плана", $"{payload.Client.DailyCalories.Value} ккал/день");
        Add("Цели", payload.Client.Goals);
        Add("Ограничения", payload.Client.Restrictions);

        return rows;
    }

    private static Table BuildKeyValueTable(IEnumerable<(string Key, string Value)> rows)
    {
        var table = CreateTable();
        foreach (var (key, value) in rows)
        {
            var row = new TableRow();
            row.AppendChild(new TableCell(CreateParagraph(key, bold: true)));
            row.AppendChild(new TableCell(CreateParagraph(value)));
            table.AppendChild(row);
        }
        return table;
    }

    private static Table BuildTotalsTable(Totals totals)
    {
        var table = CreateTable();
        table.AppendChild(CreateHeaderRow("Ккал", "Белки", "Жиры", "Углеводы", "Приёмов пищи"));
        table.AppendChild(CreateRow(
            FormatDecimal(totals.Calories),
            FormatDecimal(totals.Proteins),
            FormatDecimal(totals.Fats),
            FormatDecimal(totals.Carbs),
            totals.MealsCount.ToString(CultureInfo.InvariantCulture)));
        return table;
    }

    private static Table BuildMealsTable(ReportPayload payload)
    {
        var table = CreateTable();
        table.AppendChild(CreateHeaderRow("Дата", "Время", "Блюдо", "Ккал", "Белки", "Жиры", "Углеводы"));
        foreach (var meal in payload.Meals)
        {
            table.AppendChild(CreateRow(
                meal.LocalDate,
                meal.LocalTime,
                meal.Dish,
                FormatDecimal(meal.Calories),
                FormatDecimal(meal.Proteins),
                FormatDecimal(meal.Fats),
                FormatDecimal(meal.Carbs)));
        }
        return table;
    }

    private static Table CreateTable()
    {
        var table = new Table();
        var props = new TableProperties(
            new TableLayout { Type = TableLayoutValues.Autofit },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }
            ));
        table.AppendChild(props);
        return table;
    }

    private static TableRow CreateHeaderRow(params string[] cells)
    {
        var row = new TableRow();
        foreach (var cell in cells)
        {
            row.AppendChild(new TableCell(CreateParagraph(cell, bold: true)));
        }
        return row;
    }

    private static TableRow CreateRow(params string[] cells)
    {
        var row = new TableRow();
        foreach (var cell in cells)
        {
            row.AppendChild(new TableCell(CreateParagraph(cell)));
        }
        return row;
    }

    private static Paragraph CreateParagraph(string text, bool bold = false)
    {
        var run = new Run(new Text(text ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve });
        if (bold)
        {
            run.RunProperties = new RunProperties(new Bold());
        }

        return new Paragraph(run)
        {
            ParagraphProperties = new ParagraphProperties
            {
                SpacingBetweenLines = new SpacingBetweenLines { After = "120" }
            }
        };
    }

    private static void AppendTitle(Body body, string text)
    {
        var paragraph = new Paragraph(new Run(new Text(text)));
        paragraph.ParagraphProperties = new ParagraphProperties
        {
            Justification = new Justification { Val = JustificationValues.Center },
            SpacingBetweenLines = new SpacingBetweenLines { After = "200" }
        };
        paragraph.ParagraphProperties.AppendChild(new ParagraphMarkRunProperties(new RunProperties
        {
            Bold = new Bold(),
            FontSize = new FontSize { Val = "32" }
        }));
        body.AppendChild(paragraph);
    }

    private static void AppendSectionTitle(Body body, string text)
    {
        var run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve })
        {
            RunProperties = new RunProperties(new Bold())
        };
        var paragraph = new Paragraph(run)
        {
            ParagraphProperties = new ParagraphProperties
            {
                SpacingBetweenLines = new SpacingBetweenLines { Before = "200", After = "80" }
            }
        };
        body.AppendChild(paragraph);
    }

    private static void AppendParagraph(Body body, string text)
        => body.AppendChild(CreateParagraph(text));

    private static string FormatDecimal(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static ReportPayload? ExtractPayload(string requestJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(requestJson);
            if (!doc.RootElement.TryGetProperty("input", out var input)) return null;
            foreach (var item in input.EnumerateArray())
            {
                if (item.TryGetProperty("role", out var roleEl) && roleEl.GetString() == "user" &&
                    item.TryGetProperty("content", out var contentEl))
                {
                    foreach (var block in contentEl.EnumerateArray())
                    {
                        if (block.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "input_text" &&
                            block.TryGetProperty("text", out var textEl))
                        {
                            var raw = textEl.GetString();
                            if (!string.IsNullOrWhiteSpace(raw) && raw.TrimStart().StartsWith("{"))
                            {
                                return JsonSerializer.Deserialize<ReportPayload>(raw);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // ignore, handled by caller
        }

        return null;
    }

    private static string MakeSafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}
