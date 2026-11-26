using HidSharp;

namespace BatteryDisplay.Devices.Rapoo;

/// <summary>Rapoo collections and input reads through HidSharp. Opens for reading only and never writes.</summary>
internal sealed class HidSharpRapooHid : IRapooHid
{
    public IReadOnlyList<RapooCollection> FindCollections(int vendorId, IReadOnlyList<int> productIds)
    {
        try
        {
            return DeviceList.Local.GetHidDevices(vendorId)
                .Where(d => productIds.Contains(d.ProductID))
                .Select(Describe)
                .OfType<RapooCollection>()
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    public IRapooInput? OpenInput(string path, int readTimeoutMs)
    {
        var device = DeviceList.Local.GetHidDevices().FirstOrDefault(d => d.DevicePath == path);
        if (device is null || !device.TryOpen(out HidStream stream))
            return null;

        stream.ReadTimeout = readTimeoutMs;
        return new Input(stream, device.GetMaxInputReportLength());
    }

    // A collection whose descriptor Windows refuses is left out, like a missing one.
    private static RapooCollection? Describe(HidDevice device)
    {
        try
        {
            var descriptor = device.GetReportDescriptor();
            return new RapooCollection(
                device.DevicePath,
                descriptor.DeviceItems.SelectMany(item => item.Usages.GetAllValues()).ToList(),
                descriptor.InputReports.Select(report => report.ReportID).ToList(),
                device.GetMaxInputReportLength());
        }
        catch (Exception)
        {
            return null;
        }
    }

    private sealed class Input(HidStream stream, int reportLength) : IRapooInput
    {
        public byte[]? Read()
        {
            var buffer = new byte[reportLength];
            try
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                return buffer[..read];
            }
            catch (TimeoutException)
            {
                return null;
            }
        }

        public void Dispose() => stream.Dispose();
    }
}
