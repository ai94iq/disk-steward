namespace DiskSteward.Core.Analyze;

// One child of a folder. A file's size is known at listing; a folder's is measured later.
// Capacity is set for drives (Size is then the used space). IsSystem mirrors the System
// attribute Windows puts on files it relies on.
public sealed record FolderEntry(string Name, string Path, bool IsDirectory, long? Size, long? Capacity = null, bool IsSystem = false);
