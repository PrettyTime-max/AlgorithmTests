using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Laba_1
{
    public class Chart3DService
    {
        private readonly Canvas _canvas3D;
        private readonly StackPanel _legendPanel;
        private readonly TextBlock _txtMinZ;
        private readonly TextBlock _txtMaxZ;
        private readonly CheckBox _chkPractical;
        private readonly CheckBox _chkTheoretical;

        private double[,] _lastZData;
        private int _lastCount;
        private int _lastStartN;
        private int _lastStep;

        public Canvas Canvas => _canvas3D;
        public StackPanel LegendPanel => _legendPanel;

        public Chart3DService(CheckBox chkPractical, CheckBox chkTheoretical)
        {
            _chkPractical = chkPractical;
            _chkTheoretical = chkTheoretical;

            _canvas3D = new Canvas
            {
                Background = Brushes.White,
                Width = 750,
                Height = 500,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _legendPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(15),
                Visibility = Visibility.Hidden
            };

            _txtMaxZ = new TextBlock { FontWeight = FontWeights.Bold, Foreground = Brushes.DarkRed };
            _txtMinZ = new TextBlock { FontWeight = FontWeights.Bold, Foreground = Brushes.DarkBlue };

            _legendPanel.Children.Add(new TextBlock { Text = "Макс. время (мс):" });
            _legendPanel.Children.Add(_txtMaxZ);
            _legendPanel.Children.Add(new TextBlock { Text = "Мин. время (мс):", Margin = new Thickness(0, 5, 0, 0) });
            _legendPanel.Children.Add(_txtMinZ);
        }

        public void RedrawLast()
        {
            if (_lastZData != null)
            {
                DrawSurface(_lastZData, _lastCount, _lastStartN, _lastStep);
            }
        }

        public void Clear()
        {
            _canvas3D.Children.Clear();
            _legendPanel.Visibility = Visibility.Hidden;
        }

        public void DrawSurface(double[,] zData, int count, int startN, int step)
        {
            _lastZData = zData;
            _lastCount = count;
            _lastStartN = startN;
            _lastStep = step;

            _canvas3D.Children.Clear();

            if (_chkPractical != null && _chkTheoretical != null)
            {
                if (_chkPractical.Parent is Panel oldParent1) oldParent1.Children.Remove(_chkPractical);
                if (_chkTheoretical.Parent is Panel oldParent2) oldParent2.Children.Remove(_chkTheoretical);

                StackPanel innerPanel = new StackPanel { Orientation = Orientation.Horizontal };
                innerPanel.Children.Add(_chkPractical);
                innerPanel.Children.Add(_chkTheoretical);

                Border layersPanel = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(3),
                    Child = innerPanel
                };

                Canvas.SetLeft(layersPanel, 10);
                Canvas.SetTop(layersPanel, 10);
                _canvas3D.Children.Add(layersPanel);
            }

            double minZ = double.MaxValue;
            double maxZ = double.MinValue;

            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    if (zData[i, j] < minZ) minZ = zData[i, j];
                    if (zData[i, j] > maxZ) maxZ = zData[i, j];
                }
            }

            if (maxZ == minZ) maxZ = minZ + 1;

            _txtMinZ.Text = $"{minZ:F2}";
            _txtMaxZ.Text = $"{maxZ:F2}";
            _legendPanel.Visibility = Visibility.Visible;

            DrawAxes(count, startN, step, minZ, maxZ);

            if (_chkPractical?.IsChecked == true)
            {
                for (int i = count - 2; i >= 0; i--)
                {
                    for (int j = 0; j < count - 1; j++)
                    {
                        Point p1 = GetIsometricProjection(i, j, zData[i, j], count, minZ, maxZ);
                        Point p2 = GetIsometricProjection(i + 1, j, zData[i + 1, j], count, minZ, maxZ);
                        Point p3 = GetIsometricProjection(i + 1, j + 1, zData[i + 1, j + 1], count, minZ, maxZ);
                        Point p4 = GetIsometricProjection(i, j + 1, zData[i, j + 1], count, minZ, maxZ);

                        double avgZ = (zData[i, j] + zData[i + 1, j] + zData[i + 1, j + 1] + zData[i, j + 1]) / 4.0;
                        double normalizedZ = (avgZ - minZ) / (maxZ - minZ);

                        Polygon polygon = new Polygon
                        {
                            Points = new PointCollection { p1, p2, p3, p4 },
                            Fill = new SolidColorBrush(GetHeatmapColor(normalizedZ)),
                            Stroke = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)),
                            StrokeThickness = 0.5,
                            StrokeLineJoin = PenLineJoin.Round
                        };

                        _canvas3D.Children.Add(polygon);
                    }
                }
            }

            if (_chkTheoretical?.IsChecked == true)
            {
                DrawTheoreticalWireframe3D(zData, count, startN, step, minZ, maxZ);
            }
        }

        private void DrawTheoreticalWireframe3D(double[,] zData, int count, int startN, int step, double minZ, double maxZ)
        {
            int lastIdx = count - 1;
            int maxRowsA = startN + lastIdx * step;
            int maxColsA = startN + lastIdx * step;
            int maxColsB = maxRowsA;
            long maxOps = (long)maxRowsA * maxColsA * maxColsB;

            double maxPracticalZ = zData[lastIdx, lastIdx];
            double k = maxOps > 0 ? maxPracticalZ / maxOps : 0;

            double[,] zTheory = new double[count, count];
            for (int i = 0; i < count; i++)
            {
                int rowsA = startN + i * step;
                for (int j = 0; j < count; j++)
                {
                    int colsA = startN + j * step;
                    int colsB = rowsA;
                    long ops = (long)rowsA * colsA * colsB;
                    zTheory[i, j] = k * ops;
                }
            }

            int gridStep = Math.Max(1, count / 12);

            for (int i = 0; i < count; i += gridStep)
            {
                for (int j = 0; j < count; j += gridStep)
                {
                    Point current = GetIsometricProjection(i, j, zTheory[i, j], count, minZ, maxZ);

                    int nextJ = j + gridStep;
                    if (nextJ < count)
                    {
                        Point nextX = GetIsometricProjection(i, nextJ, zTheory[i, nextJ], count, minZ, maxZ);
                        DrawDashedLine(current, nextX);
                    }

                    int nextI = i + gridStep;
                    if (nextI < count)
                    {
                        Point nextY = GetIsometricProjection(nextI, j, zTheory[nextI, j], count, minZ, maxZ);
                        DrawDashedLine(current, nextY);
                    }
                }
            }
        }

        private void DrawDashedLine(Point p1, Point p2)
        {
            Line line = new Line
            {
                X1 = p1.X,
                Y1 = p1.Y,
                X2 = p2.X,
                Y2 = p2.Y,
                Stroke = Brushes.Crimson,
                StrokeThickness = 1.2,
                StrokeDashArray = new DoubleCollection() { 3, 2 }
            };
            _canvas3D.Children.Add(line);
        }

        private void DrawAxes(int count, int startN, int step, double minZ, double maxZ)
        {
            Point p00 = GetIsometricProjection(0, 0, minZ, count, minZ, maxZ);
            Point pN0 = GetIsometricProjection(count - 1, 0, minZ, count, minZ, maxZ);
            Point pNN = GetIsometricProjection(count - 1, count - 1, minZ, count, minZ, maxZ);
            Point p0N = GetIsometricProjection(0, count - 1, minZ, count, minZ, maxZ);

            Polygon floor = new Polygon
            {
                Points = new PointCollection { p00, pN0, pNN, p0N },
                Fill = new SolidColorBrush(Color.FromArgb(15, 0, 0, 0)),
                Stroke = Brushes.LightGray,
                StrokeThickness = 1
            };
            _canvas3D.Children.Add(floor);

            double extCount = (count - 1) * 1.05;
            Point origin = p00;
            Point xAxis = GetIsometricProjection(extCount, 0, minZ, count, minZ, maxZ);
            Point yAxis = GetIsometricProjection(0, extCount, minZ, count, minZ, maxZ);

            double zExt = maxZ + (maxZ - minZ) * 0.1;
            Point zAxis = GetIsometricProjection(0, 0, zExt, count, minZ, maxZ);

            Line CreateLine(Point p1, Point p2, Brush color, double thickness = 2) =>
                new Line { X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y, Stroke = color, StrokeThickness = thickness };

            _canvas3D.Children.Add(CreateLine(origin, xAxis, Brushes.Crimson));
            _canvas3D.Children.Add(CreateLine(origin, yAxis, Brushes.SeaGreen));
            _canvas3D.Children.Add(CreateLine(origin, zAxis, Brushes.RoyalBlue));

            AddLabelToCanvas("Ось X (Строки)", xAxis, Brushes.Crimson, -40, 15);
            AddLabelToCanvas("Ось Y (Столбцы)", yAxis, Brushes.SeaGreen, 10, -5);
            AddLabelToCanvas("Ось Z (Время, мс)", zAxis, Brushes.RoyalBlue, -45, -25);

            int ticksCount = Math.Min(5, count - 1);
            if (ticksCount <= 0) ticksCount = 1;
            double stepIdx = (double)(count - 1) / ticksCount;

            for (int i = 0; i <= ticksCount; i++)
            {
                double idxVal = i * stepIdx;
                Point pOnAxis = GetIsometricProjection(idxVal, 0, minZ, count, minZ, maxZ);
                Point pTickEnd = new Point(pOnAxis.X - 4, pOnAxis.Y + 6);
                _canvas3D.Children.Add(CreateLine(pOnAxis, pTickEnd, Brushes.Crimson, 1.5));

                int actualN = (int)Math.Round(startN + idxVal * step);
                AddTickLabel(actualN.ToString(), pTickEnd, Brushes.Crimson, -12, 5);
            }

            for (int j = 0; j <= ticksCount; j++)
            {
                double idxVal = j * stepIdx;
                Point pOnAxis = GetIsometricProjection(0, idxVal, minZ, count, minZ, maxZ);
                Point pTickEnd = new Point(pOnAxis.X, pOnAxis.Y + 7);
                _canvas3D.Children.Add(CreateLine(pOnAxis, pTickEnd, Brushes.SeaGreen, 1.5));

                int actualN = (int)Math.Round(startN + idxVal * step);
                AddTickLabel(actualN.ToString(), pTickEnd, Brushes.SeaGreen, -6, 8);
            }

            for (int k = 0; k <= ticksCount; k++)
            {
                double zVal = minZ + k * (maxZ - minZ) / ticksCount;
                Point pOnAxis = GetIsometricProjection(0, 0, zVal, count, minZ, maxZ);
                Point pTickEnd = new Point(pOnAxis.X - 7, pOnAxis.Y);
                _canvas3D.Children.Add(CreateLine(pOnAxis, pTickEnd, Brushes.RoyalBlue, 1.5));

                AddTickLabel($"{zVal:F1}", pTickEnd, Brushes.RoyalBlue, -38, -7);
            }
        }

        private void AddTickLabel(string text, Point p, Brush color, double offsetX, double offsetY)
        {
            TextBlock tb = new TextBlock
            {
                Text = text,
                Foreground = color,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
            Canvas.SetLeft(tb, p.X + offsetX);
            Canvas.SetTop(tb, p.Y + offsetY);
            _canvas3D.Children.Add(tb);
        }

        private void AddLabelToCanvas(string text, Point p, Brush color, double offsetX, double offsetY)
        {
            TextBlock tb = new TextBlock
            {
                Text = text,
                Foreground = color,
                FontWeight = FontWeights.Bold,
                FontSize = 13
            };
            Canvas.SetLeft(tb, p.X + offsetX);
            Canvas.SetTop(tb, p.Y + offsetY);
            _canvas3D.Children.Add(tb);
        }

        private Point GetIsometricProjection(double x, double y, double z, int count, double minZ, double maxZ)
        {
            double normX = count > 1 ? x / (count - 1) : 0;
            double normY = count > 1 ? y / (count - 1) : 0;
            double normZ = (z - minZ) / (maxZ - minZ);

            double angleX = Math.PI / 6;

            double isoX = normY + (normX * Math.Cos(angleX));
            double isoY = -(normX * Math.Sin(angleX)) - (normZ * 0.4);

            double canvasWidth = _canvas3D.Width;
            double canvasHeight = _canvas3D.Height;

            double finalX = (canvasWidth * 0.25) - 150 + (isoX * canvasWidth * 0.55);
            double finalY = (canvasHeight * 0.7) + 130 + (isoY * canvasHeight * 0.70);

            return new Point(finalX, finalY);
        }

        private Color GetHeatmapColor(double value)
        {
            value = Math.Max(0, Math.Min(1, value));
            byte r = 0, g = 0, b = 0;

            if (value <= 0.25)
            {
                double t = value / 0.25;
                r = 0; g = (byte)(255 * t); b = 255;
            }
            else if (value <= 0.5)
            {
                double t = (value - 0.25) / 0.25;
                r = 0; g = 255; b = (byte)(255 * (1 - t));
            }
            else if (value <= 0.75)
            {
                double t = (value - 0.5) / 0.25;
                r = (byte)(255 * t); g = 255; b = 0;
            }
            else
            {
                r = 255; g = (byte)(255 * (1 - (value - 0.75) / 0.25)); b = 0;
            }

            return Color.FromRgb(r, g, b);
        }
    }
}