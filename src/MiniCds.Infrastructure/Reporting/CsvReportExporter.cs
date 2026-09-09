// c:\Develop\Mini-CDS\src\MiniCds.Infrastructure\Reporting\CsvReportExporter.cs
using System.Globalization;
using System.Text;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;

namespace MiniCds.Infrastructure.Reporting;

/// <summary>
/// Exports sample and peak data to CSV format.
/// </summary>
public sealed class CsvReportExporter : IReportExporter
{
    public string Format => "CSV";

    public async Task ExportAsync(
        IReadOnlyList<Sample> samples,
        string filePath,
        CancellationToken ct = default)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("Sample Name,Method Name,Status,Created At,Peak #,Retention Time (s),Height,Area,FWHM,Plates,Tailing");

        foreach (var sample in samples)
        {
            if (sample.Peaks is null || sample.Peaks.Count == 0)
            {
                // Sample without peaks
                sb.AppendLine(string.Join(",",
                    EscapeCsv(sample.Name),
                    EscapeCsv(sample.Method?.Name ?? "N/A"),
                    sample.Status.ToString(),
                    sample.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)));
            }
            else
            {
                int peakNumber = 1;
                foreach (var peak in sample.Peaks)
                {
                    sb.AppendLine(string.Join(",",
                        EscapeCsv(sample.Name),
                        EscapeCsv(sample.Method?.Name ?? "N/A"),
                        sample.Status.ToString(),
                        sample.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                        peakNumber.ToString(CultureInfo.InvariantCulture),
                        peak.Metrics.RetentionTime.ToString("F3", CultureInfo.InvariantCulture),
                        peak.Metrics.Height.ToString("F2", CultureInfo.InvariantCulture),
                        peak.Metrics.Area.ToString("F2", CultureInfo.InvariantCulture),
                        peak.Metrics.Fwhm.ToString("F3", CultureInfo.InvariantCulture),
                        peak.Metrics.Plates?.ToString("F0", CultureInfo.InvariantCulture) ?? "",
                        peak.Metrics.Tailing?.ToString("F2", CultureInfo.InvariantCulture) ?? ""));
                    peakNumber++;
                }
            }
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8, ct);
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}