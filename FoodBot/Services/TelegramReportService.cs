using System.Text.Json;
using ClosedXML.Excel;
using FoodBot.Data;
using Microsoft.EntityFrameworkCore;

namespace FoodBot.Services;

public class TelegramReportService
{
    private readonly BotDbContext _db;
    public TelegramReportService(BotDbContext db) => _db = db;

    /// <summary>
    /// Формирует Excel-отчёт по всем приёмам пищи пользователя (по chatId).
    /// </summary>
    public async Task<(MemoryStream stream, string filename)> BuildUserReportAsync(long chatId, CancellationToken ct)
    {
        var entries = await _db.Meals
            .Where(x => x.ChatId == chatId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var ms = new MemoryStream();

        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("Meals");
            var r = 1;

            // Заголовки
            ws.Cell(r, 1).Value = "Дата (UTC)";
            ws.Cell(r, 2).Value = "Пользователь";
            ws.Cell(r, 3).Value = "Блюдо";
            ws.Cell(r, 4).Value = "Ингредиенты";
            ws.Cell(r, 5).Value = "Белки, г";
            ws.Cell(r, 6).Value = "Жиры, г";
            ws.Cell(r, 7).Value = "Углеводы, г";
            ws.Cell(r, 8).Value = "Калории, ккал";
            ws.Cell(r, 9).Value = "Вес, г";
            ws.Cell(r, 10).Value = "Модель";
            ws.Cell(r, 11).Value = "Доверие";

            // Немного стиля для заголовка
            var header = ws.Range(r, 1, r, 11);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.LightGray;

            r++;

            foreach (var e in entries)
            {
                // Человекочитаемые ингредиенты: парсим JSON-массив и склеиваем через ", "
                string ingredientsCsv = e.IngredientsJson ?? "";
                if (!string.IsNullOrWhiteSpace(e.IngredientsJson))
                {
                    try
                    {
                        var arr = JsonSerializer.Deserialize<string[]>(e.IngredientsJson);
                        if (arr != null) ingredientsCsv = string.Join(", ", arr);
                    }
                    catch
                    {
                        // если JSON невалидный — оставим как есть
                        ingredientsCsv = e.IngredientsJson!;
                    }
                }

                ws.Cell(r, 1).Value = e.CreatedAtUtc.UtcDateTime;
                ws.Cell(r, 1).Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss";

                ws.Cell(r, 2).Value = e.Username ?? e.UserId.ToString();
                ws.Cell(r, 3).Value = e.DishName ?? "";

                ws.Cell(r, 4).Value = ingredientsCsv;
               // ws.Cell(r, 4).DataType = XLDataType.Text; // чтоб Excel не «умничал»

                ws.Cell(r, 5).Value = e.ProteinsG;
                ws.Cell(r, 6).Value = e.FatsG;
                ws.Cell(r, 7).Value = e.CarbsG;
                ws.Cell(r, 8).Value = e.CaloriesKcal;
                ws.Cell(r, 9).Value = e.WeightG;     // новая колонка «Вес, г»
                ws.Cell(r, 10).Value = e.Model;
                ws.Cell(r, 11).Value = e.Confidence;

                // Опционально формат чисел
                ws.Range(r, 5, r, 9).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(r, 11).Style.NumberFormat.Format = "0.00%";

                r++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(ms);
        }

        ms.Position = 0;
        var fn = $"food_report_{chatId}_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";
        return (ms, fn);
    }
}
