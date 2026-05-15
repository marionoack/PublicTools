using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SolutionRelocator.Models;

public class SvnWorkingCopy : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _localPath = string.Empty;
    private string _relativePath = string.Empty;
    private string _currentUrl = string.Empty;
    private string _newUrl = string.Empty;
    private string _currentRevision = string.Empty;
    private string _newRevision = string.Empty;
    private string _status = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public string LocalPath
    {
        get => _localPath;
        set => SetField(ref _localPath, value);
    }

    public string RelativePath
    {
        get => _relativePath;
        set => SetField(ref _relativePath, value);
    }

    public string CurrentUrl
    {
        get => _currentUrl;
        set => SetField(ref _currentUrl, value);
    }

    public string NewUrl
    {
        get => _newUrl;
        set => SetField(ref _newUrl, value);
    }

    public string CurrentRevision
    {
        get => _currentRevision;
        set => SetField(ref _currentRevision, value);
    }

    public string NewRevision
    {
        get => _newRevision;
        set => SetField(ref _newRevision, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

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
