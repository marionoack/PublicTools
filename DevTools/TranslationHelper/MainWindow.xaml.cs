using System.Windows;
using System.Windows.Input;
using TranslationHelper.Models;
using TranslationHelper.ViewModels;

namespace TranslationHelper;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.D1:
                    _viewModel.SelectedMode = ProcessingMode.Translate;
                    e.Handled = true;
                    break;
                case Key.D2:
                    _viewModel.SelectedMode = ProcessingMode.LightCorrection;
                    e.Handled = true;
                    break;
                case Key.D3:
                    _viewModel.SelectedMode = ProcessingMode.ImprovedReformulation;
                    e.Handled = true;
                    break;
                case Key.E:
                    _viewModel.SelectedTargetLanguage = TargetLanguage.English;
                    e.Handled = true;
                    break;
                case Key.D when _viewModel.ShowLanguageSelector:
                    _viewModel.SelectedTargetLanguage = TargetLanguage.German;
                    e.Handled = true;
                    break;
                case Key.M:
                    _viewModel.SelectedInputFormat = TextFormat.Markdown;
                    e.Handled = true;
                    break;
                case Key.P:
                    _viewModel.SelectedInputFormat = TextFormat.Plain;
                    e.Handled = true;
                    break;
            }
        }
    }
}
