using TranslationHelper.Models;

namespace TranslationHelper.Services;

public record ProcessingResult(string ResultText, bool Success, string? ErrorMessage);

public interface ITextProcessor
{
    Task<ProcessingResult> ProcessAsync(
        string inputText,
        ProcessingMode mode,
        TargetLanguage targetLanguage,
        TextFormat inputFormat);
}
