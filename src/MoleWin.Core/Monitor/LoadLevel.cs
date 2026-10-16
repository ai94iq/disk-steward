namespace MoleWin.Core.Monitor;

// How hard a resource is working; the UI maps it to the theme's success, caution and critical colors.
public enum LoadLevel
{
    Normal,
    High,
    Critical,
}
