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

namespace Laba_1
{
    public partial class MainWindow : Window
    {
        private TextBox _txtStartN;
        private TextBox _txtEndN;
        private TextBox _txtStep;
        private TextBox _txtRuns;
        private Button _btnStart;
        private Button _btnStop;

        // Новые элементы управления для истории БД
        private ComboBox _cbHistory;
        private Button _btnLoadHistory;

        private CancellationTokenSource _cts;
        private ProgressBar _progressBar;

        private ComboBox _cbAlgorithms;

        // 2D график (с библиотекой LiveChartsCore) 
        private Grid _container2D = null!;
        private CartesianChart _chart2D = null!;
        private Axis _xAxis = null!;
        private Axis _yAxis = null!;
        private ScrollBar _chartScrollBar = null!;
        private TextBlock _lblZoomRange = null!;
        private double _dataMinN = 1;
        private double _dataMaxN = 500;
        private readonly ObservableCollection<ObservablePoint> _chartValues = new();
        private readonly ObservableCollection<ObservablePoint> _theoreticalValues = new(); // Коллекция для теоретических замеров

        // 3D график (Canvas) — для матричных операций
        private Canvas _canvas3D;
        private StackPanel _legendPanel3D;
        private TextBlock _txtMinZ;
        private TextBlock _txtMaxZ;
        private Button _btn3DLayer = null!;
        private double[,]? _lastZDataExp;
        private double[,]? _lastZDataTheo;
        private List<int>? _last3DNValues;
        private int _current3DMode = 0; // 0: Экспериментальный, 1: Теоретический, 2: Совмещенный (Срез)

        // DB & Cache UI Elements
        private DataGrid _dbDataGrid = null!;
        private TextBlock _lblDbStats = null!;

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

            _txtStartN = AddInputField("Старт (N):", "1", controlsPanel);
            _txtEndN = AddInputField("Конец (N):", "500", controlsPanel);
            _txtStep = AddInputField("Шаг (Step):", "10", controlsPanel);
            _txtRuns = AddInputField("Прогоны:", "10", controlsPanel);

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
                Text = "🔍 Навигация: колесико мыши (зум), зажатие ЛКМ (сдвиг) или кнопки справа 👉",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569")),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_lblZoomRange, Dock.Left);
            navDock.Children.Add(_lblZoomRange);

            StackPanel navButtons = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(navButtons, Dock.Right);

            Button btnSkipStart = CreateNavButton("⏩ Скрыть старт (первые 25%)", "#6366F1");
            btnSkipStart.Click += (s, e) => SkipFirst25Percent();
            navButtons.Children.Add(btnSkipStart);

            Button btnPanLeft = CreateNavButton("◀ Влево", "#64748B");
            btnPanLeft.Click += (s, e) => PanChart(-0.25);
            navButtons.Children.Add(btnPanLeft);

            Button btnPanRight = CreateNavButton("Вправо ▶", "#64748B");
            btnPanRight.Click += (s, e) => PanChart(0.25);
            navButtons.Children.Add(btnPanRight);

            Button btnZoomIn = CreateNavButton("🔍+ Приблизить", "#0EA5E9");
            btnZoomIn.Click += (s, e) => ZoomChart(0.7);
            navButtons.Children.Add(btnZoomIn);

            Button btnZoomOut = CreateNavButton("🔍- Отдалить", "#0EA5E9");
            btnZoomOut.Click += (s, e) => ZoomChart(1.4);
            navButtons.Children.Add(btnZoomOut);

            Button btnResetZoom = CreateNavButton("↺ Сбросить", "#10B981");
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

            // Отслеживание зума колесом мыши для синхронизации скроллбара
            _chart2D.PreviewMouseWheel += (s, e) =>
            {
                Dispatcher.InvokeAsync(UpdateScrollBarAndLabel, System.Windows.Threading.DispatcherPriority.Background);
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

        private TextBox AddInputField(string label, string defaultValue, StackPanel container)
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
            bool isMatrix3D = IsMatrixAlgorithm(_cbAlgorithms.SelectedIndex);

            if (_container2D != null) _container2D.Visibility = isMatrix3D ? Visibility.Collapsed : Visibility.Visible;
            if (_canvas3D.Parent is Grid container3D)
            {
                container3D.Visibility = isMatrix3D ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private bool IsMatrixAlgorithm(int index) => index == 8;

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
                for (int i = 0; i < endN; i++) maxDataInt[i] = rng.Next(0, 100);

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

                int[] intData = new int[endN];
                double[] doubleData = new double[endN];

                for (int n = startN; n <= endN; n += step)
                {
                    if (token.IsCancellationRequested) return;

                    // Очистка сборщика мусора перед точкой замера для устранения случайных пауз GC
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    int runs = runsCount;
                    double bestRunMs = double.MaxValue;

                    for (int r = 0; r < runs; r++)
                    {
                        Array.Copy(maxDataInt, intData, n);

                        for (int i = 0; i < n; i++) doubleData[i] = intData[i];

                        long startTicks = Stopwatch.GetTimestamp();
                        long steps = ExecuteAlgorithm(algorithmIndex, intData, doubleData, n);
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

            if (algorithmName.Contains("Bubble") || algorithmName.Contains("HasDuplicates") || algorithmName.Contains("Прямое вычисление"))
                return (n => n * n, "O(N²)");

            if (algorithmName.Contains("Quick") || algorithmName.Contains("TimSort") || algorithmName.Contains("Shell"))
                return (n => n * Math.Log2(Math.Max(n, 1)), "O(N log N)");

            if (algorithmName.Contains("быстрый") || algorithmName.Contains("бинарный") || algorithmName.Contains("PowBinary"))
                return (n => Math.Log2(Math.Max(n, 1)), "O(log N)");

            // По умолчанию O(N): Сумма, Произведение, Горнер, Разворот, Простой/Рекурсивный Pow и др.
            return (n => n, "O(N)");
        }

        // Построение теоретической линии поверх практических результатов
        private void CalculateTheoreticalCurve(string algorithmName, IEnumerable<ObservablePoint> actualPoints)
        {
            _theoreticalValues.Clear();
            var pointsList = actualPoints?.Where(p => p.X.HasValue && p.Y.HasValue).ToList();
            if (pointsList == null || pointsList.Count == 0) return;

            var (complexityFunc, label) = GetComplexityInfo(algorithmName);

            // Берём последнюю точку (максимальный N) для приведения к реальному масштабу времени (мс)
            var maxPoint = pointsList.OrderBy(p => p.X.Value).LastOrDefault();
            if (maxPoint == null) return;

            double maxN = maxPoint.X.Value;
            double maxY = maxPoint.Y.Value;
            double theoreticalMax = complexityFunc(maxN);

            if (theoreticalMax <= 0) theoreticalMax = 1;

            // Коэффициент масштабирования k = Y_реальное / Y_теоретическое
            double k = maxY / theoreticalMax;

            foreach (var point in pointsList.OrderBy(p => p.X.Value))
            {
                double n = point.X.Value;
                double idealTime = k * complexityFunc(n);
                _theoreticalValues.Add(new ObservablePoint(n, idealTime));
            }

            // Обновляем название теоретической серии
            if (_chart2D.Series.ElementAtOrDefault(1) is LineSeries<ObservablePoint> idealSeries)
            {
                idealSeries.Name = $"Идеальный {label}";
            }
        }

        // --- Методы навигации, масштабирования и прокрутки 2D графика ---

        private Button CreateNavButton(string text, string hexColor)
        {
            return new Button
            {
                Content = text,
                Height = 26,
                Margin = new Thickness(3, 0, 0, 0),
                Padding = new Thickness(8, 0, 8, 0),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Foreground = Brushes.White,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
        }

        // Быстрый пропуск первых 25% (убирает стартовый системный шум)
        private void SkipFirst25Percent()
        {
            double range = _dataMaxN - _dataMinN;
            if (range <= 0) return;

            double newMin = _dataMinN + range * 0.25;
            _xAxis.MinLimit = newMin;
            _xAxis.MaxLimit = _dataMaxN;
            UpdateScrollBarAndLabel();
        }

        // Прокрутка влево / вправо по оси X
        private void PanChart(double fraction)
        {
            double curMin = _xAxis.MinLimit ?? _dataMinN;
            double curMax = _xAxis.MaxLimit ?? _dataMaxN;
            double viewWidth = curMax - curMin;
            double totalRange = _dataMaxN - _dataMinN;
            if (viewWidth >= totalRange && fraction < 0) return;

            double shift = viewWidth * fraction;
            double newMin = curMin + shift;
            double newMax = curMax + shift;

            if (newMin < _dataMinN)
            {
                newMin = _dataMinN;
                newMax = newMin + viewWidth;
            }
            if (newMax > _dataMaxN)
            {
                newMax = _dataMaxN;
                newMin = newMax - viewWidth;
            }

            _xAxis.MinLimit = newMin;
            _xAxis.MaxLimit = newMax;
            UpdateScrollBarAndLabel();
        }

        // Приближение / отдаление по оси X
        private void ZoomChart(double factor)
        {
            double curMin = _xAxis.MinLimit ?? _dataMinN;
            double curMax = _xAxis.MaxLimit ?? _dataMaxN;
            double center = (curMin + curMax) / 2.0;
            double newHalfWidth = ((curMax - curMin) * factor) / 2.0;

            double newMin = center - newHalfWidth;
            double newMax = center + newHalfWidth;

            if (factor > 1.0 && (newMin <= _dataMinN && newMax >= _dataMaxN))
            {
                ResetChartZoom();
                return;
            }

            newMin = Math.Max(_dataMinN, newMin);
            newMax = Math.Min(_dataMaxN, newMax);

            if (newMax - newMin < 2) return; // Защита от чрезмерного приближения

            _xAxis.MinLimit = newMin;
            _xAxis.MaxLimit = newMax;
            UpdateScrollBarAndLabel();
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