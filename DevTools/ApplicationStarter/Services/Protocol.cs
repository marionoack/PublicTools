using System.Text.Json;
using ApplicationStarter.Models;

namespace ApplicationStarter.Services;

public sealed record WireMessage(int Id, string Type, JsonElement Data, string? Error = null)
{
    public static WireMessage Create(int id, string type, object? data = null, string? error = null) =>
        new(id, type, JsonSerializer.SerializeToElement(data), error);
    public T Read<T>() => Data.Deserialize<T>() ?? throw new InvalidDataException("Leere Prozessantwort.");
}

public sealed record StartRequest(Shortcut Shortcut, int Columns = 100, int Rows = 30);
public sealed record SizeRequest(int Columns, int Rows);
public sealed record ElevationRequest(string PipeName, int ParentId);
public sealed record FileRequest(string Operation, string Path, string? Destination = null, bool Overwrite = false);
public sealed record FileEntry(string Name, string Path, bool IsDirectory, bool Hidden, long? Size, DateTime Modified);
public sealed record DirectoryListing(string Path, List<FileEntry> Entries);
public sealed record WorkerIdentity(string Name, string UserProfile, string LocalAppData,
    string Sid = "", bool IsAdministrator = false, bool IsElevated = false)
{
    public void EnsureElevatedFor(string expectedSid)
    {
        if (!IsElevated || string.IsNullOrEmpty(expectedSid) || !string.Equals(expectedSid, Sid, StringComparison.Ordinal))
            throw new IOException("UAC hat nicht dasselbe Konto erhoeht. Der Skriptstart wurde abgelehnt; ein Kontowechsel ist nicht erlaubt.");
    }
}
public sealed record ExecutionOutput(string Stream, string Text);
