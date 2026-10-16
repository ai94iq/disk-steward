using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MoleWin.Core.Safety;

// SHFileOperation with FOF_ALLOWUNDO recycles instead of deleting. FOF_WANTNUKEWARNING makes
// Windows ask before it would delete permanently (an item too large for the bin), so a silent
// permanent delete cannot happen.
[SupportedOSPlatform("windows")]
public sealed class WindowsRecycleBin : IRecycleBin
{
    private const uint FoDelete = 0x0003;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoErrorUi = 0x0400;
    private const ushort FofWantNukeWarning = 0x4000;

    public void MoveToRecycleBin(string path)
    {
        var operation = new ShFileOpStruct
        {
            Func = FoDelete,
            From = path + "\0\0", // the API takes a double-null-terminated list
            Flags = FofAllowUndo | FofNoConfirmation | FofSilent | FofNoErrorUi | FofWantNukeWarning,
        };
        var code = SHFileOperationW(ref operation);
        if (code != 0) throw new IOException($"Windows did not recycle the item (code 0x{code:X}).");
        if (operation.AnyOperationsAborted) throw new IOException("Recycling was cancelled.");
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperationW(ref ShFileOpStruct operation);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr Window;
        public uint Func;
        [MarshalAs(UnmanagedType.LPWStr)] public string From;
        [MarshalAs(UnmanagedType.LPWStr)] public string? To;
        public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)] public bool AnyOperationsAborted;
        public IntPtr NameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? ProgressTitle;
    }
}
