using HyperBridge.Core.Models;

namespace HyperBridge.Core.Contracts;

public interface IReportService
{
    Task<string> GenerateHtmlReportAsync(ReportData data, string outputDirectory, CancellationToken cancellationToken);
    Task<string> GenerateTextReportAsync(ReportData data, string outputDirectory, CancellationToken cancellationToken);
}