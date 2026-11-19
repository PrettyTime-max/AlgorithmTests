using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WPF;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace Laba_1
{
    public partial class MainWindow : Window
    {
        private TextBox _txtStartN = null!;
        private TextBox _txtEndN = null!;
        private TextBox _txtStep = null!;
        private TextBox _txtRuns = null!;
        private Button _btnStart = null!;
        private Button _btnStop = null!;

        // Новые элементы управления для истории БД
        private ComboBox _cbHistory = null!;
        private Button _btnLoadHistory = null!;

        private CancellationTokenSource? _cts;
        private ProgressBar _progressBar = null!;

        private ComboBox _cbAlgorithms = null!;

        // 2D график (с библиотекой LiveChartsCore) 
        private Grid _container2D = null!;
        private CartesianChart _chart2D = null!;
        private Axis _xAxis = null!;
        private Axis _yAxis = null!;
        private ScrollBar _chartScrollBar = null!;
        private TextBlock _lblZoomRange = null!;
        private double _dataMinN = 1;
        private double _dataMaxN = 500;
        private Point? _dragStartPoint = null;
        private double _dragStartMin = 0;
        private double _dragStartMax = 0;
        private readonly ObservableCollection<ObservablePoint> _chartValues = new();
        private readonly ObservableCollection<ObservablePoint> _theoreticalValues = new(); // Коллекция для теоретических замеров

        // 3D график (Canvas) — для матричных операций
        private Canvas _canvas3D = null!;
        private StackPanel _legendPanel3D = null!;
        private TextBlock _txtMinZ = null!;
        private TextBlock _txtMaxZ = null!;
        private Button _btn3DLayer = null!;
        private double[,]? _lastZDataExp;
        private double[,]? _lastZDataTheo;
        private List<int>? _last3DNValues;
        private int _current3DMode = 0; // 0: Экспериментальный, 1: Теоретический, 2: Совмещенный (Срез)

        // DB & Cache UI Elements
        private DataGrid _dbDataGrid = null!;
        private TextBlock _lblDbStats = null!;

        // Elements for Comparison Tab
        private ListBox _compareAvailableListBox = null!;
        private ListBox _compareSelectedListBox = null!;
        private TextBox _txtCompareStartN = null!;
        private TextBox _txtCompareEndN = null!;
        private TextBox _txtCompareStep = null!;
        private TextBox _txtCompareRuns = null!;
        private Button _btnRunCompare = null!;
        private Button _btnStopCompare = null!;
        private ProgressBar _compareProgressBar = null!;
        private CartesianChart _compareChart = null!;
        private Axis _compareXAxis = null!;
        private Axis _compareYAxis = null!;
        private TextBlock _lblCompareStatus = null!;
        private double _compareDataMinN = 100;
        private double _compareDataMaxN = 2000;
        private CancellationTokenSource? _compareCts;
        private Point? _compareDragStartPoint = null;
        private double _compareDragStartMin = 0;
        private double _compareDragStartMax = 0;

        public MainWindow()
        {
            InitializeComponent();

            Title = "Анализ сложности алгоритмов (с БД SQLite / EF Core)";
            Width = 1250;
            Height = 750;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            Content = BuildInterface();

            Loaded += MainWindow_Loaded;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                source.AddHook(HwndHook);
            }
        }

        private const int WM_MOUSEHWHEEL = 0x020E;

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEHWHEEL)
            {
                short delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                OnTouchpadHorizontalScroll(delta);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await AppDbContext.InitDatabaseAsync();
                await RefreshHistoryComboBoxAsync();
                RefreshDatabaseGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации БД: {ex.Message}",
                                "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Вспомогательный метод для обновления списка замеров в ComboBox
        private async Task RefreshHistoryComboBoxAsync()
        {
            try
            {
                var sessions = await AppDbContext.GetExperimentSessionsAsync();
                _cbHistory.ItemsSource = sessions;
                if (sessions.Count > 0)
                {
                    _cbHistory.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки истории: {ex.Message}");
            }
        }

        private UIElement BuildInterface()
        {
            TabControl tabControl = new TabControl
            {
                Margin = new Thickness(6),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"))
            };

            TabItem tabMain = new TabItem { Header = "📊 2D & 3D Замеры и Графики", FontWeight = FontWeights.Bold };
            tabMain.Content = BuildMainBenchmarkContent();
            tabControl.Items.Add(tabMain);

            TabItem tabCompare = new TabItem { Header = "⚖️ Сравнение алгоритмов", FontWeight = FontWeights.Bold };
            tabCompare.Content = BuildComparisonTabContent();
            tabControl.Items.Add(tabCompare);

            TabItem tabDb = new TabItem { Header = "🗄️ База Данных & Кэш Памяти", FontWeight = FontWeights.Bold };
            tabDb.Content = BuildDbTabContent();
            tabControl.Items.Add(tabDb);

            return tabControl;
        }

        private UIElement BuildMainBenchmarkContent()
        {
            Grid mainGrid = new Grid { Margin = new Thickness(15) };

            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            GroupBox groupBox = new GroupBox
            {
                Header = " Панель управления измерениями и база данных ",
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10)
            };

            StackPanel controlsPanel = new StackPanel { Orientation = Orientation.Horizontal };

            _cbAlgorithms = new ComboBox
            {
                Width = 260,
                Height = 30,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            // Часть I. Операции с векторами
            _cbAlgorithms.Items.Add("1. Постоянная функция");
            _cbAlgorithms.Items.Add("2. Сумма элементов");
            _cbAlgorithms.Items.Add("3. Произведение элементов");
            _cbAlgorithms.Items.Add("4. Прямое вычисление полинома");
            _cbAlgorithms.Items.Add("5. Полином по схеме Горнера");
            _cbAlgorithms.Items.Add("6. (Bubble Sort) Алгоритм сортировки пузырьком");
            _cbAlgorithms.Items.Add("7. (Quick Sort) Алгоритм быстрой сортировки");
            _cbAlgorithms.Items.Add("8. (TimSort) Гибридный алгоритм сортировки элементов");
            // Часть II. Матричные операции (3D)
            _cbAlgorithms.Items.Add("9. Умножение матриц A*B (3D Поверхность)");
            // Часть III. Индивидуальное задание
            _cbAlgorithms.Items.Add("10. (HasDuplicates) Проверка дубликатов ");
            _cbAlgorithms.Items.Add("11. (ReverseArray) Разворот массива");
            _cbAlgorithms.Items.Add("12. (ShellSort) Сортировка Шелла");
            // Часть IV. Алгоритмы возведения в степень
            _cbAlgorithms.Items.Add("13. Простой итеративный алгоритм");
            _cbAlgorithms.Items.Add("14. Рекурсивный алгоритм");
            _cbAlgorithms.Items.Add("15. Быстрый (бинарный) алгоритм возведения в степень");

            _cbAlgorithms.SelectedIndex = 0;
            _cbAlgorithms.SelectionChanged += CbAlgorithms_SelectionChanged;

            controlsPanel.Children.Add(_cbAlgorithms);

            _txtStartN = AddInputField("Старт (N):", "10", controlsPanel);
            _txtEndN = AddInputField("Конец (N):", "500", controlsPanel);
            _txtStep = AddInputField("Шаг (Step):", "10", controlsPanel);
            _txtRuns = AddInputField("Прогоны:", "5", controlsPanel);
            ApplyDefaultAlgorithmParameters(0);

            _btnStart = new Button
            {
                Content = " Начать замер ",
                Height = 30,
                Padding = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            _btnStart.Click += BtnStart_Click;
            controlsPanel.Children.Add(_btnStart);

            _btnStop = new Button
            {
                Content = " Стоп ",
                Height = 30,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Bottom,
                IsEnabled = false
            };
            _btnStop.Click += BtnStop_Click;
            controlsPanel.Children.Add(_btnStop);

            // Вертикальный разделитель
            Border separator = new Border
            {
                Width = 1,
                Background = Brushes.LightGray,
                Margin = new Thickness(10, 2, 10, 2)
            };
            controlsPanel.Children.Add(separator);

            // Выпадающий список сохраненных замеров в БД
            StackPanel historyPanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            historyPanel.Children.Add(new TextBlock { Text = "История замеров (из БД):", Margin = new Thickness(0, 0, 0, 4) });

            _cbHistory = new ComboBox
            {
                Width = 260,
                Height = 30,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            historyPanel.Children.Add(_cbHistory);
            controlsPanel.Children.Add(historyPanel);

            // Кнопка загрузки сохраненного графика
            _btnLoadHistory = new Button
            {
                Content = " Загрузить график ",
                Height = 30,
                Padding = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            _btnLoadHistory.Click += BtnLoadHistory_Click;
            controlsPanel.Children.Add(_btnLoadHistory);

            groupBox.Content = controlsPanel;
            Grid.SetRow(groupBox, 0);
            mainGrid.Children.Add(groupBox);

            _progressBar = new ProgressBar
            {
                Height = 6,
                Margin = new Thickness(0, 0, 0, 10),
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(_progressBar, 1);
            mainGrid.Children.Add(_progressBar);

            Grid displayGrid = new Grid();

            _container2D = new Grid { Visibility = Visibility.Visible };
            _container2D.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _container2D.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _container2D.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Верхняя панель навигации по оси X (панорамирование, зум, быстрый пропуск старта)
            Border navBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 6)
            };

            DockPanel navDock = new DockPanel { LastChildFill = false };

            _lblZoomRange = new TextBlock
            {
                Text = "🖐️ Тачпад: щипок (pinch) — зум, 2 пальца влево/вправо — скролл | Или перетаскивание",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569")),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_lblZoomRange, Dock.Left);
            navDock.Children.Add(_lblZoomRange);

            StackPanel navButtons = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(navButtons, Dock.Right);

            Button btnResetZoom = new Button
            {
                Content = " ↺ Сбросить масштаб ",
                Height = 28,
                Padding = new Thickness(12, 0, 12, 0),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = Brushes.White,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnResetZoom.Click += (s, e) => ResetChartZoom();
            navButtons.Children.Add(btnResetZoom);

            navDock.Children.Add(navButtons);
            navBorder.Child = navDock;
            Grid.SetRow(navBorder, 0);
            _container2D.Children.Add(navBorder);

            _xAxis = new Axis { Name = "Размер N" };
            _yAxis = new Axis { Name = "Время (мс)" };

            _chart2D = new CartesianChart
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
                        GeometrySize = 0, // Без кружков, только гладкая линия
                        Stroke = new SolidColorPaint(SKColors.Crimson) { StrokeThickness = 2 },
                        EnableNullSplitting = false
                    }
                },
                XAxes = new Axis[] { _xAxis },
                YAxes = new Axis[] { _yAxis },
                Visibility = Visibility.Visible
            };

            // Мягкий зум тачпадом / колесом мыши (чувствительность снижена в 2.5-3 раза)
            _chart2D.PreviewMouseWheel += Chart_PreviewMouseWheel;

            // Плавное перетаскивание графика пальцем по тачпаду / зажатой мышью
            _chart2D.PreviewMouseDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed)
                {
                    _dragStartPoint = e.GetPosition(_chart2D);
                    _dragStartMin = _xAxis.MinLimit ?? _dataMinN;
                    _dragStartMax = _xAxis.MaxLimit ?? _dataMaxN;
                    _chart2D.CaptureMouse();
                }
            };

            _chart2D.PreviewMouseMove += (s, e) =>
            {
                if (_dragStartPoint.HasValue && (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed))
                {
                    Point currentPoint = e.GetPosition(_chart2D);
                    double deltaX = currentPoint.X - _dragStartPoint.Value.X;
                    double chartWidth = _chart2D.ActualWidth > 0 ? _chart2D.ActualWidth : 700;

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
                    UpdateScrollBarAndLabel();
                }
            };

            _chart2D.PreviewMouseUp += (s, e) =>
            {
                if (_dragStartPoint.HasValue)
                {
                    _dragStartPoint = null;
                    _chart2D.ReleaseMouseCapture();
                }
            };

            Grid.SetRow(_chart2D, 1);
            _container2D.Children.Add(_chart2D);

            _chartScrollBar = new ScrollBar
            {
                Orientation = Orientation.Horizontal,
                Height = 16,
                Margin = new Thickness(45, 4, 15, 0),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                ViewportSize = 100,
                IsEnabled = false
            };
            _chartScrollBar.Scroll += ChartScrollBar_Scroll;
            Grid.SetRow(_chartScrollBar, 2);
            _container2D.Children.Add(_chartScrollBar);

            displayGrid.Children.Add(_container2D);

            Grid container3D = new Grid { Visibility = Visibility.Collapsed };

            _canvas3D = new Canvas
            {
                Background = Brushes.White,
                Width = 750,
                Height = 500,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            container3D.Children.Add(_canvas3D);

            // Панель переключения 3D слоев (Экспериментальный / Теоретический / Совмещенный срез)
            StackPanel layerPanel3D = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(15)
            };

            _btn3DLayer = new Button
            {
                Content = "📊 Слой: Экспериментальный",
                Height = 34,
                Padding = new Thickness(14, 0, 14, 0),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = Brushes.White,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            _btn3DLayer.Click += Btn3DLayer_Click;
            layerPanel3D.Children.Add(_btn3DLayer);
            container3D.Children.Add(layerPanel3D);

            _legendPanel3D = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(15),
                Visibility = Visibility.Hidden
            };
            _txtMaxZ = new TextBlock { FontWeight = FontWeights.Bold, Foreground = Brushes.DarkRed };
            _txtMinZ = new TextBlock { FontWeight = FontWeights.Bold, Foreground = Brushes.DarkBlue };
            _legendPanel3D.Children.Add(new TextBlock { Text = "Макс. время (мс):" });
            _legendPanel3D.Children.Add(_txtMaxZ);
            _legendPanel3D.Children.Add(new TextBlock { Text = "Мин. время (мс):", Margin = new Thickness(0, 5, 0, 0) });
            _legendPanel3D.Children.Add(_txtMinZ);
            container3D.Children.Add(_legendPanel3D);

            Grid.SetRow(displayGrid, 2);
            displayGrid.Children.Add(container3D);

            mainGrid.Children.Add(displayGrid);

            return mainGrid;
        }

        private UIElement BuildDbTabContent()
        {
            Grid dbGrid = new Grid { Margin = new Thickness(12) };
            dbGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dbGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dbGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            GroupBox actionBox = new GroupBox
            {
                Header = " Панель управления локальной Базой Данных SQLite и Кэшем ",
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10),
                FontWeight = FontWeights.Bold
            };

            StackPanel toolBar = new StackPanel { Orientation = Orientation.Horizontal };

            Button btnRefreshDb = new Button
            {
                Content = " 🔄 Обновить БД ",
                Height = 30, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 10, 0),
                FontWeight = FontWeights.Bold, Foreground = Brushes.White,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")),
                Cursor = Cursors.Hand
            };
            btnRefreshDb.Click += (s, e) => RefreshDatabaseGrid();
            toolBar.Children.Add(btnRefreshDb);

            Button btnClearCache = new Button
            {
                Content = " 🧹 Очистить Кэш Памяти (GC) ",
                Height = 30, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 10, 0),
                FontWeight = FontWeights.Bold, Foreground = Brushes.White,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")),
                Cursor = Cursors.Hand
            };
            btnClearCache.Click += (s, e) =>
            {
                AppDbContext.ForceClearMemoryCache();
                MessageBox.Show("Оперативный кэш памяти очищен (GC Collect выполнен).", "Очистка Кэша", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshDatabaseGrid();
            };
            toolBar.Children.Add(btnClearCache);

            Button btnClearDb = new Button
            {
                Content = " 🗑️ Очистить Всю БД ",
                Height = 30, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 10, 0),
                FontWeight = FontWeights.Bold, Foreground = Brushes.White,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")),
                Cursor = Cursors.Hand
            };
            btnClearDb.Click += async (s, e) =>
            {
                if (MessageBox.Show("Вы уверены, что хотите полностью очистить историю замеров в БД?", "Очистка БД", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    await AppDbContext.ClearDatabaseAsync();
                    await RefreshHistoryComboBoxAsync();
                    RefreshDatabaseGrid();
                }
            };
            toolBar.Children.Add(btnClearDb);

            Button btnExportCsv = new Button
            {
                Content = " 📥 Экспорт в CSV ",
                Height = 30, Padding = new Thickness(10, 0, 10, 0),
                FontWeight = FontWeights.Bold, Foreground = Brushes.White,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                Cursor = Cursors.Hand
            };
            btnExportCsv.Click += (s, e) => ExportDatabaseToCsv();
            toolBar.Children.Add(btnExportCsv);

            actionBox.Content = toolBar;
            Grid.SetRow(actionBox, 0);
            dbGrid.Children.Add(actionBox);

            Border statsCard = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };

            _lblDbStats = new TextBlock
            {
                Text = "📊 Загрузка статистики БД...",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"))
            };
            statsCard.Child = _lblDbStats;
            Grid.SetRow(statsCard, 1);
            dbGrid.Children.Add(statsCard);

            _dbDataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Background = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1")),
                RowHeight = 28,
                FontSize = 12,
                AlternatingRowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC")),
                HeadersVisibility = DataGridHeadersVisibility.Column
            };

            _dbDataGrid.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new System.Windows.Data.Binding("Id"), Width = 60 });
            _dbDataGrid.Columns.Add(new DataGridTextColumn { Header = "Алгоритм", Binding = new System.Windows.Data.Binding("AlgorithmName"), Width = 300 });
            _dbDataGrid.Columns.Add(new DataGridTextColumn { Header = "Размер (N)", Binding = new System.Windows.Data.Binding("N"), Width = 100 });
            _dbDataGrid.Columns.Add(new DataGridTextColumn { Header = "№ Прогона", Binding = new System.Windows.Data.Binding("RunNumber"), Width = 90 });
            _dbDataGrid.Columns.Add(new DataGridTextColumn { Header = "Время (мс)", Binding = new System.Windows.Data.Binding("ExecutionTimeMs") { StringFormat = "{0:F4}" }, Width = 120 });
            _dbDataGrid.Columns.Add(new DataGridTextColumn { Header = "Шаги", Binding = new System.Windows.Data.Binding("StepCount"), Width = 110 });
            _dbDataGrid.Columns.Add(new DataGridTextColumn { Header = "Дата и Время", Binding = new System.Windows.Data.Binding("ExperimentDate") { StringFormat = "{0:dd.MM.yyyy HH:mm:ss}" }, Width = 170 });

            Grid.SetRow(_dbDataGrid, 2);
            dbGrid.Children.Add(_dbDataGrid);

            RefreshDatabaseGrid();
            return dbGrid;
        }

        private UIElement BuildComparisonTabContent()
        {
            Grid mainGrid = new Grid { Margin = new Thickness(12) };
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Левая панель: Выбор алгоритмов, управление, параметры
            GroupBox controlBox = new GroupBox
            {
                Header = " Панель сравнения нескольких алгоритмов ",
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 10, 0)
            };

            ScrollViewer leftScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel leftPanel = new StackPanel();

            leftPanel.Children.Add(new TextBlock
            {
                Text = "💡 Перетаскивайте мышью (Drag & Drop) или дважды кликайте на алгоритмы, чтобы добавить в сравнение:",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8),
                FontWeight = FontWeights.Normal
            });

            leftPanel.Children.Add(new TextBlock { Text = "Доступные алгоритмы:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) });

            _compareAvailableListBox = new ListBox
            {
                Height = 150,
                SelectionMode = SelectionMode.Single,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 6)
            };
            _compareAvailableListBox.Items.Add("1. Постоянная функция");
            _compareAvailableListBox.Items.Add("2. Сумма элементов");
            _compareAvailableListBox.Items.Add("3. Произведение элементов");
            _compareAvailableListBox.Items.Add("4. Прямое вычисление полинома");
            _compareAvailableListBox.Items.Add("5. Полином по схеме Горнера");
            _compareAvailableListBox.Items.Add("6. (Bubble Sort) Сортировка пузырьком");
            _compareAvailableListBox.Items.Add("7. (Quick Sort) Быстрая сортировка");
            _compareAvailableListBox.Items.Add("8. (TimSort) Сортировка элементов");
            _compareAvailableListBox.Items.Add("10. (HasDuplicates) Проверка дубликатов");
            _compareAvailableListBox.Items.Add("11. (ReverseArray) Разворот массива");
            _compareAvailableListBox.Items.Add("12. (ShellSort) Сортировка Шелла");
            _compareAvailableListBox.Items.Add("13. Простой итеративный алгоритм");
            _compareAvailableListBox.Items.Add("14. Рекурсивный алгоритм");
            _compareAvailableListBox.Items.Add("15. Быстрый (бинарный) алгоритм");

            _compareAvailableListBox.PreviewMouseDown += CompareAvailable_PreviewMouseDown;
            _compareAvailableListBox.PreviewMouseMove += CompareAvailable_PreviewMouseMove;
            _compareAvailableListBox.MouseDoubleClick += (s, e) => AddSelectedToCompare();
            leftPanel.Children.Add(_compareAvailableListBox);

            // Кнопки перемещения
            StackPanel btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) };
            Button btnAdd = new Button { Content = " ➕ Добавить ", Height = 26, Margin = new Thickness(0, 0, 6, 0), FontWeight = FontWeights.Normal };
            btnAdd.Click += (s, e) => AddSelectedToCompare();
            btnRow.Children.Add(btnAdd);

            Button btnRemove = new Button { Content = " ➖ Удалить ", Height = 26, Margin = new Thickness(0, 0, 6, 0), FontWeight = FontWeights.Normal };
            btnRemove.Click += (s, e) => RemoveSelectedFromCompare();
            btnRow.Children.Add(btnRemove);

            Button btnClearList = new Button { Content = " 🧹 Очистить всё ", Height = 26, FontWeight = FontWeights.Normal };
            btnClearList.Click += (s, e) => _compareSelectedListBox.Items.Clear();
            btnRow.Children.Add(btnClearList);
            leftPanel.Children.Add(btnRow);

            leftPanel.Children.Add(new TextBlock { Text = "Выбранные алгоритмы (будут выполнены по очереди):", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 3) });

            _compareSelectedListBox = new ListBox
            {
                Height = 130,
                SelectionMode = SelectionMode.Single,
                FontSize = 12,
                AllowDrop = true,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _compareSelectedListBox.Items.Add("6. (Bubble Sort) Сортировка пузырьком");
            _compareSelectedListBox.Items.Add("7. (Quick Sort) Быстрая сортировка");
            _compareSelectedListBox.Items.Add("8. (TimSort) Сортировка элементов");

            _compareSelectedListBox.DragOver += (s, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
            _compareSelectedListBox.Drop += CompareSelected_Drop;
            _compareSelectedListBox.MouseDoubleClick += (s, e) => RemoveSelectedFromCompare();
            leftPanel.Children.Add(_compareSelectedListBox);

            // Блок параметров диапазона N
            GroupBox paramsBox = new GroupBox
            {
                Header = " Единый диапазон измерений ",
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(6),
                Margin = new Thickness(0, 0, 0, 10)
            };
            WrapPanel paramsWrap = new WrapPanel { Orientation = Orientation.Horizontal };
            _txtCompareStartN = AddInputField("Старт (N):", "200", paramsWrap);
            _txtCompareEndN = AddInputField("Конец (N):", "3000", paramsWrap);
            _txtCompareStep = AddInputField("Шаг (Step):", "100", paramsWrap);
            _txtCompareRuns = AddInputField("Прогоны:", "3", paramsWrap);
            paramsBox.Content = paramsWrap;
            leftPanel.Children.Add(paramsBox);

            // Кнопки запуска
            StackPanel runBtnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            _btnRunCompare = new Button
            {
                Content = " ▶ Запустить сравнение ",
                Height = 32,
                Padding = new Thickness(14, 0, 14, 0),
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                Cursor = Cursors.Hand
            };
            _btnRunCompare.Click += BtnRunComparison_Click;
            runBtnRow.Children.Add(_btnRunCompare);

            _btnStopCompare = new Button
            {
                Content = " ⏹ Стоп ",
                Height = 32,
                Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(8, 0, 0, 0),
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")),
                IsEnabled = false,
                Cursor = Cursors.Hand
            };
            _btnStopCompare.Click += (s, e) => { _compareCts?.Cancel(); _btnStopCompare.IsEnabled = false; };
            runBtnRow.Children.Add(_btnStopCompare);
            leftPanel.Children.Add(runBtnRow);

            _compareProgressBar = new ProgressBar
            {
                Height = 6,
                Margin = new Thickness(0, 0, 0, 6),
                Visibility = Visibility.Collapsed
            };
            leftPanel.Children.Add(_compareProgressBar);

            _lblCompareStatus = new TextBlock
            {
                Text = "Готов к запуску. Выберите алгоритмы и нажмите 'Запустить сравнение'.",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")),
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.Normal
            };
            leftPanel.Children.Add(_lblCompareStatus);

            leftScroll.Content = leftPanel;
            controlBox.Content = leftScroll;
            Grid.SetColumn(controlBox, 0);
            mainGrid.Children.Add(controlBox);

            // Правая панель: График
            Grid chartArea = new Grid();
            chartArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            chartArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Border navBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 6)
            };
            DockPanel navDock = new DockPanel { LastChildFill = false };
            TextBlock lblHint = new TextBlock
            {
                Text = "🖐️ Колесико / Щипок: мягкий зум | Перетаскивание: сдвиг по оси N",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569")),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(lblHint, Dock.Left);
            navDock.Children.Add(lblHint);

            Button btnResetCompareZoom = new Button
            {
                Content = " ↺ Сбросить масштаб ",
                Height = 28,
                Padding = new Thickness(12, 0, 12, 0),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = Brushes.White,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnResetCompareZoom.Click += (s, e) =>
            {
                _compareXAxis.MinLimit = null;
                _compareXAxis.MaxLimit = null;
                UpdateCompareStatusLabel();
            };
            DockPanel.SetDock(btnResetCompareZoom, Dock.Right);
            navDock.Children.Add(btnResetCompareZoom);

            navBorder.Child = navDock;
            Grid.SetRow(navBorder, 0);
            chartArea.Children.Add(navBorder);

            _compareXAxis = new Axis { Name = "Размер N" };
            _compareYAxis = new Axis { Name = "Время (мс)" };

            _compareChart = new CartesianChart
            {
                IsHitTestVisible = true,
                ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.X,
                LegendPosition = LiveChartsCore.Measure.LegendPosition.Right,
                Series = new ObservableCollection<ISeries>(),
                XAxes = new Axis[] { _compareXAxis },
                YAxes = new Axis[] { _compareYAxis }
            };

            _compareChart.PreviewMouseWheel += CompareChart_PreviewMouseWheel;

            _compareChart.PreviewMouseDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed)
                {
                    _compareDragStartPoint = e.GetPosition(_compareChart);
                    _compareDragStartMin = _compareXAxis.MinLimit ?? _compareDataMinN;
                    _compareDragStartMax = _compareXAxis.MaxLimit ?? _compareDataMaxN;
                    _compareChart.CaptureMouse();
                }
            };

            _compareChart.PreviewMouseMove += (s, e) =>
            {
                if (_compareDragStartPoint.HasValue && (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed))
                {
                    Point currentPoint = e.GetPosition(_compareChart);
                    double deltaX = currentPoint.X - _compareDragStartPoint.Value.X;
                    double chartWidth = _compareChart.ActualWidth > 0 ? _compareChart.ActualWidth : 700;

                    double viewWidth = _compareDragStartMax - _compareDragStartMin;
                    double totalRange = Math.Max(1.0, _compareDataMaxN - _compareDataMinN);
                    if (viewWidth >= totalRange)
                    {
                        viewWidth = totalRange * 0.8;
                        _compareDragStartMax = _compareDragStartMin + viewWidth;
                    }

                    double shiftN = -(deltaX / chartWidth) * viewWidth;
                    double newMin = _compareDragStartMin + shiftN;
                    double newMax = _compareDragStartMax + shiftN;

                    if (newMin < _compareDataMinN)
                    {
                        newMin = _compareDataMinN;
                        newMax = newMin + viewWidth;
                    }
                    if (newMax > _compareDataMaxN)
                    {
                        newMax = _compareDataMaxN;
                        newMin = Math.Max(_compareDataMinN, newMax - viewWidth);
                    }

                    _compareXAxis.MinLimit = newMin;
                    _compareXAxis.MaxLimit = newMax;
                    UpdateCompareStatusLabel();
                }
            };

            _compareChart.PreviewMouseUp += (s, e) =>
            {
                if (_compareDragStartPoint.HasValue)
                {
                    _compareDragStartPoint = null;
                    _compareChart.ReleaseMouseCapture();
                }
            };

            Grid.SetRow(_compareChart, 1);
            chartArea.Children.Add(_compareChart);

            Grid.SetColumn(chartArea, 1);
            mainGrid.Children.Add(chartArea);

            return mainGrid;
        }

        private Point _dragItemStartPoint;

        private void CompareAvailable_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragItemStartPoint = e.GetPosition(null);
        }

        private void CompareAvailable_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPoint = e.GetPosition(null);
                Vector diff = _dragItemStartPoint - currentPoint;
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (_compareAvailableListBox.SelectedItem is string selectedAlgo)
                    {
                        DragDrop.DoDragDrop(_compareAvailableListBox, selectedAlgo, DragDropEffects.Copy);
                    }
                }
            }
        }

        private void CompareSelected_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.StringFormat) is string item)
            {
                if (!_compareSelectedListBox.Items.Contains(item))
                {
                    _compareSelectedListBox.Items.Add(item);
                }
            }
        }

        private void AddSelectedToCompare()
        {
            if (_compareAvailableListBox.SelectedItem is string item)
            {
                if (!_compareSelectedListBox.Items.Contains(item))
                {
                    _compareSelectedListBox.Items.Add(item);
                }
            }
        }

        private void RemoveSelectedFromCompare()
        {
            if (_compareSelectedListBox.SelectedItem is string item)
            {
                _compareSelectedListBox.Items.Remove(item);
            }
        }

        private int GetAlgorithmIndexByName(string name)
        {
            if (name.Contains("Постоянная")) return 0;
            if (name.Contains("Сумма")) return 1;
            if (name.Contains("Произведение")) return 2;
            if (name.Contains("Прямое вычисление")) return 3;
            if (name.Contains("Горнера")) return 4;
            if (name.Contains("Bubble")) return 5;
            if (name.Contains("Quick")) return 6;
            if (name.Contains("TimSort")) return 7;
            if (name.Contains("Duplicates")) return 9;
            if (name.Contains("ReverseArray") || name.Contains("Разворот")) return 10;
            if (name.Contains("Shell")) return 11;
            if (name.Contains("итеративный")) return 12;
            if (name.Contains("Рекурсивный")) return 13;
            if (name.Contains("бинарный") || name.Contains("Быстрый")) return 14;
            return 0;
        }

        private string CleanAlgoShortName(string name)
        {
            if (name.Contains("Постоянная")) return "Постоянная O(1)";
            if (name.Contains("Сумма")) return "Сумма O(N)";
            if (name.Contains("Произведение")) return "Произведение O(N)";
            if (name.Contains("Прямое вычисление")) return "Полином наивный O(N²)";
            if (name.Contains("Горнера")) return "Схема Горнера O(N)";
            if (name.Contains("Bubble")) return "Bubble Sort O(N²)";
            if (name.Contains("Quick")) return "Quick Sort O(N log N)";
            if (name.Contains("TimSort")) return "TimSort O(N log N)";
            if (name.Contains("Duplicates")) return "HasDuplicates O(N)";
            if (name.Contains("ReverseArray") || name.Contains("Разворот")) return "Разворот O(N)";
            if (name.Contains("Shell")) return "ShellSort O(N^1.25)";
            if (name.Contains("итеративный")) return "Pow Итеративный O(N)";
            if (name.Contains("Рекурсивный")) return "Pow Рекурсивный O(N)";
            if (name.Contains("бинарный") || name.Contains("Быстрый")) return "Pow Бинарный O(log N)";
            return name;
        }

        private async void BtnRunComparison_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(_txtCompareStartN.Text, out int startN) ||
                !int.TryParse(_txtCompareEndN.Text, out int endN) ||
                !int.TryParse(_txtCompareStep.Text, out int step) || step <= 0 || startN < 0 || endN < startN)
            {
                MessageBox.Show("Заполните корректно параметры диапазона для сравнения.");
                return;
            }

            if (!int.TryParse(_txtCompareRuns.Text, out int runsCount) || runsCount <= 0)
            {
                runsCount = 3;
            }

            var selectedAlgorithms = _compareSelectedListBox.Items.OfType<string>().ToList();
            if (selectedAlgorithms.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы один алгоритм в список сравнения.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _compareCts = new CancellationTokenSource();
            var token = _compareCts.Token;

            _btnRunCompare.IsEnabled = false;
            _btnStopCompare.IsEnabled = true;
            _compareProgressBar.Visibility = Visibility.Visible;
            _compareProgressBar.IsIndeterminate = true;

            var seriesCollection = new ObservableCollection<ISeries>();
            _compareChart.Series = seriesCollection;
            _compareDataMinN = startN;
            _compareDataMaxN = endN;
            _compareXAxis.MinLimit = null;
            _compareXAxis.MaxLimit = null;

            SKColor[] palette = new[]
            {
                SKColors.DodgerBlue,
                SKColors.Crimson,
                SKColors.ForestGreen,
                SKColors.DarkOrange,
                SKColors.Purple,
                SKColors.Teal,
                SKColors.DeepPink,
                SKColors.Brown,
                SKColors.DarkSlateBlue,
                SKColors.Goldenrod
            };

            try
            {
                for (int i = 0; i < selectedAlgorithms.Count; i++)
                {
                    if (token.IsCancellationRequested) break;

                    string algoName = selectedAlgorithms[i];
                    int algoIndex = GetAlgorithmIndexByName(algoName);
                    string displayName = CleanAlgoShortName(algoName);

                    _lblCompareStatus.Text = $"⏳ Выполняется ({i + 1}/{selectedAlgorithms.Count}): {displayName}...";

                    var points = await Task.Run(() =>
                    {
                        int[] maxDataInt = new int[endN];
                        Random rng = new Random();
                        for (int k = 0; k < endN; k++) maxDataInt[k] = rng.Next(1, Math.Max(10000, endN * 5));

                        Thread.CurrentThread.Priority = ThreadPriority.Highest;

                        int warmupSize = Math.Min(200, endN);
                        int[] warmupInt = maxDataInt.AsSpan(0, warmupSize).ToArray();
                        double[] warmupDouble = warmupInt.Select(x => (double)x).ToArray();
                        for (int w = 0; w < 3; w++)
                        {
                            if (token.IsCancellationRequested) break;
                            ExecuteAlgorithm(algoIndex, warmupInt, warmupDouble, warmupSize);
                        }

                        List<ObservablePoint> algoPoints = new List<ObservablePoint>();

                        for (int n = startN; n <= endN; n += step)
                        {
                            if (token.IsCancellationRequested) return null;

                            GC.Collect();
                            GC.WaitForPendingFinalizers();

                            int[] curInt = new int[n];
                            double[] curDouble = new double[n];
                            double bestMs = double.MaxValue;

                            for (int r = 0; r < runsCount; r++)
                            {
                                Array.Copy(maxDataInt, curInt, n);
                                for (int idx = 0; idx < n; idx++) curDouble[idx] = curInt[idx];

                                long sTicks = Stopwatch.GetTimestamp();
                                ExecuteAlgorithm(algoIndex, curInt, curDouble, n);
                                long eTicks = Stopwatch.GetTimestamp();

                                double ms = (eTicks - sTicks) * 1000.0 / Stopwatch.Frequency;
                                if (ms < bestMs) bestMs = ms;
                            }

                            algoPoints.Add(new ObservablePoint(n, bestMs));
                        }

                        Thread.CurrentThread.Priority = ThreadPriority.Normal;
                        return algoPoints;
                    }, token);

                    if (token.IsCancellationRequested || points == null) break;

                    SKColor color = palette[i % palette.Length];
                    var seriesValues = new ObservableCollection<ObservablePoint>(points);

                    var lineSeries = new LineSeries<ObservablePoint>
                    {
                        Values = seriesValues,
                        Name = displayName,
                        Stroke = new SolidColorPaint(color) { StrokeThickness = 2.5f },
                        GeometryStroke = new SolidColorPaint(color) { StrokeThickness = 2f },
                        GeometryFill = new SolidColorPaint(SKColors.White),
                        GeometrySize = 5,
                        Fill = null,
                        EnableNullSplitting = false
                    };

                    seriesCollection.Add(lineSeries);
                }

                if (token.IsCancellationRequested)
                {
                    _lblCompareStatus.Text = "Сравнение прервано пользователем.";
                }
                else
                {
                    _lblCompareStatus.Text = $"✅ Сравнение успешно завершено! Построено графиков: {selectedAlgorithms.Count}.";
                }
            }
            catch (OperationCanceledException)
            {
                _lblCompareStatus.Text = "Сравнение отменено.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сравнения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                _lblCompareStatus.Text = $"Ошибка: {ex.Message}";
            }
            finally
            {
                _btnRunCompare.IsEnabled = true;
                _btnStopCompare.IsEnabled = false;
                _compareProgressBar.Visibility = Visibility.Collapsed;
                _compareCts?.Dispose();
                _compareCts = null;
            }
        }

        private async void RefreshDatabaseGrid()
        {
            if (_dbDataGrid == null || _lblDbStats == null) return;

            try
            {
                var records = await AppDbContext.GetAllResultsAsync();
                _dbDataGrid.ItemsSource = records;

                long dbSizeKb = AppDbContext.GetDatabaseFileSizeBytes() / 1024;
                long currentRamMb = GC.GetTotalMemory(false) / (1024 * 1024);

                _lblDbStats.Text = $"📊 Записей в БД: {records.Count} шт. | 💾 Файл БД: {dbSizeKb} КБ | ⚡ Оперативная память кэша: ~{currentRamMb} МБ";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка обновления таблицы БД: {ex.Message}");
            }
        }

        private async void ExportDatabaseToCsv()
        {
            var records = await AppDbContext.GetAllResultsAsync();
            if (records.Count == 0)
            {
                MessageBox.Show("База данных пуста. Нечего экспортировать.", "Экспорт в CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string csvPath = System.IO.Path.Combine(desktopPath, "benchmarks_history_export.csv");

                using var writer = new System.IO.StreamWriter(csvPath, false, System.Text.Encoding.UTF8);
                writer.WriteLine("ID;AlgorithmName;N;RunNumber;ExecutionTimeMs;StepCount;ExperimentDate");

                foreach (var r in records)
                {
                    writer.WriteLine($"{r.Id};\"{r.AlgorithmName}\";{r.N};{r.RunNumber};{r.ExecutionTimeMs:F6};{(r.StepCount.HasValue ? r.StepCount.Value.ToString() : "")};\"{r.ExperimentDate.ToLocalTime():yyyy-MM-dd HH:mm:ss}\"");
                }

                MessageBox.Show($"Данные успешно экспортированы на Рабочий стол:\n{csvPath}", "Экспорт завершен", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта в CSV: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _cts?.Cancel();
            }
            catch (ObjectDisposedException) { }

            _btnStop.IsEnabled = false;
        }

        // Загрузка графика выбранного замера из БД
        private async void BtnLoadHistory_Click(object sender, RoutedEventArgs e)
        {
            if (_cbHistory.SelectedItem is not ExperimentSession selectedSession)
            {
                MessageBox.Show("Выберите замер из списка истории.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var records = await AppDbContext.GetResultsForSessionAsync(selectedSession.ExperimentDate, selectedSession.AlgorithmName);
                if (records == null || records.Count == 0) return;

                _chartValues.Clear();
                _theoreticalValues.Clear(); // Очищаем теоретическую коллекцию

                // Переключение отображения (2D/3D)
                bool is3D = selectedSession.AlgorithmName.Contains("3D") || selectedSession.AlgorithmName.Contains("матриц");
                if (_container2D != null) _container2D.Visibility = is3D ? Visibility.Collapsed : Visibility.Visible;
                if (_canvas3D.Parent is Grid container3D)
                {
                    container3D.Visibility = is3D ? Visibility.Visible : Visibility.Collapsed;
                }

                if (is3D)
                {
                    var distinctN = records.Select(r => r.N).Distinct().OrderBy(n => n).ToList();
                    int count = distinctN.Count;
                    if (count > 0)
                    {
                        double[,] zData = new double[count, count];
                        var sortedRecords = records.OrderBy(r => r.N).ToList();
                        if (sortedRecords.Count >= count * count)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                for (int j = 0; j < count; j++)
                                {
                                    int idx = i * count + j;
                                    zData[i, j] = sortedRecords[idx].ExecutionTimeMs;
                                }
                            }
                        }
                        else
                        {
                            var avgList = sortedRecords.GroupBy(r => r.N).Select(g => g.Average(x => x.ExecutionTimeMs)).ToList();
                            for (int i = 0; i < count; i++)
                                for (int j = 0; j < count; j++)
                                    zData[i, j] = (i < avgList.Count) ? avgList[i] : 0;
                        }

                        _lastZDataExp = zData;
                        _last3DNValues = distinctN;
                        _lastZDataTheo = CalculateTheoretical3DData(count, _last3DNValues, _lastZDataExp);
                        Redraw3DSurface();
                    }
                }
                else
                {
                    // Группируем замеры по N и берем лучший замер (Best of N) для точек графика
                    var aggregatedPoints = records
                        .GroupBy(r => r.N)
                        .OrderBy(g => g.Key)
                        .Select(g => new ObservablePoint(g.Key, g.Min(r => r.ExecutionTimeMs)))
                        .ToList();

                    foreach (var pt in aggregatedPoints)
                    {
                        _chartValues.Add(pt);
                    }

                    if (aggregatedPoints.Count > 0)
                    {
                        _dataMinN = aggregatedPoints.Min(p => p.X ?? 1);
                        _dataMaxN = aggregatedPoints.Max(p => p.X ?? 500);
                        ResetChartZoom();
                    }

                    // Расчет идеального теоретического графика из БД
                    CalculateTheoreticalCurve(selectedSession.AlgorithmName, aggregatedPoints);
                }

                MessageBox.Show($"График успешно восстановлен из БД!\nФункция: {selectedSession.AlgorithmName}\nЗаписей: {records.Count}",
                                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных из БД: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private TextBox AddInputField(string label, string defaultValue, Panel container)
        {
            StackPanel fieldGroup = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            fieldGroup.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 4) });

            TextBox input = new TextBox { Text = defaultValue, Width = 60 };
            fieldGroup.Children.Add(input);

            container.Children.Add(fieldGroup);
            return input;
        }

        private void CbAlgorithms_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int index = _cbAlgorithms.SelectedIndex;
            bool isMatrix3D = IsMatrixAlgorithm(index);

            ApplyDefaultAlgorithmParameters(index);

            if (_container2D != null) _container2D.Visibility = isMatrix3D ? Visibility.Collapsed : Visibility.Visible;
            if (_canvas3D.Parent is Grid container3D)
            {
                container3D.Visibility = isMatrix3D ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private bool IsMatrixAlgorithm(int index) => index == 8;

        private void ApplyDefaultAlgorithmParameters(int index)
        {
            if (_txtStartN == null || _txtEndN == null || _txtStep == null || _txtRuns == null) return;

            switch (index)
            {
                case 0: // 1. Постоянная функция (тяжелые вычисления для стабилизации на ~10 мс)
                    _txtStartN.Text = "10";
                    _txtEndN.Text = "1000";
                    _txtStep.Text = "50";
                    _txtRuns.Text = "5";
                    break;

                case 1: // 2. Сумма элементов O(N)
                case 2: // 3. Произведение элементов O(N)
                case 4: // 5. Полином по схеме Горнера O(N)
                case 10: // 11. Разворот массива O(N)
                    _txtStartN.Text = "10000";
                    _txtEndN.Text = "500000";
                    _txtStep.Text = "20000";
                    _txtRuns.Text = "5";
                    break;

                case 3: // 4. Прямое вычисление полинома O(N^2)
                case 5: // 6. Bubble Sort O(N^2)
                    _txtStartN.Text = "200";
                    _txtEndN.Text = "3000";
                    _txtStep.Text = "100";
                    _txtRuns.Text = "3";
                    break;

                case 6: // 7. Quick Sort O(N log N)
                case 7: // 8. TimSort O(N log N)
                case 11: // 12. ShellSort O(N^1.25)
                    _txtStartN.Text = "1000";
                    _txtEndN.Text = "25000";
                    _txtStep.Text = "1000";
                    _txtRuns.Text = "5";
                    break;

                case 8: // 9. Матрицы 3D O(N^3)
                    _txtStartN.Text = "10";
                    _txtEndN.Text = "100";
                    _txtStep.Text = "10";
                    _txtRuns.Text = "1";
                    break;

                case 9: // 10. HasDuplicates O(N)
                    _txtStartN.Text = "1000";
                    _txtEndN.Text = "50000";
                    _txtStep.Text = "2500";
                    _txtRuns.Text = "5";
                    break;

                case 12: // 13. Простой итеративный алгоритм PowIterative O(N)
                    _txtStartN.Text = "5000";
                    _txtEndN.Text = "200000";
                    _txtStep.Text = "10000";
                    _txtRuns.Text = "5";
                    break;

                case 13: // 14. Рекурсивный алгоритм PowRecursive O(N) (безопасный диапазон без переполнения стека)
                    _txtStartN.Text = "100";
                    _txtEndN.Text = "3000";
                    _txtStep.Text = "100";
                    _txtRuns.Text = "5";
                    break;

                case 14: // 15. Быстрый (бинарный) алгоритм PowBinary O(log N)
                    _txtStartN.Text = "10000";
                    _txtEndN.Text = "1000000";
                    _txtStep.Text = "50000";
                    _txtRuns.Text = "5";
                    break;

                default:
                    _txtStartN.Text = "100";
                    _txtEndN.Text = "2000";
                    _txtStep.Text = "100";
                    _txtRuns.Text = "5";
                    break;
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(_txtStartN.Text, out int startN) ||
                !int.TryParse(_txtEndN.Text, out int endN) ||
                !int.TryParse(_txtStep.Text, out int step) || step <= 0 || startN < 0 || endN < startN)
            {
                MessageBox.Show("Заполните корректно параметры диапазона.");
                return;
            }

            if (!int.TryParse(_txtRuns.Text, out int runsCount) || runsCount <= 0)
            {
                runsCount = 5;
            }

            _cts = new CancellationTokenSource();
            _btnStart.IsEnabled = false;
            _btnStop.IsEnabled = true;
            _progressBar.Visibility = Visibility.Visible;
            _progressBar.IsIndeterminate = true;

            int selectedIndex = _cbAlgorithms.SelectedIndex;

            try
            {
                List<BenchmarkResult> dbResults;

                if (IsMatrixAlgorithm(selectedIndex))
                {
                    dbResults = await RunMatrix3DBenchmarkAsync(startN, endN, step, _cts.Token);
                }
                else
                {
                    dbResults = await RunArray2DBenchmarkAsync(selectedIndex, startN, endN, step, runsCount, _cts.Token);
                }

                if (_cts != null && !_cts.IsCancellationRequested && dbResults != null && dbResults.Count > 0)
                {
                    // Автосохранение
                    await AppDbContext.SaveResultsAsync(dbResults);

                    // Автоматическое обновление выпадающего списка истории замеров
                    await RefreshHistoryComboBoxAsync();
                }
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Вычисление остановлено пользователем.", "Отмена", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выполнении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _progressBar.Visibility = Visibility.Collapsed;
                _btnStart.IsEnabled = true;
                _btnStop.IsEnabled = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task<List<BenchmarkResult>> RunMatrix3DBenchmarkAsync(int startN, int endN, int step, CancellationToken token)
        {
            _canvas3D.Children.Clear();
            _legendPanel3D.Visibility = Visibility.Hidden;

            int count = ((endN - startN) / step) + 1;
            double[,] zData = new double[count, count];
            List<BenchmarkResult> dbResults = new List<BenchmarkResult>();
            string algName = _cbAlgorithms.SelectedItem.ToString();
            DateTime experimentTimestamp = DateTime.UtcNow;

            await Task.Run(() =>
            {
                Parallel.For(0, count, (i, loopState) =>
                {
                    if (token.IsCancellationRequested)
                    {
                        loopState.Stop();
                        return;
                    }

                    int rowsA = startN + i * step;
                    Random rnd = new Random(Guid.NewGuid().GetHashCode());

                    for (int j = 0; j < count; j++)
                    {
                        if (token.IsCancellationRequested)
                        {
                            loopState.Stop();
                            return;
                        }

                        int colsA = startN + j * step;
                        int colsB = rowsA;

                        double[,] A = Algorithms.GenerateMatrix(rowsA, colsA, rnd);
                        double[,] B = Algorithms.GenerateMatrix(colsA, colsB, rnd);

                        long startTicks = Stopwatch.GetTimestamp();
                        Algorithms.Multiply(A, B);
                        long endTicks = Stopwatch.GetTimestamp();

                        double elapsedMs = (endTicks - startTicks) * 1000.0 / Stopwatch.Frequency;
                        zData[i, j] = elapsedMs;

                        lock (dbResults)
                        {
                            dbResults.Add(new BenchmarkResult
                            {
                                AlgorithmName = algName,
                                N = rowsA,
                                RunNumber = 1,
                                ExecutionTimeMs = elapsedMs,
                                StepCount = (long)rowsA * colsA * colsB,
                                ExperimentDate = experimentTimestamp
                            });
                        }
                    }
                });
            }, token);

            if (token.IsCancellationRequested) return null;

            _lastZDataExp = zData;
            _last3DNValues = Enumerable.Range(0, count).Select(i => startN + i * step).ToList();
            _lastZDataTheo = CalculateTheoretical3DData(count, _last3DNValues, _lastZDataExp);

            Redraw3DSurface();
            return dbResults;
        }

        private async Task<List<BenchmarkResult>> RunArray2DBenchmarkAsync(int algorithmIndex, int startN, int endN, int step, int runsCount, CancellationToken token)
        {
            _chartValues.Clear();
            _theoreticalValues.Clear(); // Очищаем теоретическую коллекцию перед новым замером
            string algName = _cbAlgorithms.SelectedItem.ToString();
            List<BenchmarkResult> dbResults = new List<BenchmarkResult>();
            DateTime experimentTimestamp = DateTime.UtcNow;

            await Task.Run(() =>
            {
                int[] maxDataInt = new int[endN];
                Random rng = new Random();
                for (int i = 0; i < endN; i++) maxDataInt[i] = rng.Next(1, Math.Max(10000, endN * 5));

                Thread.CurrentThread.Priority = ThreadPriority.Highest;

                // Прогрев JIT-компилятора и кэша процессора
                int warmupSize = Math.Min(300, endN);
                int[] warmupInt = maxDataInt.AsSpan(0, warmupSize).ToArray();
                double[] warmupDouble = warmupInt.Select(x => (double)x).ToArray();

                for (int i = 0; i < 5; i++)
                {
                    if (token.IsCancellationRequested) break;
                    ExecuteAlgorithm(algorithmIndex, warmupInt, warmupDouble, warmupSize);
                }

                List<ObservablePoint> results = new List<ObservablePoint>();

                for (int n = startN; n <= endN; n += step)
                {
                    if (token.IsCancellationRequested) return;

                    // Очистка сборщика мусора перед точкой замера для устранения случайных пауз GC
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    // Массив строго размера n, чтобы алгоритмы (QuickSort, BubbleSort, Sum и др.) обрабатывали ровно n элементов!
                    int[] currentIntData = new int[n];
                    double[] currentDoubleData = new double[n];

                    int runs = runsCount;
                    double bestRunMs = double.MaxValue;

                    for (int r = 0; r < runs; r++)
                    {
                        Array.Copy(maxDataInt, currentIntData, n);
                        for (int i = 0; i < n; i++) currentDoubleData[i] = currentIntData[i];

                        long startTicks = Stopwatch.GetTimestamp();
                        long steps = ExecuteAlgorithm(algorithmIndex, currentIntData, currentDoubleData, n);
                        long endTicks = Stopwatch.GetTimestamp();

                        long elapsedTicks = endTicks - startTicks;
                        double singleRunMs = (elapsedTicks * 1000.0) / Stopwatch.Frequency;

                        if (singleRunMs < bestRunMs)
                        {
                            bestRunMs = singleRunMs;
                        }

                        dbResults.Add(new BenchmarkResult
                        {
                            AlgorithmName = algName,
                            N = n,
                            RunNumber = r + 1,
                            ExecutionTimeMs = singleRunMs,
                            StepCount = steps > 0 ? steps : null,
                            ExperimentDate = experimentTimestamp
                        });
                    }

                    // Best of N: берем минимальное чистое время без помех планировщика ОС
                    results.Add(new ObservablePoint(n, bestRunMs));
                }

                Thread.CurrentThread.Priority = ThreadPriority.Normal;

                Dispatcher.Invoke(() =>
                {
                    foreach (var pt in results) _chartValues.Add(pt);

                    if (results.Count > 0)
                    {
                        _dataMinN = results.Min(p => p.X ?? 1);
                        _dataMaxN = results.Max(p => p.X ?? 500);
                        ResetChartZoom();
                    }

                    // Расчет и построение идеальной кривой
                    CalculateTheoreticalCurve(algName, results);
                });
            }, token);

            if (token.IsCancellationRequested) return null;

            return dbResults;
        }

        private long ExecuteAlgorithm(int algorithmIndex, int[] intData, double[] doubleData, int n)
        {
            switch (algorithmIndex)
            {
                // Часть I.
                case 0: Algorithms.Constant(intData); return 1;
                case 1: Algorithms.Sum(intData); return n;
                case 2: Algorithms.Product(intData); return n;
                case 3: Algorithms.PolyNaive(doubleData, 1.5); return n;
                case 4: Algorithms.PolyHorner(doubleData, 1.5); return n;
                case 5: Algorithms.BubbleSort(doubleData); return (long)n * n;
                case 6: Algorithms.QuickSort(doubleData); return (long)(n * Math.Log2(n));
                case 7: Algorithms.TimSort(doubleData); return (long)(n * Math.Log2(n));
                // Часть III.
                case 9: Algorithms.HasDuplicates(intData); return (long)n * n;
                case 10: Algorithms.ReverseArray(intData); return n / 2;
                case 11: Algorithms.ShellSort(intData); return (long)(n * Math.Log2(n));
                // Часть IV.
                case 12: Algorithms.PowIterative(1.0001, n); return n;
                case 13: Algorithms.PowRecursive(1.0001, n); return n;
                case 14: Algorithms.PowBinary(1.0001, n); return (long)Math.Log2(n);
                default: return -1;
            }
        }


        // Метод определяет математическую функцию сложности по названию выбранного алгоритма
        private (Func<double, double> Func, string Label) GetComplexityInfo(string algorithmName)
        {
            if (algorithmName.Contains("Постоянная"))
                return (n => 1.0, "O(1)");

            if (algorithmName.Contains("Bubble") || algorithmName.Contains("Прямое вычисление"))
                return (n => n * n, "O(N²)");

            if (algorithmName.Contains("Quick") || algorithmName.Contains("TimSort"))
                return (n => n * Math.Log2(Math.Max(n, 2)), "O(N log N)");

            if (algorithmName.Contains("Shell"))
                return (n => Math.Pow(n, 1.25), "O(N^1.25)");

            if (algorithmName.Contains("быстрый") || algorithmName.Contains("бинарный") || algorithmName.Contains("PowBinary"))
                return (n => Math.Log2(Math.Max(n, 2)), "O(log N)");

            // По умолчанию O(N): Сумма, Произведение, Горнер, Разворот, Простой/Рекурсивный Pow, HasDuplicates
            return (n => n, "O(N)");
        }

        // Построение теоретической линии методом наименьших квадратов (МНК) поверх практических результатов
        private void CalculateTheoreticalCurve(string algorithmName, IEnumerable<ObservablePoint> actualPoints)
        {
            _theoreticalValues.Clear();
            var pointsList = actualPoints?.Where(p => p.X.HasValue && p.Y.HasValue).ToList();
            if (pointsList == null || pointsList.Count == 0) return;

            var (complexityFunc, label) = GetComplexityInfo(algorithmName);

            // Метод наименьших квадратов (МНК): k = sum(y_i * f_i) / sum(f_i^2)
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
                if (maxPoint != null && maxPoint.X.HasValue && maxPoint.Y.HasValue)
                {
                    double fMax = complexityFunc(maxPoint.X.Value);
                    k = fMax > 0 ? (maxPoint.Y.Value / fMax) : 0.001;
                }
            }

            foreach (var point in pointsList.OrderBy(p => p.X!.Value))
            {
                double n = point.X!.Value;
                double idealTime = k * complexityFunc(n);
                _theoreticalValues.Add(new ObservablePoint(n, idealTime));
            }

            // Обновляем название теоретической серии
            if (_chart2D.Series.ElementAtOrDefault(1) is LineSeries<ObservablePoint> idealSeries)
            {
                idealSeries.Name = $"Теория {label}";
            }
        }

        // --- Методы навигации, масштабирования и прокрутки 2D графика тачпадом и мышью ---

        // Обработка жеста горизонтальной прокрутки тачпада (двупальцевый горизонтальный свайп / WM_MOUSEHWHEEL)
        private void OnTouchpadHorizontalScroll(short delta)
        {
            if (_compareChart != null && _compareChart.IsVisible)
            {
                ScrollChartAxis(_compareChart, _compareXAxis, _compareDataMinN, _compareDataMaxN, delta, UpdateCompareStatusLabel);
            }
            else if (_chart2D != null && _chart2D.IsVisible)
            {
                ScrollChartAxis(_chart2D, _xAxis, _dataMinN, _dataMaxN, delta, UpdateScrollBarAndLabel);
            }
        }

        private void ScrollChartAxis(CartesianChart chart, Axis xAxis, double dataMin, double dataMax, short delta, Action updateCallback)
        {
            if (xAxis == null) return;
            double curMin = xAxis.MinLimit ?? dataMin;
            double curMax = xAxis.MaxLimit ?? dataMax;
            double totalRange = Math.Max(1.0, dataMax - dataMin);
            double viewWidth = curMax - curMin;

            if (viewWidth >= totalRange)
            {
                viewWidth = totalRange * 0.75;
                curMin = dataMin;
                curMax = curMin + viewWidth;
            }

            double step = (viewWidth * 0.05) * (delta / 120.0);
            double newMin = curMin + step;
            double newMax = curMax + step;

            if (newMin < dataMin)
            {
                newMin = dataMin;
                newMax = newMin + viewWidth;
            }
            if (newMax > dataMax)
            {
                newMax = dataMax;
                newMin = Math.Max(dataMin, newMax - viewWidth);
            }

            xAxis.MinLimit = newMin;
            xAxis.MaxLimit = newMax;
            updateCallback?.Invoke();
        }

        // Мягкий зум колесом мыши / жестом pinch тачпада (чувствительность снижена в 2.5-3 раза)
        private void Chart_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            HandleChartMouseWheel(_chart2D, _xAxis, _dataMinN, _dataMaxN, e, UpdateScrollBarAndLabel);
        }

        private void CompareChart_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            HandleChartMouseWheel(_compareChart, _compareXAxis, _compareDataMinN, _compareDataMaxN, e, UpdateCompareStatusLabel);
        }

        private void HandleChartMouseWheel(CartesianChart chart, Axis xAxis, double dataMin, double dataMax, MouseWheelEventArgs e, Action updateCallback)
        {
            e.Handled = true;
            if (chart == null || xAxis == null) return;

            double curMin = xAxis.MinLimit ?? dataMin;
            double curMax = xAxis.MaxLimit ?? dataMax;
            double totalRange = Math.Max(1.0, dataMax - dataMin);
            double curWidth = curMax - curMin;

            if (curWidth <= 0) return;

            // Сниженная чувствительность: ~8% за один щелчок колеса вместо резких 30-40%
            double factor = e.Delta > 0 ? 0.92 : 1.087;
            double newWidth = curWidth * factor;

            if (newWidth < 5) newWidth = 5;
            if (newWidth >= totalRange)
            {
                xAxis.MinLimit = null;
                xAxis.MaxLimit = null;
                updateCallback?.Invoke();
                return;
            }

            // Масштабирование относительно текущего положения курсора мыши
            Point mousePos = e.GetPosition(chart);
            double chartWidth = chart.ActualWidth > 0 ? chart.ActualWidth : 700;
            double ratio = Math.Max(0.0, Math.Min(1.0, mousePos.X / chartWidth));

            double center = curMin + curWidth * ratio;
            double newMin = center - newWidth * ratio;
            double newMax = center + newWidth * (1.0 - ratio);

            if (newMin < dataMin)
            {
                newMin = dataMin;
                newMax = newMin + newWidth;
            }
            if (newMax > dataMax)
            {
                newMax = dataMax;
                newMin = Math.Max(dataMin, newMax - newWidth);
            }

            xAxis.MinLimit = newMin;
            xAxis.MaxLimit = newMax;
            updateCallback?.Invoke();
        }

        // Сброс масштаба на полный обзор всего графика (100%)
        private void ResetChartZoom()
        {
            _xAxis.MinLimit = null;
            _xAxis.MaxLimit = null;
            UpdateScrollBarAndLabel();
        }

        // Прокрутка с помощью горизонтального бегунка ScrollBar
        private void ChartScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            double totalRange = _dataMaxN - _dataMinN;
            if (totalRange <= 0) return;

            double curMin = _xAxis.MinLimit ?? _dataMinN;
            double curMax = _xAxis.MaxLimit ?? _dataMaxN;
            double viewWidth = curMax - curMin;

            if (viewWidth >= totalRange) return;

            double availableScroll = totalRange - viewWidth;
            double scrollFraction = Math.Max(0, Math.Min(1.0, e.NewValue / 100.0));
            double newMin = _dataMinN + availableScroll * scrollFraction;
            double newMax = newMin + viewWidth;

            _xAxis.MinLimit = newMin;
            _xAxis.MaxLimit = newMax;

            double pct = Math.Min(100.0, (viewWidth / totalRange) * 100.0);
            _lblZoomRange.Text = $"🔍 Просмотр: N от {newMin:F0} до {newMax:F0} ({pct:F0}% диапазона) | Прокручивайте вправо ▶";
        }

        // Синхронизация полосы прокрутки и текстовой подсказки с текущими границами
        private void UpdateScrollBarAndLabel()
        {
            if (_lblZoomRange == null || _chartScrollBar == null || _xAxis == null) return;

            double curMin = _xAxis.MinLimit ?? _dataMinN;
            double curMax = _xAxis.MaxLimit ?? _dataMaxN;
            double totalRange = Math.Max(1.0, _dataMaxN - _dataMinN);
            double viewWidth = curMax - curMin;

            if (_xAxis.MinLimit == null && _xAxis.MaxLimit == null)
            {
                _lblZoomRange.Text = $"🔍 Полный диапазон: N от {_dataMinN:F0} до {_dataMaxN:F0} (100%) | Колесико мыши: зум, ЛКМ: сдвиг";
                _chartScrollBar.ViewportSize = 100;
                _chartScrollBar.Value = 0;
                _chartScrollBar.IsEnabled = false;
            }
            else
            {
                double pct = Math.Min(100.0, (viewWidth / totalRange) * 100.0);
                _lblZoomRange.Text = $"🔍 Просмотр: N от {curMin:F0} до {curMax:F0} ({pct:F0}% диапазона) | Прокручивайте вправо ▶";

                double availableScroll = totalRange - viewWidth;
                if (availableScroll > 0)
                {
                    double fraction = Math.Max(0, Math.Min(1.0, (curMin - _dataMinN) / availableScroll));
                    _chartScrollBar.ViewportSize = Math.Max(5.0, (viewWidth / totalRange) * 100.0);
                    _chartScrollBar.Value = fraction * 100.0;
                    _chartScrollBar.IsEnabled = true;
                }
                else
                {
                    _chartScrollBar.ViewportSize = 100;
                    _chartScrollBar.Value = 0;
                    _chartScrollBar.IsEnabled = false;
                }
            }
        }

        private void UpdateCompareStatusLabel()
        {
            if (_lblCompareStatus == null || _compareXAxis == null) return;
            double curMin = _compareXAxis.MinLimit ?? _compareDataMinN;
            double curMax = _compareXAxis.MaxLimit ?? _compareDataMaxN;
            double totalRange = Math.Max(1.0, _compareDataMaxN - _compareDataMinN);
            double viewWidth = curMax - curMin;

            if (_compareXAxis.MinLimit == null && _compareXAxis.MaxLimit == null)
            {
                _lblCompareStatus.Text = $"🔍 Полный диапазон: N от {_compareDataMinN:F0} до {_compareDataMaxN:F0} (100%)";
            }
            else
            {
                double pct = Math.Min(100.0, (viewWidth / totalRange) * 100.0);
                _lblCompareStatus.Text = $"🔍 Просмотр: N от {curMin:F0} до {curMax:F0} ({pct:F0}% диапазона)";
            }
        }


        // дальше рисовка 3д графика для матриц


        private void Btn3DLayer_Click(object sender, RoutedEventArgs e)
        {
            _current3DMode = (_current3DMode + 1) % 3;
            Update3DLayerButtonState();
            Redraw3DSurface();
        }

        private void Update3DLayerButtonState()
        {
            if (_btn3DLayer == null) return;
            switch (_current3DMode)
            {
                case 0:
                    _btn3DLayer.Content = "📊 Слой: Экспериментальный";
                    _btn3DLayer.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
                    break;
                case 1:
                    _btn3DLayer.Content = "📐 Слой: Теоретический";
                    _btn3DLayer.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B5CF6"));
                    break;
                case 2:
                    _btn3DLayer.Content = "⚡ Совмещенный режим (Срез)";
                    _btn3DLayer.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EC4899"));
                    break;
            }
        }

        private double[,] CalculateTheoretical3DData(int count, List<int> nValues, double[,] zExpData)
        {
            double[,] zTheo = new double[count, count];
            double maxExpZ = 0;
            double maxTheoSteps = 0;

            for (int i = 0; i < count; i++)
            {
                double rowsA = nValues[i];
                for (int j = 0; j < count; j++)
                {
                    double colsA = nValues[j];
                    double steps = rowsA * colsA * rowsA; // O(M * N * K) для умножения матриц A(MxK) * B(KxN)
                    if (steps > maxTheoSteps) maxTheoSteps = steps;
                    if (zExpData[i, j] > maxExpZ) maxExpZ = zExpData[i, j];
                }
            }

            if (maxTheoSteps <= 0) maxTheoSteps = 1;
            double scaleFactor = maxExpZ / maxTheoSteps;

            for (int i = 0; i < count; i++)
            {
                double rowsA = nValues[i];
                for (int j = 0; j < count; j++)
                {
                    double colsA = nValues[j];
                    double steps = rowsA * colsA * rowsA;
                    zTheo[i, j] = steps * scaleFactor;
                }
            }

            return zTheo;
        }

        private void Redraw3DSurface()
        {
            _canvas3D.Children.Clear();
            if (_lastZDataExp == null || _last3DNValues == null || _last3DNValues.Count == 0)
            {
                _legendPanel3D.Visibility = Visibility.Hidden;
                return;
            }

            int count = _last3DNValues.Count;
            double minZExp = double.MaxValue, maxZExp = double.MinValue;
            double minZTheo = double.MaxValue, maxZTheo = double.MinValue;

            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    if (_lastZDataExp[i, j] < minZExp) minZExp = _lastZDataExp[i, j];
                    if (_lastZDataExp[i, j] > maxZExp) maxZExp = _lastZDataExp[i, j];

                    if (_lastZDataTheo != null)
                    {
                        if (_lastZDataTheo[i, j] < minZTheo) minZTheo = _lastZDataTheo[i, j];
                        if (_lastZDataTheo[i, j] > maxZTheo) maxZTheo = _lastZDataTheo[i, j];
                    }
                }
            }

            double globalMinZ = minZExp;
            double globalMaxZ = maxZExp;

            if (_lastZDataTheo != null)
            {
                globalMinZ = Math.Min(minZExp, minZTheo);
                globalMaxZ = Math.Max(maxZExp, maxZTheo);
            }

            if (globalMaxZ == globalMinZ) globalMaxZ = globalMinZ + 1;

            if (_current3DMode == 0)
            {
                _txtMinZ.Text = $"{minZExp:F2}";
                _txtMaxZ.Text = $"{maxZExp:F2}";
            }
            else if (_current3DMode == 1)
            {
                _txtMinZ.Text = $"{minZTheo:F2}";
                _txtMaxZ.Text = $"{maxZTheo:F2}";
            }
            else
            {
                _txtMinZ.Text = $"Эксп: {minZExp:F2} | Теор: {minZTheo:F2}";
                _txtMaxZ.Text = $"Эксп: {maxZExp:F2} | Теор: {maxZTheo:F2}";
            }

            _legendPanel3D.Visibility = Visibility.Visible;

            // Рисуем единые математические оси
            DrawAxes(count, globalMinZ, globalMaxZ);

            // Отрисовка полигонов от дальних к ближним (Painter's algorithm)
            for (int i = count - 2; i >= 0; i--)
            {
                for (int j = 0; j < count - 1; j++)
                {
                    // 1. Экспериментальный слой (Режим 0 и Режим 2)
                    if (_current3DMode == 0 || _current3DMode == 2)
                    {
                        Point p1 = GetIsometricProjection(i, j, _lastZDataExp[i, j], count, globalMinZ, globalMaxZ);
                        Point p2 = GetIsometricProjection(i + 1, j, _lastZDataExp[i + 1, j], count, globalMinZ, globalMaxZ);
                        Point p3 = GetIsometricProjection(i + 1, j + 1, _lastZDataExp[i + 1, j + 1], count, globalMinZ, globalMaxZ);
                        Point p4 = GetIsometricProjection(i, j + 1, _lastZDataExp[i, j + 1], count, globalMinZ, globalMaxZ);

                        double avgZ = (_lastZDataExp[i, j] + _lastZDataExp[i + 1, j] + _lastZDataExp[i + 1, j + 1] + _lastZDataExp[i, j + 1]) / 4.0;
                        double normalizedZ = (avgZ - globalMinZ) / (globalMaxZ - globalMinZ);

                        Polygon polygonExp = new Polygon
                        {
                            Points = new PointCollection { p1, p2, p3, p4 },
                            Fill = new SolidColorBrush(GetHeatmapColor(normalizedZ)),
                            Stroke = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)),
                            StrokeThickness = 0.5,
                            StrokeLineJoin = PenLineJoin.Round
                        };

                        _canvas3D.Children.Add(polygonExp);
                    }

                    // 2. Теоретический слой (Режим 1 и Режим 2)
                    if (_lastZDataTheo != null && (_current3DMode == 1 || _current3DMode == 2))
                    {
                        Point p1 = GetIsometricProjection(i, j, _lastZDataTheo[i, j], count, globalMinZ, globalMaxZ);
                        Point p2 = GetIsometricProjection(i + 1, j, _lastZDataTheo[i + 1, j], count, globalMinZ, globalMaxZ);
                        Point p3 = GetIsometricProjection(i + 1, j + 1, _lastZDataTheo[i + 1, j + 1], count, globalMinZ, globalMaxZ);
                        Point p4 = GetIsometricProjection(i, j + 1, _lastZDataTheo[i, j + 1], count, globalMinZ, globalMaxZ);

                        if (_current3DMode == 1)
                        {
                            double avgZ = (_lastZDataTheo[i, j] + _lastZDataTheo[i + 1, j] + _lastZDataTheo[i + 1, j + 1] + _lastZDataTheo[i, j + 1]) / 4.0;
                            double normalizedZ = (avgZ - globalMinZ) / (globalMaxZ - globalMinZ);

                            Polygon polygonTheo = new Polygon
                            {
                                Points = new PointCollection { p1, p2, p3, p4 },
                                Fill = new SolidColorBrush(GetTheoreticalHeatmapColor(normalizedZ)),
                                Stroke = new SolidColorBrush(Color.FromArgb(120, 139, 92, 246)),
                                StrokeThickness = 0.6,
                                StrokeLineJoin = PenLineJoin.Round
                            };
                            _canvas3D.Children.Add(polygonTheo);
                        }
                        else
                        {
                            // Совмещенный "срез" — полупрозрачная фиолетовая каркасная сетка
                            Polygon polygonCut = new Polygon
                            {
                                Points = new PointCollection { p1, p2, p3, p4 },
                                Fill = new SolidColorBrush(Color.FromArgb(50, 139, 92, 246)),
                                Stroke = new SolidColorBrush(Color.FromArgb(220, 91, 33, 182)),
                                StrokeThickness = 1.3,
                                StrokeDashArray = new DoubleCollection { 3, 2 },
                                StrokeLineJoin = PenLineJoin.Round
                            };
                            _canvas3D.Children.Add(polygonCut);
                        }
                    }
                }
            }
        }

        private Color GetTheoreticalHeatmapColor(double value)
        {
            value = Math.Max(0, Math.Min(1, value));
            byte r = (byte)(76 + (6 - 76) * value);
            byte g = (byte)(29 + (182 - 29) * value);
            byte b = (byte)(149 + (212 - 149) * value);
            return Color.FromRgb(r, g, b);
        }

        private void DrawAxes(int count, double minZ, double maxZ)
        {
            Point p00 = GetIsometricProjection(0, 0, minZ, count, minZ, maxZ);
            Point pN0 = GetIsometricProjection(count - 1, 0, minZ, count, minZ, maxZ);
            Point pNN = GetIsometricProjection(count - 1, count - 1, minZ, count, minZ, maxZ);
            Point p0N = GetIsometricProjection(0, count - 1, minZ, count, minZ, maxZ);

            Polygon floor = new Polygon
            {
                Points = new PointCollection { p00, pN0, pNN, p0N },
                Fill = new SolidColorBrush(Color.FromArgb(12, 0, 0, 0)),
                Stroke = Brushes.LightGray,
                StrokeThickness = 1
            };
            _canvas3D.Children.Add(floor);

            double extCount = (count - 1) * 1.15;
            Point origin = p00;
            Point xAxis = GetIsometricProjection(extCount, 0, minZ, count, minZ, maxZ);
            Point yAxis = GetIsometricProjection(0, extCount, minZ, count, minZ, maxZ);

            double zExt = maxZ + (maxZ - minZ) * 0.2;
            Point zAxis = GetIsometricProjection(0, 0, zExt, count, minZ, maxZ);

            Line CreateLine(Point p1, Point p2, Brush color) =>
                new Line { X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y, Stroke = color, StrokeThickness = 2 };

            _canvas3D.Children.Add(CreateLine(origin, xAxis, Brushes.Crimson));
            _canvas3D.Children.Add(CreateLine(origin, yAxis, Brushes.SeaGreen));
            _canvas3D.Children.Add(CreateLine(origin, zAxis, Brushes.RoyalBlue));

            AddLabelToCanvas("Строки A (X)", xAxis, Brushes.Crimson, -45, 10);
            AddLabelToCanvas("Столбцы A (Y)", yAxis, Brushes.SeaGreen, 10, 10);
            AddLabelToCanvas("Время (Z)", zAxis, Brushes.RoyalBlue, -40, -25);
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
            double normX = x / (count - 1);
            double normY = y / (count - 1);
            double normZ = (z - minZ) / (maxZ - minZ);

            double angleX = Math.PI / 6;

            double isoX = normY + (normX * Math.Cos(angleX));
            double isoY = -(normX * Math.Sin(angleX)) - (normZ * 0.4);

            double canvasWidth = _canvas3D.Width;
            double canvasHeight = _canvas3D.Height;

            double finalX = (canvasWidth * 0.25) + (isoX * canvasWidth * 0.35);
            double finalY = (canvasHeight * 0.7) + (isoY * canvasHeight * 0.45);

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
                double t = (value - 0.75) / 0.25;
                r = 255; g = (byte)(255 * (1 - t)); b = 0;
            }

            return Color.FromRgb(r, g, b);
        }
    }
}