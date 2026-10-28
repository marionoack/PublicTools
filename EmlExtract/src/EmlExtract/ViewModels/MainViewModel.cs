using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmlExtract.Models;
using EmlExtract.Services;
using Microsoft.Win32;

namespace EmlExtract.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IEmlParserService _parserService;
    private readonly IHtmlExportService _htmlExportService;
    private readonly IMarkdownConversionService _markdownConversionService;
    private readonly IAttachmentExportService _attachmentExportService;

    public MainViewModel()
        : this(new EmlParserService(),
               new HtmlExportService(new FooterRemovalService()),
               new MarkdownConversionService(),
               new AttachmentExportService())
    {
    }

    public MainViewModel(
        IEmlParserService parserService,
        IHtmlExportService htmlExportService,
        IMarkdownConversionService markdownConversionService,
        IAttachmentExportService attachmentExportService)
    {
        _parserService = parserService;
        _htmlExportService = htmlExportService;
        _markdownConversionService = markdownConversionService;
        _attachmentExportService = attachmentExportService;
        OutputFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    [ObservableProperty]
    private EmlDocument? currentDocument;

    [ObservableProperty]
    private string? loadedFilePath;

    [ObservableProperty]
    private string outputFolder;

    [ObservableProperty]
    private bool exportAttachments = true;

    [ObservableProperty]
    private bool exportHtml = true;

    [ObservableProperty]
    private bool exportMarkdown;

    [ObservableProperty]
    private bool removeFooter;

    [ObservableProperty]
    private string statusMessage = "Keine Datei geladen.";

    public ObservableCollection<AttachmentItem> Attachments => CurrentDocument?.Attachments ?? new();

    [RelayCommand]
    private void BrowseFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "E-Mail-Dateien (*.eml;*.msg)|*.eml;*.msg|EML-Dateien (*.eml)|*.eml|Outlook-Dateien (*.msg)|*.msg|Alle Dateien (*.*)|*.*",
            Title = "E-Mail-Datei auswählen"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadEmlFile(dialog.FileName);
        }
    }

    [RelayCommand]
    private void BrowseOutputFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Zielordner für den Export auswählen",
            InitialDirectory = OutputFolder
        };

        if (dialog.ShowDialog() == true)
        {
            OutputFolder = dialog.FolderName;
        }
    }

    public void LoadEmlFile(string path)
    {
        try
        {
            CurrentDocument = _parserService.Parse(path);
            LoadedFilePath = path;
            OnPropertyChanged(nameof(Attachments));
            StatusMessage = $"Geladen: {Path.GetFileName(path)} ({CurrentDocument.Attachments.Count} Anhänge)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Laden: {ex.Message}";
        }
    }

    private bool CanExport() => CurrentDocument is not null && (ExportAttachments || ExportHtml || ExportMarkdown);

    private bool HasDocument() => CurrentDocument is not null;

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void CopyHtml()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        try
        {
            var html = _htmlExportService.BuildExportHtml(CurrentDocument, RemoveFooter);
            Clipboard.SetText(html);
            StatusMessage = "HTML in die Zwischenablage kopiert.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Kopieren (HTML): {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void CopyMarkdown()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        try
        {
            var html = _htmlExportService.BuildExportHtml(CurrentDocument, RemoveFooter);
            var markdown = _markdownConversionService.ConvertHtmlToMarkdown(html);
            Clipboard.SetText(markdown);
            StatusMessage = "Markdown in die Zwischenablage kopiert.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Kopieren (Markdown): {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void CopyText()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        try
        {
            var text = _htmlExportService.BuildExportText(CurrentDocument, RemoveFooter);
            Clipboard.SetText(text);
            StatusMessage = "Text in die Zwischenablage kopiert.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Kopieren (Text): {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void Export()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(OutputFolder);
            var baseName = Path.GetFileNameWithoutExtension(LoadedFilePath) ?? "export";
            var exportedFiles = new List<string>();

            string? html = null;
            if (ExportHtml || ExportMarkdown)
            {
                html = _htmlExportService.BuildExportHtml(CurrentDocument, RemoveFooter);
            }

            // UTF-8 with BOM so external editors (Notepad, Excel, etc.) that don't
            // sniff encoding reliably detect UTF-8 instead of falling back to ANSI
            // and mangling Umlaute/special characters.
            var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

            if (ExportHtml && html is not null)
            {
                var htmlPath = Path.Combine(OutputFolder, $"{baseName}.html");
                File.WriteAllText(htmlPath, html, utf8WithBom);
                exportedFiles.Add(htmlPath);
            }

            if (ExportMarkdown && html is not null)
            {
                var markdown = _markdownConversionService.ConvertHtmlToMarkdown(html);
                var mdPath = Path.Combine(OutputFolder, $"{baseName}.md");
                File.WriteAllText(mdPath, markdown, utf8WithBom);
                exportedFiles.Add(mdPath);
            }

            if (ExportAttachments)
            {
                var selected = CurrentDocument.Attachments.Where(a => a.IsSelected);
                var attachmentFolder = Path.Combine(OutputFolder, $"{baseName}_attachments");
                exportedFiles.AddRange(_attachmentExportService.SaveAttachments(selected, attachmentFolder));
            }

            StatusMessage = $"Export abgeschlossen: {exportedFiles.Count} Datei(en) in '{OutputFolder}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Export: {ex.Message}";
        }
    }

    partial void OnCurrentDocumentChanged(EmlDocument? value)
    {
        ExportCommand.NotifyCanExecuteChanged();
        CopyHtmlCommand.NotifyCanExecuteChanged();
        CopyMarkdownCommand.NotifyCanExecuteChanged();
        CopyTextCommand.NotifyCanExecuteChanged();
    }

    partial void OnExportAttachmentsChanged(bool value) => ExportCommand.NotifyCanExecuteChanged();

    partial void OnExportHtmlChanged(bool value) => ExportCommand.NotifyCanExecuteChanged();

    partial void OnExportMarkdownChanged(bool value) => ExportCommand.NotifyCanExecuteChanged();
}
