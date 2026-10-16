using System.Text.RegularExpressions;

namespace MoleWin.Core.Apps;

// An UninstallString split into program and arguments, so it can run without a shell.
// MSI entries often store the install verb (/I{code}); they become msiexec /x {code}.
public sealed partial record UninstallCommand(string FileName, string Arguments)
{
    public static UninstallCommand Parse(string command)
    {
        var text = command.Trim();
        var msi = MsiProduct().Match(text);
        if (text.StartsWith("msiexec", StringComparison.OrdinalIgnoreCase) && msi.Success)
            return new UninstallCommand("msiexec.exe", $"/x {msi.Value}");

        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            if (end > 0) return new UninstallCommand(text[1..end], text[(end + 1)..].Trim());
        }

        // Unquoted: the program ends at the first ".exe".
        var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe > 0
            ? new UninstallCommand(text[..(exe + 4)], text[(exe + 4)..].Trim())
            : new UninstallCommand(text, "");
    }

    [GeneratedRegex(@"\{[0-9A-Fa-f-]{36}\}")]
    private static partial Regex MsiProduct();
}
