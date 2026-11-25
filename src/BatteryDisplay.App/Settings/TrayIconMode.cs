namespace BatteryDisplay.App.Settings;

/// <summary>How the connected devices map to tray icons.</summary>
public enum TrayIconMode
{
    /// <summary>One icon per connected device.</summary>
    PerDevice,

    /// <summary>A single icon for the device with the lowest known percent.</summary>
    Lowest,

    /// <summary>One combined icon for the lowest non-pinned device, plus its own icon for every pinned device.</summary>
    LowestWithPinned,
}
