using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;

namespace SqlTransfer.Infrastructure;

public sealed record LanguageOption(string Code, string Name);

/// <summary>Laufzeit-umschaltbare Texte (Deutsch/Englisch). In XAML: {inf:T Key}.</summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    public static IReadOnlyList<LanguageOption> Languages { get; } =
        [new("de", "Deutsch"), new("en", "English")];

    private string _language = "de";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Language
    {
        get => _language;
        set
        {
            if (_language == value || !Tables.ContainsKey(value))
                return;
            _language = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        }
    }

    public string this[string key] =>
        Tables[_language].TryGetValue(key, out var text) ? text
        : Tables["en"].TryGetValue(key, out text) ? text
        : key;

    public static string T(string key, params object?[] args)
    {
        var text = Instance[key];
        return args.Length == 0 ? text : string.Format(text, args);
    }

    private static readonly Dictionary<string, Dictionary<string, string>> Tables = new()
    {
        ["de"] = new()
        {
            ["App_Servers"] = "Server verwalten…",
            ["App_Language"] = "Sprache",
            ["Src_Header"] = "Quelle",
            ["Dst_Header"] = "Ziel (Restore)",
            ["Mode_New"] = "Neues Backup von einer Datenbank erstellen",
            ["Mode_Existing"] = "Vorhandenes Backup verwenden",
            ["BackupFile"] = "Backup-Datei",
            ["BackupFileHint"] = "Der Zieldatenbankname wird aus dem Dateinamen (Server-Instanz-DB-Datum.bak) abgeleitet und kann rechts korrigiert werden.",
            ["Browse"] = "Durchsuchen…",
            ["Server"] = "Server",
            ["Test"] = "Verbindung testen",
            ["Database"] = "Datenbank",
            ["TargetDb"] = "Datenbankname auf dem Ziel",
            ["Overwrite"] = "Overwrite (bestehende Zieldatenbank überschreiben)",
            ["DeleteSource"] = "Quell-Backup nach erfolgreichem Transfer löschen",
            ["DeleteTarget"] = "Ziel-Backup nach erfolgreichem Transfer löschen",
            ["Swap"] = "Quelle und Ziel tauschen",
            ["Start"] = "Transfer starten",
            ["Cancel"] = "Abbrechen",
            ["Loading"] = "Lade Datenbanken…",
            ["Progress"] = "Ablauf",
            ["TotalTime"] = "Gesamtdauer",
            ["Log"] = "Protokoll",
            ["Step_Prepare"] = "1. Vorbereitung & Prüfung",
            ["Step_Backup"] = "2. Backup auf Quelle",
            ["Step_Copy"] = "3. Backup kopieren",
            ["Step_Restore"] = "4. Restore auf Ziel",
            ["Step_Cleanup"] = "5. Aufräumen",
            ["Srv_Title"] = "Server verwalten",
            ["Srv_List"] = "Gespeicherte Server",
            ["Srv_Add"] = "Hinzufügen",
            ["Srv_Remove"] = "Entfernen",
            ["Srv_Name"] = "Anzeigename (optional)",
            ["Srv_DataSource"] = "Host\\Instanz",
            ["Srv_Auth"] = "Authentifizierung",
            ["Srv_WinAuth"] = "Windows-Authentifizierung",
            ["Srv_SqlAuth"] = "SQL-Server-Authentifizierung",
            ["Srv_User"] = "Benutzer",
            ["Srv_Password"] = "Passwort",
            ["Srv_PasswordNote"] = "Das Passwort wird verschlüsselt (Windows DPAPI, nur für diesen Benutzer) gespeichert.",
            ["Srv_BackupPath"] = "Backup-Pfad (UNC)",
            ["Srv_BackupPathHint"] = "Muss für beide SQL-Server-Dienstkonten und für diese App erreichbar sein, z. B. \\\\server\\backup",
            ["Srv_NewServer"] = "Neuer Server",
            ["Srv_Save"] = "Speichern",
            ["Srv_Invalid"] = "Bitte für jeden Server Host\\Instanz angeben.",
            ["Test_Running"] = "Verbinde…",
            ["Test_Fail"] = "✖ Verbindung fehlgeschlagen: {0}",
            ["Test_Ok"] = "✔ {0}",
            ["Test_PathOk"] = "✔ Backup-Pfad erreichbar",
            ["Test_PathFail"] = "✖ Backup-Pfad nicht erreichbar: {0}",
            ["Test_PathNone"] = "– Kein Backup-Pfad angegeben",
            ["Log_Start"] = "Transfer gestartet: {0} [{1}] → {2} [{3}]",
            ["Err_NoSelection"] = "Bitte Quell- und Zielserver sowie die Quelldatenbank wählen.",
            ["Err_NoBackupFile"] = "Bitte eine Backup-Datei angeben.",
            ["Err_BackupFileMissing"] = "Backup-Datei nicht gefunden oder nicht erreichbar: {0}",
            ["Log_StartExisting"] = "Transfer gestartet: Backup {0} → {1} [{2}]",
            ["Prep_ExistingFile"] = "Quelle: vorhandenes Backup {0}",
            ["Backup_Existing"] = "Vorhandenes Backup wird verwendet: {0}",
            ["Cleanup_SameFile"] = "Die Datei ist zugleich Quelle und Ziel und wurde nicht gelöscht (Löschen nur, wenn Quell- und Ziel-Löschung aktiv sind).",
            ["Cleanup_KeptExisting"] = "Das vorhandene Backup liegt bereits im Zielpfad und wird nicht gelöscht.",
            ["Err_NoTargetName"] = "Bitte einen Datenbanknamen für das Ziel angeben.",
            ["Err_SameServer"] = "Quelle und Ziel sind derselbe SQL-Server ({0}). Ein Transfer innerhalb einer Instanz ist nicht vorgesehen.",
            ["Err_NoPath"] = "Für Server '{0}' ist kein Backup-Pfad hinterlegt.",
            ["Err_Version"] = "Die Quelle ist SQL Server {0}, das Ziel {1}. Ein Restore auf eine ältere Version ist nicht möglich.",
            ["Err_SourceDbMissing"] = "Quelldatenbank '{0}' nicht gefunden oder nicht online.",
            ["Err_TargetExists"] = "Die Zieldatenbank '{0}' existiert bereits auf {1}. Bitte 'Overwrite' aktivieren, um sie zu überschreiben.",
            ["Err_PathUnreachable"] = "Backup-Pfad nicht erreichbar: {0}",
            ["Err_CopySize"] = "Die kopierte Datei hat eine abweichende Größe ({0} statt {1}).",
            ["Prep_Source"] = "Quelle: {0}",
            ["Prep_Target"] = "Ziel: {0}",
            ["Prep_Overwrite"] = "Zieldatenbank '{0}' existiert und wird überschrieben.",
            ["Prep_Ok"] = "Alle Prüfungen bestanden",
            ["Backup_File"] = "Datei: {0}",
            ["Backup_Done"] = "Backup erstellt: {0} ({1})",
            ["Copy_Progress"] = "{0} von {1} · {2}/s · noch {3}",
            ["Copy_Start"] = "Kopiere nach {0}",
            ["Copy_Done"] = "{0} kopiert in {1} ({2}/s)",
            ["Copy_Same"] = "Quell- und Zielpfad sind identisch – Kopieren nicht nötig",
            ["Restore_Reading"] = "Lese Dateiliste des Backups…",
            ["Restore_Running"] = "Stelle '{0}' wieder her…",
            ["Restore_Done"] = "Datenbank '{0}' wiederhergestellt auf {1}",
            ["Restore_CancelNote"] = "Hinweis: Die Zieldatenbank '{0}' kann nach dem Abbruch unvollständig bzw. im Zustand 'Wird wiederhergestellt' sein.",
            ["Cleanup_Off"] = "Nicht aktiviert",
            ["Cleanup_Done"] = "{0} Datei(en) gelöscht",
            ["Cleanup_Warn"] = "Konnte nicht gelöscht werden: {0}",
            ["Skipped"] = "Nicht ausgeführt",
            ["Canceled"] = "Abgebrochen",
            ["Partial_Deleted"] = "Unvollständige Datei gelöscht: {0}",
            ["Partial_DeleteFailed"] = "Unvollständige Datei konnte nicht gelöscht werden: {0}",
            ["Result_Success"] = "Transfer erfolgreich abgeschlossen ({0}).",
            ["Result_Failed"] = "Transfer fehlgeschlagen: {0}",
            ["Result_Canceled"] = "Transfer abgebrochen.",
            ["Close_Confirm"] = "Ein Transfer läuft noch. Abbrechen und Anwendung beenden?",
        },
        ["en"] = new()
        {
            ["App_Servers"] = "Manage servers…",
            ["App_Language"] = "Language",
            ["Src_Header"] = "Source",
            ["Dst_Header"] = "Target (restore)",
            ["Mode_New"] = "Create a new backup of a database",
            ["Mode_Existing"] = "Use an existing backup",
            ["BackupFile"] = "Backup file",
            ["BackupFileHint"] = "The target database name is derived from the file name (Server-Instance-DB-Date.bak) and can be corrected on the right.",
            ["Browse"] = "Browse…",
            ["Server"] = "Server",
            ["Test"] = "Test connection",
            ["Database"] = "Database",
            ["TargetDb"] = "Database name on target",
            ["Overwrite"] = "Overwrite (replace existing target database)",
            ["DeleteSource"] = "Delete source backup after successful transfer",
            ["DeleteTarget"] = "Delete target backup after successful transfer",
            ["Swap"] = "Swap source and target",
            ["Start"] = "Start transfer",
            ["Cancel"] = "Cancel",
            ["Loading"] = "Loading databases…",
            ["Progress"] = "Progress",
            ["TotalTime"] = "Total time",
            ["Log"] = "Log",
            ["Step_Prepare"] = "1. Preparation & checks",
            ["Step_Backup"] = "2. Backup on source",
            ["Step_Copy"] = "3. Copy backup",
            ["Step_Restore"] = "4. Restore on target",
            ["Step_Cleanup"] = "5. Clean up",
            ["Srv_Title"] = "Manage servers",
            ["Srv_List"] = "Saved servers",
            ["Srv_Add"] = "Add",
            ["Srv_Remove"] = "Remove",
            ["Srv_Name"] = "Display name (optional)",
            ["Srv_DataSource"] = "Host\\Instance",
            ["Srv_Auth"] = "Authentication",
            ["Srv_WinAuth"] = "Windows authentication",
            ["Srv_SqlAuth"] = "SQL Server authentication",
            ["Srv_User"] = "User",
            ["Srv_Password"] = "Password",
            ["Srv_PasswordNote"] = "The password is stored encrypted (Windows DPAPI, current user only).",
            ["Srv_BackupPath"] = "Backup path (UNC)",
            ["Srv_BackupPathHint"] = "Must be reachable by both SQL Server service accounts and by this app, e.g. \\\\server\\backup",
            ["Srv_NewServer"] = "New server",
            ["Srv_Save"] = "Save",
            ["Srv_Invalid"] = "Please enter Host\\Instance for every server.",
            ["Test_Running"] = "Connecting…",
            ["Test_Fail"] = "✖ Connection failed: {0}",
            ["Test_Ok"] = "✔ {0}",
            ["Test_PathOk"] = "✔ Backup path reachable",
            ["Test_PathFail"] = "✖ Backup path not reachable: {0}",
            ["Test_PathNone"] = "– No backup path specified",
            ["Log_Start"] = "Transfer started: {0} [{1}] → {2} [{3}]",
            ["Err_NoSelection"] = "Please select source and target server and the source database.",
            ["Err_NoBackupFile"] = "Please specify a backup file.",
            ["Err_BackupFileMissing"] = "Backup file not found or not reachable: {0}",
            ["Log_StartExisting"] = "Transfer started: backup {0} → {1} [{2}]",
            ["Prep_ExistingFile"] = "Source: existing backup {0}",
            ["Backup_Existing"] = "Using existing backup: {0}",
            ["Cleanup_SameFile"] = "The file is both source and target and was not deleted (only deleted when both source and target deletion are enabled).",
            ["Cleanup_KeptExisting"] = "The existing backup is already in the target path and is not deleted.",
            ["Err_NoTargetName"] = "Please enter a database name for the target.",
            ["Err_SameServer"] = "Source and target are the same SQL Server ({0}). Transfers within one instance are not supported.",
            ["Err_NoPath"] = "No backup path is configured for server '{0}'.",
            ["Err_Version"] = "The source is SQL Server {0}, the target is {1}. Restoring to an older version is not possible.",
            ["Err_SourceDbMissing"] = "Source database '{0}' was not found or is not online.",
            ["Err_TargetExists"] = "The target database '{0}' already exists on {1}. Enable 'Overwrite' to replace it.",
            ["Err_PathUnreachable"] = "Backup path not reachable: {0}",
            ["Err_CopySize"] = "The copied file has a different size ({0} instead of {1}).",
            ["Prep_Source"] = "Source: {0}",
            ["Prep_Target"] = "Target: {0}",
            ["Prep_Overwrite"] = "Target database '{0}' exists and will be overwritten.",
            ["Prep_Ok"] = "All checks passed",
            ["Backup_File"] = "File: {0}",
            ["Backup_Done"] = "Backup created: {0} ({1})",
            ["Copy_Progress"] = "{0} of {1} · {2}/s · {3} remaining",
            ["Copy_Start"] = "Copying to {0}",
            ["Copy_Done"] = "{0} copied in {1} ({2}/s)",
            ["Copy_Same"] = "Source and target path are identical – no copy needed",
            ["Restore_Reading"] = "Reading backup file list…",
            ["Restore_Running"] = "Restoring '{0}'…",
            ["Restore_Done"] = "Database '{0}' restored on {1}",
            ["Restore_CancelNote"] = "Note: After cancelling, the target database '{0}' may be incomplete or in the 'Restoring' state.",
            ["Cleanup_Off"] = "Not enabled",
            ["Cleanup_Done"] = "{0} file(s) deleted",
            ["Cleanup_Warn"] = "Could not be deleted: {0}",
            ["Skipped"] = "Not executed",
            ["Canceled"] = "Canceled",
            ["Partial_Deleted"] = "Incomplete file deleted: {0}",
            ["Partial_DeleteFailed"] = "Incomplete file could not be deleted: {0}",
            ["Result_Success"] = "Transfer completed successfully ({0}).",
            ["Result_Failed"] = "Transfer failed: {0}",
            ["Result_Canceled"] = "Transfer canceled.",
            ["Close_Confirm"] = "A transfer is still running. Cancel it and exit?",
        },
    };
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class TExtension : MarkupExtension
{
    public TExtension(string key) => Key = key;

    public string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
