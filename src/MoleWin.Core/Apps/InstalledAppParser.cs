using System.Text.RegularExpressions;

namespace MoleWin.Core.Apps;

// Registry values to an app worth listing. Hidden: system components, updates (entries with
// a parent), entries without a name or uninstaller, and the protected apps below.
public static class InstalledAppParser
{
    // Never offered for uninstall: shared runtimes other programs load, the browser engine
    // Windows and apps embed, Windows' own servicing tools, and hardware drivers. Removing
    // any of these breaks other software or the hardware rather than freeing space.
    private static readonly string[] ProtectedApps =
    [
        "Microsoft Windows*",
        "Microsoft Visual C++ *",
        "Microsoft .NET *",
        "Microsoft ASP.NET Core *",
        "Microsoft Windows Desktop Runtime*",
        ".NET Desktop Runtime*",
        "Microsoft Edge",
        "Microsoft Edge Update",
        "Microsoft Edge WebView2*",
        "Microsoft Update Health Tools",
        "Microsoft GameInput",
        "NVIDIA *Driver*",
        "AMD Software*",
        "AMD Chipset Software",
        "Intel(R) *Driver*",
        "Intel(R) Management Engine*",
        "Realtek *Driver*",
    ];

    private static readonly Regex[] Protected =
        [.. ProtectedApps.Select(p => new Regex("^" + Regex.Escape(p).Replace(@"\*", ".*", StringComparison.Ordinal) + "$", RegexOptions.IgnoreCase))];

    public static InstalledApp? FromValues(string key, IReadOnlyDictionary<string, object?> values)
    {
        var name = Text(values, "DisplayName");
        var uninstall = Text(values, "UninstallString");
        if (name is null || uninstall is null) return null;
        if (values.TryGetValue("SystemComponent", out var system) && system is int and 1) return null;
        if (Text(values, "ParentKeyName") is not null) return null;
        if (Protected.Any(p => p.IsMatch(name))) return null;

        var size = values.TryGetValue("EstimatedSize", out var kb) && kb is int sizeKb ? sizeKb * 1024L : 0;
        DateTime? installed = DateTime.TryParseExact(Text(values, "InstallDate"), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
        return new InstalledApp(key, name, Text(values, "Publisher"), Text(values, "DisplayVersion"), size, installed, uninstall,
            Text(values, "InstallLocation")?.Trim('"').TrimEnd('\\'), Text(values, "DisplayIcon"));
    }

    private static string? Text(IReadOnlyDictionary<string, object?> values, string name) =>
        values.TryGetValue(name, out var value) && value is string text && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;
}
