using System.IO;
using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;
using TranslationHelper.Models;

namespace TranslationHelper.Services;

public class BedrockProcessor : ITextProcessor
{
    private AppSettings _settings;

    public BedrockProcessor(AppSettings settings)
    {
        _settings = settings;
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task<ProcessingResult> ProcessAsync(
        string inputText,
        ProcessingMode mode,
        TargetLanguage targetLanguage,
        TextFormat inputFormat,
        Tone tone,
        Audience audience,
        ModelQuality quality,
        Scope? scope)
    {
        if (string.IsNullOrWhiteSpace(inputText))
            return new ProcessingResult(string.Empty, false, "Kein Text vorhanden.");

        if (!_settings.HasCredentials)
            return new ProcessingResult(string.Empty, false, "AWS-Zugangsdaten nicht konfiguriert. Bitte Einstellungen öffnen.");

        var modelId = quality == ModelQuality.Precise && !string.IsNullOrWhiteSpace(_settings.ModelIdPrecise)
            ? _settings.ModelIdPrecise
            : _settings.ModelId;

        try
        {
            var prompt = BuildPrompt(inputText, mode, targetLanguage, inputFormat, tone, audience, scope);
            var result = await InvokeClaudeAsync(prompt, modelId);
            return new ProcessingResult(result, true, null);
        }
        catch (AmazonBedrockRuntimeException ex) when (ex.Message.Contains("UnrecognizedClientException") || ex.Message.Contains("InvalidClientTokenId"))
        {
            return new ProcessingResult(string.Empty, false, "AWS-Authentifizierung fehlgeschlagen. Bitte Zugangsdaten prüfen.");
        }
        catch (AmazonBedrockRuntimeException ex) when (ex.Message.Contains("ThrottlingException"))
        {
            return new ProcessingResult(string.Empty, false, "Anfrage wurde gedrosselt. Bitte einen Moment warten.");
        }
        catch (AmazonBedrockRuntimeException ex) when (ex.Message.Contains("AccessDeniedException"))
        {
            return new ProcessingResult(string.Empty, false, "Zugriff verweigert. Bitte Bedrock-Modellzugriff in AWS prüfen.");
        }
        catch (Exception ex) when (ex.Message.Contains("HttpRequest") || ex.Message.Contains("connect"))
        {
            return new ProcessingResult(string.Empty, false, "Verbindungsfehler. Bitte Internetverbindung prüfen.");
        }
        catch (Exception ex)
        {
            LogError(ex);
            return new ProcessingResult(string.Empty, false, $"Fehler: {ex.Message}");
        }
    }

    private static void LogError(Exception ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TranslationHelper");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"), $"[{DateTime.Now:s}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { }
    }

    private string BuildPrompt(string inputText, ProcessingMode mode, TargetLanguage targetLanguage, TextFormat inputFormat, Tone tone, Audience audience, Scope? scope)
    {
        var formatNote = inputFormat == TextFormat.Markdown
            ? "Der Text enthält Markdown-Formatierung. Bitte die Markdown-Struktur (Überschriften, Listen, Fettdruck usw.) exakt erhalten."
            : "Der Text ist unformatierter Klartext.";

        var scopeNote = BuildScopeNote(scope);
        var glossaryNote = BuildGlossaryNote(scope, mode, targetLanguage);

        // Flair wird bei LightCorrection bewusst nicht angehängt.
        var flairNote = mode == ProcessingMode.LightCorrection ? string.Empty : BuildFlairNote(tone, audience);

        var extra = string.Join("\n", new[] { flairNote, scopeNote, glossaryNote }.Where(s => !string.IsNullOrEmpty(s)));
        if (extra.Length > 0) extra = "\n" + extra;

        return mode switch
        {
            ProcessingMode.Translate => BuildTranslationPrompt(inputText, targetLanguage, formatNote, extra),
            ProcessingMode.LightCorrection => BuildLightCorrectionPrompt(inputText, formatNote, extra),
            ProcessingMode.ImprovedReformulation => BuildReformulationPrompt(inputText, formatNote, extra),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private static string BuildScopeNote(Scope? scope)
    {
        if (scope == null || scope.IsNone || string.IsNullOrWhiteSpace(scope.Name)) return string.Empty;
        return $"Fachlicher Kontext (Scope): \"{OneLine(scope.Name)}\". Verwende dazu passende Fachterminologie.";
    }

    private static string BuildGlossaryNote(Scope? scope, ProcessingMode mode, TargetLanguage targetLanguage)
    {
        if (scope == null || scope.IsNone) return string.Empty;

        var entries = scope.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.German) && !string.IsNullOrWhiteSpace(e.English))
            .ToList();
        if (entries.Count == 0) return string.Empty;

        var sb = new StringBuilder();

        if (mode == ProcessingMode.Translate)
        {
            var toEnglish = targetLanguage == TargetLanguage.English;
            sb.AppendLine(toEnglish
                ? "Fachwörterbuch (Deutsch → Englisch). Verwende diese Fachbegriffe verbindlich, sobald sie im Text vorkommen:"
                : "Fachwörterbuch (Englisch → Deutsch). Verwende diese Fachbegriffe verbindlich, sobald sie im Text vorkommen:");
            foreach (var e in entries)
            {
                sb.AppendLine(toEnglish
                    ? $"- {OneLine(e.German)} → {OneLine(e.English)}"
                    : $"- {OneLine(e.English)} → {OneLine(e.German)}");
            }
        }
        else
        {
            sb.AppendLine("Fachwörterbuch (Deutsch = Englisch). Kommen diese Fachbegriffe im Text vor, verwende sie exakt in der hier genannten Schreibweise der jeweiligen Textsprache und übersetze sie nicht in die andere Sprache:");
            foreach (var e in entries)
                sb.AppendLine($"- {OneLine(e.German)} = {OneLine(e.English)}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string OneLine(string s) =>
        s.Replace("\r", " ").Replace("\n", " ").Trim();

    private static string BuildFlairNote(Tone tone, Audience audience)
    {
        var parts = new List<string>();

        var toneDesc = tone switch
        {
            Tone.Formal => "förmlich (Sie-Form, geschäftlich korrekt)",
            Tone.Friendly => "freundlich (höflich aber nahbar)",
            Tone.Direct => "direkt (knapp, auf den Punkt)",
            Tone.Casual => "locker (umgangssprachlich, Du-Form)",
            _ => null
        };

        var audienceDesc = audience switch
        {
            Audience.Customer => "an einen Kunden (extern, professionell)",
            Audience.Colleague => "an einen Kollegen (intern, unkompliziert)",
            Audience.Superior => "an einen Vorgesetzten (respektvoll, strukturiert)",
            _ => null
        };

        if (toneDesc != null) parts.Add($"Tonalität: {toneDesc}");
        if (audienceDesc != null) parts.Add($"Zielgruppe: {audienceDesc}");

        if (parts.Count == 0) return string.Empty;

        return "Bitte den folgenden Stil verwenden: " + string.Join(". ", parts) + ".";
    }

    private static string BuildTranslationPrompt(string inputText, TargetLanguage targetLanguage, string formatNote, string extra)
    {
        var targetLang = targetLanguage == TargetLanguage.English ? "Englisch" : "Deutsch";
        return $"""
            Übersetze den folgenden Text ins {targetLang}.
            {formatNote}{extra}
            Gib ausschließlich den übersetzten Text zurück, ohne Erklärungen oder Kommentare.

            Text:
            {inputText}
            """;
    }

    private static string BuildLightCorrectionPrompt(string inputText, string formatNote, string extra)
    {
        return $"""
            Korrigiere den folgenden Text ausschließlich in Bezug auf Rechtschreib- und Grammatikfehler.
            Verändere NICHT den Stil, die Wortwahl oder den Satzbau. Nur fehlerhafte Stellen korrigieren.
            Wenn kein Fehler gefunden wird, gib den Text unverändert zurück.
            {formatNote}{extra}
            Gib ausschließlich den korrigierten Text zurück, ohne Erklärungen oder Kommentare.

            Text:
            {inputText}
            """;
    }

    private static string BuildReformulationPrompt(string inputText, string formatNote, string extra)
    {
        return $"""
            Formuliere den folgenden Text in einem gehobenen, professionellen Stil um.
            Behalte den ursprünglichen Inhalt und die Sprache des Textes bei.
            Verbessere Ausdruck, Klarheit und Fluss, ohne die Bedeutung zu verändern.
            {formatNote}{extra}
            Gib ausschließlich den umformulierten Text zurück, ohne Erklärungen oder Kommentare.

            Text:
            {inputText}
            """;
    }

    private async Task<string> InvokeClaudeAsync(string prompt, string modelId)
    {
        var credentials = new BasicAWSCredentials(_settings.AwsAccessKeyId, _settings.AwsSecretKey);
        var region = RegionEndpoint.GetBySystemName(_settings.AwsRegion);

        using var client = new AmazonBedrockRuntimeClient(credentials, region);

        var requestBody = new
        {
            anthropic_version = "bedrock-2023-05-31",
            max_tokens = 4096,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var requestJson = JsonSerializer.Serialize(requestBody);

        var request = new InvokeModelRequest
        {
            ModelId = modelId,
            ContentType = "application/json",
            Accept = "application/json",
            Body = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(requestJson))
        };

        var response = await client.InvokeModelAsync(request);

        using var reader = new System.IO.StreamReader(response.Body);
        var responseJson = await reader.ReadToEndAsync();

        return ExtractText(responseJson);
    }

    private static string ExtractText(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        var sb = new StringBuilder();
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                if (block.ValueKind == JsonValueKind.Object
                    && block.TryGetProperty("text", out var text)
                    && text.ValueKind == JsonValueKind.String)
                {
                    sb.Append(text.GetString());
                }
            }
        }

        if (sb.Length > 0)
            return sb.ToString();

        var stopReason = root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("stop_reason", out var sr) && sr.ValueKind == JsonValueKind.String
                ? sr.GetString()
                : null;
        var raw = responseJson.Length > 300 ? responseJson[..300] + "…" : responseJson;
        throw new InvalidOperationException(
            $"Unerwartete Antwort vom Modell (stop_reason: {stopReason ?? "unbekannt"}): {raw}");
    }
}
