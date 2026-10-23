using System.Text.RegularExpressions;

namespace DiskSteward.Core.Apps;

// An UninstallString split into program and arguments, so it can run without a shell.
// MSI entries often store the install verb (/I{code}); they become msiexec /x {code}.
public sealed partial record UninstallCommand(string FileName, string Arguments)
{
    public static UninstallCommand Parse(string command)
    {
        var text = command.Trim();
        var msi = MsiProduct().Match(text);
        // The system's own msiexec by full path, not whichever one the search order finds.
        if (text.StartsWith("msiexec", StringComparison.OrdinalIgnoreCase) && msi.Success)
            return new UninstallCommand(Path.Combine(Environment.SystemDirectory, "msiexec.exe"), $"/x {msi.Value}");

        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            if (end > 0) return new UninstallCommand(text[1..end], text[(end + 1)..].Trim());
        }

        // Unquoted: the program ends at the first ".exe" followed by a space, a switch ("/S"), a
        // quote or the end, so a folder named "v2.exe.d" stays part of the path.
        var exe = ExeEnd().Match(text);
        return exe.Success
            ? new UninstallCommand(text[..exe.Length], text[exe.Length..].Trim())
            : new UninstallCommand(text, "");
    }

    [GeneratedRegex(@"\{[0-9A-Fa-f-]{36}\}")]
    private static partial Regex MsiProduct();

    [GeneratedRegex(@"^.+?\.exe(?=[\s/""]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex ExeEnd();
}
