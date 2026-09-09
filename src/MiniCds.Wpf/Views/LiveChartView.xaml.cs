// c:\Develop\Mini-CDS\src\MiniCds.Wpf\Views\LiveChartView.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MiniCds.Domain.ValueObjects;
using MiniCds.Wpf.ViewModels;

namespace MiniCds.Wpf.Views;

public partial class LiveChartView : UserControl
{
    private LiveChartViewModel? _viewModel;
    private const double CanvasWidth = 900;
    private const double CanvasHeight = 500;
    private const double MarginLeft = 50;
    private const double MarginBottom = 50;

    public LiveChartView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.SignalData.CollectionChanged -= OnSignalDataChanged;
            _viewModel.DetectedPeaks.CollectionChanged -= OnPeaksChanged;
        }

        if (e.NewValue is LiveChartViewModel viewModel)
        {
            _viewModel = viewModel;
            _viewModel.SignalData.CollectionChanged += OnSignalDataChanged;
            _viewModel.DetectedPeaks.CollectionChanged += OnPeaksChanged;
        }
    }

    private void OnSignalDataChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel is null) return;
        DrawChromatogram();
    }

    private void OnPeaksChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel is null) return;
        DrawPeaks();
    }

    private void DrawChromatogram()
    {
        ChromatogramCanvas.Children.Clear();

        if (_viewModel?.SignalData.Count == 0) return;

        var data = _viewModel!.SignalData;
        double maxTime = data[^1].TimestampSeconds;
        double maxValue = data.Max(f => f.Value);

        if (maxTime <= 0 || maxValue <= 0) return;

        var polyline = new Polyline
        {
            Stroke = Brushes.Blue,
            StrokeThickness = 1.5
        };

        foreach (var frame in data)
        {
            double x = MarginLeft + (frame.TimestampSeconds / maxTime) * CanvasWidth;
            double y = CanvasHeight - (frame.Value / maxValue) * CanvasHeight + MarginBottom;
            polyline.Points.Add(new System.Windows.Point(x, y));
        }

        ChromatogramCanvas.Children.Add(polyline);
    }

    private void DrawPeaks()
    {
        if (_viewModel?.SignalData.Count == 0 || _viewModel.DetectedPeaks.Count == 0) return;

        var data = _viewModel.SignalData;
        double maxTime = data[^1].TimestampSeconds;
        double maxValue = data.Max(f => f.Value);

        foreach (var peak in _viewModel.DetectedPeaks)
        {
            if (peak.ApexIndex >= data.Count) continue;

            var apexFrame = data[peak.ApexIndex];
            double x = MarginLeft + (apexFrame.TimestampSeconds / maxTime) * CanvasWidth;

            var line = new Line
            {
                X1 = x,
                Y1 = MarginBottom,
                X2 = x,
                Y2 = CanvasHeight + MarginBottom,
                Stroke = Brushes.Red,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 }
            };

            ChromatogramCanvas.Children.Add(line);
        }
    }
}