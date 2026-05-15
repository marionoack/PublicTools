using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PasswordGenerate.ViewModels;

namespace PasswordGenerate;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel();
        DataContext = vm;
        BuildPasswordCells(vm);
    }

    private void BuildPasswordCells(MainViewModel vm)
    {
        for (int row = 0; row < MainViewModel.RowCount; row++)
        {
            for (int col = 0; col < MainViewModel.ColCount; col++)
            {
                var cell = vm.GetCell(row, col);
                var panel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var textBlock = new TextBlock
                {
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center
                };
                textBlock.SetBinding(TextBlock.TextProperty, new Binding("DisplayText")
                {
                    Source = cell,
                    Mode = BindingMode.OneWay
                });

                var copyButton = new Button
                {
                    Content = "\U0001f4cb",
                    Width = 26,
                    Height = 26,
                    Margin = new Thickness(4, 0, 0, 0),
                    ToolTip = "In Zwischenablage kopieren",
                    VerticalAlignment = VerticalAlignment.Center,
                    Command = cell.CopyCommand
                };

                panel.Children.Add(textBlock);
                panel.Children.Add(copyButton);

                Grid.SetRow(panel, row + 1);
                Grid.SetColumn(panel, col + 1);
                PasswordGrid.Children.Add(panel);
            }
        }
    }
}
