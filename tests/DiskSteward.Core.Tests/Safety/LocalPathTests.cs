using DiskSteward.Core.Safety;

namespace DiskSteward.Core.Tests.Safety;

public sealed class LocalPathTests
{
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
}
