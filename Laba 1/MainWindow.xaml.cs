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

        private CheckBox _chkPractical;
        private CheckBox _chkTheoretical;

        // Элементы управления для работы с историей БД
        private ComboBox _cbHistory;
        private Button _btnLoadHistory;
        private DataGrid _dgResults; // Таблица для вывода записей из БД на UI

        private CancellationTokenSource _cts;
        private ProgressBar _progressBar;

        private ComboBox _cbAlgorithms;

        private Chart2DService _chart2DService;
        private Chart3DService _chart3DService;

        public MainWindow()
        {
            InitializeComponent();
            InitLayerControls();
            
            _chart2DService = new Chart2DService();
            _chart3DService = new Chart3DService(_chkPractical, _chkTheoretical);

            _ = AppDbContext.InitDatabaseAsync();

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
            _chart3DService.RedrawLast();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await AppDbContext.InitDatabaseAsync();
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

        // Считывает историю замеров из БД и обновляет выпадающий список _cbHistory

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

            // Подписываемся на событие до установки индекса
            _cbAlgorithms.SelectionChanged += CbAlgorithms_SelectionChanged;
            _cbAlgorithms.SelectedIndex = 0; // Теперь при старте приложения поля разблокируются корректно

            controlsPanel.Children.Add(_cbAlgorithms);

            _txtStartN = UiComponentsFactory.AddInputField("Старт (N):", "1", controlsPanel);
            _txtEndN = UiComponentsFactory.AddInputField("Конец (N):", "100", controlsPanel);
            _txtStep = UiComponentsFactory.AddInputField("Шаг (Step):", "10", controlsPanel);

            // Кнопка "Начать замер"
            _btnStart = UiComponentsFactory.CreateActionButton(" Начать замер ", BtnStart_Click);
            controlsPanel.Children.Add(_btnStart);

            // Кнопка "Стоп"
            _btnStop = UiComponentsFactory.CreateActionButton(" Стоп ", BtnStop_Click, isEnabled: false);
            _btnStop.Margin = new Thickness(6, 0, 0, 0); // Небольшой отступ от кнопки "Старт"
            controlsPanel.Children.Add(_btnStop);

            // Вертикальный разделитель
            controlsPanel.Children.Add(UiComponentsFactory.CreateVerticalSeparator());

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
            _btnLoadHistory = UiComponentsFactory.CreateActionButton(" Загрузить график ", BtnLoadHistory_Click);
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

            // Подключаем 2D график из сервиса
            displayGrid.Children.Add(_chart2DService.Chart);

            Grid container3D = new Grid { Visibility = Visibility.Collapsed };

            // Подключаем Canvas типа 3D график
            container3D.Children.Add(_chart3DService.Canvas);
            container3D.Children.Add(_chart3DService.LegendPanel);

            displayGrid.Children.Add(container3D);
            tabCharts.Content = displayGrid;
            tabControl.Items.Add(tabCharts);

            // Вкладка 2: Вывод детальных результатов из базы данных (DataGrid)
            TabItem tabData = new TabItem { Header = " Таблица замеров (БД) " };

            // Используем фабрику вместо ручной сборки DataGrid
            _dgResults = UiComponentsFactory.CreateResultsDataGrid();

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

                _chart2DService.Clear();

                // 1. Определение типа графика
                bool is3D = selectedSession.AlgorithmName.Contains("3D") || selectedSession.AlgorithmName.Contains("матриц");

                // 2. Находим TabControl и принудительно переключаем на вкладку с графиками (индекс 0)
                if (Content is Grid mainGrid)
                {
                    var tabControl = mainGrid.Children.OfType<TabControl>().FirstOrDefault();
                    if (tabControl != null)
                    {
                        tabControl.SelectedIndex = 0; // Вкладка "График"
                    }
                }

                // 3. Устанавливаем видимость контейнеров 2D и 3D
                _chart2DService.Chart.Visibility = is3D ? Visibility.Collapsed : Visibility.Visible;
                if (_chart3DService.Canvas.Parent is Grid container3D)
                {
                    container3D.Visibility = is3D ? Visibility.Visible : Visibility.Collapsed;
                }

                // 4. Задерживаем наполнение данных до момента, когда WPF обновит Layout (разметку)
                await Dispatcher.InvokeAsync(() =>
                {
                    if (!is3D)
                    {
                        var aggregatedPoints = records
                            .GroupBy(r => r.N)
                            .OrderBy(g => g.Key)
                            .Select(g => new ObservablePoint(g.Key, g.Average(r => r.ExecutionTimeMs)))
                            .ToList();

                        foreach (var pt in aggregatedPoints)
                        {
                            _chart2DService.ChartValues.Add(pt);
                        }

                        _chart2DService.CalculateTheoreticalCurve(selectedSession.AlgorithmName, aggregatedPoints);
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

                            // Группируем результаты по N (строки i)
                            var groupedByN = records
                                .GroupBy(r => r.N)
                                .ToDictionary(g => g.Key, g => g.ToList());

                            for (int i = 0; i < count; i++)
                            {
                                int currentN = startN + i * step;

                                if (groupedByN.TryGetValue(currentN, out var rowList))
                                {
                                    // Записи одной строки i упорядочиваем по порядку следования j
                                    for (int j = 0; j < count && j < rowList.Count; j++)
                                    {
                                        zData[i, j] = rowList[j].ExecutionTimeMs;
                                    }
                                }
                            }

                            _chart3DService.DrawSurface(zData, count, startN, step);
                        }
                    }
                }, System.Windows.Threading.DispatcherPriority.Render);

                MessageBox.Show($"График и таблица успешно восстановлены из БД!\nФункция: {selectedSession.AlgorithmName}\nЗаписей: {records.Count}",
                                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных из БД: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CbAlgorithms_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Защита от NullReferenceException во время инициализации UI
            if (_cbAlgorithms == null || _chart2DService == null || _txtStartN == null || _txtStep == null)
                return;

            int index = _cbAlgorithms.SelectedIndex;
            if (index < 0) return;

            bool isMatrix3D = IsMatrixAlgorithm(index);

            // Управление отображением 2D / 3D графиков
            _chart2DService.Chart.Visibility = isMatrix3D ? Visibility.Collapsed : Visibility.Visible;
            if (_chart3DService.Canvas?.Parent is Grid container3D)
            {
                container3D.Visibility = isMatrix3D ? Visibility.Visible : Visibility.Collapsed;
            }

            // Алгоритмы степеней (индексы 12, 13, 14 в ComboBox)
            bool isPowerAlgorithm = index >= 12 && index <= 14;

            if (isPowerAlgorithm)
            {
                _txtStartN.IsEnabled = false;
                _txtStartN.Text = "1";

                _txtStep.IsEnabled = false;
                _txtStep.Text = "1";
            }
            else
            {
                _txtStartN.IsEnabled = true;
                _txtStep.IsEnabled = true;
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
            _chart3DService.Clear();

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

            _chart3DService.DrawSurface(zData, count, startN, step);
            return newDbResults;
        }

        private async Task<List<BenchmarkResult>> RunArray2DBenchmarkAsync(int algorithmIndex, int startN, int endN, int step, bool bypassCache, CancellationToken token)
        {
            _chart2DService.Clear();
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
                    AlgorithmExecutor.Execute(algorithmIndex, warmupInt, warmupDouble, warmupSize);
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
                        long steps = AlgorithmExecutor.Execute(algorithmIndex, intData, doubleData, n);
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
                    _chart2DService.SetYAxisTitle(isStepBased ? "Количество операций (шагов)" : "Время (мс)");

                    foreach (var pt in results) _chart2DService.ChartValues.Add(pt);

                    _chart2DService.CalculateTheoreticalCurve(algName, results);
                });
            }, token);

            if (token.IsCancellationRequested) return null;

            return newDbResults;
        }
    }
}