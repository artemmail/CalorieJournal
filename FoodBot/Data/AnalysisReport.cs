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

    public bool IsProcessing1 { get; set; }


}
