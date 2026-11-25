namespace BatteryDisplay.Devices.Razer;

/// <summary>Status byte of a Razer report (OpenRazer razercommon.h).</summary>
public enum RazerStatus : byte { New = 0, Busy = 1, Successful = 2, Failure = 3, Timeout = 4, NotSupported = 5 }

public readonly record struct RazerCommand(byte Class, byte Id, byte DataSize);

/// <summary>Decoded response; Value is arguments[1], where battery and charging answers live.</summary>
public readonly record struct RazerResponse(RazerStatus Status, byte CommandClass, byte CommandId, byte Value);

/// <summary>
/// Pure encoding of the 90-byte Razer feature report:
/// [0] status, [1] transaction id, [2..3] remaining packets, [4] protocol type, [5] data size,
/// [6] command class, [7] command id, [8..87] arguments, [88] checksum, [89] reserved.
/// </summary>
public static class RazerReport
{
    public const int Length = 90;

    public static readonly RazerCommand BatteryLevel = new(0x07, 0x80, 0x02);
    public static readonly RazerCommand ChargingStatus = new(0x07, 0x84, 0x02);

    private const int DataSizeIndex = 5;
    private const int ClassIndex = 6;
    private const int IdIndex = 7;
    private const int ArgumentsIndex = 8;
    private const int ChecksumIndex = 88;

    public static byte[] BuildRequest(byte transactionId, RazerCommand command)
    {
        var report = new byte[Length];
        report[1] = transactionId;
        report[DataSizeIndex] = command.DataSize;
        report[ClassIndex] = command.Class;
        report[IdIndex] = command.Id;
        report[ChecksumIndex] = Checksum(report);
        return report;
    }

    public static byte Checksum(ReadOnlySpan<byte> report)
    {
        byte crc = 0;
        foreach (var b in report[2..ChecksumIndex])
            crc ^= b;
        return crc;
    }

    public static bool TryParse(ReadOnlySpan<byte> report, out RazerResponse response)
    {
        response = default;
        if (report.Length != Length || Checksum(report) != report[ChecksumIndex])
            return false;
        if (!Enum.IsDefined((RazerStatus)report[0]))
            return false;

        response = new RazerResponse(
            (RazerStatus)report[0], report[ClassIndex], report[IdIndex], report[ArgumentsIndex + 1]);
        return true;
    }

    /// <summary>Maps the raw 0..255 level to 0..100, rounded to nearest.</summary>
    public static int ToPercent(byte raw) => (raw * 100 + 127) / 255;
}
