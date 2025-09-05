using System.ComponentModel.DataAnnotations;

namespace FoodBot.Data;

public enum PeriodPdfJobStatus
{
    Queued,
    InProgress,
    Done,
    Error
}

public class PeriodPdfJob
{
    [Key]
    public long Id { get; set; }
    public long ChatId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public PeriodPdfJobStatus Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    [MaxLength(512)]
    public string? FilePath { get; set; }
}

