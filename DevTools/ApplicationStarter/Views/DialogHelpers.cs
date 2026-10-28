using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ApplicationStarter.Views;

internal static class DialogHelpers
{
    public static void Error(Window? owner, Exception error) =>
        MessageBox.Show(owner, error.Message, "Application Starter", MessageBoxButton.OK, MessageBoxImage.Error);

    public static string? Prompt(Window owner, string title, string label, string initial = "")
    {
        var window = new Window
        {
            Owner = owner,
            Title = title,
            Width = 560,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White
        };
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        var input = new TextBox { Text = initial, MinWidth = 480 };
        panel.Children.Add(input);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80 };
        var cancel = new Button { Content = "Abbrechen", IsCancel = true, MinWidth = 80 };
        ok.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(input.Text)) window.DialogResult = true; };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        window.Content = panel;
        window.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        return window.ShowDialog() == true ? input.Text.Trim() : null;
    }

    public static bool Confirm(Window owner, string message) =>
        MessageBox.Show(owner, message, "Bitte bestaetigen", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public static string ChildPath(string parent, string name)
    {
        if (name is "." or ".." || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 ||
            name.EndsWith('.') || name.EndsWith(' '))
            throw new ArgumentException("Bitte einen gueltigen Namen ohne Pfadangabe eingeben.");
        return System.IO.Path.Combine(parent, name);
    }
}
