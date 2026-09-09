// c:\Develop\Mini-CDS\tests\MiniCds.Tests\Reporting\CsvReportExporterTests.cs
using System.IO;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;
using MiniCds.Domain.ValueObjects;
using MiniCds.Infrastructure.Reporting;

namespace MiniCds.Tests.Reporting;

public class CsvReportExporterTests : IDisposable
{
    private readonly CsvReportExporter _exporter;
    private readonly string _testFilePath;

    public CsvReportExporterTests()
    {
        _exporter = new CsvReportExporter();
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test_report_{Guid.NewGuid()}.csv");
    }

    [Fact]
    public async Task ExportAsync_WithSingleSampleAndPeaks_CreatesValidCsv()
    {
        var sample = CreateSampleWithPeaks("Sample_001", "Method_A", 2);

        await _exporter.ExportAsync(new List<Sample> { sample }, _testFilePath);

        var lines = await File.ReadAllLinesAsync(_testFilePath);
        lines.Should().HaveCount(3); // header + 2 peaks
        lines[0].Should().Contain("Sample Name,Method Name,Status,Created At,Peak #");
        lines[1].Should().Contain("Sample_001");
        lines[1].Should().Contain("Method_A");
    }

    [Fact]
    public async Task ExportAsync_WithSampleWithoutPeaks_CreatesSingleLine()
    {
        var sample = CreateSampleWithoutPeaks("Sample_002");

        await _exporter.ExportAsync(new List<Sample> { sample }, _testFilePath);

        var lines = await File.ReadAllLinesAsync(_testFilePath);
        lines.Should().HaveCount(2); // header + 1 sample
        lines[1].Should().Contain("Sample_002");
    }

    [Fact]
    public async Task ExportAsync_WithMultipleSamples_CreatesCorrectLineCount()
    {
        var samples = new List<Sample>
        {
            CreateSampleWithPeaks("Sample_001", "Method_A", 2),
            CreateSampleWithPeaks("Sample_002", "Method_B", 3),
            CreateSampleWithoutPeaks("Sample_003")
        };

        await _exporter.ExportAsync(samples, _testFilePath);

        var lines = await File.ReadAllLinesAsync(_testFilePath);
        lines.Should().HaveCount(7); // header + 2 + 3 + 1
    }

    [Fact]
    public async Task ExportAsync_WithCommaInSampleName_EscapesCorrectly()
    {
        var sample = CreateSampleWithPeaks("Sample, with comma", "Method_A", 1);

        await _exporter.ExportAsync(new List<Sample> { sample }, _testFilePath);

        var content = await File.ReadAllTextAsync(_testFilePath);
        content.Should().Contain("\"Sample, with comma\"");
    }

    [Fact]
    public void Format_ReturnsCsv()
    {
        _exporter.Format.Should().Be("CSV");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    private static Sample CreateSampleWithPeaks(string name, string methodName, int peakCount)
    {
        var peaks = Enumerable.Range(1, peakCount).Select(i => new Peak
        {
            Id = i,
            SampleId = 1,
            ApexIndex = i * 100,
            StartIndex = i * 100 - 50,
            EndIndex = i * 100 + 50,
            Metrics = new PeakMetrics
            {
                RetentionTime = i * 1.5,
                Height = 1000 + i * 100,
                Area = 5000 + i * 500,
                Fwhm = 0.5 + i * 0.1,
                Plates = 10000 + i * 1000,
                Tailing = 1.0 + i * 0.05
            },
            DetectedAtUtc = DateTime.UtcNow,
            IsManual = false
        }).ToList();

        return new Sample
        {
            Id = 1,
            Name = name,
            MethodId = 1,
            Status = SampleStatus.Completed,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = 1,
            Method = new Method
            {
                Id = 1,
                Name = methodName,
                Version = 1,
                Parameters = new ProcessingParameters(),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = 1
            },
            Peaks = peaks
        };
    }

    private static Sample CreateSampleWithoutPeaks(string name)
    {
        return new Sample
        {
            Id = 1,
            Name = name,
            MethodId = 1,
            Status = SampleStatus.Completed,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = 1,
            Method = new Method
            {
                Id = 1,
                Name = "Method_A",
                Version = 1,
                Parameters = new ProcessingParameters(),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = 1
            },
            Peaks = new List<Peak>()
        };
    }
}