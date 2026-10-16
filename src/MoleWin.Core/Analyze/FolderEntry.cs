namespace MoleWin.Core.Analyze;

// One child of a folder. A file's size is known at listing; a folder's is measured later.
public sealed record FolderEntry(string Name, string Path, bool IsDirectory, long? Size);
