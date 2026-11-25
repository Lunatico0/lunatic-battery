using BatteryDisplay.Core;

namespace BatteryDisplay.Devices.SteelSeries;

/// <summary>
/// Battery status protocol of the Arctis Nova 5/5X base station, ported from HeadsetControl
/// (lib/devices/steelseries_arctis_nova_5.hpp and protocols/steelseries_protocol.hpp,
/// commit 6b3f7788c9561a467cad7a90576ccec15e10f6bf). Only the 0xB0 status query is used.
/// Payload offsets match HeadsetControl, whose hidapi reads drop the report ID byte.
/// </summary>
public static class ArctisNova5Protocol
{
    public const int VendorId = 0x1038;
    public static readonly int[] ProductIds = [0x2232, 0x2253];
    public const int InterfaceNumber = 3;
    public const uint Usage = 0xFFC00001;
    public const int ReadTimeoutMs = 5000;
    public const int MaxReportsPerQuery = 8;

    private const byte StatusCommand = 0xB0;
    private const int MinStatusLength = 16;
    private const int ConnectionOffset = 1;
    private const int LevelOffset = 3;
    private const int ChargingOffset = 4;
    private const byte HeadsetOffline = 0x02;
    private const byte HeadsetCharging = 0x01;

    public static bool IsStatusInterface(string devicePath) =>
        devicePath.Contains($"&mi_{InterfaceNumber:D2}", StringComparison.OrdinalIgnoreCase);

    public static byte[] BuildStatusRequest(int outputReportLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(outputReportLength, 2);
        var request = new byte[outputReportLength];
        request[1] = StatusCommand;
        return request;
    }

    public static ReadOnlySpan<byte> PayloadOf(byte[] inputReport, int bytesRead) =>
        bytesRead <= 1 ? ReadOnlySpan<byte>.Empty : inputReport.AsSpan(1, bytesRead - 1);

    /// <summary>
    /// Returns false when the payload is not a status report. When it is, status is null
    /// if the headset is off or out of range of the dongle.
    /// </summary>
    public static bool TryParseStatus(ReadOnlySpan<byte> payload, out BatteryStatus? status)
    {
        status = null;
        if (payload.Length < MinStatusLength || payload[0] != StatusCommand)
            return false;

        if (payload[ConnectionOffset] == HeadsetOffline)
            return true;

        int level = payload[LevelOffset];
        var charge = payload[ChargingOffset] == HeadsetCharging ? ChargeState.Charging : ChargeState.Discharging;
        status = new BatteryStatus(level <= 100 ? level : null, charge);
        return true;
    }

    /// <summary>
    /// Sends the status query and waits for its reply, skipping reports meant for other
    /// clients (SteelSeries GG shares the interface). readPayload returns null on timeout.
    /// </summary>
    public static BatteryStatus? QueryStatus(Action<byte[]> write, Func<byte[]?> readPayload, int outputReportLength)
    {
        write(BuildStatusRequest(outputReportLength));

        for (int i = 0; i < MaxReportsPerQuery; i++)
        {
            var payload = readPayload();
            if (payload is null)
                return null;
            if (TryParseStatus(payload, out var status))
                return status;
        }

        return null;
    }
}
