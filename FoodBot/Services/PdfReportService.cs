using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using FoodBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FoodBot.Services;

/// <summary>
/// Service that builds a nutrition report as PDF using LaTeX.
/// </summary>
public sealed class PdfReportService
{
    private readonly BotDbContext _db;
    private readonly string _latexPath;

    public PdfReportService(BotDbContext db, IConfiguration cfg)
    {
        _db = db;
        _latexPath = cfg["PdfReport:LaTeXPath"] ?? "pdflatex";
    }

    /// <summary>
    /// Build PDF report for user within period.
    /// </summary>
    public async Task<(MemoryStream Stream, string FileName)> BuildAsync(
        long chatId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        var card = await _db.PersonalCards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == chatId, ct);

        var meals = await _db.Meals
            .AsNoTracking()
            .Where(m => m.ChatId == chatId &&
                        m.CreatedAtUtc >= from &&
                        m.CreatedAtUtc <= to)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(ct);

        var tempDir = Path.Combine(Path.GetTempPath(), "foodbot_pdf_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var sb = new StringBuilder();
        sb.AppendLine("\\documentclass{article}");
        sb.AppendLine("\\usepackage[utf8]{inputenc}");
        sb.AppendLine("\\usepackage{graphicx}");
        sb.AppendLine("\\begin{document}");
        sb.AppendLine("\\section*{Отчёт о питании}");
        sb.AppendLine($"ФИО: {Escape(card?.Name)}\\\\");
        sb.AppendLine($"Год рождения: {card?.BirthYear}\\\\");
        sb.AppendLine($"Цели: {Escape(card?.DietGoals)}\\\\");
        sb.AppendLine($"Ограничения: {Escape(card?.MedicalRestrictions)}\\\\");
        sb.AppendLine("\\begin{tabular}{|c|c|c|c|c|c|c|c|}");
        sb.AppendLine("\\hline");
        sb.AppendLine("Фото & Блюдо & Калории & Белки & Жиры & Углеводы & Состав & Состав (ккал/БЖУ)\\\\");
        sb.AppendLine("\\hline");

        for (int i = 0; i < meals.Count; i++)
        {
            var m = meals[i];
            string? imgFile = null;
            if (m.ImageBytes?.Length > 0)
            {
                imgFile = Path.Combine(tempDir, $"img{i}.jpg");
                await File.WriteAllBytesAsync(imgFile, m.ImageBytes, ct);
            }

            var ingredients = m.IngredientsJson;
            if (!string.IsNullOrWhiteSpace(ingredients))
            {
                try
                {
                    var arr = JsonSerializer.Deserialize<string[]>(ingredients);
                    if (arr != null) ingredients = string.Join(", ", arr);
                }
                catch { }
            }

            var composition = $"Ккал: {m.CaloriesKcal ?? 0}, Б: {m.ProteinsG ?? 0}, Ж: {m.FatsG ?? 0}, У: {m.CarbsG ?? 0}";
            sb.Append(imgFile != null ? $"\\includegraphics[width=2cm]{{{Path.GetFileName(imgFile)}}}" : "");
            sb.Append(" & ");
            sb.Append($"{Escape(m.DishName)} & {m.CaloriesKcal ?? 0} & {m.ProteinsG ?? 0} & {m.FatsG ?? 0} & {m.CarbsG ?? 0} & {Escape(ingredients)} & {Escape(composition)}\\\\");
            sb.AppendLine("\\hline");
        }

        sb.AppendLine("\\end{tabular}");
        sb.AppendLine("\\end{document}");

        var texPath = Path.Combine(tempDir, "report.tex");
        await File.WriteAllTextAsync(texPath, sb.ToString(), ct);

        var psi = new ProcessStartInfo(_latexPath, "-interaction=nonstopmode report.tex")
        {
            WorkingDirectory = tempDir,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using (var proc = Process.Start(psi))
        {
            if (proc != null)
            {
                await proc.WaitForExitAsync(ct);
            }
        }

        var pdfPath = Path.Combine(tempDir, "report.pdf");
        var bytes = await File.ReadAllBytesAsync(pdfPath, ct);
        Directory.Delete(tempDir, true);
        var fileName = $"report_{from:yyyyMMdd}_{to:yyyyMMdd}.pdf";
        return (new MemoryStream(bytes), fileName);
    }

    private static string Escape(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s
            .Replace("\\", "\\textbackslash{}")
            .Replace("&", "\\&")
            .Replace("%", "\\%")
            .Replace("$", "\\$")
            .Replace("#", "\\#")
            .Replace("_", "\\_")
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace("~", "\\textasciitilde{}")
            .Replace("^", "\\textasciicircum{}");
    }
}

