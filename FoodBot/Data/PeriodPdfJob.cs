using System.ComponentModel.DataAnnotations;

namespace FoodBot.Data;

public enum PeriodPdfJobStatus
{
    Queued,
    InProgress,
    Done,
    Error
}

public enum PeriodReportFormat
{
    Pdf = 0,
    Docx = 1
}

public class PeriodPdfJob
{
    [Key]
    public long Id { get; set; }
    public long ChatId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public PeriodReportFormat Format { get; set; } = PeriodReportFormat.Pdf;
    public PeriodPdfJobStatus Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    [MaxLength(512)]
    public string? FilePath { get; set; }
}

