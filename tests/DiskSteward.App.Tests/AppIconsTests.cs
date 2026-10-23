using System.Diagnostics;
using DiskSteward.App.Common;

namespace DiskSteward.App.Tests;

public sealed class AppIconsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("disksteward-icons-").FullName;

    public void Dispose()
    {
        // Remove each junction as a link first, so the recursive delete never walks into its target.
        foreach (var sub in Directory.GetDirectories(_root))
            if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0) Directory.Delete(sub);
        Directory.Delete(_root, recursive: true);
    }

    private static readonly string IconExe = Path.Combine(Environment.SystemDirectory, "notepad.exe");

    [Fact]
    public void A_plain_local_icon_path_loads()
    {
        Assert.SkipUnless(File.Exists(IconExe), "No icon-bearing system exe to read.");
        Assert.NotNull(AppIcons.Load(IconExe));
    }

    [Fact]
    public void An_icon_path_reached_through_a_link_is_not_loaded()
    {
        Assert.SkipUnless(File.Exists(IconExe), "No icon-bearing system exe to read.");
        var link = Path.Combine(_root, "sys");
        if (!TryJunction(link, Environment.SystemDirectory))
        {
            Assert.Skip("Could not create a directory junction on this machine.");
            return;
        }

        Assert.Null(AppIcons.Load(Path.Combine(link, "notepad.exe")));
    }

    private static bool TryJunction(string link, string target)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
            p.WaitForExit();
            return p.ExitCode == 0 && Directory.Exists(link);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            return false;
        }
    }
}
