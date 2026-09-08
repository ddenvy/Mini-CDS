using MiniCds.Domain.Enums;

namespace MiniCds.Domain.Abstractions;

public enum ReportFormat { Csv, Pdf }

/// <summary>
/// Exports a complete sample report to file.
/// Implementations: CsvReportExporter, PdfReportExporter (QuestPDF).
/// </summary>
public interface IReportExporter
{
    Task ExportAsync(SampleReport report, string outputPath, ReportFormat format, CancellationToken ct = default);
}

/// <summary>All data required to render/export a sample report.</summary>
public readonly record struct SampleReport(
    string SampleName,
    string MethodName,
    DateTime CapturedAtUtc,
    IReadOnlyList<DetectedPeak> Peaks,
    IReadOnlyList<AuditEntryQueryRow> AuditLog,
    IReadOnlyList<ElectronicSignatureInfo> Signatures);

public readonly record struct ElectronicSignatureInfo(
    string Username,
    SignatureMeaning Meaning,
    string Reason,
    DateTime SignedAtUtc);