using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using FoodBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FoodBot.Services;

public sealed class PdfReportService
{
    private readonly BotDbContext _db;
    private readonly string _latexPath;

    private readonly ILogger<PdfReportService> _log;

    public PdfReportService(BotDbContext db, IConfiguration cfg, ILogger<PdfReportService> log)
    {
        _db = db;
        _latexPath = cfg["PdfReport:LaTeXPath"] ?? @"C:\texlive\2025\bin\windows\pdflatex";
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
            .OrderByDescending(m => m.CreatedAtUtc) // убывание по дате приёма
            .ToListAsync(ct);

        var card = await _db.PersonalCards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == chatId, ct);

        var tempDir = Path.Combine(Path.GetTempPath(), "foodbot_pdf_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var sb = new StringBuilder();
        sb.AppendLine(@"\documentclass{article}");
        sb.AppendLine(@"\usepackage[utf8]{inputenc}");
        sb.AppendLine(@"\usepackage[T2A]{fontenc}");
        sb.AppendLine(@"\usepackage[russian]{babel}");
        sb.AppendLine(@"\usepackage{graphicx}");
        sb.AppendLine(@"\usepackage[margin=0.75cm]{geometry}");
        sb.AppendLine(@"\usepackage{array}");
        sb.AppendLine(@"\usepackage{longtable}");
        sb.AppendLine(@"\setlength{\parskip}{0pt}");
        sb.AppendLine(@"\setlength{\parindent}{0pt}");
        sb.AppendLine(@"\setlength{\tabcolsep}{4pt}"); // важно для расчёта общей ширины
        sb.AppendLine(@"\renewcommand{\arraystretch}{1.1}");
        sb.AppendLine(@"\sloppy\emergencystretch=3em");
        sb.AppendLine(@"\setlength{\LTleft}{0pt} \setlength{\LTright}{0pt}");
        sb.AppendLine(@"\begin{document}");

        // Карточка пользователя
        if (card != null)
        {
            sb.AppendLine(@"\textbf{Карточка пользователя}");
            sb.AppendLine(@"{\small");
            sb.Append("ФИО: "); sb.Append(Escape(card.Name)); sb.AppendLine(@"\\");
            sb.Append("Год рождения: "); sb.Append(card.BirthYear.ToString()); sb.AppendLine(@"\\");
            sb.Append("Цели: "); sb.Append(Escape(card.DietGoals)); sb.AppendLine(@"\\");
            sb.Append("Ограничения: "); sb.Append(Escape(card.MedicalRestrictions)); sb.AppendLine();
            sb.AppendLine(@"}");
            sb.AppendLine();
        }

        // Заголовок отчёта
        sb.AppendLine(@"\section*{Отчёт о питании}");

        // Разбивка по датам
        bool tableOpen = false;
        DateTime? currentDate = null;

        for (int i = 0; i < meals.Count; i++)
        {
            var m = meals[i];
            var mealDate = m.CreatedAtUtc.Date;

            if (currentDate == null || mealDate != currentDate.Value)
            {
                if (tableOpen)
                {
                    sb.AppendLine(@"\end{longtable}");
                    sb.AppendLine();
                }

                var dateStr = mealDate.ToString("yyyy-MM-dd");
                sb.Append(@"\textbf{");
                sb.Append(Escape(dateStr));
                sb.AppendLine(@"}");
                sb.AppendLine();

                AppendTableBegin(sb);
                tableOpen = true;
                currentDate = mealDate;
            }

            // Сохранить изображение (если есть)
            string? imgFile = null;
            if (m.ImageBytes?.Length > 0)
            {
                var ext = "jpg";
                if (!string.IsNullOrWhiteSpace(m.FileMime))
                {
                    var mime = m.FileMime.ToLowerInvariant();
                    var idx = mime.IndexOf('/');
                    if (idx >= 0 && idx < mime.Length - 1)
                    {
                        ext = mime.Substring(idx + 1);
                        if (ext == "jpeg") ext = "jpg";
                    }
                }
                imgFile = Path.Combine(tempDir, $"img{i}.{ext}");
                await File.WriteAllBytesAsync(imgFile, m.ImageBytes, ct);
            }

            // Состав
            string ingredients = m.IngredientsJson ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(ingredients))
            {
                try
                {
                    var arr = JsonSerializer.Deserialize<string[]>(ingredients);
                    if (arr != null) ingredients = string.Join(", ", arr);
                }
                catch { /* как есть */ }
            }

            var timeStr = m.CreatedAtUtc.ToString("HH:mm");

            // ----- строка таблицы -----
            if (imgFile != null)
            {
                // по ширине ячейки, пропорции сохраняются, умеренная высота
                sb.Append(@"\includegraphics[width=\linewidth,height=3.0cm,keepaspectratio]{");
                sb.Append(Path.GetFileName(imgFile));
                sb.Append('}');
            }
            sb.Append(" & ");

            sb.Append(Escape(m.DishName)); sb.Append(" & ");
            sb.Append(Escape(ingredients)); sb.Append(" & ");
            sb.Append(m.WeightG?.ToString("0.##") ?? "0"); sb.Append(" & ");
            sb.Append(Escape(timeStr)); sb.Append(" & ");
            sb.Append((m.CaloriesKcal ?? 0).ToString()); sb.Append(" & ");
            sb.Append((m.ProteinsG ?? 0).ToString()); sb.Append(" & ");
            sb.Append((m.FatsG ?? 0).ToString()); sb.Append(" & ");
            sb.Append((m.CarbsG ?? 0).ToString());

            sb.AppendLine(@"\\");
            sb.AppendLine(@"\hline");
        }

        if (tableOpen)
        {
            sb.AppendLine(@"\end{longtable}");
        }

        sb.AppendLine(@"\end{document}");

        // Компиляция
        var texPath = Path.Combine(tempDir, "report.tex");
        await File.WriteAllTextAsync(texPath, sb.ToString(), ct);

        var psi = new ProcessStartInfo(_latexPath, "-interaction=nonstopmode -halt-on-error report.tex")
        {
            WorkingDirectory = tempDir,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using (var proc = Process.Start(psi))
        {
            if (proc != null)
            {
                var stdOutTask = proc.StandardOutput.ReadToEndAsync();
                var stdErrTask = proc.StandardError.ReadToEndAsync();
                await Task.WhenAll(proc.WaitForExitAsync(ct), stdOutTask, stdErrTask);
                if (proc.ExitCode != 0)
                {
                    var output = await stdOutTask;
                    var error = await stdErrTask;
                    var message = $"pdflatex exited with code {proc.ExitCode}.\nSTDOUT: {output}\nSTDERR: {error}";
                    _log.LogError(message);
                    throw new InvalidOperationException(message);
                }
            }
        }

        var pdfPath = Path.Combine(tempDir, "report.pdf");
        var bytes = await File.ReadAllBytesAsync(pdfPath, ct);
        Directory.Delete(tempDir, true);
        var fileName = $"report_{from:yyyyMMdd}_{to:yyyyMMdd}.pdf";
        return (new MemoryStream(bytes), fileName);
    }

    /// <summary>
    /// Открывает longtable и печатает заголовки.
    /// Порядок колонок: Фото | Блюдо | Состав | Вес | Время | Ккал | Б | Ж | У
    /// Ширины подобраны, чтобы вся строка гарантированно помещалась.
    /// </summary>
    private static void AppendTableBegin(StringBuilder sb)
    {
        // Итоговая сумма p{...} = 16.8 cm.
        // Учитывая 9 колонок по 2*\tabcolsep (=8pt ≈ 0.28cm) и 10 вертикальных линий по ~0.4pt,
        // общая ширина ≈ 16.8 + 9*0.28 + 0.14 ≈ 19.5 cm = \textwidth при margin=0.75cm.
        sb.AppendLine(@"\begin{longtable}{|p{2.7cm}|p{2.9cm}|p{5.6cm}|p{1.3cm}|p{1.3cm}|p{1.2cm}|p{0.6cm}|p{0.6cm}|p{0.6cm}|}");
        sb.AppendLine(@"\hline");
        sb.AppendLine(@"\textbf{Фото} & \textbf{Блюдо} & \textbf{Состав} & \textbf{Вес (г)} & \textbf{Время} & \textbf{Ккал} & \textbf{Б} & \textbf{Ж} & \textbf{У}\\");
        sb.AppendLine(@"\hline");
        sb.AppendLine(@"\endfirsthead");
        sb.AppendLine(@"\hline");
        sb.AppendLine(@"\textbf{Фото} & \textbf{Блюдо} & \textbf{Состав} & \textbf{Вес (г)} & \textbf{Время} & \textbf{Ккал} & \textbf{Б} & \textbf{Ж} & \textbf{У}\\");
        sb.AppendLine(@"\hline");
        sb.AppendLine(@"\endhead");
        sb.AppendLine(@"\hline");
        sb.AppendLine(@"\endfoot");
        sb.AppendLine(@"\hline");
        sb.AppendLine(@"\endlastfoot");
    }

    private static string Escape(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s
            .Replace(@"\", @"\textbackslash{}")
            .Replace("&", @"\&")
            .Replace("%", @"\%")
            .Replace("$", @"\$")
            .Replace("#", @"\#")
            .Replace("_", @"\_")
            .Replace("{", @"\{")
            .Replace("}", @"\}")
            .Replace("~", @"\textasciitilde{}")
            .Replace("^", @"\textasciicircum{}");
    }
}
