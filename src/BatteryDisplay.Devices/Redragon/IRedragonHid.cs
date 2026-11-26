namespace BatteryDisplay.Devices.Redragon;

/// <summary>One HID collection of a Redragon receiver: its path, usages, input report ids and input report length (id included).</summary>
public sealed record RedragonCollection(
    string Path, IReadOnlyList<uint> Usages, IReadOnlyList<byte> InputReportIds, int MaxInputReportLength);

/// <summary>Input reports of one opened collection. Read only: nothing is ever written to a Redragon receiver.</summary>
public interface IRedragonInput : IDisposable
{
    /// <summary>Next input report, report id first; null when none arrives before the read timeout.</summary>
    byte[]? Read();
}

/// <summary>The HID calls the Redragon listener needs, so it can be tested without a receiver. There is no write.</summary>
public interface IRedragonHid
{
    IReadOnlyList<RedragonCollection> FindCollections(int vendorId, IReadOnlyList<int> productIds);

    /// <summary>Opens a collection for reading; null when Windows refuses it.</summary>
    IRedragonInput? OpenInput(string path, int readTimeoutMs);
}
