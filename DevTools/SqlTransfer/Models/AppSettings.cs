namespace SqlTransfer.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "";

    public Guid? LastSourceId { get; set; }

    public Guid? LastTargetId { get; set; }
}
