// c:\Develop\Mini-CDS\src\MiniCds.Application\Reporting\ReportService.cs
using Microsoft.EntityFrameworkCore;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;

namespace MiniCds.Application.Reporting;

public sealed class ReportService : IReportService
{
    private readonly ISampleRepository _sampleRepository;
    private readonly IReportRepository _reportRepository;
    private readonly IReadOnlyDictionary<string, IReportExporter> _exporters;
    private readonly IAuditTrail _auditTrail;

    public ReportService(
        ISampleRepository sampleRepository,
        IReportRepository reportRepository,
        IEnumerable<IReportExporter> exporters,
        IAuditTrail auditTrail)
    {
        _sampleRepository = sampleRepository;
        _reportRepository = reportRepository;
        _auditTrail = auditTrail;
        _exporters = exporters.ToDictionary(e => e.Format, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Report> GenerateReportAsync(
        IReadOnlyList<long> sampleIds,
        string title,
        string format,
        long actorUserId,
        string? outputDirectory = null,
        CancellationToken ct = default)
    {
        if (!_exporters.TryGetValue(format, out var exporter))
            throw new NotSupportedException($"Report format '{format}' is not supported.");

        var samples = new List<Sample>();
        foreach (var sampleId in sampleIds)
        {
            var sample = await _sampleRepository.FindByIdWithPeaksAsync(sampleId, ct)
                ?? throw new InvalidOperationException($"Sample {sampleId} not found.");
            samples.Add(sample);
        }

        var report = new Report
        {
            Title = title,
            GeneratedAtUtc = DateTime.UtcNow,
            GeneratedByUserId = actorUserId,
            FilePath = string.Empty,
            Format = format
        };

        var reportId = await _reportRepository.AddAsync(report, ct);
        report.Id = reportId;

        var extension = format.Equals("PDF", StringComparison.OrdinalIgnoreCase) ? "pdf" : "csv";
        var directory = string.IsNullOrWhiteSpace(outputDirectory) ? "Reports" : outputDirectory;
        var outputPath = Path.Combine(directory, $"{reportId}.{extension}");
        Directory.CreateDirectory(directory);
        await exporter.ExportAsync(samples, outputPath, ct);

        report.FilePath = outputPath;
        await _reportRepository.UpdateAsync(report, ct);

        await _auditTrail.AppendAsync(
            AuditAction.ReportGenerated,
            nameof(Report),
            report.Id,
            $"Report '{title}' generated with {samples.Count} samples",
            oldValues: null,
            newValues: new { Title = title, Format = format, SampleCount = samples.Count },
            actorUserId: actorUserId,
            signatureId: null);

        return report;
    }
}