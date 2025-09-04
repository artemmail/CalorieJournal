using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace FoodBot.Services;

public sealed class AnalysisPdfService
{
    private readonly string _pandocPath;
    private readonly string _pandocWorkingDir;
    private readonly string _pandocFont;
    private readonly string _pandocMargin;

    public AnalysisPdfService(IConfiguration cfg)
    {
        _pandocPath = cfg["PandocPath"] ?? "pandoc";
        _pandocWorkingDir = cfg["PandocWorkingDirectory"] ?? Directory.GetCurrentDirectory();
        _pandocFont = cfg["PandocMainFont"] ?? "DejaVu Sans";
        _pandocMargin = cfg["PandocMargin"] ?? "0.75cm";
    }

    public async Task<(MemoryStream Stream, string FileName)> BuildAsync(string baseName, string markdown, CancellationToken ct = default)
    {
        var mdPath = Path.Combine(Path.GetTempPath(), $"analysis_{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(mdPath, markdown, Encoding.UTF8, ct);

        var pdfPath = Path.Combine(Path.GetTempPath(), $"analysis_{Guid.NewGuid():N}.pdf");

        var psi = new ProcessStartInfo
        {
            FileName = _pandocPath,
            Arguments = $"\"{mdPath}\" -o \"{pdfPath}\" --pdf-engine=xelatex -V mainfont=\"{_pandocFont}\" -V geometry:margin={_pandocMargin}",
            WorkingDirectory = _pandocWorkingDir,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var proc = Process.Start(psi)!;
        var stdErrTask = proc.StandardError.ReadToEndAsync();
        var stdOutTask = proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0)
        {
            var err = await stdErrTask;
            throw new InvalidOperationException($"Pandoc exited with code {proc.ExitCode}: {err}");
        }

        var bytes = await File.ReadAllBytesAsync(pdfPath, ct);
        try { File.Delete(mdPath); } catch { }
        try { File.Delete(pdfPath); } catch { }

        var safeName = MakeSafeFileName(string.IsNullOrWhiteSpace(baseName) ? "report" : baseName) + ".pdf";
        return (new MemoryStream(bytes), safeName);
    }

    private static string MakeSafeFileName(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            sb.Append(invalid.Contains(ch) ? '_' : ch);
        return sb.ToString();
    }
}

