namespace BatteryDisplay.Devices.Tests.Redragon;

/// <summary>Frames the K673 receiver (3554:FA09) pushed on 2026-11-26, in capture order, plus a builder for others.</summary>
internal static class RedragonFrames
{
    public static readonly byte[] CablePlugged75 = Hex("130A010004054B11000000000000000000000083");
    public static readonly byte[] Cable75 = Hex("130A010004054B10000000000000000000000082");
    public static readonly byte[] LinkLost = Hex("130A010004020000000000000000000000000024");
    public static readonly byte[] LinkUp = Hex("130A010004020100000000000000000000000025");
    public static readonly byte[] Cable100 = Hex("130A01000405641000000000000000000000009B");
    public static readonly byte[] OnBattery86 = Hex("130A01000405560100000000000000000000007E");
    public static readonly byte[] OnBattery85 = Hex("130A01000405550100000000000000000000007D");
    public static readonly byte[] OnBattery84 = Hex("130A01000405540100000000000000000000007C");

    /// <summary>The whole capture, in the order the keyboard pushed it.</summary>
    public static readonly byte[][] Captured =
        [CablePlugged75, Cable75, LinkLost, LinkUp, Cable100, LinkLost, LinkUp, OnBattery86, OnBattery85, OnBattery84];

    /// <summary>A frame of the given type and payload with a valid checksum.</summary>
    public static byte[] Frame(byte type, byte value, byte flags = 0)
    {
        var frame = new byte[20];
        byte[] head = [0x13, 0x0A, 0x01, 0x00, 0x04, type, value, flags];
        head.CopyTo(frame, 0);
        return Sealed(frame);
    }

    /// <summary>Recomputes byte 19 as the sum of bytes 0..18.</summary>
    public static byte[] Sealed(byte[] frame)
    {
        var copy = (byte[])frame.Clone();
        copy[19] = (byte)copy.AsSpan(0, 19).ToArray().Sum(b => b);
        return copy;
    }

    public static byte[] With(byte[] frame, int index, byte value, bool reseal = true)
    {
        var copy = (byte[])frame.Clone();
        copy[index] = value;
        return reseal ? Sealed(copy) : copy;
    }

    private static byte[] Hex(string text) => Convert.FromHexString(text);
}
