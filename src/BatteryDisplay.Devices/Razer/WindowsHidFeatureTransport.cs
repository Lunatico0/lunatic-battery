using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BatteryDisplay.Devices.Razer;

/// <summary>
/// Feature-report transport over a HID collection opened with zero access rights.
/// The Razer control collection is the mouse collection, which Windows holds exclusively, so it
/// cannot be opened for read/write (HidSharp's Open fails); HidD_SetFeature/GetFeature still work.
/// </summary>
internal sealed class WindowsHidFeatureTransport : IRazerTransport, IDisposable
{
    private const byte ReportId = 0x00;
    private const uint FileShareReadWrite = 0x1 | 0x2;
    private const uint OpenExisting = 3;

    private readonly SafeFileHandle _handle;

    private WindowsHidFeatureTransport(SafeFileHandle handle) => _handle = handle;

    public static WindowsHidFeatureTransport? TryOpen(string devicePath)
    {
        var handle = CreateFileW(devicePath, 0, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return null;
        }
        return new WindowsHidFeatureTransport(handle);
    }

    public bool TrySend(byte[] report)
    {
        var wire = new byte[RazerReport.Length + 1];
        wire[0] = ReportId;
        report.CopyTo(wire, 1);
        return HidD_SetFeature(_handle, wire, wire.Length);
    }

    public byte[]? TryReceive()
    {
        var wire = new byte[RazerReport.Length + 1];
        wire[0] = ReportId;
        return HidD_GetFeature(_handle, wire, wire.Length) ? wire[1..] : null;
    }

    public void Dispose() => _handle.Dispose();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_SetFeature(SafeFileHandle device, byte[] buffer, int length);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetFeature(SafeFileHandle device, byte[] buffer, int length);
}
