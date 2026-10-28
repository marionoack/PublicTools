using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ApplicationStarter.Models;

namespace ApplicationStarter.Services;

public static class IconService
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconExW(string file, int index, out IntPtr large, out IntPtr small, uint count);
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    public static ImageSource Get(Shortcut shortcut, UserProfile profile, string dataFolder)
    {
        var iconPath = shortcut.IconPath;
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            var cached = CachePath(shortcut, profile, dataFolder);
            if (File.Exists(cached)) iconPath = cached;
            else if (shortcut.IsPowerShellScript) iconPath = PowerShellScripts.ExecutablePath(shortcut.Kind);
            else if (profile.IsCurrent || !shortcut.FileName.Contains('%')) iconPath = shortcut.FileName;
        }
        iconPath = Environment.ExpandEnvironmentVariables(iconPath);
        return ReadImage(iconPath) ?? BitmapFrame.Create(new Uri("pack://application:,,,/Assets/Launcher.ico"));
    }

    public static byte[] TargetIcon(Shortcut shortcut)
    {
        var image = ReadImage(shortcut.IsPowerShellScript ? PowerShellScripts.ExecutablePath(shortcut.Kind) : FileOperations.Expand(shortcut.FileName));
        if (image is null) return [];
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    public static void Cache(Shortcut shortcut, UserProfile profile, string dataFolder, byte[] png)
    {
        if (png.Length == 0) return;
        var path = CachePath(shortcut, profile, dataFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, png);
    }

    private static string CachePath(Shortcut shortcut, UserProfile profile, string folder)
    {
        var target = shortcut.IsPowerShellScript ? PowerShellScripts.ExecutablePath(shortcut.Kind) : shortcut.FileName;
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(profile.Id + "|" + profile.UserName + "|" + target)));
        return Path.Combine(folder, "Icons", key + ".png");
    }

    private static BitmapSource? ReadImage(string iconPath)
    {
        try
        {
            if (File.Exists(iconPath))
            {
                if (Path.GetExtension(iconPath).Equals(".ico", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetExtension(iconPath).Equals(".png", StringComparison.OrdinalIgnoreCase))
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.UriSource = new Uri(Path.GetFullPath(iconPath));
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
                ExtractIconExW(iconPath, 0, out var large, out var small, 1);
                try
                {
                    var handle = small != IntPtr.Zero ? small : large;
                    if (handle != IntPtr.Zero)
                    {
                        var image = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        image.Freeze();
                        return image;
                    }
                }
                finally
                {
                    if (large != IntPtr.Zero) DestroyIcon(large);
                    if (small != IntPtr.Zero) DestroyIcon(small);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or System.UnauthorizedAccessException) { }
        return null;
    }
}
