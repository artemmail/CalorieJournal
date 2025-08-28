using FoodBot.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoodBot.Data;

public class AnalysisReport
{
    [Key] public int Id { get; set; }

    public long ChatId { get; set; }

    public DateTime ReportDate { get; set; }

    [Column(TypeName = "nvarchar(max)")] public string? Markdown { get; set; }

    public bool IsProcessing { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }



    // Ќќ¬ќ≈
    public AnalysisPeriod Period { get; set; } = AnalysisPeriod.Day;

    // Ќќ¬ќ≈: начало периода в локальной ћ— -дате (дл€ Day Ч это дата дн€)
    public DateOnly PeriodStartLocalDate { get; set; }





}
