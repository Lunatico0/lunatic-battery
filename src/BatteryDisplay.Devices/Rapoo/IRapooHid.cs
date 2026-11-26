namespace BatteryDisplay.Devices.Rapoo;

/// <summary>One HID collection of a Rapoo dongle: its path, usages, input report ids and input report length (id included).</summary>
public sealed record RapooCollection(
    string Path, IReadOnlyList<uint> Usages, IReadOnlyList<byte> InputReportIds, int MaxInputReportLength);

/// <summary>Input reports of one opened collection. Read only: nothing is ever written to a Rapoo device.</summary>
public interface IRapooInput : IDisposable
{
    /// <summary>Next input report, report id first; null when none arrives before the read timeout.</summary>
    byte[]? Read();
}

/// <summary>The HID calls the Rapoo provider needs, so it can be tested without a dongle.</summary>
public interface IRapooHid
{
    IReadOnlyList<RapooCollection> FindCollections(int vendorId, IReadOnlyList<int> productIds);

    /// <summary>Opens a collection for reading; null when Windows refuses it.</summary>
    IRapooInput? OpenInput(string path, int readTimeoutMs);
}
