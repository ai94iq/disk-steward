using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DiskSteward.Core.Safety;

namespace DiskSteward.App.Common;

// An installed app's own icon from its DisplayIcon value ("path" or "path,index", sometimes
// quoted, sometimes with %variables%). Null when there is none or it cannot be read. Safe off
// the UI thread (the image is frozen), so the Apps list reads icons in the background instead
// of stalling while it scrolls. Cached by DisplayIcon value.
public static class AppIcons
{
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? Load(string? iconPath) =>
        string.IsNullOrWhiteSpace(iconPath) ? null : Cache.GetOrAdd(iconPath, Read);

    private static ImageSource? Read(string iconPath)
    {
        var (file, index) = Split(iconPath);
        if (!LocalPath.IsOnLocalDrive(file) || LocalPath.CrossesLink(file) || !File.Exists(file)) return null;

        var large = new IntPtr[1];
        if (ExtractIconExW(file, index, large, null, 1) == 0 || large[0] == IntPtr.Zero) return null;
        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(large[0], Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException)
        {
            return null;
        }
        finally
        {
            DestroyIcon(large[0]);
        }
    }

    private static (string File, int Index) Split(string iconPath)
    {
        var text = Environment.ExpandEnvironmentVariables(iconPath.Trim());
        var index = 0;
        var comma = text.LastIndexOf(',');
        if (comma > 0 && int.TryParse(text[(comma + 1)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            index = parsed;
            text = text[..comma];
        }
        return (text.Trim().Trim('"'), index);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconExW(string file, int index, IntPtr[]? large, IntPtr[]? small, uint count);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);
}
