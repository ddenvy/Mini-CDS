using System.Globalization;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MiniCds.Infrastructure.Reporting;

/// <summary>
/// Exports sample and peak data to PDF format using QuestPDF.
/// QuestPDF Community license is set once in the static constructor.
/// </summary>
public sealed class PdfReportExporter : IReportExporter
{
    static PdfReportExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string Format => "PDF";

    public Task ExportAsync(
        IReadOnlyList<Sample> samples,
        string filePath,
        CancellationToken ct = default)
    {
        var document = CreateDocument(samples);
        document.GeneratePdf(filePath);
        return Task.CompletedTask;
    }

    private static IDocument CreateDocument(IReadOnlyList<Sample> samples)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(column =>
                {
                    column.Item().Text("Chromatography Report").FontSize(18).Bold();
                    column.Item().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC").FontSize(9).FontColor(Colors.Grey.Lighten1);
                    column.Item().PaddingBottom(10).LineHorizontal(1);
                });

                page.Content().Column(column =>
                {
                    foreach (var sample in samples)
                    {
                        column.Item().PaddingBottom(15).Column(sampleColumn =>
                        {
                            sampleColumn.Item().Text($"Sample: {sample.Name}").FontSize(13).Bold();
                            sampleColumn.Item().Text($"Method: {sample.Method?.Name ?? "N/A"}").FontSize(10);
                            sampleColumn.Item().Text($"Status: {sample.Status}").FontSize(10);
                            sampleColumn.Item().Text($"Created: {sample.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC").FontSize(10);
                            sampleColumn.Item().PaddingBottom(8).Text($"Peaks: {sample.Peaks?.Count ?? 0}").FontSize(10).Bold();

                            if (sample.Peaks is null || sample.Peaks.Count == 0)
                            {
                                sampleColumn.Item().Text("No peaks detected.").Italic().FontColor(Colors.Grey.Lighten1);
                            }
                            else
                            {
                                sampleColumn.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(40);
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Text("#").Bold();
                                        header.Cell().Text("RT (s)").Bold();
                                        header.Cell().Text("Height").Bold();
                                        header.Cell().Text("Area").Bold();
                                        header.Cell().Text("FWHM (s)").Bold();
                                        header.Cell().Text("Plates").Bold();
                                        header.Cell().Text("Tailing").Bold();
                                    });

                                    int peakNumber = 1;
                                    foreach (var peak in sample.Peaks)
                                    {
                                        table.Cell().Text(peakNumber.ToString(CultureInfo.InvariantCulture));
                                        table.Cell().Text(peak.Metrics.RetentionTime.ToString("F3", CultureInfo.InvariantCulture));
                                        table.Cell().Text(peak.Metrics.Height.ToString("F2", CultureInfo.InvariantCulture));
                                        table.Cell().Text(peak.Metrics.Area.ToString("F2", CultureInfo.InvariantCulture));
                                        table.Cell().Text(peak.Metrics.Fwhm.ToString("F3", CultureInfo.InvariantCulture));
                                        table.Cell().Text(peak.Metrics.Plates?.ToString("F0", CultureInfo.InvariantCulture) ?? "-");
                                        table.Cell().Text(peak.Metrics.Tailing?.ToString("F2", CultureInfo.InvariantCulture) ?? "-");
                                        peakNumber++;
                                    }
                                });
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });
    }
}
