namespace DiskSteward.Core.Safety;

// Why ProtectedPaths refuses a path, so the screen can say it in the user's terms.
public enum ProtectionReason
{
    None,
    NotAFullPath,
    DriveRoot,
    VolumeSystem,
    Windows,
    Programs,
    ProgramData,
    AppOwnData,
    PersonalFolder,
}
