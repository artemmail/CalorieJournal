using FoodBot.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoodBot.Data;

using System;





public sealed class AnalysisReport1
{
    public long Id { get; set; }

    public long ChatId { get; set; }

    public AnalysisPeriod Period { get; set; }

    /// <summary>Локальная дата начала периода (для кэширования, сравнения и имени)</summary>
    public DateOnly PeriodStartLocalDate { get; set; }

    /// <summary>Человекочитаемое имя: "2025-08-29 · день"</summary>
    public string? Name { get; set; }

    /// <summary>Сохранённый markdown отчёта</summary>
    public string? Markdown { get; set; }

    /// <summary>Сумма калорий на момент генерации (контрольная сумма)</summary>
    public int CaloriesChecksum { get; set; }

    /// <summary>Флаг, что отчёт ещё в обработке</summary>
    public bool IsProcessing { get; set; }

    /// <summary>Когда начата обработка (для таймаутов)</summary>
    public DateTimeOffset? ProcessingStartedAtUtc { get; set; }

    /// <summary>Когда запись создана/обновлена</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
}

