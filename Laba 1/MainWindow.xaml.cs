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
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Laba_1
{
    public class CompareItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public bool IsFromDb { get; set; }
        public ExperimentSession? DbSession { get; set; }
        public int AlgorithmIndex { get; set; }
        public override string ToString() => DisplayName;
    }

    public partial class MainWindow : Window
    {
        private TextBox _txtStartN = null!;
        private TextBox _txtEndN = null!;
        private TextBox _txtStep = null!;
        private TextBox _txtRuns = null!;
        private Button _btnStart = null!;
        private Button _btnStop = null!;

        private CheckBox _chkPractical = null!;
        private CheckBox _chkTheoretical = null!;

        // Элементы управления для работы с историей БД
        private ComboBox _cbHistory = null!;
        private Button _btnLoadHistory = null!;
        private DataGrid _dgResults = null!; // Таблица для вывода записей из БД на UI

        private CancellationTokenSource? _cts;
        private ProgressBar _progressBar = null!;

        private ComboBox _cbAlgorithms = null!;

        private Chart2DService _chart2DService = null!;
        private Chart3DService _chart3DService = null!;
        private TextBlock _lblZoomRange = null!;

        // Элементы для вкладки сравнения алгоритмов
        private ListBox _compareAvailableListBox = null!;
        private ComboBox _compareCachedSessionsComboBox = null!;
        private ListBox _compareSelectedListBox = null!;
        private CheckBox _chkCompareTheoretical = null!;
        private TextBox _txtCompareStartN = null!;
        private TextBox _txtCompareEndN = null!;
        private TextBox _txtCompareStep = null!;
        private TextBox _txtCompareRuns = null!;
        private Button _btnRunCompare = null!;
        private Button _btnStopCompare = null!;
        private Button _btnLoadCompareFromDb = null!;
        private ProgressBar _compareProgressBar = null!;
        private CartesianChart _compareChart = null!;
        private Axis _compareXAxis = null!;
        private Axis _compareYAxis = null!;
        private TextBlock _lblCompareStatus = null!;
        private TextBlock _lblCompareZoom = null!;
        private double _compareDataMinN = 100;
        private double _compareDataMaxN = 2000;
        private CancellationTokenSource? _compareCts;
        private Point? _compareDragStartPoint = null;
        private double _compareDragStartMin = 0;
        private double _compareDragStartMax = 0;
        private TabControl _mainTabControl = null!;

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

        private void OnTouchpadHorizontalScroll(short delta)
        {
            if (_mainTabControl == null) return;

            if (_mainTabControl.SelectedIndex == 0 && _chart2DService?.Chart != null && _chart2DService.Chart.IsVisible)
            {
                _chart2DService.ScrollByDelta(delta);
            }
            else if (_mainTabControl.SelectedIndex == 1 && _compareChart != null && _compareChart.IsVisible)
            {
                ScrollCompareChartByDelta(delta);
            }
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

        // Считывает историю замеров из БД и обновляет выпадающий список _cbHistory и сессии сравнения
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

                if (_compareCachedSessionsComboBox != null)
                {
                    _compareCachedSessionsComboBox.ItemsSource = sessions;
                    if (sessions != null && sessions.Count > 0)
                    {
                        _compareCachedSessionsComboBox.SelectedIndex = 0;
                    }
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

            _cbAlgorithms.SelectionChanged += CbAlgorithms_SelectionChanged;

            controlsPanel.Children.Add(_cbAlgorithms);

            _txtStartN = UiComponentsFactory.AddInputField("Старт (N):", "10", controlsPanel);
            _txtEndN = UiComponentsFactory.AddInputField("Конец (N):", "1000", controlsPanel);
            _txtStep = UiComponentsFactory.AddInputField("Шаг (Step):", "50", controlsPanel);
            _txtRuns = UiComponentsFactory.AddInputField("Прогоны:", "5", controlsPanel);

            _cbAlgorithms.SelectedIndex = 0; // Инициализация значений
            ApplyDefaultAlgorithmParameters(0);

            // Кнопка "Начать замер"
            _btnStart = UiComponentsFactory.CreateActionButton(" Начать замер ", BtnStart_Click);
            controlsPanel.Children.Add(_btnStart);

            // Кнопка "Стоп"
            _btnStop = UiComponentsFactory.CreateActionButton(" Стоп ", BtnStop_Click, isEnabled: false);
            _btnStop.Margin = new Thickness(6, 0, 0, 0);
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

            // Создание вкладок для разделения графиков, сравнения и таблицы БД
            _mainTabControl = new TabControl
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"))
            };

            // Вкладка 1: Визуализация (2D / 3D графики)
            TabItem tabCharts = new TabItem { Header = "📊 2D & 3D Замеры и Графики", FontWeight = FontWeights.Bold };
            Grid chartContainerGrid = new Grid();
            chartContainerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            chartContainerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Верхняя навигационная панель зума и панорамирования
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
                Text = "🖐️ Тачпад: щипок (pinch) — зум, 2 пальца влево/вправо — скролл | Или перетаскивание мышью",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569")),
                VerticalAlignment = VerticalAlignment.Center
            };
            _chart2DService.ZoomStatusChanged += (msg) => _lblZoomRange.Text = msg;
            DockPanel.SetDock(_lblZoomRange, Dock.Left);
            navDock.Children.Add(_lblZoomRange);

            Button btnResetZoom = new Button
            {
                Content = " ↺ Сбросить масштаб ",
                Height = 26,
                Padding = new Thickness(12, 0, 12, 0),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = Brushes.White,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnResetZoom.Click += (s, e) => _chart2DService.ResetZoom();
            DockPanel.SetDock(btnResetZoom, Dock.Right);
            navDock.Children.Add(btnResetZoom);
            navBorder.Child = navDock;

            Grid.SetRow(navBorder, 0);
            chartContainerGrid.Children.Add(navBorder);

            Grid displayGrid = new Grid();
            displayGrid.Children.Add(_chart2DService.Chart);

            Grid container3D = new Grid { Visibility = Visibility.Collapsed };
            container3D.Children.Add(_chart3DService.Canvas);
            container3D.Children.Add(_chart3DService.LegendPanel);
            displayGrid.Children.Add(container3D);

            Grid.SetRow(displayGrid, 1);
            chartContainerGrid.Children.Add(displayGrid);

            tabCharts.Content = chartContainerGrid;
            _mainTabControl.Items.Add(tabCharts);

            // Вкладка 2: Сравнение алгоритмов (с поддержкой выбора из БД, живого замера и теоретических линий)
            TabItem tabCompare = new TabItem { Header = "⚖️ Сравнение алгоритмов", FontWeight = FontWeights.Bold };
            tabCompare.Content = BuildComparisonTabContent();
            _mainTabControl.Items.Add(tabCompare);

            // Вкладка 3: Вывод детальных результатов из базы данных (чистый DataGrid)
            TabItem tabData = new TabItem { Header = "🗄️ Таблица замеров (БД)", FontWeight = FontWeights.Bold };
            _dgResults = UiComponentsFactory.CreateResultsDataGrid();
            tabData.Content = _dgResults;
            _mainTabControl.Items.Add(tabData);

            Grid.SetRow(_mainTabControl, 2);
            mainGrid.Children.Add(_mainTabControl);

            return mainGrid;
        }

        private UIElement BuildComparisonTabContent()
        {
            Grid mainGrid = new Grid { Margin = new Thickness(8) };
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(390) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Левая панель
            GroupBox controlBox = new GroupBox
            {
                Header = " Настройки сравнения алгоритмов ",
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 10, 0)
            };

            ScrollViewer leftScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel leftPanel = new StackPanel();

            leftPanel.Children.Add(new TextBlock
            {
                Text = "💡 Добавляйте алгоритмы для живого замера или выбирайте готовые замеры из кэша БД:",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8),
                FontWeight = FontWeights.Normal
            });

            // Блок 1: Живой замер
            Expander expLive = new Expander
            {
                Header = "▶ 1. Живой замер алгоритмов",
                IsExpanded = true,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            StackPanel pnlLive = new StackPanel { Margin = new Thickness(4) };

            _compareAvailableListBox = new ListBox
            {
                Height = 110,
                SelectionMode = SelectionMode.Single,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            };
            // 2D алгоритмы
            _compareAvailableListBox.Items.Add("1. Постоянная функция");
            _compareAvailableListBox.Items.Add("2. Сумма элементов");
            _compareAvailableListBox.Items.Add("3. Произведение элементов");
            _compareAvailableListBox.Items.Add("4. Прямое вычисление полинома");
            _compareAvailableListBox.Items.Add("5. Полином по схеме Горнера");
            _compareAvailableListBox.Items.Add("6. (Bubble Sort) Алгоритм сортировки пузырьком");
            _compareAvailableListBox.Items.Add("7. (Quick Sort) Алгоритм быстрой сортировки");
            _compareAvailableListBox.Items.Add("8. (TimSort) Гибридный алгоритм сортировки элементов");
            _compareAvailableListBox.Items.Add("10. (HasDuplicates) Проверка дубликатов ");
            _compareAvailableListBox.Items.Add("11. (ReverseArray) Разворот массива");
            _compareAvailableListBox.Items.Add("12. (ShellSort) Сортировка Шелла");
            _compareAvailableListBox.Items.Add("13. Простой итеративный алгоритм");
            _compareAvailableListBox.Items.Add("14. Рекурсивный алгоритм");
            _compareAvailableListBox.Items.Add("15. Быстрый (бинарный) алгоритм возведения в степень");

            _compareAvailableListBox.PreviewMouseDown += CompareAvailable_PreviewMouseDown;
            _compareAvailableListBox.PreviewMouseMove += CompareAvailable_PreviewMouseMove;
            _compareAvailableListBox.MouseDoubleClick += (s, e) => AddSelectedToCompareLive();
            pnlLive.Children.Add(_compareAvailableListBox);

            Button btnAddLive = new Button
            {
                Content = " ➕ Добавить выбранный в сравнение ",
                Height = 26,
                FontWeight = FontWeights.Normal,
                Margin = new Thickness(0, 0, 0, 4)
            };
            btnAddLive.Click += (s, e) => AddSelectedToCompareLive();
            pnlLive.Children.Add(btnAddLive);
            expLive.Content = pnlLive;
            leftPanel.Children.Add(expLive);

            // Блок 2: Выбор из кэша БД
            Expander expDb = new Expander
            {
                Header = "💾 2. Выбрать замер из кэша БД",
                IsExpanded = true,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            StackPanel pnlDb = new StackPanel { Margin = new Thickness(4) };

            _compareCachedSessionsComboBox = new ComboBox
            {
                Height = 28,
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            pnlDb.Children.Add(_compareCachedSessionsComboBox);

            _btnLoadCompareFromDb = new Button
            {
                Content = " 📥 Добавить график из БД в сравнение ",
                Height = 26,
                FontWeight = FontWeights.Normal
            };
            _btnLoadCompareFromDb.Click += BtnLoadCompareFromDb_Click;
            pnlDb.Children.Add(_btnLoadCompareFromDb);
            expDb.Content = pnlDb;
            leftPanel.Children.Add(expDb);

            // Список выбранных
            leftPanel.Children.Add(new TextBlock
            {
                Text = "Выбранные алгоритмы / замеры для графика:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 3)
            });

            _compareSelectedListBox = new ListBox
            {
                Height = 100,
                SelectionMode = SelectionMode.Single,
                FontSize = 11,
                AllowDrop = true,
                Margin = new Thickness(0, 0, 0, 4)
            };
            // Добавим начальные алгоритмы по умолчанию
            _compareSelectedListBox.Items.Add(new CompareItem { DisplayName = "6. (Bubble Sort) Сортировка пузырьком", IsFromDb = false, AlgorithmIndex = 5 });
            _compareSelectedListBox.Items.Add(new CompareItem { DisplayName = "7. (Quick Sort) Быстрая сортировка", IsFromDb = false, AlgorithmIndex = 6 });
            _compareSelectedListBox.Items.Add(new CompareItem { DisplayName = "8. (TimSort) Сортировка элементов", IsFromDb = false, AlgorithmIndex = 7 });

            _compareSelectedListBox.DragOver += (s, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
            _compareSelectedListBox.Drop += CompareSelected_Drop;
            _compareSelectedListBox.MouseDoubleClick += (s, e) => RemoveSelectedFromCompare();
            leftPanel.Children.Add(_compareSelectedListBox);

            StackPanel btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) };
            Button btnRemove = new Button { Content = " ➖ Удалить выбранный ", Height = 24, Margin = new Thickness(0, 0, 6, 0), FontWeight = FontWeights.Normal };
            btnRemove.Click += (s, e) => RemoveSelectedFromCompare();
            btnRow.Children.Add(btnRemove);

            Button btnClearList = new Button { Content = " 🧹 Очистить график ", Height = 24, FontWeight = FontWeights.Normal };
            btnClearList.Click += (s, e) =>
            {
                _compareSelectedListBox.Items.Clear();
                _compareChart.Series = new ObservableCollection<ISeries>();
                _lblCompareStatus.Text = "График сравнения очищен.";
            };
            btnRow.Children.Add(btnClearList);
            leftPanel.Children.Add(btnRow);

            // Чекбокс теоретических графиков
            _chkCompareTheoretical = new CheckBox
            {
                Content = "📐 Показывать теоретические графики O(...)",
                IsChecked = true,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4F46E5")),
                Margin = new Thickness(0, 2, 0, 8)
            };
            _chkCompareTheoretical.Click += OnCompareTheoreticalToggled;
            leftPanel.Children.Add(_chkCompareTheoretical);

            // Параметры для живого запуска
            GroupBox paramsBox = new GroupBox
            {
                Header = " Параметры живого замера ",
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(4),
                Margin = new Thickness(0, 0, 0, 8)
            };
            WrapPanel paramsWrap = new WrapPanel { Orientation = Orientation.Horizontal };
            _txtCompareStartN = UiComponentsFactory.AddInputField("Старт:", "200", paramsWrap);
            _txtCompareEndN = UiComponentsFactory.AddInputField("Конец:", "3000", paramsWrap);
            _txtCompareStep = UiComponentsFactory.AddInputField("Шаг:", "100", paramsWrap);
            _txtCompareRuns = UiComponentsFactory.AddInputField("Прогоны:", "5", paramsWrap);
            paramsBox.Content = paramsWrap;
            leftPanel.Children.Add(paramsBox);

            // Кнопки запуска живого сравнения
            StackPanel runBtnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            _btnRunCompare = new Button
            {
                Content = " ▶ Запустить сравнение ",
                Height = 30,
                Padding = new Thickness(12, 0, 12, 0),
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
                Height = 30,
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
                Text = "Готов к работе. Выберите алгоритмы и запустите сравнение или добавьте замеры из БД.",
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
            _lblCompareZoom = new TextBlock
            {
                Text = "🖐️ Колесико / Щипок: мягкий зум | Перетаскивание мышью: сдвиг по оси N",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569")),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_lblCompareZoom, Dock.Left);
            navDock.Children.Add(_lblCompareZoom);

            Button btnResetCompareZoom = new Button
            {
                Content = " ↺ Сбросить масштаб ",
                Height = 26,
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
                UpdateCompareZoomLabel();
            };
            DockPanel.SetDock(btnResetCompareZoom, Dock.Right);
            navDock.Children.Add(btnResetCompareZoom);

            navBorder.Child = navDock;
            Grid.SetRow(navBorder, 0);
            chartArea.Children.Add(navBorder);

            _compareXAxis = new Axis { Name = "Размер N" };
            _compareYAxis = new Axis { Name = "Время (мс) / Шаги" };

            _compareChart = new CartesianChart
            {
                IsHitTestVisible = true,
                ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.X,
                LegendPosition = LiveChartsCore.Measure.LegendPosition.Right,
                Series = new ObservableCollection<ISeries>(),
                XAxes = new Axis[] { _compareXAxis },
                YAxes = new Axis[] { _compareYAxis }
            };

            SetupCompareChartInteraction();

            Grid.SetRow(_compareChart, 1);
            chartArea.Children.Add(_compareChart);

            Grid.SetColumn(chartArea, 1);
            mainGrid.Children.Add(chartArea);

            return mainGrid;
        }

        private void SetupCompareChartInteraction()
        {
            // Мягкий зум
            _compareChart.PreviewMouseWheel += (s, e) =>
            {
                e.Handled = true;
                double curMin = _compareXAxis.MinLimit ?? _compareDataMinN;
                double curMax = _compareXAxis.MaxLimit ?? _compareDataMaxN;
                double totalRange = Math.Max(1.0, _compareDataMaxN - _compareDataMinN);
                double curWidth = curMax - curMin;

                if (curWidth <= 0) return;

                double factor = e.Delta > 0 ? 0.92 : 1.087;
                double newWidth = curWidth * factor;

                if (newWidth < 5) newWidth = 5;
                if (newWidth >= totalRange)
                {
                    _compareXAxis.MinLimit = null;
                    _compareXAxis.MaxLimit = null;
                    UpdateCompareZoomLabel();
                    return;
                }

                Point mousePos = e.GetPosition(_compareChart);
                double chartWidth = _compareChart.ActualWidth > 0 ? _compareChart.ActualWidth : 700;
                double ratio = Math.Max(0.0, Math.Min(1.0, mousePos.X / chartWidth));

                double center = curMin + curWidth * ratio;
                double newMin = center - newWidth * ratio;
                double newMax = center + newWidth * (1.0 - ratio);

                if (newMin < _compareDataMinN)
                {
                    newMin = _compareDataMinN;
                    newMax = newMin + newWidth;
                }
                if (newMax > _compareDataMaxN)
                {
                    newMax = _compareDataMaxN;
                    newMin = Math.Max(_compareDataMinN, newMax - newWidth);
                }

                _compareXAxis.MinLimit = newMin;
                _compareXAxis.MaxLimit = newMax;
                UpdateCompareZoomLabel();
            };

            // Сдвиг (панорамирование)
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
                    UpdateCompareZoomLabel();
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
        }

        private void ScrollCompareChartByDelta(short delta)
        {
            double curMin = _compareXAxis.MinLimit ?? _compareDataMinN;
            double curMax = _compareXAxis.MaxLimit ?? _compareDataMaxN;
            double totalRange = Math.Max(1.0, _compareDataMaxN - _compareDataMinN);
            double viewWidth = curMax - curMin;

            if (viewWidth >= totalRange)
            {
                viewWidth = totalRange * 0.75;
                curMin = _compareDataMinN;
                curMax = curMin + viewWidth;
            }

            double step = (viewWidth * 0.05) * (delta / 120.0);
            double newMin = curMin + step;
            double newMax = curMax + step;

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
            UpdateCompareZoomLabel();
        }

        private void UpdateCompareZoomLabel()
        {
            if (_lblCompareZoom == null || _compareXAxis == null) return;
            double curMin = _compareXAxis.MinLimit ?? _compareDataMinN;
            double curMax = _compareXAxis.MaxLimit ?? _compareDataMaxN;
            double totalRange = Math.Max(1.0, _compareDataMaxN - _compareDataMinN);
            double viewWidth = curMax - curMin;

            if (_compareXAxis.MinLimit == null && _compareXAxis.MaxLimit == null)
            {
                _lblCompareZoom.Text = $"🔍 Полный диапазон: N от {_compareDataMinN:F0} до {_compareDataMaxN:F0} (100%)";
            }
            else
            {
                double pct = Math.Min(100.0, (viewWidth / totalRange) * 100.0);
                _lblCompareZoom.Text = $"🔍 Просмотр: N от {curMin:F0} до {curMax:F0} ({pct:F0}% диапазона)";
            }
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
            if (e.Data.GetData(DataFormats.StringFormat) is string itemStr)
            {
                AddCompareItemByName(itemStr);
            }
        }

        private void AddSelectedToCompareLive()
        {
            if (_compareAvailableListBox.SelectedItem is string itemStr)
            {
                AddCompareItemByName(itemStr);
            }
        }

        private void AddCompareItemByName(string itemStr)
        {
            int algoIdx = GetAlgorithmIndexByName(itemStr);
            string cleanName = CleanAlgoShortName(itemStr);
            bool alreadyExists = _compareSelectedListBox.Items.OfType<CompareItem>().Any(x => !x.IsFromDb && x.AlgorithmIndex == algoIdx);
            if (!alreadyExists)
            {
                _compareSelectedListBox.Items.Add(new CompareItem
                {
                    DisplayName = cleanName,
                    IsFromDb = false,
                    AlgorithmIndex = algoIdx
                });
            }
        }

        private void RemoveSelectedFromCompare()
        {
            if (_compareSelectedListBox.SelectedItem is CompareItem item)
            {
                _compareSelectedListBox.Items.Remove(item);
                // Удаляем соответствующие серии с графика
                if (_compareChart.Series is ObservableCollection<ISeries> seriesList)
                {
                    var toRemove = seriesList.Where(s => s.Name != null && s.Name.StartsWith(CleanAlgoShortName(item.DisplayName))).ToList();
                    foreach (var s in toRemove) seriesList.Remove(s);
                }
            }
        }

        private async void BtnLoadCompareFromDb_Click(object sender, RoutedEventArgs e)
        {
            if (_compareCachedSessionsComboBox.SelectedItem is not ExperimentSession session)
            {
                MessageBox.Show("Выберите сохраненный замер из выпадающего списка БД.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SetControlsEnabled(false);

            try
            {
                var records = await AppDbContext.GetResultsForSessionAsync(session.ExperimentDate, session.AlgorithmName);
                if (records == null || records.Count == 0)
                {
                    MessageBox.Show("Данные для этого замера не найдены в БД.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string displayName = $"[БД] {CleanAlgoShortName(session.AlgorithmName)} ({session.MinN}..{session.MaxN}, R={session.RunsCount})";
                var compareItem = new CompareItem
                {
                    DisplayName = displayName,
                    IsFromDb = true,
                    DbSession = session,
                    AlgorithmIndex = GetAlgorithmIndexByName(session.AlgorithmName)
                };

                if (!_compareSelectedListBox.Items.OfType<CompareItem>().Any(x => x.DisplayName == displayName))
                {
                    _compareSelectedListBox.Items.Add(compareItem);
                }

                // Строим график для сессии из БД
                PlotSessionOnCompareChart(session, records);
                _lblCompareStatus.Text = $"✅ Загружен график из БД: {displayName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке графика из БД: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetControlsEnabled(true);
            }
        }

        private static readonly SKColor[] ComparePalette = new[]
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

        private void PlotSessionOnCompareChart(ExperimentSession session, List<BenchmarkResult> records)
        {
            bool isStepBased = records.Any(r => r.StepCount.HasValue) || session.AlgorithmName.Contains("степень");

            var aggregatedPoints = records
                .GroupBy(r => r.N)
                .OrderBy(g => g.Key)
                .Select(g => new ObservablePoint(
                    g.Key,
                    isStepBased ? (g.First().StepCount ?? 0) : g.Min(r => r.ExecutionTimeMs)
                ))
                .ToList();

            if (aggregatedPoints.Count == 0) return;

            int colorIndex = _compareChart.Series.Count() / 2;
            SKColor color = ComparePalette[colorIndex % ComparePalette.Length];
            string shortName = $"[БД] {CleanAlgoShortName(session.AlgorithmName)} (R={session.RunsCount})";

            var (complexityFunc, label) = AlgorithmExecutor.GetComplexityInfo(session.AlgorithmName);
            var theoPoints = CalculateTheoreticalPoints(complexityFunc, aggregatedPoints);

            var factSeries = new LineSeries<ObservablePoint>
            {
                Values = new ObservableCollection<ObservablePoint>(aggregatedPoints),
                Name = $"{shortName} (факт)",
                Stroke = new SolidColorPaint(color) { StrokeThickness = 2.5f },
                GeometryStroke = new SolidColorPaint(color) { StrokeThickness = 2f },
                GeometryFill = new SolidColorPaint(SKColors.White),
                GeometrySize = 5,
                Fill = null,
                EnableNullSplitting = false
            };

            var theoSeries = new LineSeries<ObservablePoint>
            {
                Values = new ObservableCollection<ObservablePoint>(theoPoints),
                Name = $"{shortName} (теор {label})",
                Stroke = new SolidColorPaint(new SKColor(color.Red, color.Green, color.Blue, 180)) { StrokeThickness = 1.8f },
                GeometrySize = 0,
                Fill = null,
                EnableNullSplitting = false,
                IsVisible = _chkCompareTheoretical.IsChecked == true
            };

            if (_compareChart.Series is ObservableCollection<ISeries> seriesList)
            {
                seriesList.Add(factSeries);
                seriesList.Add(theoSeries);
            }

            _compareDataMinN = Math.Min(_compareDataMinN, session.MinN);
            _compareDataMaxN = Math.Max(_compareDataMaxN, session.MaxN);
            _compareXAxis.MinLimit = null;
            _compareXAxis.MaxLimit = null;
            UpdateCompareZoomLabel();
        }

        private List<ObservablePoint> CalculateTheoreticalPoints(Func<double, double> complexityFunc, List<ObservablePoint> pointsList)
        {
            List<ObservablePoint> theoPoints = new List<ObservablePoint>();
            if (pointsList == null || pointsList.Count == 0) return theoPoints;

            double sumYF = 0;
            double sumF2 = 0;
            foreach (var point in pointsList)
            {
                if (!point.X.HasValue || !point.Y.HasValue) continue;
                double n = point.X.Value;
                double y = point.Y.Value;
                double f = complexityFunc(n);
                sumYF += y * f;
                sumF2 += f * f;
            }

            double k = sumF2 > 0 ? (sumYF / sumF2) : 0;
            if (k <= 0)
            {
                var maxP = pointsList.OrderBy(p => p.X!.Value).LastOrDefault();
                if (maxP != null && maxP.X.HasValue && maxP.Y.HasValue)
                {
                    double fMax = complexityFunc(maxP.X.Value);
                    k = fMax > 0 ? (maxP.Y.Value / fMax) : 0.001;
                }
            }

            foreach (var point in pointsList.OrderBy(p => p.X!.Value))
            {
                if (!point.X.HasValue) continue;
                double n = point.X.Value;
                theoPoints.Add(new ObservablePoint(n, k * complexityFunc(n)));
            }

            return theoPoints;
        }

        private void OnCompareTheoreticalToggled(object sender, RoutedEventArgs e)
        {
            bool showTheo = _chkCompareTheoretical.IsChecked == true;
            if (_compareChart?.Series != null)
            {
                foreach (var s in _compareChart.Series)
                {
                    if (s.Name != null && s.Name.Contains("теор"))
                    {
                        s.IsVisible = showTheo;
                    }
                }
            }
        }

        private async void BtnRunComparison_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(_txtCompareStartN.Text, out int startN) ||
                !int.TryParse(_txtCompareEndN.Text, out int endN) ||
                !int.TryParse(_txtCompareStep.Text, out int step) || step <= 0 || startN < 0 || endN < startN)
            {
                MessageBox.Show("Заполните корректно параметры диапазона для сравнения.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(_txtCompareRuns.Text, out int runsCount) || runsCount <= 0)
            {
                runsCount = 3;
            }

            var selectedItems = _compareSelectedListBox.Items.OfType<CompareItem>().ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы один алгоритм в список сравнения.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _compareCts = new CancellationTokenSource();
            var token = _compareCts.Token;

            SetControlsEnabled(false);
            _btnStopCompare.IsEnabled = true;
            _compareProgressBar.Visibility = Visibility.Visible;
            _compareProgressBar.IsIndeterminate = true;

            var seriesCollection = new ObservableCollection<ISeries>();
            _compareChart.Series = seriesCollection;
            _compareDataMinN = startN;
            _compareDataMaxN = endN;
            _compareXAxis.MinLimit = null;
            _compareXAxis.MaxLimit = null;

            try
            {
                for (int i = 0; i < selectedItems.Count; i++)
                {
                    if (token.IsCancellationRequested) break;

                    var item = selectedItems[i];
                    SKColor color = ComparePalette[i % ComparePalette.Length];

                    if (item.IsFromDb && item.DbSession != null)
                    {
                        _lblCompareStatus.Text = $"⏳ Загрузка из БД ({i + 1}/{selectedItems.Count}): {item.DisplayName}...";
                        var dbRecords = await AppDbContext.GetResultsForSessionAsync(item.DbSession.ExperimentDate, item.DbSession.AlgorithmName);
                        if (dbRecords != null && dbRecords.Count > 0)
                        {
                            PlotSessionOnCompareChart(item.DbSession, dbRecords);
                        }
                        continue;
                    }

                    int algoIndex = item.AlgorithmIndex;
                    string rawName = GetAlgorithmFullNameByIndex(algoIndex);
                    string displayName = CleanAlgoShortName(rawName);
                    bool isStepBased = algoIndex >= 12 && algoIndex <= 14;

                    _lblCompareStatus.Text = $"⏳ Выполняется живой замер ({i + 1}/{selectedItems.Count}): {displayName}...";

                    var points = await Task.Run(() =>
                    {
                        Random rng = new Random();
                        Thread.CurrentThread.Priority = ThreadPriority.Highest;

                        int warmupSize = Math.Min(500, endN);
                        int[] warmupInt = new int[warmupSize];
                        double[] warmupDouble = new double[warmupSize];
                        for (int k = 0; k < warmupSize; k++)
                        {
                            warmupInt[k] = rng.Next(1, 100000);
                            warmupDouble[k] = warmupInt[k];
                        }
                        for (int w = 0; w < 5; w++)
                        {
                            if (token.IsCancellationRequested) break;
                            AlgorithmExecutor.Execute(algoIndex, warmupInt, warmupDouble, warmupSize);
                        }

                        List<ObservablePoint> algoPoints = new List<ObservablePoint>();

                        int[] curInt = new int[endN];
                        double[] curDouble = new double[endN];

                        for (int n = startN; n <= endN; n += step)
                        {
                            if (token.IsCancellationRequested) return null;

                            GC.Collect();
                            GC.WaitForPendingFinalizers();

                            long lastSteps = 0;
                            List<double> runTimes = new List<double>();

                            for (int r = 0; r < runsCount; r++)
                            {
                                // Генерируем свежие случайные данные для каждого прогона n
                                for (int idx = 0; idx < n; idx++)
                                {
                                    int val = rng.Next(1, Math.Max(10000, endN * 5));
                                    curInt[idx] = val;
                                    curDouble[idx] = val;
                                }

                                long sTicks = Stopwatch.GetTimestamp();
                                long steps = AlgorithmExecutor.Execute(algoIndex, curInt, curDouble, n);
                                long eTicks = Stopwatch.GetTimestamp();

                                lastSteps = steps;
                                double ms = (eTicks - sTicks) * 1000.0 / Stopwatch.Frequency;
                                runTimes.Add(ms);
                            }

                            if (isStepBased)
                            {
                                algoPoints.Add(new ObservablePoint(n, lastSteps));
                            }
                            else
                            {
                                runTimes.Sort();
                                // Берем наименьшее время прогона (минимум из runsCount), так как любые прерывания ОС могут только завысить время
                                double pureTime = runTimes[0];
                                algoPoints.Add(new ObservablePoint(n, pureTime));
                            }
                        }

                        // Устранение редких изолированных выбросов планировщика Windows двухпроходным сглаживанием
                        if (!isStepBased && algoPoints.Count > 2)
                        {
                            for (int pass = 0; pass < 2; pass++)
                            {
                                for (int p = 1; p < algoPoints.Count - 1; p++)
                                {
                                    double prevY = algoPoints[p - 1].Y ?? 0;
                                    double currY = algoPoints[p].Y ?? 0;
                                    double nextY = algoPoints[p + 1].Y ?? 0;
                                    if (prevY > 0 && nextY > 0)
                                    {
                                        double baseline = (prevY + nextY) / 2.0;
                                        if (currY > baseline * 1.3 || currY < baseline * 0.7)
                                        {
                                            algoPoints[p].Y = baseline;
                                        }
                                    }
                                }
                            }
                        }

                        Thread.CurrentThread.Priority = ThreadPriority.Normal;
                        return algoPoints;
                    }, token);

                    if (token.IsCancellationRequested || points == null) break;

                    var (complexityFunc, label) = AlgorithmExecutor.GetComplexityInfo(rawName);
                    var theoPoints = CalculateTheoreticalPoints(complexityFunc, points);

                    var factSeries = new LineSeries<ObservablePoint>
                    {
                        Values = new ObservableCollection<ObservablePoint>(points),
                        Name = $"{displayName} (факт)",
                        Stroke = new SolidColorPaint(color) { StrokeThickness = 2.5f },
                        GeometryStroke = new SolidColorPaint(color) { StrokeThickness = 2f },
                        GeometryFill = new SolidColorPaint(SKColors.White),
                        GeometrySize = 5,
                        Fill = null,
                        EnableNullSplitting = false
                    };

                    var theoSeries = new LineSeries<ObservablePoint>
                    {
                        Values = new ObservableCollection<ObservablePoint>(theoPoints),
                        Name = $"{displayName} (теор {label})",
                        Stroke = new SolidColorPaint(new SKColor(color.Red, color.Green, color.Blue, 180)) { StrokeThickness = 1.8f },
                        GeometrySize = 0,
                        Fill = null,
                        EnableNullSplitting = false,
                        IsVisible = _chkCompareTheoretical.IsChecked == true
                    };

                    seriesCollection.Add(factSeries);
                    seriesCollection.Add(theoSeries);
                }

                if (token.IsCancellationRequested)
                {
                    _lblCompareStatus.Text = "Сравнение прервано пользователем.";
                }
                else
                {
                    _lblCompareStatus.Text = $"✅ Сравнение завершено! Построено серий: {seriesCollection.Count}.";
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
                SetControlsEnabled(true);
                _btnStopCompare.IsEnabled = false;
                _compareProgressBar.Visibility = Visibility.Collapsed;
                _compareCts?.Dispose();
                _compareCts = null;
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

        private string GetAlgorithmFullNameByIndex(int index)
        {
            if (index >= 0 && index < _cbAlgorithms.Items.Count)
            {
                return _cbAlgorithms.Items[index]?.ToString() ?? "";
            }
            return "";
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

            SetControlsEnabled(false);

            try
            {
                var records = await AppDbContext.GetResultsForSessionAsync(selectedSession.ExperimentDate, selectedSession.AlgorithmName);
                if (records == null || records.Count == 0) return;

                // Синхронизируем поля ввода с параметрами загруженного замера
                for (int i = 0; i < _cbAlgorithms.Items.Count; i++)
                {
                    if (_cbAlgorithms.Items[i]?.ToString() == selectedSession.AlgorithmName)
                    {
                        _cbAlgorithms.SelectedIndex = i;
                        break;
                    }
                }
                _txtStartN.Text = selectedSession.StartN.ToString();
                _txtEndN.Text = selectedSession.EndN.ToString();
                _txtStep.Text = selectedSession.Step.ToString();
                if (_txtRuns != null && selectedSession.RunsCount > 0)
                {
                    _txtRuns.Text = selectedSession.RunsCount.ToString();
                }

                // Вывод списка всех замеров текущей сессии в DataGrid на UI
                _dgResults.ItemsSource = records;

                _chart2DService.Clear();

                // 1. Определение типа графика
                bool is3D = selectedSession.AlgorithmName.Contains("3D") || selectedSession.AlgorithmName.Contains("матриц");

                // 2. Находим TabControl и принудительно переключаем на вкладку с графиками (индекс 0)
                if (_mainTabControl != null)
                {
                    _mainTabControl.SelectedIndex = 0;
                }

                // 3. Устанавливаем видимость контейнеров 2D и 3D
                _chart2DService.Chart.Visibility = is3D ? Visibility.Collapsed : Visibility.Visible;
                if (_chart3DService.Canvas.Parent is Grid container3D)
                {
                    container3D.Visibility = is3D ? Visibility.Visible : Visibility.Collapsed;
                }

                // 4. Наполнение данных
                await Dispatcher.InvokeAsync(() =>
                {
                    if (!is3D)
                    {
                        // Определяем, степенной ли это алгоритм по наличию StepCount или названию
                        bool isStepBased = records.Any(r => r.StepCount.HasValue) ||
                                           selectedSession.AlgorithmName.Contains("степень");

                        _chart2DService.SetYAxisTitle(isStepBased ? "Количество операций (шагов)" : "Время (мс)");
                        _chart2DService.SetXAxisTitle(isStepBased ? "Показатель степени (N)" : "Размер массива (N)");

                        var aggregatedPoints = records
                            .GroupBy(r => r.N)
                            .OrderBy(g => g.Key)
                            .Select(g => new ObservablePoint(
                                g.Key,
                                isStepBased ? (g.First().StepCount ?? 0) : g.Min(r => r.ExecutionTimeMs)
                            ))
                            .ToList();

                        foreach (var pt in aggregatedPoints)
                        {
                            _chart2DService.ChartValues.Add(pt);
                        }

                        _chart2DService.SetDataRange(selectedSession.MinN, selectedSession.MaxN);
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

                            var groupedByN = records
                                .GroupBy(r => r.N)
                                .ToDictionary(g => g.Key, g => g.ToList());

                            for (int i = 0; i < count; i++)
                            {
                                int currentN = startN + i * step;

                                if (groupedByN.TryGetValue(currentN, out var rowList))
                                {
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
            finally
            {
                SetControlsEnabled(true);
            }
        }

        private void CbAlgorithms_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cbAlgorithms == null || _chart2DService == null || _txtStartN == null || _txtStep == null || _txtEndN == null || _txtRuns == null)
                return;

            int index = _cbAlgorithms.SelectedIndex;
            if (index < 0) return;

            bool isMatrix3D = IsMatrixAlgorithm(index);

            _chart2DService.Chart.Visibility = isMatrix3D ? Visibility.Collapsed : Visibility.Visible;
            if (_chart3DService.Canvas?.Parent is Grid container3D)
            {
                container3D.Visibility = isMatrix3D ? Visibility.Visible : Visibility.Collapsed;
            }

            bool isPowerAlgorithm = index >= 12 && index <= 14;

            if (isPowerAlgorithm)
            {
                _txtStartN.IsEnabled = false;
                _txtStartN.Text = "1";

                _txtStep.IsEnabled = false;
                _txtStep.Text = "1";

                _txtRuns.IsEnabled = false;
                _txtRuns.Text = "1";

                _txtEndN.ToolTip = "Максимальный показатель степени";

                if (_txtEndN.Parent is StackPanel parentPanel)
                {
                    int indexInParent = parentPanel.Children.IndexOf(_txtEndN);
                    if (indexInParent > 0 && parentPanel.Children[indexInParent - 1] is TextBlock lbl)
                    {
                        lbl.Text = "Степень (до):";
                    }
                }

                if (index == 14) _txtEndN.Text = "100000";
                else _txtEndN.Text = "3000";
            }
            else
            {
                _txtStartN.IsEnabled = true;
                _txtStep.IsEnabled = true;
                _txtRuns.IsEnabled = true;
                _txtEndN.ToolTip = "Конечный размер данных N";

                if (_txtEndN.Parent is StackPanel parentPanel)
                {
                    int indexInParent = parentPanel.Children.IndexOf(_txtEndN);
                    if (indexInParent > 0 && parentPanel.Children[indexInParent - 1] is TextBlock lbl)
                    {
                        lbl.Text = "Конец (N):";
                    }
                }

                ApplyDefaultAlgorithmParameters(index);
            }
        }

        private void ApplyDefaultAlgorithmParameters(int index)
        {
            if (_txtStartN == null || _txtEndN == null || _txtStep == null || _txtRuns == null) return;

            switch (index)
            {
                case 0: // 1. Постоянная функция (честная нагрузка ~10-12 мс)
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

                case 12: // 13. PowIterative O(N)
                case 13: // 14. PowRecursiveSafe O(N)
                    _txtStartN.Text = "1";
                    _txtEndN.Text = "3000";
                    _txtStep.Text = "1";
                    _txtRuns.Text = "1";
                    break;

                case 14: // 15. PowBinary O(log N)
                    _txtStartN.Text = "1";
                    _txtEndN.Text = "100000";
                    _txtStep.Text = "1";
                    _txtRuns.Text = "1";
                    break;

                default:
                    _txtStartN.Text = "100";
                    _txtEndN.Text = "2000";
                    _txtStep.Text = "100";
                    _txtRuns.Text = "5";
                    break;
            }
        }

        private bool IsMatrixAlgorithm(int index) => index == 8;

        // Блокировка элементов управления при выполнении подсчетов (Пункт 4)
        private void SetControlsEnabled(bool isEnabled)
        {
            if (_btnStart != null) _btnStart.IsEnabled = isEnabled;
            if (_btnLoadHistory != null) _btnLoadHistory.IsEnabled = isEnabled;
            if (_cbHistory != null) _cbHistory.IsEnabled = isEnabled;
            if (_cbAlgorithms != null) _cbAlgorithms.IsEnabled = isEnabled;
            if (_btnRunCompare != null) _btnRunCompare.IsEnabled = isEnabled;
            if (_btnLoadCompareFromDb != null) _btnLoadCompareFromDb.IsEnabled = isEnabled;
        }

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

            bool isMatrix = IsMatrixAlgorithm(selectedIndex);
            bool isStepBased = selectedIndex >= 12 && selectedIndex <= 14;
            int defaultRuns = (isMatrix || isStepBased) ? 1 : 5;

            if (!int.TryParse(_txtRuns.Text, out int runsCount) || runsCount <= 0)
            {
                runsCount = defaultRuns;
            }

            string selectedAlgName = _cbAlgorithms.SelectedItem.ToString() ?? "";
            bool loadFromDbOnly = false;

            try
            {
                // 2. Загружаем сохраненные сессии из БД
                var sessions = await AppDbContext.GetExperimentSessionsAsync();

                // 3. Ищем замер, где совпадают ВСЕ параметры: AlgorithmName, StartN, EndN, Step и RunsCount
                var existingSession = sessions?.FirstOrDefault(s =>
                    s.AlgorithmName == selectedAlgName &&
                    s.StartN == startN &&
                    s.EndN == endN &&
                    s.Step == step &&
                    s.RunsCount == runsCount);

                // 4. Показываем диалог выбора (Пункт 5: выбор создать новый график или взять из кэша)
                if (existingSession != null)
                {
                    var choice = MessageBox.Show(
                        $"В базе данных найден сохраненный замер с аналогичными параметрами:\n\n" +
                        $"• Алгоритм: {selectedAlgName}\n" +
                        $"• Диапазон N: {startN} .. {endN} (шаг {step})\n" +
                        $"• Количество прогонов: {existingSession.RunsCount}\n" +
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

            if (loadFromDbOnly)
            {
                BtnLoadHistory_Click(sender, e);
                return;
            }

            // 5. Выполнение нового замера
            _cts = new CancellationTokenSource();
            SetControlsEnabled(false); // Замораживаем кнопки загрузки и запуска (Пункт 4)
            _btnStop.IsEnabled = true;
            _progressBar.Visibility = Visibility.Visible;
            _progressBar.IsIndeterminate = true;

            try
            {
                List<BenchmarkResult>? dbResults;

                if (IsMatrixAlgorithm(selectedIndex))
                {
                    dbResults = await RunMatrix3DBenchmarkAsync(startN, endN, step, true, _cts.Token);
                }
                else
                {
                    dbResults = await RunArray2DBenchmarkAsync(selectedIndex, startN, endN, step, runsCount, true, _cts.Token);
                }

                if (_cts != null && !_cts.IsCancellationRequested && dbResults != null && dbResults.Count > 0)
                {
                    _dgResults.ItemsSource = dbResults;

                    await AppDbContext.SaveResultsAsync(dbResults, selectedAlgName, startN, endN, step, runsCount);
                    await LoadHistoryAsync();
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
                SetControlsEnabled(true);
                _btnStop.IsEnabled = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task<List<BenchmarkResult>?> RunMatrix3DBenchmarkAsync(int startN, int endN, int step, bool bypassCache, CancellationToken token)
        {
            _chart3DService.Clear();

            int count = ((endN - startN) / step) + 1;
            double[,] zData = new double[count, count];
            List<BenchmarkResult> newDbResults = new List<BenchmarkResult>();
            string algName = _cbAlgorithms.SelectedItem.ToString() ?? "";
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
                                zData[i, j] = cached.Min(r => r.ExecutionTimeMs);
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

        private async Task<List<BenchmarkResult>?> RunArray2DBenchmarkAsync(int algorithmIndex, int startN, int endN, int step, int runsCount, bool bypassCache, CancellationToken token)
        {
            _chart2DService.Clear();
            string algName = _cbAlgorithms.SelectedItem.ToString() ?? "";
            List<BenchmarkResult> newDbResults = new List<BenchmarkResult>();
            DateTime experimentTimestamp = DateTime.UtcNow;

            bool isStepBased = algorithmIndex >= 12 && algorithmIndex <= 14;
            if (isStepBased) runsCount = 1;

            await Task.Run(async () =>
            {
                // Широкий диапазон случайных чисел для исключения коллизий одинаковых элементов в QuickSort
                int[] maxDataInt = new int[endN];
                Random rng = new Random();
                for (int i = 0; i < endN; i++) maxDataInt[i] = rng.Next(1, Math.Max(10000, endN * 5));

                Thread.CurrentThread.Priority = ThreadPriority.Highest;

                int warmupSize = Math.Min(1000, endN);
                int[] warmupInt = maxDataInt.AsSpan(0, warmupSize).ToArray();
                double[] warmupDouble = warmupInt.Select(x => (double)x).ToArray();

                for (int i = 0; i < 5; i++)
                {
                    if (token.IsCancellationRequested) break;
                    AlgorithmExecutor.Execute(algorithmIndex, warmupInt, warmupDouble, warmupSize);
                }

                List<ObservablePoint> results = new List<ObservablePoint>();

                int[] intData = new int[endN];
                double[] doubleData = new double[endN];

                for (int n = startN; n <= endN; n += step)
                {
                    if (token.IsCancellationRequested) return;

                    GC.Collect();
                    GC.WaitForPendingFinalizers();

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
                                results.Add(new ObservablePoint(n, cachedRecords.Min(r => r.ExecutionTimeMs)));
                            }
                            continue;
                        }
                    }

                    long bestSteps = 0;
                    List<double> runTimes = new List<double>();

                    for (int r = 0; r < runsCount; r++)
                    {
                        for (int idx = 0; idx < n; idx++)
                        {
                            int val = rng.Next(1, Math.Max(10000, endN * 5));
                            intData[idx] = val;
                            doubleData[idx] = val;
                        }

                        long startTicks = Stopwatch.GetTimestamp();
                        long steps = AlgorithmExecutor.Execute(algorithmIndex, intData, doubleData, n);
                        long endTicks = Stopwatch.GetTimestamp();

                        bestSteps = steps;
                        long elapsedTicks = endTicks - startTicks;
                        double singleRunMs = (elapsedTicks * 1000.0) / Stopwatch.Frequency;
                        runTimes.Add(singleRunMs);

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
                        results.Add(new ObservablePoint(n, bestSteps));
                    }
                    else
                    {
                        runTimes.Sort();
                        double pureTime = runTimes[0];
                        results.Add(new ObservablePoint(n, pureTime));
                    }
                }

                // Фильтрация одиночных скачков двухпроходным сглаживанием
                if (!isStepBased && results.Count > 2)
                {
                    for (int pass = 0; pass < 2; pass++)
                    {
                        for (int p = 1; p < results.Count - 1; p++)
                        {
                            double prevY = results[p - 1].Y ?? 0;
                            double currY = results[p].Y ?? 0;
                            double nextY = results[p + 1].Y ?? 0;
                            if (prevY > 0 && nextY > 0)
                            {
                                double baseline = (prevY + nextY) / 2.0;
                                if (currY > baseline * 1.3 || currY < baseline * 0.7)
                                {
                                    results[p].Y = baseline;
                                }
                            }
                        }
                    }
                }

                Thread.CurrentThread.Priority = ThreadPriority.Normal;

                Dispatcher.Invoke(() =>
                {
                    _chart2DService.SetYAxisTitle(isStepBased ? "Количество операций (шагов)" : "Время (мс)");
                    _chart2DService.SetXAxisTitle(isStepBased ? "Показатель степени (N)" : "Размер массива (N)");

                    foreach (var pt in results) _chart2DService.ChartValues.Add(pt);

                    _chart2DService.SetDataRange(startN, endN);
                    _chart2DService.CalculateTheoreticalCurve(algName, results);
                });
            }, token);

            if (token.IsCancellationRequested) return null;

            return newDbResults;
        }
    }
}
