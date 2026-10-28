using System.Diagnostics;
using SqlTransfer.Infrastructure;

namespace SqlTransfer.ViewModels;

public enum StepStatus { Pending, Running, Done, Warning, Failed, Canceled, Skipped }

public sealed class StepViewModel : ObservableObject
{
    private readonly string _titleKey;
    private readonly Stopwatch _clock = new();
    private StepStatus _status;
    private double _progress;
    private bool _isIndeterminate;
    private string _detail = "";
    private string _elapsed = "";

    public StepViewModel(string titleKey)
    {
        _titleKey = titleKey;
        Loc.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Title));
    }

    public string Title => Loc.T(_titleKey);

    public StepStatus Status
    {
        get => _status;
        private set
        {
            if (Set(ref _status, value))
                OnPropertyChanged(nameof(Glyph));
        }
    }

    public string Glyph => Status switch
    {
        StepStatus.Running => "▶",
        StepStatus.Done => "✔",
        StepStatus.Warning => "⚠",
        StepStatus.Failed => "✖",
        StepStatus.Canceled => "■",
        StepStatus.Skipped => "–",
        _ => "○",
    };

    public double Progress { get => _progress; private set => Set(ref _progress, value); }

    public bool IsIndeterminate { get => _isIndeterminate; private set => Set(ref _isIndeterminate, value); }

    public string Detail { get => _detail; private set => Set(ref _detail, value); }

    public string Elapsed { get => _elapsed; private set => Set(ref _elapsed, value); }

    public void Reset()
    {
        _clock.Reset();
        Status = StepStatus.Pending;
        Progress = 0;
        IsIndeterminate = false;
        Detail = "";
        Elapsed = "";
    }

    public void Start(string? detail = null, bool indeterminate = true)
    {
        _clock.Restart();
        Status = StepStatus.Running;
        Progress = 0;
        IsIndeterminate = indeterminate;
        Detail = detail ?? "";
        Tick();
    }

    public void Report(double percent, string detail)
    {
        if (Status != StepStatus.Running)
            return;
        IsIndeterminate = false;
        Progress = percent;
        Detail = detail;
    }

    public void SetDetail(string detail) => Detail = detail;

    public void Complete(string? detail = null, bool warning = false)
    {
        Finish(warning ? StepStatus.Warning : StepStatus.Done, detail);
        Progress = 100;
    }

    public void Fail(string detail) => Finish(StepStatus.Failed, detail);

    public void Cancel() => Finish(StepStatus.Canceled, Loc.T("Canceled"));

    public void Skip(string? detail = null)
    {
        Status = StepStatus.Skipped;
        IsIndeterminate = false;
        Detail = detail ?? Loc.T("Skipped");
    }

    public void Tick()
    {
        if (Status == StepStatus.Running)
            Elapsed = Fmt.Duration(_clock.Elapsed);
    }

    private void Finish(StepStatus status, string? detail)
    {
        _clock.Stop();
        IsIndeterminate = false;
        Status = status;
        if (detail is not null)
            Detail = detail;
        Elapsed = Fmt.Duration(_clock.Elapsed);
    }
}
