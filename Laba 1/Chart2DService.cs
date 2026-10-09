using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Laba_1
{
    public class Chart2DService
    {
        private readonly CartesianChart _chart;
        private readonly ObservableCollection<ObservablePoint> _chartValues = new();
        private readonly ObservableCollection<ObservablePoint> _theoreticalValues = new();

        public CartesianChart Chart => _chart;
        public ObservableCollection<ObservablePoint> ChartValues => _chartValues;
        public ObservableCollection<ObservablePoint> TheoreticalValues => _theoreticalValues;

        public Chart2DService()
        {
            _chart = new CartesianChart
            {
                IsHitTestVisible = false,
                TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Hidden,
                Series = new ISeries[]
                {
                    // Практический замер
                    new LineSeries<ObservablePoint>
                    {
                        Values = _chartValues,
                        Name = "Фактический график",
                        Fill = null,
                        GeometrySize = 6,
                        EnableNullSplitting = false
                    },
                    // Теоретический график
                    new LineSeries<ObservablePoint>
                    {
                        Values = _theoreticalValues,
                        Name = "Идеальный график",
                        Fill = null,
                        GeometrySize = 0,
                        Stroke = new SolidColorPaint(SKColors.Crimson) { StrokeThickness = 2 },
                        EnableNullSplitting = false
                    }
                },
                XAxes = new Axis[] { new Axis { Name = "Размер N" } },
                YAxes = new Axis[] { new Axis { Name = "Время (мс)" } }
            };
        }

        public void Clear()
        {
            _chartValues.Clear();
            _theoreticalValues.Clear();
        }

        public void SetYAxisTitle(string title)
        {
            if (_chart.YAxes.FirstOrDefault() is Axis yAxis)
            {
                yAxis.Name = title;
            }
        }

        public void CalculateTheoreticalCurve(string algorithmName, IEnumerable<ObservablePoint> actualPoints)
        {
            _theoreticalValues.Clear();
            var pointsList = actualPoints?.Where(p => p.X.HasValue && p.Y.HasValue).ToList();
            if (pointsList == null || pointsList.Count == 0) return;

            var (complexityFunc, label) = AlgorithmExecutor.GetComplexityInfo(algorithmName);

            var maxPoint = pointsList.OrderBy(p => p.X.Value).LastOrDefault();
            if (maxPoint == null) return;

            double maxN = maxPoint.X.Value;
            double maxY = maxPoint.Y.Value;
            double theoreticalMax = complexityFunc(maxN);

            if (theoreticalMax <= 0) theoreticalMax = 1;

            double k = maxY / theoreticalMax;

            foreach (var point in pointsList.OrderBy(p => p.X.Value))
            {
                double n = point.X.Value;
                double idealTime = k * complexityFunc(n);
                _theoreticalValues.Add(new ObservablePoint(n, idealTime));
            }

            if (_chart.Series.ElementAtOrDefault(1) is LineSeries<ObservablePoint> idealSeries)
            {
                idealSeries.Name = $"Идеальный {label}";
            }
        }
    }
}