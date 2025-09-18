using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FoodBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FoodBot.Services;

public sealed class DocxReportService
{
    private readonly BotDbContext _db;
    private readonly ILogger<DocxReportService> _log;

    public DocxReportService(BotDbContext db, ILogger<DocxReportService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<(MemoryStream Stream, string FileName)> BuildAsync(
        long chatId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        var meals = await _db.Meals
            .AsNoTracking()
            .Where(m => m.ChatId == chatId &&
                        m.CreatedAtUtc >= from &&
                        m.CreatedAtUtc <= to)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(ct);

        var card = await _db.PersonalCards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == chatId, ct);

        var stream = new MemoryStream();

        try
        {
            using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
            {
                var mainPart = document.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());
                var body = mainPart.Document.Body ?? throw new InvalidOperationException("Document body is missing");

                AppendTitle(body, "Отчёт о питании");
                AppendParagraph(body, $"Период: {from:yyyy-MM-dd} — {to:yyyy-MM-dd}", bold: true);

                if (card != null)
                {
                    AppendParagraph(body, "Карточка пользователя", bold: true);
                    AppendParagraph(body, $"ФИО: {card.Name}");
                    AppendParagraph(body, $"Год рождения: {card.BirthYear}");
                    if (!string.IsNullOrWhiteSpace(card.DietGoals))
                        AppendParagraph(body, $"Цели: {card.DietGoals}");
                    if (!string.IsNullOrWhiteSpace(card.MedicalRestrictions))
                        AppendParagraph(body, $"Ограничения: {card.MedicalRestrictions}");
                }

                if (meals.Count == 0)
                {
                    AppendParagraph(body, "За выбранный период данных не найдено.");
                }
                else
                {
                    foreach (var byDay in meals.GroupBy(m => m.CreatedAtUtc.Date).OrderBy(g => g.Key))
                    {
                        AppendEmptyParagraph(body);
                        AppendParagraph(body, byDay.Key.ToString("yyyy-MM-dd"), bold: true);

                        var table = BuildTable();
                        table.AppendChild(CreateHeaderRow(
                            "Время",
                            "Блюдо",
                            "Ингредиенты",
                            "Вес, г",
                            "Ккал",
                            "Белки",
                            "Жиры",
                            "Углеводы"));

                        foreach (var meal in byDay)
                        {
                            table.AppendChild(CreateRow(
                                meal.CreatedAtUtc.ToLocalTime().ToString("HH:mm"),
                                meal.DishName ?? string.Empty,
                                BuildIngredients(meal.IngredientsJson),
                                FormatDecimal(meal.WeightG),
                                FormatDecimal(meal.CaloriesKcal),
                                FormatDecimal(meal.ProteinsG),
                                FormatDecimal(meal.FatsG),
                                FormatDecimal(meal.CarbsG)));
                        }

                        body.AppendChild(table);
                    }
                }

                mainPart.Document.Save();
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to build DOCX report for chat {ChatId}", chatId);
            stream.Dispose();
            throw;
        }

        stream.Position = 0;
        var fileName = $"nutrition_report_{from:yyyyMMdd}_{to:yyyyMMdd}.docx";
        return (stream, fileName);
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
            FontSize = new FontSize { Val = "36" }
        }));
        body.AppendChild(paragraph);
    }

    private static void AppendParagraph(Body body, string text, bool bold = false)
    {
        var run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        if (bold)
        {
            run.RunProperties = new RunProperties(new Bold());
        }

        var paragraph = new Paragraph(run)
        {
            ParagraphProperties = new ParagraphProperties
            {
                SpacingBetweenLines = new SpacingBetweenLines { After = "120" }
            }
        };

        body.AppendChild(paragraph);
    }

    private static void AppendEmptyParagraph(Body body)
    {
        body.AppendChild(new Paragraph(new Run(new Text(string.Empty))));
    }

    private static Table BuildTable()
    {
        var table = new Table();
        var props = new TableProperties(
            new TableLayout { Type = TableLayoutValues.Autofit },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 6 },
                new BottomBorder { Val = BorderValues.Single, Size = 6 },
                new LeftBorder { Val = BorderValues.Single, Size = 6 },
                new RightBorder { Val = BorderValues.Single, Size = 6 },
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
            var run = new Run(new Text(cell) { Space = SpaceProcessingModeValues.Preserve })
            {
                RunProperties = new RunProperties(new Bold())
            };
            var paragraph = new Paragraph(run)
            {
                ParagraphProperties = new ParagraphProperties
                {
                    SpacingBetweenLines = new SpacingBetweenLines { After = "0" }
                }
            };
            row.AppendChild(new TableCell(paragraph));
        }
        return row;
    }

    private static TableRow CreateRow(params string[] cells)
    {
        var row = new TableRow();
        foreach (var cell in cells)
        {
            var paragraph = new Paragraph(new Run(new Text(cell ?? string.Empty)
            {
                Space = SpaceProcessingModeValues.Preserve
            }))
            {
                ParagraphProperties = new ParagraphProperties
                {
                    SpacingBetweenLines = new SpacingBetweenLines { After = "0" }
                }
            };
            row.AppendChild(new TableCell(paragraph));
        }
        return row;
    }

    private static string FormatDecimal(decimal? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.##", CultureInfo.InvariantCulture)
            : "—";
    }

    private static string BuildIngredients(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return string.Empty;

        try
        {
            var arr = JsonSerializer.Deserialize<string[]>(json);
            if (arr != null && arr.Length > 0)
            {
                return string.Join(", ", arr.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
        }
        catch
        {
            // оставить как есть
        }

        return json;
    }
}

