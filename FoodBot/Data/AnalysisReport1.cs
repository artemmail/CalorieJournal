using FoodBot.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoodBot.Data;

using System;





public sealed class AnalysisReport1
{
    public long Id { get; set; }

    public long AppUserId { get; set; }

    public AnalysisPeriod Period { get; set; }

    /// <summary>Raw OpenAI request payload in JSON.</summary>
    public string? RequestJson { get; set; }

    /// <summary>Ëîêàëüíàÿ äàòà íà÷àëà ïåðèîäà (äëÿ êýøèðîâàíèÿ, ñðàâíåíèÿ è èìåíè)</summary>
    public DateOnly PeriodStartLocalDate { get; set; }

    /// <summary>×åëîâåêî÷èòàåìîå èìÿ: "2025-08-29 · äåíü"</summary>
    public string? Name { get; set; }

    /// <summary>Ñîõðàí¸ííûé markdown îò÷¸òà</summary>
    public string? Markdown { get; set; }

    /// <summary>Ñóììà êàëîðèé íà ìîìåíò ãåíåðàöèè (êîíòðîëüíàÿ ñóììà)</summary>
    public int CaloriesChecksum { get; set; }

    /// <summary>Ôëàã, ÷òî îò÷¸ò åù¸ â îáðàáîòêå</summary>
    public bool IsProcessing { get; set; }

    /// <summary>Êîãäà íà÷àòà îáðàáîòêà (äëÿ òàéìàóòîâ)</summary>
    public DateTimeOffset? ProcessingStartedAtUtc { get; set; }

    /// <summary>Êîãäà çàïèñü ñîçäàíà/îáíîâëåíà</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
}

