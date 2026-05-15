namespace TranslationHelper.Services;

public class ClipboardService
{
    public string ReadText()
    {
        try
        {
            return System.Windows.Clipboard.ContainsText()
                ? System.Windows.Clipboard.GetText()
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public void WriteText(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
        }
        catch { }
    }
}
