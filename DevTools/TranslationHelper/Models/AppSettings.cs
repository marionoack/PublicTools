namespace TranslationHelper.Models;

public class AppSettings
{
    public string AwsAccessKeyId { get; set; } = string.Empty;
    public string AwsSecretKey { get; set; } = string.Empty;
    public string AwsRegion { get; set; } = "us-east-1";
    // "Einfach" (Schlüssel bleibt ModelId, damit bestehende settings.json weiter gilt)
    public string ModelId { get; set; } = "anthropic.claude-sonnet-4-5";
    // "Genau"
    public string ModelIdPrecise { get; set; } = "eu.anthropic.claude-sonnet-5-5";
    public ModelQuality DefaultQuality { get; set; } = ModelQuality.Simple;
    public InputFont InputFont { get; set; } = InputFont.Fixed;
    // Schriftgröße des Eingabetexts, Stufe 1-5 (2 = 13px)
    public int InputFontSizeLevel { get; set; } = 2;
    public ProcessingMode DefaultMode { get; set; } = ProcessingMode.LightCorrection;
    public TargetLanguage DefaultLanguage { get; set; } = TargetLanguage.English;
    public TextFormat DefaultFormat { get; set; } = TextFormat.Plain;

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(AwsAccessKeyId) &&
        !string.IsNullOrWhiteSpace(AwsSecretKey);
}
