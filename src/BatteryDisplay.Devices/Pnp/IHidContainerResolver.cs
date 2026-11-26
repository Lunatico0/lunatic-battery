namespace BatteryDisplay.Devices.Pnp;

/// <summary>Seam over the PnP property store so a source can learn which physical device a HID collection belongs to.</summary>
public interface IHidContainerResolver
{
    /// <summary>
    /// PnP ContainerId of the device behind a HID interface path, or null when Windows does not know it. It may be
    /// a generic container (see PhysicalIdentity), which callers must not treat as a device.
    /// </summary>
    Guid? ContainerOf(string devicePath);
}
