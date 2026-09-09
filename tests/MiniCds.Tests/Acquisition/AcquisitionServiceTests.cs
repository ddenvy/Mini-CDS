// c:\Develop\Mini-CDS\tests\MiniCds.Tests\Acquisition\AcquisitionServiceTests.cs
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniCds.Application.Acquisition;
using MiniCds.Application.SignalProcessing;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;
using MiniCds.Domain.ValueObjects;
using MiniCds.Infrastructure.Instruments;
using MiniCds.Infrastructure.Persistence;
using MiniCds.Infrastructure.Persistence.Repositories;
using NSubstitute;

namespace MiniCds.Tests.Acquisition;

public class AcquisitionServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly CdsDbContext _dbContext;
    private readonly ISampleRepository _sampleRepository;
    private readonly IMethodRepository _methodRepository;
    private readonly IRawSignalRepository _rawSignalRepository;
    private readonly IPeakRepository _peakRepository;
    private readonly ISignalProcessor _signalProcessor;
    private readonly IAuditTrail _auditTrail;
    private readonly AcquisitionService _service;

    public AcquisitionServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CdsDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new CdsDbContext(options);
        _dbContext.Database.EnsureCreated();

        _sampleRepository = new SampleRepository(_dbContext);
        _methodRepository = new MethodRepository(_dbContext);
        _rawSignalRepository = new RawSignalRepository(_dbContext);
        _peakRepository = new PeakRepository(_dbContext);
        _signalProcessor = new SignalProcessor();
        _auditTrail = Substitute.For<IAuditTrail>();

        _service = new AcquisitionService(
            _sampleRepository,
            _methodRepository,
            _rawSignalRepository,
            _peakRepository,
            _signalProcessor,
            _auditTrail);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public async Task StartAsync_TransitionsSampleToRunning()
    {
        var method = await SeedMethodAsync();
        var sample = await SeedSampleAsync(method.Id, SampleStatus.Queued);
        var instrument = CreateSimulator();

        await _service.StartAsync(sample.Id, instrument, actorUserId: 1);

        var updated = await _sampleRepository.FindByIdAsync(sample.Id);
        updated!.Status.Should().Be(SampleStatus.Running);

        await _service.StopAsync();
    }

    [Fact]
    public async Task StopAsync_PersistsRawSignal()
    {
        var method = await SeedMethodAsync();
        var sample = await SeedSampleAsync(method.Id, SampleStatus.Queued);
        var instrument = CreateSimulator(durationSeconds: 1.0);

        await _service.StartAsync(sample.Id, instrument, actorUserId: 1);
        await Task.Delay(1200);
        await _service.StopAsync();

        var rawSignals = await _dbContext.RawSignals
            .Where(r => r.SampleId == sample.Id)
            .ToListAsync();
        rawSignals.Should().HaveCount(1);
        rawSignals[0].Points.Should().NotBeEmpty();
    }

    [Fact]
    public async Task StopAsync_PersistsDetectedPeaks()
    {
        var method = await SeedMethodAsync();
        var sample = await SeedSampleAsync(method.Id, SampleStatus.Queued);
        var instrument = CreateSimulator(durationSeconds: 1.0);

        await _service.StartAsync(sample.Id, instrument, actorUserId: 1);
        await Task.Delay(1200);
        await _service.StopAsync();

        var peaks = await _dbContext.Peaks
            .Where(p => p.SampleId == sample.Id)
            .ToListAsync();
        peaks.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task StopAsync_TransitionsSampleToCompleted()
    {
        var method = await SeedMethodAsync();
        var sample = await SeedSampleAsync(method.Id, SampleStatus.Queued);
        var instrument = CreateSimulator(durationSeconds: 1.0);

        await _service.StartAsync(sample.Id, instrument, actorUserId: 1);
        await Task.Delay(1200);
        await _service.StopAsync();

        var updated = await _sampleRepository.FindByIdAsync(sample.Id);
        updated!.Status.Should().Be(SampleStatus.Completed);
    }

    [Fact]
    public async Task StartAsync_WhenSampleNotQueued_ThrowsInvalidOperationException()
    {
        var method = await SeedMethodAsync();
        var sample = await SeedSampleAsync(method.Id, SampleStatus.Running);
        var instrument = CreateSimulator();

        var act = () => _service.StartAsync(sample.Id, instrument, actorUserId: 1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not in Queued state*");
    }

    [Fact]
    public async Task StartAsync_CalledTwice_ThrowsInvalidOperationException()
    {
        var method = await SeedMethodAsync();
        var sample = await SeedSampleAsync(method.Id, SampleStatus.Queued);
        var instrument = CreateSimulator();

        await _service.StartAsync(sample.Id, instrument, actorUserId: 1);

        var act = () => _service.StartAsync(sample.Id, instrument, actorUserId: 1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Acquisition already in progress.");

        await _service.StopAsync();
    }

    [Fact]
    public async Task FrameProcessed_RaisesEvent_WithSignalFrames()
    {
        var method = await SeedMethodAsync();
        var sample = await SeedSampleAsync(method.Id, SampleStatus.Queued);
        var instrument = CreateSimulator(durationSeconds: 1.0);

        var frames = new List<SignalFrame>();
        _service.FrameProcessed += (_, frame) => frames.Add(frame);

        await _service.StartAsync(sample.Id, instrument, actorUserId: 1);
        await Task.Delay(1200);
        await _service.StopAsync();

        frames.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task PeaksDetected_RaisesEvent_WithDetectedPeaks()
    {
        var method = await SeedMethodAsync();
        var sample = await SeedSampleAsync(method.Id, SampleStatus.Queued);
        var instrument = CreateSimulator(durationSeconds: 1.0);

        IReadOnlyList<DetectedPeak>? detectedPeaks = null;
        _service.PeaksDetected += (_, peaks) => detectedPeaks = peaks;

        await _service.StartAsync(sample.Id, instrument, actorUserId: 1);
        await Task.Delay(1200);
        await _service.StopAsync();

        detectedPeaks.Should().NotBeNull();
        detectedPeaks!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AuditTrail_RecordsSampleStatusChanged()
    {
        var method = await SeedMethodAsync();
        var sample = await SeedSampleAsync(method.Id, SampleStatus.Queued);
        var instrument = CreateSimulator(durationSeconds: 1.0);

        await _service.StartAsync(sample.Id, instrument, actorUserId: 1);
        await Task.Delay(1200);
        await _service.StopAsync();

        await _auditTrail.Received(2).AppendAsync(
            AuditAction.SampleStatusChanged,
            nameof(Sample),
            sample.Id,
            Arg.Any<string>(),
            Arg.Any<object?>(),
            Arg.Any<object?>(),
            1);
    }

    private async Task<User> SeedUserAsync()
    {
        var user = new User
        {
            Id = 1,
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
        return user;
    }

    private async Task<Method> SeedMethodAsync()
    {
        await SeedUserAsync();
        
        var method = new Method
        {
            Name = "Test Method",
            Version = 1,
            Parameters = new ProcessingParameters
            {
                SampleRateHz = 10,
                MovingAverageWindow = 3,
                SavitzkyGolayWindow = 5,
                SavitzkyGolayOrder = 2,
                BaselineLambda = 1e5,
                BaselineP = 0.01,
                MinPeakHeight = 10,
                MinProminence = 5,
                MinWidthPoints = 3
            },
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = 1
        };
        _dbContext.Methods.Add(method);
        await _dbContext.SaveChangesAsync();
        return method;
    }

    private async Task<Sample> SeedSampleAsync(long methodId, SampleStatus status)
    {
        var sample = new Sample
        {
            Name = $"Sample_{Guid.NewGuid():N}",
            MethodId = methodId,
            Status = status,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = 1
        };
        _dbContext.Samples.Add(sample);
        await _dbContext.SaveChangesAsync();
        return sample;
    }

    private static SimulatorInstrumentSource CreateSimulator(double durationSeconds = 10.0)
    {
        var peaks = new List<SimulatorPeakDefinition>
        {
            new(Amplitude: 100, RetentionTime: 0.5, Sigma: 0.1)
        };
        return new SimulatorInstrumentSource(
            sampleRateHz: 10,
            durationSeconds: durationSeconds,
            peaks: peaks);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }
}