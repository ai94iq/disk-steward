using DiskSteward.Core.Safety;

namespace DiskSteward.Core.Tests.Safety;

public sealed class ProtectedPathsTests
{
    private static readonly ProtectedPaths Rules = new(new ProtectedRoots(
        Windows: @"C:\Windows",
        ProgramFiles: [@"C:\Program Files", @"C:\Program Files (x86)"],
        ProgramData: @"C:\ProgramData",
        UserProfile: @"C:\Users\me",
        AppOwnData: @"C:\Users\me\AppData\Local\DiskSteward",
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
    [InlineData(@"C:\Users\me\AppData\Local\DiskSteward\logs")]
    [InlineData(@"D:\$Recycle.Bin\S-1-5")]
    [InlineData(@"D:\System Volume Information")]
    [InlineData(@"relative\path")]
    [InlineData("")]
    [InlineData(@"\\?\C:\Windows\System32")]            // extended-length prefix must not bypass
    [InlineData(@"\\.\C:\Program Files (x86)\Steam")]   // device prefix must not bypass
    public void System_and_profile_roots_are_protected(string path) =>
        Assert.True(Rules.IsProtected(path));

    [Theory]
    [InlineData(@"C:\Users\me\Desktop\old.zip", ProtectionReason.None)]
    [InlineData(@"C:\", ProtectionReason.DriveRoot)]
    [InlineData(@"C:\pagefile.sys", ProtectionReason.VolumeSystem)]
    [InlineData(@"C:\Windows\System32", ProtectionReason.Windows)]
    [InlineData(@"C:\Program Files (x86)\Steam", ProtectionReason.Programs)]
    [InlineData(@"C:\ProgramData\Microsoft", ProtectionReason.ProgramData)]
    [InlineData(@"C:\Users\me\AppData\Local\DiskSteward\logs", ProtectionReason.AppOwnData)]
    [InlineData(@"C:\Users\me\Documents", ProtectionReason.PersonalFolder)]
    [InlineData(@"relative\path", ProtectionReason.NotAFullPath)]
    public void Each_protected_path_says_why(string path, ProtectionReason expected) =>
        Assert.Equal(expected, Rules.ReasonFor(path));

    [Theory]
    [InlineData(@"C:\Users\me\Desktop\old.zip")]
    [InlineData(@"C:\Users\me\AppData\Local\Temp")]
    [InlineData(@"C:\Users\me\AppData\Local\NVIDIA\DXCache")]
    [InlineData(@"D:\Games\cache")]
    [InlineData(@"C:\Users\me\Downloads")]
    [InlineData(@"C:\WindowsApps-backup")]
    public void Ordinary_content_is_not_protected(string path) =>
        Assert.False(Rules.IsProtected(path));

    [Theory]
    [InlineData(@"C:\pagefile.sys")]
    [InlineData(@"C:\hiberfil.sys")]
    [InlineData(@"C:\swapfile.sys")]
    [InlineData(@"C:\DumpStack.log.tmp")]
    [InlineData(@"C:\bootmgr")]
    [InlineData(@"D:\pagefile.sys")]
    public void Root_system_files_are_protected(string path) =>
        Assert.True(Rules.IsProtected(path));

    [Fact]
    public void A_file_named_like_a_system_file_away_from_the_root_is_not() =>
        Assert.False(Rules.IsProtected(@"D:\Backups\pagefile.sys"));

    [Theory]
    [InlineData(@"\\?\GLOBALROOT\Device\HarddiskVolume3\Windows")]
    [InlineData(@"\\?\Volume{0a1b2c3d-0000-0000-0000-100000000000}\Windows")]
    [InlineData(@"\\.\GLOBALROOT\Device\HarddiskVolume3\Users\me")]
    public void Device_namespace_spellings_without_a_drive_are_protected(string path) =>
        Assert.Equal(ProtectionReason.NotAFullPath, Rules.ReasonFor(path));

    [Theory]
    [InlineData("Public")]
    [InlineData("Default")]
    public void Other_profile_roots_on_this_machine_are_protected_themselves(string profile)
    {
        var profiles = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))!;
        var rules = new ProtectedPaths(ProtectedRoots.ForCurrentUser(@"Z:\own"));

        Assert.Equal(ProtectionReason.PersonalFolder, rules.ReasonFor(Path.Combine(profiles, profile)));
        Assert.False(rules.IsProtected(Path.Combine(profiles, profile, "some-folder")));
    }

    [Fact]
    public void Dot_segments_cannot_escape_into_a_protected_root() =>
        Assert.True(Rules.IsProtected(@"C:\Users\me\Desktop\..\..\..\Windows\System32"));
}
