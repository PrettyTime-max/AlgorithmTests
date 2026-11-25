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
using System.Windows;
using System.Windows.Input;

namespace Laba_1
{
    public class Chart2DService
    {
        private readonly CartesianChart _chart;
        private readonly Axis _xAxis;
        private readonly Axis _yAxis;
        private readonly ObservableCollection<ObservablePoint> _chartValues = new();
        private readonly ObservableCollection<ObservablePoint> _theoreticalValues = new();

        private double _dataMinN = 1;
        private double _dataMaxN = 100;
        private Point? _dragStartPoint = null;
        private double _dragStartMin = 0;
        private double _dragStartMax = 0;

        public CartesianChart Chart => _chart;
        public Axis XAxis => _xAxis;
        public Axis YAxis => _yAxis;
        public ObservableCollection<ObservablePoint> ChartValues => _chartValues;
        public ObservableCollection<ObservablePoint> TheoreticalValues => _theoreticalValues;

        public event Action<string>? ZoomStatusChanged;

        public Chart2DService()
        {
            _xAxis = new Axis { Name = "Размер N" };
            _yAxis = new Axis { Name = "Время (мс)", MinLimit = 0 };

            _chart = new CartesianChart
            {
                IsHitTestVisible = true,
                ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.X,
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
                XAxes = new Axis[] { _xAxis },
                YAxes = new Axis[] { _yAxis }
            };

            SetupInteraction();
        }

        private void SetupInteraction()
        {
            // Мягкий зум колесиком мыши (чувствительность снижена в 2.5-3 раза)
            _chart.PreviewMouseWheel += (s, e) =>
            {
                e.Handled = true;
                double curMin = _xAxis.MinLimit ?? _dataMinN;
                double curMax = _xAxis.MaxLimit ?? _dataMaxN;
                double totalRange = Math.Max(1.0, _dataMaxN - _dataMinN);
                double curWidth = curMax - curMin;

                if (curWidth <= 0) return;

                // Сниженная чувствительность: ~8% за шаг вместо резких 30-40%
                double factor = e.Delta > 0 ? 0.92 : 1.087;
                double newWidth = curWidth * factor;

                if (newWidth < 5) newWidth = 5;
                if (newWidth >= totalRange)
                {
                    ResetZoom();
                    return;
                }

                Point mousePos = e.GetPosition(_chart);
                double chartWidth = _chart.ActualWidth > 0 ? _chart.ActualWidth : 700;
                double ratio = Math.Max(0.0, Math.Min(1.0, mousePos.X / chartWidth));

                double center = curMin + curWidth * ratio;
                double newMin = center - newWidth * ratio;
                double newMax = center + newWidth * (1.0 - ratio);

                if (newMin < _dataMinN)
                {
                    newMin = _dataMinN;
                    newMax = newMin + newWidth;
                }
                if (newMax > _dataMaxN)
                {
                    newMax = _dataMaxN;
                    newMin = Math.Max(_dataMinN, newMax - newWidth);
                }

                _xAxis.MinLimit = newMin;
                _xAxis.MaxLimit = newMax;
                NotifyZoomStatus();
            };

            // Перетаскивание (панорамирование) графика мышью / тачпадом
            _chart.PreviewMouseDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed)
                {
                    _dragStartPoint = e.GetPosition(_chart);
                    _dragStartMin = _xAxis.MinLimit ?? _dataMinN;
                    _dragStartMax = _xAxis.MaxLimit ?? _dataMaxN;
                    _chart.CaptureMouse();
                }
            };

            _chart.PreviewMouseMove += (s, e) =>
            {
                if (_dragStartPoint.HasValue && (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed))
                {
                    Point currentPoint = e.GetPosition(_chart);
                    double deltaX = currentPoint.X - _dragStartPoint.Value.X;
                    double chartWidth = _chart.ActualWidth > 0 ? _chart.ActualWidth : 700;

                    double viewWidth = _dragStartMax - _dragStartMin;
                    double totalRange = Math.Max(1.0, _dataMaxN - _dataMinN);
                    if (viewWidth >= totalRange)
                    {
                        viewWidth = totalRange * 0.8;
                        _dragStartMax = _dragStartMin + viewWidth;
                    }

                    double shiftN = -(deltaX / chartWidth) * viewWidth;
                    double newMin = _dragStartMin + shiftN;
                    double newMax = _dragStartMax + shiftN;

                    if (newMin < _dataMinN)
                    {
                        newMin = _dataMinN;
                        newMax = newMin + viewWidth;
                    }
                    if (newMax > _dataMaxN)
                    {
                        newMax = _dataMaxN;
                        newMin = Math.Max(_dataMinN, newMax - viewWidth);
                    }

                    _xAxis.MinLimit = newMin;
                    _xAxis.MaxLimit = newMax;
                    NotifyZoomStatus();
                }
            };

            _chart.PreviewMouseUp += (s, e) =>
            {
                if (_dragStartPoint.HasValue)
                {
                    _dragStartPoint = null;
                    _chart.ReleaseMouseCapture();
                }
            };
        }

        public void ScrollByDelta(short delta)
        {
            double curMin = _xAxis.MinLimit ?? _dataMinN;
            double curMax = _xAxis.MaxLimit ?? _dataMaxN;
            double totalRange = Math.Max(1.0, _dataMaxN - _dataMinN);
            double viewWidth = curMax - curMin;

            if (viewWidth >= totalRange)
            {
                viewWidth = totalRange * 0.75;
                curMin = _dataMinN;
                curMax = curMin + viewWidth;
            }

            double step = (viewWidth * 0.05) * (delta / 120.0);
            double newMin = curMin + step;
            double newMax = curMax + step;

            if (newMin < _dataMinN)
            {
                newMin = _dataMinN;
                newMax = newMin + viewWidth;
            }
            if (newMax > _dataMaxN)
            {
                newMax = _dataMaxN;
                newMin = Math.Max(_dataMinN, newMax - viewWidth);
            }

            _xAxis.MinLimit = newMin;
            _xAxis.MaxLimit = newMax;
            NotifyZoomStatus();
        }

        public void ResetZoom()
        {
            _xAxis.MinLimit = null;
            _xAxis.MaxLimit = null;
            NotifyZoomStatus();
        }

        public void SetDataRange(double minN, double maxN)
        {
            _dataMinN = minN;
            _dataMaxN = maxN;
            ResetZoom();
        }

        private void NotifyZoomStatus()
        {
            double curMin = _xAxis.MinLimit ?? _dataMinN;
            double curMax = _xAxis.MaxLimit ?? _dataMaxN;
            double totalRange = Math.Max(1.0, _dataMaxN - _dataMinN);
            double viewWidth = curMax - curMin;

            if (_xAxis.MinLimit == null && _xAxis.MaxLimit == null)
            {
                ZoomStatusChanged?.Invoke($"🔍 Полный диапазон: N от {_dataMinN:F0} до {_dataMaxN:F0} (100%) | Колесико мыши: зум, ЛКМ: сдвиг");
            }
            else
            {
                double pct = Math.Min(100.0, (viewWidth / totalRange) * 100.0);
                ZoomStatusChanged?.Invoke($"🔍 Просмотр: N от {curMin:F0} до {curMax:F0} ({pct:F0}% диапазона) | Сдвиг мышью");
            }
        }

        public void Clear()
        {
            _chartValues.Clear();
            _theoreticalValues.Clear();
        }

        public void SetYAxisTitle(string title)
        {
            _yAxis.Name = title;
        }

        public void SetXAxisTitle(string title)
        {
            _xAxis.Name = title;
        }

        public void CalculateTheoreticalCurve(string algorithmName, IEnumerable<ObservablePoint> actualPoints)
        {
            _theoreticalValues.Clear();
            var pointsList = actualPoints?.Where(p => p.X.HasValue && p.Y.HasValue).ToList();
            if (pointsList == null || pointsList.Count == 0) return;

            var (complexityFunc, label) = AlgorithmExecutor.GetComplexityInfo(algorithmName);

            // Метод наименьших квадратов (МНК): k = sum(y_i * f(n_i)) / sum(f(n_i)^2)
            double sumYF = 0;
            double sumF2 = 0;
            foreach (var point in pointsList)
            {
                double n = point.X!.Value;
                double y = point.Y!.Value;
                double f = complexityFunc(n);
                sumYF += y * f;
                sumF2 += f * f;
            }

            double k = sumF2 > 0 ? (sumYF / sumF2) : 0;
            if (k <= 0)
            {
                var maxPoint = pointsList.OrderBy(p => p.X!.Value).LastOrDefault();
                if (maxPoint != null)
                {
                    double fMax = complexityFunc(maxPoint.X!.Value);
                    k = fMax > 0 ? (maxPoint.Y!.Value / fMax) : 0.001;
                }
            }

            foreach (var point in pointsList.OrderBy(p => p.X!.Value))
            {
                double n = point.X!.Value;
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