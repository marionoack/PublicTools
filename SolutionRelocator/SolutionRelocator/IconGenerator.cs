using System.IO;
using System.Windows.Media.Imaging;

namespace SolutionRelocator;

public static class IconGenerator
{
    public static void EnsureIconExists(string iconPath, string pngSourcePath)
    {
        if (File.Exists(iconPath)) return;

        if (!File.Exists(pngSourcePath)) return;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(pngSourcePath, UriKind.Absolute);
        bitmap.DecodePixelWidth = 64;
        bitmap.DecodePixelHeight = 64;
        bitmap.EndInit();

        var resized = new FormatConvertedBitmap(bitmap, System.Windows.Media.PixelFormats.Bgra32, null, 0);

        using var pngStream = new MemoryStream();
        var pngEncoder = new PngBitmapEncoder();
        pngEncoder.Frames.Add(BitmapFrame.Create(resized));
        pngEncoder.Save(pngStream);
        var pngBytes = pngStream.ToArray();

        using var fs = new FileStream(iconPath, FileMode.Create);
        using var bw = new BinaryWriter(fs);

        // ICO header
        bw.Write((short)0);   // reserved
        bw.Write((short)1);   // type: icon
        bw.Write((short)1);   // count

        // ICO directory entry
        bw.Write((byte)64);          // width
        bw.Write((byte)64);          // height
        bw.Write((byte)0);           // color palette
        bw.Write((byte)0);           // reserved
        bw.Write((short)1);          // color planes
        bw.Write((short)32);         // bits per pixel
        bw.Write(pngBytes.Length);    // image size
        bw.Write(22);                // offset (6 header + 16 entry)

        // PNG data
        bw.Write(pngBytes);
    }
}
