// c:\Develop\Mini-CDS\tests\MiniCds.Tests\Reporting\ReportServiceTests.cs
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniCds.Application.Reporting;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;
using MiniCds.Domain.ValueObjects;
using MiniCds.Infrastructure.Persistence;
using MiniCds.Infrastructure.Persistence.Repositories;
using MiniCds.Infrastructure.Reporting;
using NSubstitute;

namespace MiniCds.Tests.Reporting;

public class ReportServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly CdsDbContext _dbContext;
    private readonly ISampleRepository _sampleRepository;
    private readonly IReportRepository _reportRepository;
    private readonly IReportExporter _reportExporter;
    private readonly IAuditTrail _auditTrail;
    private readonly ReportService _reportService;

    public ReportServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CdsDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new CdsDbContext(options);
        _dbContext.Database.EnsureCreated();

        _sampleRepository = new SampleRepository(_dbContext);
        _reportRepository = new ReportRepository(_dbContext);
        _reportExporter = Substitute.For<IReportExporter>();
        _reportExporter.Format.Returns("CSV");
        
        _auditTrail = Substitute.For<IAuditTrail>();
        _auditTrail.AppendAsync(
            Arg.Any<AuditAction>(),
            Arg.Any<string>(),
            Arg.Any<long>(),
            Arg.Any<string?>(),
            Arg.Any<object?>(),
            Arg.Any<object?>(),
            Arg.Any<long>(),
            Arg.Any<long?>()).Returns(1L);

        _reportService = new ReportService(
            _sampleRepository,
            _reportRepository,
            _reportExporter,
            _auditTrail);
    }

    [Fact]
    public async Task GenerateReportAsync_WithValidSamples_CreatesReport()
    {
        var sample = await SeedSampleWithPeaksAsync("Sample_001", 2);

        var report = await _reportService.GenerateReportAsync(
            new List<long> { sample.Id },
            "Test Report",
            "CSV",
            actorUserId: 1);

        report.Should().NotBeNull();
        report.Title.Should().Be("Test Report");
        report.Format.Should().Be("CSV");
        report.FilePath.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateReportAsync_CallsExporter()
    {
        var sample = await SeedSampleWithPeaksAsync("Sample_001", 2);

        await _reportService.GenerateReportAsync(
            new List<long> { sample.Id },
            "Test Report",
            "CSV",
            actorUserId: 1);

        await _reportExporter.Received(1).ExportAsync(
            Arg.Any<IReadOnlyList<Sample>>(),
            Arg.Any<string>(),
            Arg.Any<System.Threading.CancellationToken>());
    }

    [Fact]
    public async Task GenerateReportAsync_RecordsAuditEntry()
    {
        var sample = await SeedSampleWithPeaksAsync("Sample_001", 2);

        await _reportService.GenerateReportAsync(
            new List<long> { sample.Id },
            "Test Report",
            "CSV",
            actorUserId: 1);

        await _auditTrail.Received(1).AppendAsync(
            AuditAction.ReportGenerated,
            nameof(Report),
            Arg.Any<long>(),
            Arg.Is<string>(s => s.Contains("Test Report")),
            Arg.Any<object?>(),
            Arg.Any<object?>(),
            1,
            Arg.Any<long?>());
    }

    [Fact]
    public async Task GenerateReportAsync_PersistsReportToDatabase()
    {
        var sample = await SeedSampleWithPeaksAsync("Sample_001", 2);

        var report = await _reportService.GenerateReportAsync(
            new List<long> { sample.Id },
            "Test Report",
            "CSV",
            actorUserId: 1);

        var reports = await _reportRepository.GetAllAsync();
        reports.Should().ContainSingle(r => r.Id == report.Id);
    }

    [Fact]
    public async Task GenerateReportAsync_WithNonExistentSample_ThrowsException()
    {
        var act = () => _reportService.GenerateReportAsync(
            new List<long> { 999 },
            "Test Report",
            "CSV",
            actorUserId: 1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Sample 999 not found*");
    }

    private async Task<Sample> SeedSampleWithPeaksAsync(string name, int peakCount)
    {
        var user = new User
        {
            Username = "testuser",
            FullName = "Test User",
            Role = UserRole.Administrator,
            IsActive = true,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var method = new Method
        {
            Name = "Test Method",
            Version = 1,
            Parameters = new ProcessingParameters(),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = user.Id
        };
        _dbContext.Methods.Add(method);
        await _dbContext.SaveChangesAsync();

        var sample = new Sample
        {
            Name = name,
            MethodId = method.Id,
            Status = SampleStatus.Completed,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = user.Id
        };
        _dbContext.Samples.Add(sample);
        await _dbContext.SaveChangesAsync();

        for (int i = 0; i < peakCount; i++)
        {
            var peak = new Peak
            {
                SampleId = sample.Id,
                ApexIndex = (i + 1) * 100,
                StartIndex = (i + 1) * 100 - 50,
                EndIndex = (i + 1) * 100 + 50,
                Metrics = new PeakMetrics
                {
                    RetentionTime = (i + 1) * 1.5,
                    Height = 1000 + i * 100,
                    Area = 5000 + i * 500,
                    Fwhm = 0.5 + i * 0.1,
                    Plates = 10000 + i * 1000,
                    Tailing = 1.0 + i * 0.05
                },
                DetectedAtUtc = DateTime.UtcNow,
                IsManual = false
            };
            _dbContext.Peaks.Add(peak);
        }
        await _dbContext.SaveChangesAsync();

        return sample;
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }
}