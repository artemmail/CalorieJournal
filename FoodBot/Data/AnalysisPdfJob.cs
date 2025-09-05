using System.ComponentModel.DataAnnotations;

namespace FoodBot.Data;

public enum AnalysisPdfJobStatus
{
    Queued,
    InProgress,
    Done,
    Error
}

public class AnalysisPdfJob
{
    [Key]
    public long Id { get; set; }
    public long ChatId { get; set; }
    public long ReportId { get; set; }
    public AnalysisPdfJobStatus Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    [MaxLength(512)]
    public string? FilePath { get; set; }
}
