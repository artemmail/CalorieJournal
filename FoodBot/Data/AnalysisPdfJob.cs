using System;

namespace FoodBot.Data;

public enum AnalysisPdfJobStatus
{
    Queued = 0,
    Processing = 1,
    Done = 2,
    Failed = 3
}

public class AnalysisPdfJob
{
    public long Id { get; set; }
    public long ChatId { get; set; }
    public long ReportId { get; set; }
    public AnalysisPdfJobStatus Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public string? FilePath { get; set; }
}
