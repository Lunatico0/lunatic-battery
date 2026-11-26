using BatteryDisplay.App.Presentation;

namespace BatteryDisplay.App.Tests.Presentation;

/// <summary>A taskbar theme the test sets by hand; <see cref="Switch"/> changes it and raises Changed like the real one.</summary>
internal sealed class FakeTaskbarTheme : ITaskbarTheme
{
    public TrayIconTheme Theme { get; set; } = TrayIconTheme.Dark;

    public TrayIconInk Ink { get; set; }

    public event EventHandler? Changed;

    public void Switch(TrayIconTheme theme, TrayIconInk ink = default)
    {
        Theme = theme;
        Ink = ink;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public int Subscribers => Changed?.GetInvocationList().Length ?? 0;
}
