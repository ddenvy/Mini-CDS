// c:\Develop\Mini-CDS\src\MiniCds.Wpf\Views\LiveChartView.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MiniCds.Domain.ValueObjects;
using MiniCds.Wpf.Infrastructure;
using MiniCds.Wpf.ViewModels;

namespace MiniCds.Wpf.Views;

public partial class LiveChartView : UserControl
{
    private LiveChartViewModel? _viewModel;

    private const double MarginLeft = 55;
    private const double MarginRight = 20;
    private const double MarginTop = 20;
    private const double MarginBottom = 40;

    public LiveChartView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            await ((AsyncRelayCommand)_viewModel.LoadSamplesCommand).ExecuteAsync(null);
        }
        DrawAxes();
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

    private void ChromatogramCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawAxes();
        if (_viewModel?.SignalData.Count > 0)
        {
            DrawChromatogram();
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

    private void DrawAxes()
    {
        double w = ChromatogramCanvas.ActualWidth;
        double h = ChromatogramCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Remove old axes and labels (keep chromatogram polyline and peak lines)
        for (int i = ChromatogramCanvas.Children.Count - 1; i >= 0; i--)
        {
            var child = ChromatogramCanvas.Children[i];
            if (child is Line line && line.Tag?.ToString() == "axis")
                ChromatogramCanvas.Children.RemoveAt(i);
            else if (child is TextBlock tb && tb.Tag?.ToString() == "axis-label")
                ChromatogramCanvas.Children.RemoveAt(i);
        }

        double xAxisY = h - MarginBottom;
        double yAxisX = MarginLeft;

        // Y axis
        ChromatogramCanvas.Children.Add(new Line
        {
            X1 = yAxisX, Y1 = MarginTop,
            X2 = yAxisX, Y2 = xAxisY,
            Stroke = Brushes.Black, StrokeThickness = 2,
            Tag = "axis"
        });

        // X axis
        ChromatogramCanvas.Children.Add(new Line
        {
            X1 = yAxisX, Y1 = xAxisY,
            X2 = w - MarginRight, Y2 = xAxisY,
            Stroke = Brushes.Black, StrokeThickness = 2,
            Tag = "axis"
        });

        // "Time (s)" label
        var timeLabel = new TextBlock
        {
            Text = "Time (s)",
            FontSize = 12,
            Tag = "axis-label"
        };
        Canvas.SetLeft(timeLabel, (w - MarginLeft - MarginRight) / 2 + MarginLeft - 25);
        Canvas.SetTop(timeLabel, xAxisY + 5);
        ChromatogramCanvas.Children.Add(timeLabel);

        // "Intensity" label (rotated -90°)
        var intensityLabel = new TextBlock
        {
            Text = "Intensity",
            FontSize = 12,
            Tag = "axis-label",
            RenderTransform = new RotateTransform(-90)
        };
        Canvas.SetLeft(intensityLabel, 12);
        Canvas.SetTop(intensityLabel, h / 2);
        ChromatogramCanvas.Children.Add(intensityLabel);
    }

    private void DrawChromatogram()
    {
        double w = ChromatogramCanvas.ActualWidth;
        double h = ChromatogramCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Clear everything and redraw axes + data
        ChromatogramCanvas.Children.Clear();
        DrawAxes();

        if (_viewModel?.SignalData.Count == 0) return;

        var data = _viewModel!.SignalData;
        double maxTime = data[^1].TimestampSeconds;
        double maxValue = data.Max(f => f.Value);

        if (maxTime <= 0 || maxValue <= 0) return;

        double plotWidth = w - MarginLeft - MarginRight;
        double plotHeight = h - MarginTop - MarginBottom;
        double xAxisY = h - MarginBottom;

        var polyline = new Polyline
        {
            Stroke = Brushes.Blue,
            StrokeThickness = 1.5
        };

        foreach (var frame in data)
        {
            double x = MarginLeft + (frame.TimestampSeconds / maxTime) * plotWidth;
            double y = xAxisY - (frame.Value / maxValue) * plotHeight;
            polyline.Points.Add(new System.Windows.Point(x, y));
        }

        ChromatogramCanvas.Children.Add(polyline);
    }

    private void DrawPeaks()
    {
        double w = ChromatogramCanvas.ActualWidth;
        double h = ChromatogramCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;
        if (_viewModel?.SignalData.Count == 0 || _viewModel.DetectedPeaks.Count == 0) return;

        var data = _viewModel.SignalData;
        double maxTime = data[^1].TimestampSeconds;
        double maxValue = data.Max(f => f.Value);

        double plotWidth = w - MarginLeft - MarginRight;
        double plotHeight = h - MarginTop - MarginBottom;
        double xAxisY = h - MarginBottom;

        // Remove old peak marker lines (Tag = "peak")
        for (int i = ChromatogramCanvas.Children.Count - 1; i >= 0; i--)
        {
            var child = ChromatogramCanvas.Children[i];
            if (child is Line line && line.Tag?.ToString() == "peak")
                ChromatogramCanvas.Children.RemoveAt(i);
        }

        foreach (var peak in _viewModel.DetectedPeaks)
        {
            if (peak.ApexIndex >= data.Count) continue;

            var apexFrame = data[peak.ApexIndex];
            double x = MarginLeft + (apexFrame.TimestampSeconds / maxTime) * plotWidth;

            ChromatogramCanvas.Children.Add(new Line
            {
                X1 = x, Y1 = MarginTop,
                X2 = x, Y2 = xAxisY,
                Stroke = Brushes.Red,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 },
                Tag = "peak"
            });
        }
    }
}
