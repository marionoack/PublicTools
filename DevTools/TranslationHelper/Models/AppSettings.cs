namespace TranslationHelper.Models;

public class AppSettings
{
    public string AwsAccessKeyId { get; set; } = string.Empty;
    public string AwsSecretKey { get; set; } = string.Empty;
    public string AwsRegion { get; set; } = "us-east-1";
    public string ModelId { get; set; } = "anthropic.claude-sonnet-4-5";
    public ProcessingMode DefaultMode { get; set; } = ProcessingMode.LightCorrection;
    public TargetLanguage DefaultLanguage { get; set; } = TargetLanguage.English;
    public TextFormat DefaultFormat { get; set; } = TextFormat.Plain;

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(AwsAccessKeyId) &&
        !string.IsNullOrWhiteSpace(AwsSecretKey);
}
