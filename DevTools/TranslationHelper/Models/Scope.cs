using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace TranslationHelper.Models;

public class GlossaryEntry
{
    public string German { get; set; } = string.Empty;
    public string English { get; set; } = string.Empty;
}

public class Scope
{
    // Sentinel für "kein Scope" in der Auswahl; wird nie persistiert.
    [JsonIgnore]
    public static Scope None { get; } = new() { Name = "—" };

    public string Name { get; set; } = string.Empty;
    public ObservableCollection<GlossaryEntry> Entries { get; set; } = new();

    [JsonIgnore]
    public bool IsNone => ReferenceEquals(this, None);
}
