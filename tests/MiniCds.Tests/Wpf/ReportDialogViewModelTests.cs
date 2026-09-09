// c:\Develop\Mini-CDS\tests\MiniCds.Tests\Wpf\ReportDialogViewModelTests.cs
using FluentAssertions;
using MiniCds.Domain.Abstractions;
using MiniCds.Domain.Entities;
using MiniCds.Domain.Enums;
using MiniCds.Domain.ValueObjects;
using MiniCds.Wpf.Infrastructure;
using MiniCds.Wpf.ViewModels;
using NSubstitute;

namespace MiniCds.Tests.Wpf;

public class ReportDialogViewModelTests
{
    private readonly ISampleRepository _sampleRepository;
    private readonly ReportDialogViewModel _viewModel;
    private bool _dialogClosed;
    private bool _dialogResult;

    public ReportDialogViewModelTests()
    {
        _sampleRepository = Substitute.For<ISampleRepository>();
        _dialogClosed = false;
        _dialogResult = false;
        _viewModel = new ReportDialogViewModel(_sampleRepository, result =>
        {
            _dialogClosed = true;
            _dialogResult = result;
        });
    }

    [Fact]
    public void InitialState_ReportTitleIsDefault()
    {
        _viewModel.ReportTitle.Should().Be("Chromatography Report");
    }

    [Fact]
    public void InitialState_AvailableSamplesIsEmpty()
    {
        _viewModel.AvailableSamples.Should().BeEmpty();
    }

    [Fact]
    public void InitialState_IsLoadingIsFalse()
    {
        _viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_LoadsSamplesFromRepository()
    {
        var samples = new List<Sample>
        {
            CreateSample("Sample_001", SampleStatus.Completed),
            CreateSample("Sample_002", SampleStatus.Running)
        };
        _sampleRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(samples);

        await _viewModel.InitializeAsync();

        _viewModel.AvailableSamples.Should().HaveCount(2);
        _viewModel.AvailableSamples[0].Sample.Name.Should().Be("Sample_001");
        _viewModel.AvailableSamples[1].Sample.Name.Should().Be("Sample_002");
    }

    [Fact]
    public async Task GetSelectedSampleIds_ReturnsOnlySelectedSamples()
    {
        var samples = new List<Sample>
        {
            CreateSample("Sample_001", SampleStatus.Completed),
            CreateSample("Sample_002", SampleStatus.Completed),
            CreateSample("Sample_003", SampleStatus.Completed)
        };
        _sampleRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(samples);

        await _viewModel.InitializeAsync();

        _viewModel.AvailableSamples[0].IsSelected = true;
        _viewModel.AvailableSamples[2].IsSelected = true;

        var selectedIds = _viewModel.GetSelectedSampleIds();
        selectedIds.Should().HaveCount(2);
        selectedIds.Should().Contain(samples[0].Id);
        selectedIds.Should().Contain(samples[2].Id);
    }

    [Fact]
    public void ReportTitle_SetValue_RaisesPropertyChanged()
    {
        string? changedProperty = null;
        _viewModel.PropertyChanged += (_, e) => changedProperty = e.PropertyName;

        _viewModel.ReportTitle = "New Title";

        changedProperty.Should().Be(nameof(_viewModel.ReportTitle));
        _viewModel.ReportTitle.Should().Be("New Title");
    }

    [Fact]
    public void SampleSelectionItem_IsSelected_RaisesPropertyChanged()
    {
        var sample = CreateSample("Sample_001", SampleStatus.Completed);
        var item = new SampleSelectionItem(sample);

        string? changedProperty = null;
        item.PropertyChanged += (_, e) => changedProperty = e.PropertyName;

        item.IsSelected = true;

        changedProperty.Should().Be(nameof(item.IsSelected));
        item.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void SampleSelectionItem_DisplayName_IncludesNameAndStatus()
    {
        var sample = CreateSample("Sample_001", SampleStatus.Completed);
        var item = new SampleSelectionItem(sample);

        item.DisplayName.Should().Be("Sample_001 (Completed)");
    }

    [Fact]
    public void OkCommand_WithSelectedSamples_ClosesDialogWithTrue()
    {
        var sample = CreateSample("Sample_001", SampleStatus.Completed);
        _viewModel.AvailableSamples.Add(new SampleSelectionItem(sample));
        _viewModel.AvailableSamples[0].IsSelected = true;

        ((RelayCommand)_viewModel.OkCommand).Execute(null);

        _dialogClosed.Should().BeTrue();
        _dialogResult.Should().BeTrue();
    }

    [Fact]
    public void CancelCommand_ClosesDialogWithFalse()
    {
        ((RelayCommand)_viewModel.CancelCommand).Execute(null);

        _dialogClosed.Should().BeTrue();
        _dialogResult.Should().BeFalse();
    }

    private static Sample CreateSample(string name, SampleStatus status)
    {
        return new Sample
        {
            Id = name.GetHashCode(),
            Name = name,
            MethodId = 1,
            Status = status,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = 1
        };
    }
}