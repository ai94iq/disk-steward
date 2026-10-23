using System.Diagnostics;

namespace DiskSteward.Core.Tests;

public static class TestJunctions
{
    // A real directory junction (mklink /J needs no admin). False when this machine cannot make one.
    public static bool TryCreate(string link, string target)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            process!.WaitForExit();
            return process.ExitCode == 0 && Directory.Exists(link);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            return false;
        }
    }

    // Deletes a temp tree that may hold junctions, removing each junction as a link first so the
    // recursive delete never walks into a junction's target.
    public static void DeleteTree(string root)
    {
        RemoveLinks(root);
        Directory.Delete(root, recursive: true);
    }

    private static void RemoveLinks(string dir)
    {
        foreach (var sub in Directory.GetDirectories(dir))
            if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0) Directory.Delete(sub);
            else RemoveLinks(sub);
    }
}
