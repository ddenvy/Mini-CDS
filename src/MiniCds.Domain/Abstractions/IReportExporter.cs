// c:\Develop\Mini-CDS\src\MiniCds.Domain\Abstractions\IReportExporter.cs
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;

namespace MiniCds.Domain.Abstractions;

public enum ReportFormat { Csv, Pdf }

public interface IReportExporter
{
    string Format { get; }
    
    Task ExportAsync(
        IReadOnlyList<Sample> samples,
        string filePath,
        CancellationToken ct = default);
}