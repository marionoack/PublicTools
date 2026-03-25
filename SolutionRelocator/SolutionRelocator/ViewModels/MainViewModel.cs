using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using SolutionRelocator.Models;
using SolutionRelocator.Services;

namespace SolutionRelocator.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly SvnService _svnService = new();

    private string _rootPath = string.Empty;
    private int _maxDepth = 3;
    private string _searchText = string.Empty;
    private string _replaceText = string.Empty;
    private string _statusText = string.Empty;
    private bool _isScanning;
    private bool _allSelected;
    private SearchMode _searchMode = SearchMode.Contains;

    public MainViewModel()
    {
        ScanCommand = new RelayCommand(async () => await ScanAsync(), () => !IsScanning && !string.IsNullOrWhiteSpace(RootPath));
        CheckCommand = new RelayCommand(async () => await CheckRevisionsAsync(), () => !IsScanning && WorkingCopies.Any(wc => wc.IsSelected));
        RelocateCommand = new RelayCommand(async () => await RelocateAsync(), () => !IsScanning && HasValidSelection());
        BrowseCommand = new RelayCommand(Browse);

        LoadSettings();
    }

    public ObservableCollection<SvnWorkingCopy> WorkingCopies { get; } = [];

    public string RootPath
    {
        get => _rootPath;
        set => SetField(ref _rootPath, value);
    }

    public int MaxDepth
    {
        get => _maxDepth;
        set => SetField(ref _maxDepth, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
                UpdateNewUrls();
        }
    }

    public string ReplaceText
    {
        get => _replaceText;
        set
        {
            if (SetField(ref _replaceText, value))
                UpdateNewUrls();
        }
    }

    public SearchMode SearchMode
    {
        get => _searchMode;
        set
        {
            if (SetField(ref _searchMode, value))
                UpdateNewUrls();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        set => SetField(ref _isScanning, value);
    }

    public bool AllSelected
    {
        get => _allSelected;
        set
        {
            if (SetField(ref _allSelected, value))
            {
                foreach (var wc in WorkingCopies)
                    wc.IsSelected = value;
            }
        }
    }

    public IReadOnlyList<int> DepthOptions { get; } = Enumerable.Range(1, 10).ToList();
    public IReadOnlyList<SearchMode> SearchModeOptions { get; } = Enum.GetValues<SearchMode>().ToList();

    public ICommand ScanCommand { get; }
    public ICommand CheckCommand { get; }
    public ICommand RelocateCommand { get; }
    public ICommand BrowseCommand { get; }

    private void Browse()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Stammverzeichnis auswählen"
        };

        if (dialog.ShowDialog() == true)
        {
            RootPath = dialog.FolderName;
        }
    }

    private async Task ScanAsync()
    {
        IsScanning = true;
        StatusText = "Scanne...";
        WorkingCopies.Clear();

        try
        {
            var paths = await _svnService.FindWorkingCopiesAsync(RootPath, MaxDepth);
            var rootNormalized = RootPath.TrimEnd('\\', '/');

            foreach (var path in paths)
            {
                var (url, revision) = await _svnService.GetSvnInfoAsync(path);
                var relativePath = path.StartsWith(rootNormalized, StringComparison.OrdinalIgnoreCase)
                    ? path[rootNormalized.Length..].TrimStart('\\', '/')
                    : path;

                var wc = new SvnWorkingCopy
                {
                    LocalPath = path,
                    RelativePath = relativePath,
                    CurrentUrl = url,
                    CurrentRevision = revision,
                    Status = "Bereit"
                };
                WorkingCopies.Add(wc);
            }

            UpdateNewUrls();
            StatusText = $"{WorkingCopies.Count} Working Copies gefunden";
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task CheckRevisionsAsync()
    {
        IsScanning = true;
        var selected = WorkingCopies.Where(wc => wc.IsSelected).ToList();
        StatusText = $"Prüfe {selected.Count} Einträge...";
        int deselected = 0;

        try
        {
            foreach (var wc in selected)
            {
                var (currentRev, currentExists) = await _svnService.GetRemoteRevisionAsync(wc.CurrentUrl);
                wc.CurrentRevision = currentExists ? currentRev : "N/A";

                if (string.Equals(wc.CurrentUrl, wc.NewUrl, StringComparison.OrdinalIgnoreCase))
                {
                    wc.NewRevision = string.Empty;
                    wc.Status = "Quelle = Ziel";
                    wc.IsSelected = false;
                    deselected++;
                }
                else if (!string.IsNullOrWhiteSpace(wc.NewUrl))
                {
                    var (newRev, newExists) = await _svnService.GetRemoteRevisionAsync(wc.NewUrl);
                    if (newExists)
                    {
                        wc.NewRevision = newRev;
                        wc.Status = "Bereit";
                    }
                    else
                    {
                        wc.NewRevision = "N/A";
                        wc.Status = "Ziel existiert nicht";
                        wc.IsSelected = false;
                        deselected++;
                    }
                }
            }

            var msg = "Prüfung abgeschlossen";
            if (deselected > 0)
                msg += $" ({deselected} nicht erreichbare Ziele abgewählt)";
            StatusText = msg;
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler bei Prüfung: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task RelocateAsync()
    {
        var selected = WorkingCopies.Where(wc => wc.IsSelected && !string.IsNullOrWhiteSpace(wc.NewUrl)).ToList();

        if (selected.Count == 0) return;

        var result = MessageBox.Show(
            $"Sollen {selected.Count} Working Copies relocated werden?",
            "Relocate bestätigen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        IsScanning = true;
        StatusText = $"Relocate für {selected.Count} Einträge...";

        int success = 0, failed = 0;

        foreach (var wc in selected)
        {
            var (ok, message) = await _svnService.RelocateAsync(wc.LocalPath, wc.CurrentUrl, wc.NewUrl);
            if (ok)
            {
                wc.Status = "Erfolgreich";
                wc.CurrentUrl = wc.NewUrl;
                success++;
            }
            else
            {
                wc.Status = $"Fehler: {message}";
                failed++;
            }
        }

        UpdateNewUrls();
        StatusText = $"Relocate abgeschlossen: {success} erfolgreich, {failed} fehlgeschlagen";
        IsScanning = false;
    }

    private void UpdateNewUrls()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            foreach (var wc in WorkingCopies)
                wc.NewUrl = string.Empty;
            return;
        }

        foreach (var wc in WorkingCopies)
        {
            wc.NewUrl = SearchMode switch
            {
                SearchMode.Contains => wc.CurrentUrl.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    ? wc.CurrentUrl.Replace(SearchText, ReplaceText, StringComparison.OrdinalIgnoreCase)
                    : wc.CurrentUrl,
                SearchMode.StartsWith => wc.CurrentUrl.StartsWith(SearchText, StringComparison.OrdinalIgnoreCase)
                    ? ReplaceText + wc.CurrentUrl[SearchText.Length..]
                    : wc.CurrentUrl,
                SearchMode.EndsWith => wc.CurrentUrl.EndsWith(SearchText, StringComparison.OrdinalIgnoreCase)
                    ? wc.CurrentUrl[..^SearchText.Length] + ReplaceText
                    : wc.CurrentUrl,
                _ => wc.CurrentUrl
            };
        }
    }

    private bool HasValidSelection()
    {
        return !string.IsNullOrWhiteSpace(SearchText)
               && WorkingCopies.Any(wc => wc.IsSelected && !string.IsNullOrWhiteSpace(wc.NewUrl));
    }

    private void LoadSettings()
    {
        var s = SettingsService.Load();
        _rootPath = s.RootPath;
        _maxDepth = s.MaxDepth;
        _searchText = s.SearchText;
        _replaceText = s.ReplaceText;
        _searchMode = s.SearchMode;
    }

    public void SaveSettings(double windowWidth, double windowHeight)
    {
        SettingsService.Save(new AppSettings
        {
            RootPath = RootPath,
            MaxDepth = MaxDepth,
            SearchText = SearchText,
            ReplaceText = ReplaceText,
            SearchMode = SearchMode,
            WindowWidth = windowWidth,
            WindowHeight = windowHeight
        });
    }

    public AppSettings GetCurrentSettings() => SettingsService.Load();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
