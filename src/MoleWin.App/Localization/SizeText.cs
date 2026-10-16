namespace MoleWin.App.Localization;

// Byte counts on screen: 1024-based like the CLI, number and unit from resx.
public static class SizeText
{
    public static string Format(long bytes) => bytes switch
    {
        >= 1L << 40 => Tr.Format("Size_TB", bytes / (double)(1L << 40)),
        >= 1L << 30 => Tr.Format("Size_GB", bytes / (double)(1L << 30)),
        >= 1L << 20 => Tr.Format("Size_MB", bytes / (double)(1L << 20)),
        >= 1L << 10 => Tr.Format("Size_KB", bytes / (double)(1L << 10)),
        _ => Tr.Format("Size_B", bytes),
    };
}
