using System.ComponentModel;
using System.Reflection;
using System.Windows;
using EmlExtract.Services;
using EmlExtract.ViewModels;

namespace EmlExtract;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var buildDate = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildDate")?.Value;
        if (!string.IsNullOrEmpty(buildDate))
        {
            Title = $"{Title} [{buildDate}]";
        }
        Loaded += (_, _) => AttachViewModel();
    }

    private void AttachViewModel()
    {
        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateBodyPreview(vm);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.CurrentDocument) or null && sender is MainViewModel vm)
        {
            UpdateBodyPreview(vm);
        }
    }

    private void UpdateBodyPreview(MainViewModel vm)
    {
        var doc = vm.CurrentDocument;
        var html = doc?.HtmlBody ?? (doc?.TextBody is { } text
            ? $"<pre>{System.Net.WebUtility.HtmlEncode(text)}</pre>"
            : "<p style='color:gray'>Keine Vorschau verfügbar.</p>");

        // The legacy WebBrowser (MSHTML) control guesses the codepage when fed a raw
        // fragment via NavigateToString and otherwise mangles Umlaute/special chars.
        BodyPreview.NavigateToString(HtmlDocumentHelper.EnsureUtf8Charset(html));
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = IsEmlDrag(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!IsEmlDrag(e))
        {
            return;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        var emlFile = files.FirstOrDefault(EmlParserService.IsSupported);

        if (emlFile is not null && DataContext is MainViewModel vm)
        {
            vm.LoadEmlFile(emlFile);
        }
    }

    private static bool IsEmlDrag(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return false;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        return files.Any(EmlParserService.IsSupported);
    }
}