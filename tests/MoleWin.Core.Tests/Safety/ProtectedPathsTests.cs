using MoleWin.Core.Safety;

namespace MoleWin.Core.Tests.Safety;

public sealed class ProtectedPathsTests
{
    private static readonly ProtectedPaths Rules = new(new ProtectedRoots(
        Windows: @"C:\Windows",
        ProgramFiles: [@"C:\Program Files", @"C:\Program Files (x86)"],
        ProgramData: @"C:\ProgramData",
        UserProfile: @"C:\Users\me",
        AppOwnData: @"C:\Users\me\AppData\Local\MoleWin",
        KnownFolders: [@"C:\Users\me\Desktop", @"C:\Users\me\Documents", @"C:\Users\me\AppData\Local"]));

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"D:\")]
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\windows\system32\drivers")]
    [InlineData(@"C:\Program Files (x86)\Steam")]
    [InlineData(@"C:\ProgramData\Microsoft")]
    [InlineData(@"C:\Users")]
    [InlineData(@"C:\Users\me")]
    [InlineData(@"C:\Users\me\")]
    [InlineData(@"C:\Users\me\Desktop")]
    [InlineData(@"C:\Users\me\AppData")]
    [InlineData(@"C:\Users\me\AppData\Local")]
    [InlineData(@"C:\Users\me\AppData\Local\MoleWin\logs")]
    [InlineData(@"D:\$Recycle.Bin\S-1-5")]
    [InlineData(@"D:\System Volume Information")]
    [InlineData(@"relative\path")]
    [InlineData("")]
    public void System_and_profile_roots_are_protected(string path) =>
        Assert.True(Rules.IsProtected(path));

    [Theory]
    [InlineData(@"C:\Users\me\Desktop\old.zip")]
    [InlineData(@"C:\Users\me\AppData\Local\Temp")]
    [InlineData(@"C:\Users\me\AppData\Local\NVIDIA\DXCache")]
    [InlineData(@"D:\Games\cache")]
    [InlineData(@"C:\Users\me\Downloads")]
    [InlineData(@"C:\WindowsApps-backup")]
    public void Ordinary_content_is_not_protected(string path) =>
        Assert.False(Rules.IsProtected(path));

    [Fact]
    public void Dot_segments_cannot_escape_into_a_protected_root() =>
        Assert.True(Rules.IsProtected(@"C:\Users\me\Desktop\..\..\..\Windows\System32"));
}
