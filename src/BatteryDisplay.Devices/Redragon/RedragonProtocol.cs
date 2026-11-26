using System.Diagnostics.CodeAnalysis;

namespace BatteryDisplay.Devices.Redragon;

/// <summary>One frame the receiver pushed: a battery level or a 2.4 GHz link change.</summary>
public abstract record RedragonFrame;

/// <summary>Battery level 1..100 and whether the USB cable is in (the level is not reliable while it is).</summary>
public sealed record RedragonBatteryFrame(int Percent, bool CableIn) : RedragonFrame;

/// <summary>Link to the keyboard came up (power on, wake) or was lost (power off, sleep).</summary>
public sealed record RedragonLinkFrame(bool Up) : RedragonFrame;

/// <summary>
/// Frames the Redragon K673 pushes through its Compx 2.4 GHz receiver (3554:FA09) on the vendor collection of report
/// 0x13 (20 bytes), captured on hardware 2026-11-26. The keyboard answers no query and pushes only on events: cable
/// plug or unplug, link up (followed by a battery frame within ~200 ms) and every percent change on battery.
/// [0..4] 13 0A 01 00 04, [5] type, [6..] payload, [19] sum of bytes 0..18.
/// Battery (type 05): [6] percent, [7] flags, 0x10 = cable in (0x11 seen at the plug), 0x01 = on battery.
/// Link (type 02): [6] 1 = up, 0 = lost.
/// Vendor, product id, interface and usage live in the device catalog.
/// </summary>
public static class RedragonProtocol
{
    // Bounds how long a stop or a lease check waits on an idle keyboard; it pushes nothing for minutes.
    public const int ReadTimeoutMs = 1000;

    public const byte ReportId = 0x13;
    public const int ReportLength = 20;

    private static readonly byte[] Prefix = [ReportId, 0x0A, 0x01, 0x00, 0x04];
    private const int TypeOffset = 5;
    private const int ValueOffset = 6;
    private const int FlagsOffset = 7;
    private const int ChecksumOffset = 19;

    private const byte BatteryType = 0x05;
    private const byte LinkType = 0x02;
    private const byte CableFlag = 0x10;

    /// <summary>The receiver has other vendor collections on the same interface, so report id and length pick this one.</summary>
    public static bool IsBatteryCollection(RedragonCollection collection, int interfaceNumber, uint usage) =>
        collection.Path.Contains($"&mi_{interfaceNumber:D2}", StringComparison.OrdinalIgnoreCase)
        && collection.Usages.Contains(usage)
        && collection.InputReportIds.Contains(ReportId)
        && collection.MaxInputReportLength == ReportLength;

    /// <summary>Returns false for anything that is not a well formed battery or link frame with a valid checksum.</summary>
    public static bool TryParse(ReadOnlySpan<byte> report, [NotNullWhen(true)] out RedragonFrame? frame)
    {
        frame = null;
        if (report.Length < ReportLength || !report.StartsWith(Prefix) || report[ChecksumOffset] != Checksum(report))
            return false;

        int value = report[ValueOffset];
        frame = report[TypeOffset] switch
        {
            BatteryType when value is >= 1 and <= 100 =>
                new RedragonBatteryFrame(value, CableIn: (report[FlagsOffset] & CableFlag) != 0),
            LinkType when value is 0 or 1 => new RedragonLinkFrame(Up: value == 1),
            _ => null,
        };
        return frame is not null;
    }

    private static byte Checksum(ReadOnlySpan<byte> report)
    {
        var sum = 0;
        foreach (var b in report[..ChecksumOffset])
            sum += b;
        return (byte)sum;
    }
}
