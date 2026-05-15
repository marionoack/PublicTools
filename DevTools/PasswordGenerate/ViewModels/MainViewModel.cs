using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using PasswordGenerate.Services;

namespace PasswordGenerate.ViewModels;

public class PasswordCell : INotifyPropertyChanged
{
    private string _password = string.Empty;
    private bool _isVisible = true;

    public string Password
    {
        get => _password;
        set { _password = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set { _isVisible = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); }
    }

    public string DisplayText => IsVisible ? Password : new string('•', Password.Length);

    public int Length { get; init; }
    public CharsetType CharsetType { get; init; }

    public ICommand CopyCommand { get; }

    public PasswordCell()
    {
        CopyCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrEmpty(Password))
                Clipboard.SetText(Password);
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class MainViewModel : INotifyPropertyChanged
{
    private static readonly int[] Lengths = [8, 12, 16, 20];
    private static readonly CharsetType[] Charsets =
    [
        CharsetType.Digits,
        CharsetType.Hex,
        CharsetType.LowerAndDigits,
        CharsetType.MixedAndDigits,
        CharsetType.MixedAndDigitsAndSpecial
    ];

    public static int ColCount => Lengths.Length;
    public static int RowCount => Charsets.Length;

    private string _excludeChars = "0OoIl1";
    private string _specialChars = "-_#+!%&/()?=";
    private bool _isPasswordVisible = true;

    public PasswordCell[,] Cells { get; } = new PasswordCell[5, 4];
    public List<PasswordCell> CellList { get; } = new();

    public string ExcludeChars
    {
        get => _excludeChars;
        set { _excludeChars = value; OnPropertyChanged(); RegenerateAll(); }
    }

    public string SpecialChars
    {
        get => _specialChars;
        set { _specialChars = value; OnPropertyChanged(); RegenerateAll(); }
    }

    public bool IsPasswordVisible
    {
        get => _isPasswordVisible;
        set
        {
            _isPasswordVisible = value;
            OnPropertyChanged();
            foreach (var cell in CellList)
                cell.IsVisible = value;
        }
    }

    public ICommand RegenerateCommand { get; }
    public ICommand ToggleVisibilityCommand { get; }

    public MainViewModel()
    {
        for (int row = 0; row < RowCount; row++)
        {
            for (int col = 0; col < ColCount; col++)
            {
                var cell = new PasswordCell
                {
                    Length = Lengths[col],
                    CharsetType = Charsets[row]
                };
                Cells[row, col] = cell;
                CellList.Add(cell);
            }
        }

        RegenerateCommand = new RelayCommand(RegenerateAll);
        ToggleVisibilityCommand = new RelayCommand(() => IsPasswordVisible = !IsPasswordVisible);

        RegenerateAll();
    }

    public void RegenerateAll()
    {
        foreach (var cell in CellList)
        {
            cell.Password = PasswordGeneratorService.Generate(
                cell.Length, cell.CharsetType, _excludeChars, _specialChars);
        }
    }

    public PasswordCell GetCell(int row, int col) => Cells[row, col];

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
