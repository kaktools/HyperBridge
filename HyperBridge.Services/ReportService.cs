using System.Text;
using HyperBridge.Core.Contracts;
using HyperBridge.Core.Models;

namespace HyperBridge.Services;

public sealed class ReportService : IReportService
{
    public Task<string> GenerateHtmlReportAsync(ReportData data, string outputDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(outputDirectory);

        var filePath = Path.Combine(outputDirectory, $"HyperBridge_Report_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang='de'><head><meta charset='utf-8'/><title>HyperBridge Report</title>");
        html.AppendLine("<style>body{font-family:Segoe UI,Tahoma,sans-serif;background:#0b0f17;color:#e7edf6;padding:20px;}h1,h2{color:#67dbff;}pre{background:#0f1624;padding:12px;border-radius:8px;}table{border-collapse:collapse;}td,th{border:1px solid #30415f;padding:8px;}</style>");
        html.AppendLine("</head><body>");
        html.AppendLine("<h1>HyperBridge Migrationsbericht</h1>");
        html.AppendLine("<table>");
        html.AppendLine($"<tr><th>Quelle VM</th><td>{Escape(data.SourceVm)}</td></tr>");
        html.AppendLine($"<tr><th>Quelle Datenträger</th><td>{Escape(data.SourceDisk)}</td></tr>");
        html.AppendLine($"<tr><th>Ziel VM</th><td>{Escape(data.TargetVm)}</td></tr>");
        html.AppendLine($"<tr><th>Ziel VHDX</th><td>{Escape(data.TargetVhdx)}</td></tr>");
        html.AppendLine($"<tr><th>Start (UTC)</th><td>{data.StartedAtUtc:O}</td></tr>");
        html.AppendLine($"<tr><th>Ende (UTC)</th><td>{data.FinishedAtUtc:O}</td></tr>");
        html.AppendLine($"<tr><th>Ergebnis</th><td>{Escape(data.ResultSummary)}</td></tr>");
        html.AppendLine("</table>");

        html.AppendLine("<h2>Konfiguration</h2>");
        html.AppendLine($"<pre>{Escape(data.ConfigurationJson)}</pre>");

        html.AppendLine("<h2>Warnungen</h2>");
        html.AppendLine("<ul>");
        foreach (var warning in data.Warnings)
        {
            html.AppendLine($"<li>{Escape(warning)}</li>");
        }

        html.AppendLine("</ul>");

        html.AppendLine("<h2>Fehler</h2>");
        html.AppendLine("<ul>");
        foreach (var error in data.Errors)
        {
            html.AppendLine($"<li>{Escape(error)}</li>");
        }

        html.AppendLine("</ul>");

        html.AppendLine("<h2>Logauszug</h2>");
        html.AppendLine("<pre>");
        foreach (var entry in data.LogExcerpt)
        {
            html.AppendLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] {Escape(entry.Message)}");
        }

        html.AppendLine("</pre>");
        html.AppendLine("</body></html>");

        File.WriteAllText(filePath, html.ToString(), Encoding.UTF8);
        return Task.FromResult(filePath);
    }

    public Task<string> GenerateTextReportAsync(ReportData data, string outputDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(outputDirectory);

        var filePath = Path.Combine(outputDirectory, $"HyperBridge_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        var sb = new StringBuilder();
        sb.AppendLine("HyperBridge Migrationsbericht");
        sb.AppendLine("==========================");
        sb.AppendLine($"Quelle VM: {data.SourceVm}");
        sb.AppendLine($"Quelle Datenträger: {data.SourceDisk}");
        sb.AppendLine($"Ziel VM: {data.TargetVm}");
        sb.AppendLine($"Ziel VHDX: {data.TargetVhdx}");
        sb.AppendLine($"Start (UTC): {data.StartedAtUtc:O}");
        sb.AppendLine($"Ende (UTC): {data.FinishedAtUtc:O}");
        sb.AppendLine($"Ergebnis: {data.ResultSummary}");
        sb.AppendLine();
        sb.AppendLine("Konfiguration:");
        sb.AppendLine(data.ConfigurationJson);
        sb.AppendLine();
        sb.AppendLine("Warnungen:");
        foreach (var warning in data.Warnings)
        {
            sb.AppendLine($"- {warning}");
        }

        sb.AppendLine();
        sb.AppendLine("Fehler:");
        foreach (var error in data.Errors)
        {
            sb.AppendLine($"- {error}");
        }

        sb.AppendLine();
        sb.AppendLine("Logauszug:");
        foreach (var entry in data.LogExcerpt)
        {
            sb.AppendLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] {entry.Message}");
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        return Task.FromResult(filePath);
    }

    private static string Escape(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
    }
}