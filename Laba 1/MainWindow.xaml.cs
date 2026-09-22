using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Laba_1
{
    public partial class MainWindow : Window
    {
        private TextBox _txtStartN;
        private TextBox _txtEndN;
        private TextBox _txtStep;
        private Button _btnStart;
        private Button _btnStop;

        private double[,] _lastZData;
        private int _lastCount;
        private int _lastStartN;
        private int _lastStep;

        private CheckBox _chkPractical;
        private CheckBox _chkTheoretical;

        // Элементы управления для работы с историей БД
        private ComboBox _cbHistory;
        private Button _btnLoadHistory;
        private DataGrid _dgResults; // Таблица для вывода записей из БД на UI

        private CancellationTokenSource _cts;
        private ProgressBar _progressBar;

        private ComboBox _cbAlgorithms;

        // 2D график (с библиотекой LiveChartsCore) 
        private CartesianChart _chart2D;
        private readonly ObservableCollection<ObservablePoint> _chartValues = new();
        private readonly ObservableCollection<ObservablePoint> _theoreticalValues = new(); // Коллекция для теоретических замеров

        // 3D график (Canvas) — для матричных операций
        private Canvas _canvas3D;
        private StackPanel _legendPanel3D;
        private TextBlock _txtMinZ;
        private TextBlock _txtMaxZ;

        public MainWindow()
        {
            InitializeComponent();
            InitLayerControls();

            Title = "Анализ сложности алгоритмов (с БД PostgreSQL)";
            Width = 1350;
            Height = 750;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            Content = BuildInterface();

            Loaded += MainWindow_Loaded;
        }

        private void InitLayerControls()
        {
            _chkPractical = new CheckBox
            {
                Content = "Практический (Heatmap)",
                IsChecked = true,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                Margin = new Thickness(0, 0, 15, 0)
            };
            _chkPractical.Click += OnLayerToggle_Click;

            _chkTheoretical = new CheckBox
            {
                Content = "Теоретический O(N³)",
                IsChecked = true,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Crimson
            };
            _chkTheoretical.Click += OnLayerToggle_Click;
        }

        private void OnLayerToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_lastZData != null)
            {
                DrawMatrix3DSurface(_lastZData, _lastCount, _lastStartN, _lastStep);
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await AppDbContext.InitDatabaseAsync();
                await LoadHistoryAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения к PostgreSQL: {ex.Message}\nПроверьте строку подключения в AppDbContext.cs",
                                "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Считывает историю замеров из БД и обновляет выпадающий список _cbHistory
        /// </summary>
        public async Task LoadHistoryAsync()
        {
            try
            {
                var sessions = await AppDbContext.GetExperimentSessionsAsync();
                _cbHistory.ItemsSource = sessions;

                if (sessions != null && sessions.Count > 0)
                {
                    _cbHistory.SelectedIndex = 0;
                }
                else
                {
                    _cbHistory.ItemsSource = null;
                    _cbHistory.SelectedIndex = -1;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки истории: {ex.Message}");
                _cbHistory.ItemsSource = null;
                _cbHistory.SelectedIndex = -1;
            }
        }

        private UIElement BuildInterface()
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
            _txtEndN = AddInputField("Конец (N):", "100", controlsPanel);
            _txtStep = AddInputField("Шаг (Step):", "10", controlsPanel);

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
                Width = 320,
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

            // Создание вкладок для разделения графиков и таблицы БД на UI
            TabControl tabControl = new TabControl();

            // Вкладка 1: Визуализация (2D / 3D графики)
            TabItem tabCharts = new TabItem { Header = " График " };
            Grid displayGrid = new Grid();

            _chart2D = new CartesianChart
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
                        GeometrySize = 0, // Без кружков, только линия
                        Stroke = new SolidColorPaint(SKColors.Crimson) { StrokeThickness = 2 },
                        EnableNullSplitting = false
                    }
                },
                XAxes = new Axis[] { new Axis { Name = "Размер N" } },
                YAxes = new Axis[] { new Axis { Name = "Время (мс)" } },
                Visibility = Visibility.Visible
            };
            displayGrid.Children.Add(_chart2D);

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

            displayGrid.Children.Add(container3D);
            tabCharts.Content = displayGrid;
            tabControl.Items.Add(tabCharts);

            // Вкладка 2: Вывод детальных результатов из базы данных (DataGrid)
            TabItem tabData = new TabItem { Header = " Таблица замеров (БД) " };

            _dgResults = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                CanUserSortColumns = true,
                Background = Brushes.White
            };

            // Колонки таблицы
            _dgResults.Columns.Add(new DataGridTextColumn { Header = "Запуск №", Binding = new Binding("RunNumber") });
            _dgResults.Columns.Add(new DataGridTextColumn { Header = "Размер N", Binding = new Binding("N") });
            _dgResults.Columns.Add(new DataGridTextColumn { Header = "Время (мс)", Binding = new Binding("ExecutionTimeMs") { StringFormat = "{0:F4}" } });
            _dgResults.Columns.Add(new DataGridTextColumn { Header = "Шаги (StepCount)", Binding = new Binding("StepCount") });
            _dgResults.Columns.Add(new DataGridTextColumn { Header = "Дата эксперимента", Binding = new Binding("ExperimentDate") { StringFormat = "{0:dd.MM.yyyy HH:mm:ss}" } });

            tabData.Content = _dgResults;
            tabControl.Items.Add(tabData);

            Grid.SetRow(tabControl, 2);
            mainGrid.Children.Add(tabControl);

            return mainGrid;
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _cts?.Cancel();
            }
            catch (ObjectDisposedException) { }

            _btnStop.IsEnabled = false;

            if (_progressBar != null) _progressBar.Visibility = Visibility.Hidden;
            if (_btnStart != null) _btnStart.IsEnabled = true;
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

                // Вывод списка всех замеров текущей сессии в DataGrid на UI
                _dgResults.ItemsSource = records;

                _chartValues.Clear();
                _theoreticalValues.Clear();

                // Переключение отображения (2D/3D)
                bool is3D = selectedSession.AlgorithmName.Contains("3D") || selectedSession.AlgorithmName.Contains("матриц");
                _chart2D.Visibility = is3D ? Visibility.Collapsed : Visibility.Visible;
                if (_canvas3D.Parent is Grid container3D)
                {
                    container3D.Visibility = is3D ? Visibility.Visible : Visibility.Collapsed;
                }

                if (!is3D)
                {
                    var aggregatedPoints = records
                        .GroupBy(r => r.N)
                        .OrderBy(g => g.Key)
                        .Select(g => new ObservablePoint(g.Key, g.Average(r => r.ExecutionTimeMs)))
                        .ToList();

                    foreach (var pt in aggregatedPoints)
                    {
                        _chartValues.Add(pt);
                    }

                    CalculateTheoreticalCurve(selectedSession.AlgorithmName, aggregatedPoints);
                }
                else
                {
                    var distinctN = records.Select(r => r.N).Distinct().OrderBy(n => n).ToList();
                    int count = distinctN.Count;

                    if (count > 0)
                    {
                        int startN = distinctN.First();
                        int step = count > 1 ? distinctN[1] - distinctN[0] : 1;

                        double[,] zData = new double[count, count];
                        var recordsDict = records.ToDictionary(r => r.N, r => r.ExecutionTimeMs);

                        for (int i = 0; i < count; i++)
                        {
                            for (int j = 0; j < count; j++)
                            {
                                int currentN = startN + i * step;
                                if (recordsDict.TryGetValue(currentN, out double val))
                                {
                                    zData[i, j] = val;
                                }
                            }
                        }

                        DrawMatrix3DSurface(zData, count, startN, step);
                    }
                }

                MessageBox.Show($"График и таблица успешно восстановлены из БД!\nФункция: {selectedSession.AlgorithmName}\nЗаписей: {records.Count}",
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

            _chart2D.Visibility = isMatrix3D ? Visibility.Collapsed : Visibility.Visible;
            if (_canvas3D.Parent is Grid container3D)
            {
                container3D.Visibility = isMatrix3D ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private bool IsMatrixAlgorithm(int index) => index == 8;

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            // 1. Проверка правильности заполнения полей ввода
            if (!int.TryParse(_txtStartN.Text, out int startN) ||
                !int.TryParse(_txtEndN.Text, out int endN) ||
                !int.TryParse(_txtStep.Text, out int step) || step <= 0 || startN < 0 || endN < startN)
            {
                MessageBox.Show("Заполните корректно параметры диапазона.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int selectedIndex = _cbAlgorithms.SelectedIndex;
            if (selectedIndex < 0)
            {
                MessageBox.Show("Выберите алгоритм для запуска.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string selectedAlgName = _cbAlgorithms.SelectedItem.ToString();
            bool loadFromDbOnly = false;

            try
            {
                // 2. Загружаем сохраненные сессии из БД
                var sessions = await AppDbContext.GetExperimentSessionsAsync();

                // 3. Ищем замер, где совпадают ВСЕ 4 параметра: AlgorithmName, StartN, EndN и Step
                var existingSession = sessions?.FirstOrDefault(s =>
                    s.AlgorithmName == selectedAlgName &&
                    s.StartN == startN &&
                    s.EndN == endN &&
                    s.Step == step);

                // 4. Показываем диалог ТОЛЬКО если найден замер с полностью совпадающими входными данными
                if (existingSession != null)
                {
                    var choice = MessageBox.Show(
                        $"В базе данных найден сохраненный замер с аналогичными параметрами:\n\n" +
                        $"• Алгоритм: {selectedAlgName}\n" +
                        $"• Диапазон N: {startN} .. {endN} (шаг {step})\n" +
                        $"• Дата замера: {existingSession.CreatedAt:dd.MM.yyyy HH:mm}\n\n" +
                        "[Да] — Загрузить готовый результат из БД\n" +
                        "[Нет] — Выполнить новый перерасчет\n" +
                        "[Отмена] — Отменить действие",
                        "Замер найден в базе",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (choice == MessageBoxResult.Cancel)
                        return;

                    if (choice == MessageBoxResult.Yes)
                    {
                        loadFromDbOnly = true;

                        // Переключаем выбор в выпадающем списке истории на конкретно найденную сессию по Id
                        if (_cbHistory != null && _cbHistory.Items.Count > 0)
                        {
                            var matchingHistoryItem = _cbHistory.Items
                                .Cast<ExperimentSession>()
                                .FirstOrDefault(x => x.Id == existingSession.Id);

                            if (matchingHistoryItem != null)
                            {
                                _cbHistory.SelectedItem = matchingHistoryItem;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при проверке кэша: {ex.Message}");
            }

            // Если нажат "Да" — загружаем найденную сессию и выходим
            if (loadFromDbOnly)
            {
                BtnLoadHistory_Click(sender, e);
                return;
            }

            // 5. Выполнение нового замера (если замера в БД не было или нажат "Нет")
            _cts = new CancellationTokenSource();
            _btnStart.IsEnabled = false;
            _btnStop.IsEnabled = true;
            _progressBar.Visibility = Visibility.Visible;
            _progressBar.IsIndeterminate = true;

            try
            {
                List<BenchmarkResult> dbResults;

                if (IsMatrixAlgorithm(selectedIndex))
                {
                    dbResults = await RunMatrix3DBenchmarkAsync(startN, endN, step, true, _cts.Token);
                }
                else
                {
                    dbResults = await RunArray2DBenchmarkAsync(selectedIndex, startN, endN, step, true, _cts.Token);
                }

                if (_cts != null && !_cts.IsCancellationRequested && dbResults != null && dbResults.Count > 0)
                {
                    _dgResults.ItemsSource = dbResults;

                    await AppDbContext.SaveResultsAsync(dbResults, selectedAlgName, startN, endN, step);
                    await LoadHistoryAsync();

                    MessageBox.Show("Замер успешно выполнен и сохранен в базу данных!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
        private async Task<List<BenchmarkResult>> RunMatrix3DBenchmarkAsync(int startN, int endN, int step, bool bypassCache, CancellationToken token)
        {
            _canvas3D.Children.Clear();
            _legendPanel3D.Visibility = Visibility.Hidden;

            int count = ((endN - startN) / step) + 1;
            double[,] zData = new double[count, count];
            List<BenchmarkResult> newDbResults = new List<BenchmarkResult>();
            string algName = _cbAlgorithms.SelectedItem.ToString();
            DateTime experimentTimestamp = DateTime.UtcNow;

            await Task.Run(async () =>
            {
                for (int i = 0; i < count; i++)
                {
                    if (token.IsCancellationRequested) break;
                    int rowsA = startN + i * step;

                    for (int j = 0; j < count; j++)
                    {
                        if (token.IsCancellationRequested) break;
                        int colsA = startN + j * step;
                        int colsB = rowsA;

                        if (!bypassCache)
                        {
                            var cached = await AppDbContext.GetCachedResultsAsync(algName, rowsA);
                            if (cached.Count > 0)
                            {
                                zData[i, j] = cached.Average(r => r.ExecutionTimeMs);
                                continue;
                            }
                        }

                        Random rnd = new Random(Guid.NewGuid().GetHashCode());
                        double[,] A = Algorithms.GenerateMatrix(rowsA, colsA, rnd);
                        double[,] B = Algorithms.GenerateMatrix(colsA, colsB, rnd);

                        long startTicks = Stopwatch.GetTimestamp();
                        Algorithms.Multiply(A, B);
                        long endTicks = Stopwatch.GetTimestamp();

                        double elapsedMs = (endTicks - startTicks) * 1000.0 / Stopwatch.Frequency;
                        zData[i, j] = elapsedMs;

                        lock (newDbResults)
                        {
                            newDbResults.Add(new BenchmarkResult
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
                }
            }, token);

            if (token.IsCancellationRequested) return null;

            DrawMatrix3DSurface(zData, count, startN, step);
            return newDbResults;
        }

        private async Task<List<BenchmarkResult>> RunArray2DBenchmarkAsync(int algorithmIndex, int startN, int endN, int step, bool bypassCache, CancellationToken token)
        {
            _chartValues.Clear();
            _theoreticalValues.Clear();
            string algName = _cbAlgorithms.SelectedItem.ToString();
            List<BenchmarkResult> newDbResults = new List<BenchmarkResult>();
            DateTime experimentTimestamp = DateTime.UtcNow;

            bool isStepBased = algorithmIndex >= 12 && algorithmIndex <= 14;

            await Task.Run(async () =>
            {
                int[] maxDataInt = new int[endN];
                Random rng = new Random();
                for (int i = 0; i < endN; i++) maxDataInt[i] = rng.Next(0, 100);

                Thread.CurrentThread.Priority = ThreadPriority.Highest;

                int warmupSize = Math.Min(1000, endN);
                int[] warmupInt = maxDataInt.AsSpan(0, warmupSize).ToArray();
                double[] warmupDouble = warmupInt.Select(x => (double)x).ToArray();

                for (int i = 0; i < 10; i++)
                {
                    if (token.IsCancellationRequested) break;
                    ExecuteAlgorithm(algorithmIndex, warmupInt, warmupDouble, warmupSize);
                }

                List<ObservablePoint> results = new List<ObservablePoint>();

                GC.Collect();
                GC.WaitForPendingFinalizers();

                int[] intData = new int[endN];
                double[] doubleData = new double[endN];

                for (int n = startN; n <= endN; n += step)
                {
                    if (token.IsCancellationRequested) return;

                    if (!bypassCache)
                    {
                        var cachedRecords = await AppDbContext.GetCachedResultsAsync(algName, n);
                        if (cachedRecords.Count > 0)
                        {
                            if (isStepBased)
                            {
                                results.Add(new ObservablePoint(n, cachedRecords.First().StepCount ?? 0));
                            }
                            else
                            {
                                results.Add(new ObservablePoint(n, cachedRecords.Average(r => r.ExecutionTimeMs)));
                            }
                            continue;
                        }
                    }

                    const int runs = 5;
                    long totalTicks = 0;
                    long lastSteps = 0;

                    for (int r = 0; r < runs; r++)
                    {
                        Array.Copy(maxDataInt, intData, n);
                        for (int i = 0; i < n; i++) doubleData[i] = intData[i];

                        long startTicks = Stopwatch.GetTimestamp();
                        long steps = ExecuteAlgorithm(algorithmIndex, intData, doubleData, n);
                        long endTicks = Stopwatch.GetTimestamp();

                        lastSteps = steps;
                        long elapsedTicks = endTicks - startTicks;
                        totalTicks += elapsedTicks;

                        double singleRunMs = (elapsedTicks * 1000.0) / Stopwatch.Frequency;

                        newDbResults.Add(new BenchmarkResult
                        {
                            AlgorithmName = algName,
                            N = n,
                            RunNumber = r + 1,
                            ExecutionTimeMs = singleRunMs,
                            StepCount = isStepBased ? steps : null,
                            ExperimentDate = experimentTimestamp
                        });
                    }

                    if (isStepBased)
                    {
                        results.Add(new ObservablePoint(n, lastSteps));
                    }
                    else
                    {
                        double avgMs = ((double)totalTicks / runs * 1000.0) / Stopwatch.Frequency;
                        results.Add(new ObservablePoint(n, avgMs));
                    }
                }

                Thread.CurrentThread.Priority = ThreadPriority.Normal;

                Dispatcher.Invoke(() =>
                {
                    if (_chart2D.YAxes.FirstOrDefault() is Axis yAxis)
                    {
                        yAxis.Name = isStepBased ? "Количество операций (шагов)" : "Время (мс)";
                    }

                    foreach (var pt in results) _chartValues.Add(pt);

                    CalculateTheoreticalCurve(algName, results);
                });
            }, token);

            if (token.IsCancellationRequested) return null;

            return newDbResults;
        }

        private long ExecuteAlgorithm(int algorithmIndex, int[] intData, double[] doubleData, int n)
        {
            switch (algorithmIndex)
            {
                case 0:
                    Algorithms.Constant(intData, n);
                    return 1;

                case 1:
                    Algorithms.Sum(intData, n);
                    return n;

                case 2:
                    Algorithms.Product(intData, n);
                    return n;

                case 3:
                    Algorithms.PolyNaive(doubleData, 1.5, n);
                    return (long)n * n;

                case 4:
                    Algorithms.PolyHorner(doubleData, 1.5, n);
                    return n;

                case 5:
                    {
                        double[] copy = new double[n];
                        Array.Copy(doubleData, copy, n);
                        Algorithms.BubbleSort(copy, n);
                        return (long)n * n;
                    }

                case 6:
                    {
                        double[] copy = new double[n];
                        Array.Copy(doubleData, copy, n);
                        Algorithms.QuickSort(copy, n);
                        return (long)(n * Math.Log2(n));
                    }

                case 7:
                    {
                        double[] copy = new double[n];
                        Array.Copy(doubleData, copy, n);
                        Algorithms.TimSort(copy, n);
                        return (long)(n * Math.Log2(n));
                    }

                case 9:
                    Algorithms.HasDuplicates(intData, n);
                    return n;

                case 10:
                    {
                        int[] copy = new int[n];
                        Array.Copy(intData, copy, n);
                        Algorithms.ReverseArray(copy, n);
                        return n / 2;
                    }

                case 11:
                    {
                        int[] copy = new int[n];
                        Array.Copy(intData, copy, n);
                        Algorithms.ShellSort(copy, n);
                        return (long)(n * Math.Log2(n));
                    }

                case 12:
                    Algorithms.ResetSteps();
                    Algorithms.PowIterative(1.0001, n);
                    return Algorithms.StepCount;

                case 13:
                    Algorithms.ResetSteps();
                    Algorithms.PowRecursiveSafe(1.0001, n);
                    return Algorithms.StepCount;

                case 14:
                    Algorithms.ResetSteps();
                    Algorithms.PowBinary(1.0001, n);
                    return Algorithms.StepCount;

                default:
                    return -1;
            }
        }

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

            return (n => n, "O(N)");
        }

        private void CalculateTheoreticalCurve(string algorithmName, IEnumerable<ObservablePoint> actualPoints)
        {
            _theoreticalValues.Clear();
            var pointsList = actualPoints?.Where(p => p.X.HasValue && p.Y.HasValue).ToList();
            if (pointsList == null || pointsList.Count == 0) return;

            var (complexityFunc, label) = GetComplexityInfo(algorithmName);

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

            if (_chart2D.Series.ElementAtOrDefault(1) is LineSeries<ObservablePoint> idealSeries)
            {
                idealSeries.Name = $"Идеальный {label}";
            }
        }

        private void DrawMatrix3DSurface(double[,] zData, int count, int startN, int step)
        {
            _lastZData = zData;
            _lastCount = count;
            _lastStartN = startN;
            _lastStep = step;

            _canvas3D.Children.Clear();

            if (_chkPractical != null && _chkTheoretical != null)
            {
                if (_chkPractical.Parent is Panel oldParent1)
                {
                    oldParent1.Children.Remove(_chkPractical);
                }
                if (_chkTheoretical.Parent is Panel oldParent2)
                {
                    oldParent2.Children.Remove(_chkTheoretical);
                }

                StackPanel innerPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal
                };
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
            _legendPanel3D.Visibility = Visibility.Visible;

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
                double t = 255;
                r = 255; g = (byte)(255 * (1 - (value - 0.75) / 0.25)); b = 0;
            }

            return Color.FromRgb(r, g, b);
        }
    }
}