namespace DiskSteward.App.Features.Analyze;

public enum FolderKind
{
    Drive,
    Folder,
    File,

    // Stands in for children not listed yet, so the tree shows an expand arrow.
    Placeholder,
}
