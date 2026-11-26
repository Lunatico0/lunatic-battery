using System.Drawing;
using System.Runtime.InteropServices;

namespace BatteryDisplay.App.Tray;

internal static class NativeMethods
{
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(IntPtr hIcon);

    internal static readonly IntPtr HwndTopmost = new(-1);
    internal const uint SwpNoSize = 0x0001;
    internal const uint SwpNoActivate = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    internal const int ColorWindowText = 8;
    internal const int ColorGrayText = 17;

    // GetSysColor directly, not WinForms SystemColors: those can follow the app's own color mode instead of the
    // system's, and a high contrast theme's colors are the system's.
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetSysColor(int nIndex);

    /// <summary>The system color as opaque ARGB (GetSysColor returns a COLORREF, 0x00BBGGRR).</summary>
    internal static int SysColorArgb(int index) => ColorTranslator.FromWin32(unchecked((int)GetSysColor(index))).ToArgb();

    private const uint SpiGetHighContrast = 0x0042;
    private const uint HcfHighContrastOn = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    private struct HighContrastInfo
    {
        public uint CbSize;
        public uint DwFlags;
        public IntPtr LpszDefaultScheme;
    }

    [DllImport("user32.dll", ExactSpelling = true, EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref HighContrastInfo pvParam, uint fWinIni);

    /// <summary>
    /// Whether a Windows high contrast theme is on, read live. SystemInformation.HighContrast caches the value until its
    /// own UserPreferenceChanged handler runs, which may come after the recheck.
    /// </summary>
    internal static bool IsHighContrastOn()
    {
        var info = new HighContrastInfo { CbSize = (uint)Marshal.SizeOf<HighContrastInfo>() };
        return SystemParametersInfo(SpiGetHighContrast, info.CbSize, ref info, 0) && (info.DwFlags & HcfHighContrastOn) != 0;
    }

    [DllImport("shell32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(in Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

    internal static string? GetKnownFolderPath(Guid folderId)
    {
        var hr = SHGetKnownFolderPath(folderId, 0, IntPtr.Zero, out var buffer);
        try
        {
            return hr == 0 ? Marshal.PtrToStringUni(buffer) : null;
        }
        finally
        {
            Marshal.FreeCoTaskMem(buffer);
        }
    }
}
