using DiskSteward.Core.Apps;
using DiskSteward.Core.Safety;

namespace DiskSteward.Core.Tests.Apps;

public sealed class AppsCoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("disksteward-apps-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static Dictionary<string, object?> Values(string name = "Tool", string uninstall = @"""C:\Tools\unins000.exe""") => new()
    {
        ["DisplayName"] = name,
        ["UninstallString"] = uninstall,
        ["Publisher"] = "Acme",
        ["DisplayVersion"] = "1.2",
        ["EstimatedSize"] = 2048,
        ["InstallDate"] = "20260101",
        ["InstallLocation"] = @"Z:\no-such-drive\Tool\",
    };

    [Fact]
    public void Registry_values_become_an_app()
    {
        var app = InstalledAppParser.FromValues(@"HKCU\x\Tool", Values());

        Assert.NotNull(app);
        Assert.Equal("Tool", app.Name);
        Assert.Equal(2048L * 1024, app.SizeBytes);
        Assert.Equal(new DateTime(2026, 1, 1), app.InstallDate);
    }

    [Fact]
    public void Display_icon_is_read_from_the_registry()
    {
        var values = Values();
        values["DisplayIcon"] = @"""C:\Tools\tool.exe"",0";

        Assert.Equal(@"""C:\Tools\tool.exe"",0", InstalledAppParser.FromValues("k", values)?.IconPath);
    }

    [Theory]
    [InlineData("SystemComponent", 1)]
    [InlineData("ParentKeyName", "OfficeUpdate")]
    public void System_components_and_updates_are_hidden(string key, object value)
    {
        var values = Values();
        values[key] = value;

        Assert.Null(InstalledAppParser.FromValues("k", values));
    }

    [Theory]
    [InlineData("Microsoft Edge")]
    [InlineData("Microsoft Visual C++ 2022 X64 Minimum Runtime")]
    [InlineData("NVIDIA Graphics Driver 560.94")]
    public void Protected_apps_are_hidden(string name) =>
        Assert.Null(InstalledAppParser.FromValues("k", Values(name)));

    [Fact]
    public void Entries_without_an_uninstaller_are_hidden() =>
        Assert.Null(InstalledAppParser.FromValues("k", Values(uninstall: "")));

    [Theory]
    [InlineData(@"""C:\Program Files\Tool\unins000.exe""", @"C:\Program Files\Tool\unins000.exe", "")]
    [InlineData(@"""C:\Program Files\Tool\uninstall.exe"" /x --keep", @"C:\Program Files\Tool\uninstall.exe", "/x --keep")]
    [InlineData(@"C:\Program Files\Tool\uninst.exe /S", @"C:\Program Files\Tool\uninst.exe", "/S")]
    [InlineData(@"C:\Tools\v2.exe.d\uninst.exe /S", @"C:\Tools\v2.exe.d\uninst.exe", "/S")]
    [InlineData(@"C:\Apps\Foo\uninst.exe/S", @"C:\Apps\Foo\uninst.exe", "/S")]
    [InlineData(@"C:\Apps\Foo\uninst.exe""--quiet""", @"C:\Apps\Foo\uninst.exe", @"""--quiet""")]
    public void Uninstall_commands_split_into_program_and_arguments(string command, string file, string arguments)
    {
        var parsed = UninstallCommand.Parse(command);

        Assert.Equal(file, parsed.FileName);
        Assert.Equal(arguments, parsed.Arguments);
    }

    [Fact]
    public void Msi_entries_run_the_system_msiexec_by_its_full_path()
    {
        var parsed = UninstallCommand.Parse("MsiExec.exe /I{12345678-1234-1234-1234-123456789ABC}");

        Assert.Equal(Path.Combine(Environment.SystemDirectory, "msiexec.exe"), parsed.FileName);
        Assert.Equal("/x {12345678-1234-1234-1234-123456789ABC}", parsed.Arguments);
    }

    [Fact]
    public void Leftovers_are_only_folders_named_exactly_after_the_app()
    {
        var roaming = Path.Combine(_root, "Roaming");
        var local = Path.Combine(_root, "Local");
        Directory.CreateDirectory(Path.Combine(roaming, "Tool"));
        Directory.CreateDirectory(Path.Combine(local, "Acme", "Tool"));
        Directory.CreateDirectory(Path.Combine(local, "Acme", "Other"));
        Directory.CreateDirectory(Path.Combine(local, "Toolbox"));
        var app = InstalledAppParser.FromValues("k", Values())!;
        var finder = new LeftoverFinder(new ProtectedPaths(new ProtectedRoots(@"Z:\W", [], @"Z:\P", @"Z:\U", @"Z:\O", [roaming, local])));

        var found = finder.Find(app, new LeftoverRoots([roaming, local], []));

        string[] expected = [Path.Combine(roaming, "Tool"), Path.Combine(local, "Acme", "Tool")];
        Assert.Equal(expected.Order(StringComparer.Ordinal), found.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Leftovers_never_include_a_protected_folder()
    {
        // The install folder exists but sits inside a protected root (as Program Files would).
        var install = Path.Combine(_root, "Tool");
        Directory.CreateDirectory(install);
        var app = InstalledAppParser.FromValues("k", Values())! with { InstallLocation = install };
        var finder = new LeftoverFinder(new ProtectedPaths(new ProtectedRoots(@"Z:\W", [_root], @"Z:\P", @"Z:\U", @"Z:\O", [])));

        Assert.Empty(finder.Find(app, new LeftoverRoots([], [])));
    }

    [Fact]
    public void An_install_folder_that_is_not_a_plain_local_path_is_not_offered()
    {
        // InstallLocation comes from the registry; a share or device spelling is never probed.
        // The device spelling names a real, unprotected local folder here.
        var install = Path.Combine(_root, "Tool");
        Directory.CreateDirectory(install);
        var finder = new LeftoverFinder(new ProtectedPaths(new ProtectedRoots(@"Z:\W", [], @"Z:\P", @"Z:\U", @"Z:\O", [])));

        var app = InstalledAppParser.FromValues("k", Values())! with { InstallLocation = @"\\?\" + install };

        Assert.Empty(finder.Find(app, new LeftoverRoots([], [])));
    }

    [Fact]
    public void A_crafted_publisher_cannot_point_leftovers_outside_the_search_root()
    {
        var search = Path.Combine(_root, "Roaming");
        Directory.CreateDirectory(Path.Combine(_root, "Secrets", "Tool"));
        var finder = new LeftoverFinder(new ProtectedPaths(new ProtectedRoots(@"Z:\W", [], @"Z:\P", @"Z:\U", @"Z:\O", [])));

        var app = InstalledAppParser.FromValues("k", Values())! with { Publisher = @"..\Secrets", InstallLocation = null };

        Assert.Empty(finder.Find(app, new LeftoverRoots([search], [])));
    }

    [Fact]
    public void A_crafted_app_name_cannot_point_leftovers_outside_the_search_root()
    {
        // DisplayName/Publisher come from the registry (user-writable). An absolute value, or
        // one that climbs out with "..", must not turn an unrelated existing folder into a
        // deletable leftover, even when nothing marks that folder protected.
        var search = Path.Combine(_root, "Roaming");
        var outside = Path.Combine(_root, "Secrets");
        Directory.CreateDirectory(outside);
        var finder = new LeftoverFinder(new ProtectedPaths(new ProtectedRoots(@"Z:\W", [], @"Z:\P", @"Z:\U", @"Z:\O", [])));

        foreach (var evil in new[] { outside, @"..\Secrets" })
        {
            var app = InstalledAppParser.FromValues("k", Values(name: evil))! with { InstallLocation = null };
            Assert.Empty(finder.Find(app, new LeftoverRoots([search], [search])));
        }
    }
}
