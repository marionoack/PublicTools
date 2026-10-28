using CommunityToolkit.Mvvm.ComponentModel;
using MimeKit;

namespace EmlExtract.Models;

/// <summary>
/// Represents a single attachment extracted from an EML file, selectable for export.
/// </summary>
public partial class AttachmentItem : ObservableObject
{
    public AttachmentItem(MimeEntity entity, string fileName, long sizeBytes)
    {
        Entity = entity;
        FileName = fileName;
        SizeBytes = sizeBytes;
        IsSelected = true;
    }

    public MimeEntity Entity { get; }

    public string FileName { get; }

    public long SizeBytes { get; }

    public string SizeDisplay => SizeBytes < 1024
        ? $"{SizeBytes} B"
        : SizeBytes < 1024 * 1024
            ? $"{SizeBytes / 1024.0:F1} KB"
            : $"{SizeBytes / 1024.0 / 1024.0:F1} MB";

    [ObservableProperty]
    private bool isSelected;
}
