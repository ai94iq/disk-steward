using DiskSteward.Core.Safety;
using DiskSteward.Core.Tests;

namespace DiskSteward.Core.Tests.Safety;

public sealed class LocalPathTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("disksteward-localpath-").FullName;

    public void Dispose() => TestJunctions.DeleteTree(_root);

    [Theory]
    [InlineData(@"C:\Program Files\Tool\tool.exe")]
    [InlineData(@"c:/tools/tool.ico")]
    public void Plain_drive_paths_are_local(string path) => Assert.True(LocalPath.IsOnLocalDrive(path));

    [Theory]
    [InlineData(@"\\server\share\tool.ico")]
    [InlineData(@"\\?\UNC\server\share\tool.ico")]
    [InlineData(@"\\?\C:\Tools\tool.ico")]
    [InlineData(@"\\.\C:\Tools\tool.ico")]
    [InlineData(@"C:tool.ico")]
    [InlineData(@"tool.ico")]
    [InlineData("")]
    public void Network_device_and_relative_paths_are_not(string path) => Assert.False(LocalPath.IsOnLocalDrive(path));

    [Fact]
    public void A_drive_letter_with_nothing_behind_it_is_not_local()
    {
        // A disconnected mapped drive also reports no root; opening it could reconnect the share.
        var free = Enumerable.Range('D', 'Z' - 'D' + 1).Select(c => (char)c)
            .FirstOrDefault(c => new DriveInfo(c.ToString()).DriveType == DriveType.NoRootDirectory);
        if (free == default)
        {
            Assert.Skip("Every drive letter is in use on this machine.");
            return;
        }

        Assert.False(LocalPath.IsOnLocalDrive($@"{free}:\Tools\tool.ico"));
    }

    [Fact]
    public void A_plain_path_with_no_link_on_it_does_not_cross_a_link()
    {
        var file = Path.Combine(_root, "sub", "tool.ico");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, [0]);

        Assert.False(LocalPath.CrossesLink(file));
    }

    [Fact]
    public void A_missing_path_does_not_cross_a_link()
    {
        Assert.False(LocalPath.CrossesLink(Path.Combine(_root, "none", "tool.ico")));
    }

    [Fact]
    public void A_link_at_the_leaf_crosses_a_link()
    {
        var target = Path.Combine(_root, "real");
        Directory.CreateDirectory(target);
        var link = Path.Combine(_root, "leaf");
        if (!TestJunctions.TryCreate(link, target))
        {
            Assert.Skip("Could not create a directory junction on this machine.");
            return;
        }

        Assert.True(LocalPath.CrossesLink(link));
    }

    [Fact]
    public void A_link_above_the_leaf_crosses_a_link_without_reading_past_it()
    {
        // The leaf below the link is never queried, so a link to an unreachable place is still caught.
        var target = Path.Combine(_root, "real");
        Directory.CreateDirectory(target);
        var link = Path.Combine(_root, "mid");
        if (!TestJunctions.TryCreate(link, target))
        {
            Assert.Skip("Could not create a directory junction on this machine.");
            return;
        }

        Assert.True(LocalPath.CrossesLink(Path.Combine(link, "deeper", "tool.ico")));
    }
}
