using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Laba_1
{
    public static class UiComponentsFactory
    {
        public static DataGrid CreateResultsDataGrid()
        {
            var dgResults = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                CanUserSortColumns = true,
                Background = Brushes.White
            };

            // Добавление колонок таблицы
            dgResults.Columns.Add(new DataGridTextColumn
            {
                Header = "Запуск №",
                Binding = new Binding("RunNumber")
            });

            dgResults.Columns.Add(new DataGridTextColumn
            {
                Header = "Размер N",
                Binding = new Binding("N")
            });

            dgResults.Columns.Add(new DataGridTextColumn
            {
                Header = "Время (мс)",
                Binding = new Binding("ExecutionTimeMs") { StringFormat = "{0:F4}" }
            });

            dgResults.Columns.Add(new DataGridTextColumn
            {
                Header = "Шаги (StepCount)",
                Binding = new Binding("StepCount")
            });

            dgResults.Columns.Add(new DataGridTextColumn
            {
                Header = "Дата эксперимента",
                Binding = new Binding("ExperimentDate") { StringFormat = "{0:dd.MM.yyyy HH:mm:ss}" }
            });

            return dgResults;
        }

        public static TextBox AddInputField(string label, string defaultValue, StackPanel container)
        {
            StackPanel fieldGroup = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            fieldGroup.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 4) });

            TextBox input = new TextBox { Text = defaultValue, Width = 60 };
            fieldGroup.Children.Add(input);

            container.Children.Add(fieldGroup);
            return input;
        }

        public static Button CreateActionButton(string content, RoutedEventHandler onClick, bool isEnabled = true)
        {
            var button = new Button
            {
                Content = content,
                Height = 30,
                Padding = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Bottom,
                IsEnabled = isEnabled
            };

            if (onClick != null)
            {
                button.Click += onClick;
            }

            return button;
        }

        public static Border CreateVerticalSeparator()
        {
            return new Border
            {
                Width = 1,
                Background = Brushes.LightGray,
                Margin = new Thickness(10, 2, 10, 2)
            };
        }
    }
}