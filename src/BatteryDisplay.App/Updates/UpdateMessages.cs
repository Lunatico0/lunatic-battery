namespace BatteryDisplay.App.Updates;

/// <summary>Texts of the tray menu item and of the notice that answers "Buscar actualizaciones".</summary>
public static class UpdateMessages
{
    public const string Title = "Actualizaciones";

    /// <summary>The notice for a check the user asked for; null when there is nothing to say.</summary>
    public static string? For(UpdateCheck check) => check.Status switch
    {
        UpdateCheckStatus.UpToDate => "Ya está instalada la última versión.",
        UpdateCheckStatus.Ready => $"La versión {check.Version} está lista. Se instala al salir de la app.",
        UpdateCheckStatus.Failed => "No se pudieron buscar actualizaciones. Se vuelve a intentar más tarde.",
        _ => null,
    };

    /// <summary>The menu item checks for updates, or restarts into the one already downloaded.</summary>
    public static string MenuText(string? pendingVersion) =>
        pendingVersion is null ? "Buscar actualizaciones" : $"Reiniciar para actualizar a {pendingVersion}";
}
