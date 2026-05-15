using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TranslationHelper.Models;
using TranslationHelper.Services;

namespace TranslationHelper.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ClipboardService _clipboard;
    private readonly MarkdownService _markdown;
    private readonly SettingsService _settingsService;
    private BedrockProcessor _processor;
    private AppSettings _settings;

    private string _inputText = string.Empty;
    private string _resultText = string.Empty;
    private string _statusMessage = string.Empty;
    private ProcessingMode _selectedMode;
    private TargetLanguage _selectedTargetLanguage;
    private TextFormat _selectedInputFormat;
    private TextFormat _selectedExportFormat;
    private bool _isProcessing;
    private Tone _selectedTone;
    private Audience _selectedAudience;

    public MainViewModel(
        ClipboardService clipboard,
        MarkdownService markdown,
        SettingsService settingsService,
        AppSettings settings)
    {
        _clipboard = clipboard;
        _markdown = markdown;
        _settingsService = settingsService;
        _settings = settings;
        _processor = new BedrockProcessor(settings);

        _selectedMode = settings.DefaultMode;
        _selectedTargetLanguage = settings.DefaultLanguage;
        _selectedInputFormat = settings.DefaultFormat;
        _selectedExportFormat = settings.DefaultFormat;

        PasteCommand = new RelayCommand(_ => ExecutePaste());
        CopyResultCommand = new RelayCommand(_ => ExecuteCopyResult(), _ => !string.IsNullOrEmpty(ResultText));
        ProcessCommand = new RelayCommand(async _ => await ExecuteProcessAsync(), _ => !IsProcessing);
        OpenSettingsCommand = new RelayCommand(_ => ExecuteOpenSettings());
        SetModeCommand = new RelayCommand(p => { if (p is ProcessingMode m) SelectedMode = m; });
        SetLanguageCommand = new RelayCommand(p => { if (p is TargetLanguage l) SelectedTargetLanguage = l; });
        SetInputFormatCommand = new RelayCommand(p => { if (p is TextFormat f) SelectedInputFormat = f; });
        SetExportFormatCommand = new RelayCommand(p => { if (p is TextFormat f) SelectedExportFormat = f; });
    }

    public string InputText
    {
        get => _inputText;
        set { _inputText = value; OnPropertyChanged(); }
    }

    public string ResultText
    {
        get => _resultText;
        set { _resultText = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set { _isProcessing = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    public ProcessingMode SelectedMode
    {
        get => _selectedMode;
        set { _selectedMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsTranslateMode)); OnPropertyChanged(nameof(IsLightCorrectionMode)); OnPropertyChanged(nameof(IsReformulationMode)); OnPropertyChanged(nameof(ShowLanguageSelector)); }
    }

    public TargetLanguage SelectedTargetLanguage
    {
        get => _selectedTargetLanguage;
        set { _selectedTargetLanguage = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEnglish)); OnPropertyChanged(nameof(IsGerman)); }
    }

    public TextFormat SelectedInputFormat
    {
        get => _selectedInputFormat;
        set { _selectedInputFormat = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsInputMarkdown)); OnPropertyChanged(nameof(IsInputPlain)); }
    }

    public TextFormat SelectedExportFormat
    {
        get => _selectedExportFormat;
        set { _selectedExportFormat = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsExportMarkdown)); OnPropertyChanged(nameof(IsExportPlain)); }
    }

    public Tone SelectedTone
    {
        get => _selectedTone;
        set { _selectedTone = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsToneNone)); OnPropertyChanged(nameof(IsToneFormal)); OnPropertyChanged(nameof(IsToneFriendly)); OnPropertyChanged(nameof(IsToneDirect)); OnPropertyChanged(nameof(IsToneCasual)); }
    }

    public Audience SelectedAudience
    {
        get => _selectedAudience;
        set { _selectedAudience = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsAudienceNone)); OnPropertyChanged(nameof(IsAudienceCustomer)); OnPropertyChanged(nameof(IsAudienceColleague)); OnPropertyChanged(nameof(IsAudienceSuperior)); }
    }

    public bool IsToneNone { get => SelectedTone == Tone.None; set { if (value) SelectedTone = Tone.None; } }
    public bool IsToneFormal { get => SelectedTone == Tone.Formal; set { if (value) SelectedTone = Tone.Formal; } }
    public bool IsToneFriendly { get => SelectedTone == Tone.Friendly; set { if (value) SelectedTone = Tone.Friendly; } }
    public bool IsToneDirect { get => SelectedTone == Tone.Direct; set { if (value) SelectedTone = Tone.Direct; } }
    public bool IsToneCasual { get => SelectedTone == Tone.Casual; set { if (value) SelectedTone = Tone.Casual; } }

    public bool IsAudienceNone { get => SelectedAudience == Audience.None; set { if (value) SelectedAudience = Audience.None; } }
    public bool IsAudienceCustomer { get => SelectedAudience == Audience.Customer; set { if (value) SelectedAudience = Audience.Customer; } }
    public bool IsAudienceColleague { get => SelectedAudience == Audience.Colleague; set { if (value) SelectedAudience = Audience.Colleague; } }
    public bool IsAudienceSuperior { get => SelectedAudience == Audience.Superior; set { if (value) SelectedAudience = Audience.Superior; } }

    // Convenience properties for radio button binding
    public bool IsTranslateMode
    {
        get => SelectedMode == ProcessingMode.Translate;
        set { if (value) SelectedMode = ProcessingMode.Translate; }
    }

    public bool IsLightCorrectionMode
    {
        get => SelectedMode == ProcessingMode.LightCorrection;
        set { if (value) SelectedMode = ProcessingMode.LightCorrection; }
    }

    public bool IsReformulationMode
    {
        get => SelectedMode == ProcessingMode.ImprovedReformulation;
        set { if (value) SelectedMode = ProcessingMode.ImprovedReformulation; }
    }

    public bool ShowLanguageSelector => SelectedMode == ProcessingMode.Translate;

    public bool IsEnglish
    {
        get => SelectedTargetLanguage == TargetLanguage.English;
        set { if (value) SelectedTargetLanguage = TargetLanguage.English; }
    }

    public bool IsGerman
    {
        get => SelectedTargetLanguage == TargetLanguage.German;
        set { if (value) SelectedTargetLanguage = TargetLanguage.German; }
    }

    public bool IsInputMarkdown
    {
        get => SelectedInputFormat == TextFormat.Markdown;
        set { if (value) SelectedInputFormat = TextFormat.Markdown; }
    }

    public bool IsInputPlain
    {
        get => SelectedInputFormat == TextFormat.Plain;
        set { if (value) SelectedInputFormat = TextFormat.Plain; }
    }

    public bool IsExportMarkdown
    {
        get => SelectedExportFormat == TextFormat.Markdown;
        set { if (value) SelectedExportFormat = TextFormat.Markdown; }
    }

    public bool IsExportPlain
    {
        get => SelectedExportFormat == TextFormat.Plain;
        set { if (value) SelectedExportFormat = TextFormat.Plain; }
    }

    public ICommand PasteCommand { get; }
    public ICommand CopyResultCommand { get; }
    public ICommand ProcessCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand SetModeCommand { get; }
    public ICommand SetLanguageCommand { get; }
    public ICommand SetInputFormatCommand { get; }
    public ICommand SetExportFormatCommand { get; }

    private void ExecutePaste()
    {
        var text = _clipboard.ReadText();
        if (string.IsNullOrEmpty(text))
            StatusMessage = "Zwischenablage ist leer.";
        else
        {
            InputText = text;
            StatusMessage = string.Empty;
        }
    }

    private void ExecuteCopyResult()
    {
        if (string.IsNullOrEmpty(ResultText)) return;

        var textToExport = ResultText;
        if (SelectedExportFormat == TextFormat.Plain && SelectedInputFormat == TextFormat.Markdown)
            textToExport = _markdown.StripMarkdown(ResultText);

        _clipboard.WriteText(textToExport);
        StatusMessage = "Ergebnis in Zwischenablage kopiert.";
    }

    private async Task ExecuteProcessAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText))
        {
            StatusMessage = "Bitte zuerst Text einfügen.";
            return;
        }

        if (!_settings.HasCredentials)
        {
            StatusMessage = "AWS-Zugangsdaten nicht konfiguriert. Bitte Einstellungen öffnen.";
            ExecuteOpenSettings();
            return;
        }

        IsProcessing = true;
        StatusMessage = "Wird verarbeitet…";
        ResultText = string.Empty;

        var result = await _processor.ProcessAsync(
            InputText,
            SelectedMode,
            SelectedTargetLanguage,
            SelectedInputFormat,
            SelectedTone,
            SelectedAudience);

        IsProcessing = false;

        if (result.Success)
        {
            ResultText = result.ResultText;
            StatusMessage = "Fertig.";
        }
        else
        {
            StatusMessage = result.ErrorMessage ?? "Unbekannter Fehler.";
        }
    }

    private void ExecuteOpenSettings()
    {
        var settingsWindow = new Views.SettingsWindow(_settings, _settingsService);
        settingsWindow.ShowDialog();

        // Reload settings after dialog closes
        _settings = _settingsService.Load();
        _processor.UpdateSettings(_settings);
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        _processor.UpdateSettings(settings);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
