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
        TextFormat inputFormat)
    {
        if (string.IsNullOrWhiteSpace(inputText))
            return new ProcessingResult(string.Empty, false, "Kein Text vorhanden.");

        if (!_settings.HasCredentials)
            return new ProcessingResult(string.Empty, false, "AWS-Zugangsdaten nicht konfiguriert. Bitte Einstellungen öffnen.");

        try
        {
            var prompt = BuildPrompt(inputText, mode, targetLanguage, inputFormat);
            var result = await InvokeClaudeAsync(prompt);
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
            return new ProcessingResult(string.Empty, false, $"Fehler: {ex.Message}");
        }
    }

    private string BuildPrompt(string inputText, ProcessingMode mode, TargetLanguage targetLanguage, TextFormat inputFormat)
    {
        var formatNote = inputFormat == TextFormat.Markdown
            ? "Der Text enthält Markdown-Formatierung. Bitte die Markdown-Struktur (Überschriften, Listen, Fettdruck usw.) exakt erhalten."
            : "Der Text ist unformatierter Klartext.";

        return mode switch
        {
            ProcessingMode.Translate => BuildTranslationPrompt(inputText, targetLanguage, formatNote),
            ProcessingMode.LightCorrection => BuildLightCorrectionPrompt(inputText, formatNote),
            ProcessingMode.ImprovedReformulation => BuildReformulationPrompt(inputText, formatNote),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private static string BuildTranslationPrompt(string inputText, TargetLanguage targetLanguage, string formatNote)
    {
        var targetLang = targetLanguage == TargetLanguage.English ? "Englisch" : "Deutsch";
        return $"""
            Übersetze den folgenden Text ins {targetLang}.
            {formatNote}
            Gib ausschließlich den übersetzten Text zurück, ohne Erklärungen oder Kommentare.

            Text:
            {inputText}
            """;
    }

    private static string BuildLightCorrectionPrompt(string inputText, string formatNote)
    {
        return $"""
            Korrigiere den folgenden Text ausschließlich in Bezug auf Rechtschreib- und Grammatikfehler.
            Verändere NICHT den Stil, die Wortwahl oder den Satzbau. Nur fehlerhafte Stellen korrigieren.
            Wenn kein Fehler gefunden wird, gib den Text unverändert zurück.
            {formatNote}
            Gib ausschließlich den korrigierten Text zurück, ohne Erklärungen oder Kommentare.

            Text:
            {inputText}
            """;
    }

    private static string BuildReformulationPrompt(string inputText, string formatNote)
    {
        return $"""
            Formuliere den folgenden Text in einem gehobenen, professionellen Stil um.
            Behalte den ursprünglichen Inhalt und die Sprache des Textes bei.
            Verbessere Ausdruck, Klarheit und Fluss, ohne die Bedeutung zu verändern.
            {formatNote}
            Gib ausschließlich den umformulierten Text zurück, ohne Erklärungen oder Kommentare.

            Text:
            {inputText}
            """;
    }

    private async Task<string> InvokeClaudeAsync(string prompt)
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
            ModelId = _settings.ModelId,
            ContentType = "application/json",
            Accept = "application/json",
            Body = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(requestJson))
        };

        var response = await client.InvokeModelAsync(request);

        using var reader = new System.IO.StreamReader(response.Body);
        var responseJson = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(responseJson);
        var content = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        return content ?? string.Empty;
    }
}
