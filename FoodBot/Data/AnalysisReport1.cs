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

    /// <summary>��������� ���� ������ ������� (��� �����������, ��������� � �����)</summary>
    public DateOnly PeriodStartLocalDate { get; set; }

    /// <summary>���������������� ���: "2025-08-29 � ����"</summary>
    public string? Name { get; set; }

    /// <summary>���������� markdown ������</summary>
    public string? Markdown { get; set; }

    /// <summary>����� ������� �� ������ ��������� (����������� �����)</summary>
    public int CaloriesChecksum { get; set; }

    /// <summary>����, ��� ����� ��� � ���������</summary>
    public bool IsProcessing { get; set; }

    /// <summary>����� ������ ��������� (��� ���������)</summary>
    public DateTimeOffset? ProcessingStartedAtUtc { get; set; }

    /// <summary>����� ������ �������/���������</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
}

