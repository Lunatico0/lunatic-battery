namespace BatteryDisplay.Devices.Razer;

/// <summary>Carries 90-byte Razer reports (without the HID report id) to a device and back.</summary>
public interface IRazerTransport
{
    /// <summary>Sends a request as a feature report; false on an I/O failure.</summary>
    bool TrySend(byte[] report);

    /// <summary>Reads the pending feature report; null on an I/O failure.</summary>
    byte[]? TryReceive();
}
