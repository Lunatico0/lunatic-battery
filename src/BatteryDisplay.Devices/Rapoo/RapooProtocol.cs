using BatteryDisplay.Core;

namespace BatteryDisplay.Devices.Rapoo;

/// <summary>
/// Battery frame the Rapoo 2.4 GHz dongle pushes unsolicited, about every 3 s while the mouse is awake, on its
/// vendor collection with input report 0xBB. Verified on 24AE:1211 against the Rapoo software (35%, 700 DPI):
/// BB B0 41 BC 02 01 23 = [0] report id, [1] frame type 0xB0, [2] unknown, [3..4] DPI little endian,
/// [5] unknown, [6] battery percent. Same layout as 24AE:1870 in github.com/gdsantana/rapoo-mouse docs/protocol.md.
/// The frame carries no verified charging flag, so charging is reported as unknown.
/// Vendor, product ids, interface and usage live in the device catalog.
/// </summary>
public static class RapooProtocol
{
    // Longer than the push period, so an awake mouse always answers within one read.
    public const int ReadTimeoutMs = 5000;
    public const int MaxReportsPerRead = 8;

    public const byte ReportId = 0xBB;
    public const int ReportLength = 7;

    private const byte FrameType = 0xB0;
    private const int FrameTypeOffset = 1;
    private const int BatteryOffset = 6;

    /// <summary>
    /// The interface also has a 0xBC collection with the same usage (10 bytes), so the report id and
    /// length pick the battery one, not the usage alone.
    /// </summary>
    public static bool IsBatteryCollection(RapooCollection collection, int interfaceNumber, uint usage) =>
        collection.Path.Contains($"&mi_{interfaceNumber:D2}", StringComparison.OrdinalIgnoreCase)
        && collection.Usages.Contains(usage)
        && collection.InputReportIds.Contains(ReportId)
        && collection.MaxInputReportLength == ReportLength;

    /// <summary>Returns false when the report is not a battery frame or its level is outside 0..100.</summary>
    public static bool TryParseBattery(ReadOnlySpan<byte> report, out BatteryStatus? status)
    {
        status = null;
        if (report.Length < ReportLength || report[0] != ReportId || report[FrameTypeOffset] != FrameType)
            return false;

        int level = report[BatteryOffset];
        if (level > 100)
            return false;

        status = new BatteryStatus(level, ChargeState.Unknown);
        return true;
    }

    /// <summary>
    /// Waits for the next battery frame, skipping any other report. readReport returns null on timeout,
    /// which is what a sleeping mouse gives: it pushes nothing.
    /// </summary>
    public static BatteryStatus? ReadStatus(Func<byte[]?> readReport)
    {
        for (int i = 0; i < MaxReportsPerRead; i++)
        {
            var report = readReport();
            if (report is null)
                return null;
            if (TryParseBattery(report, out var status))
                return status;
        }

        return null;
    }
}
