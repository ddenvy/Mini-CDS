// c:\Develop\Mini-CDS\src\MiniCds.Domain\Abstractions\IReportService.cs
using MiniCds.Domain.Entities;

namespace MiniCds.Domain.Abstractions;

/// <summary>
/// Service for generating chromatography reports from Sample/Peak data.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Generates a report for the specified samples.
    /// </summary>
    /// <param name="sampleIds">IDs of samples to include in the report.</param>
    /// <param name="title">Report title.</param>
    /// <param name="format">Export format (e.g., "CSV").</param>
    /// <param name="actorUserId">User generating the report.</param>
    /// <param name="outputDirectory">Directory to save the report file into.
    /// When null, defaults to the "Reports" folder next to the executable.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Generated report entity.</returns>
    Task<Report> GenerateReportAsync(
        IReadOnlyList<long> sampleIds,
        string title,
        string format,
        long actorUserId,
        string? outputDirectory = null,
        CancellationToken ct = default);
}