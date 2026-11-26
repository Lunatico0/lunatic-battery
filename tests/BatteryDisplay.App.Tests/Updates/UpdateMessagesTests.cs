using BatteryDisplay.App.Updates;

namespace BatteryDisplay.App.Tests.Updates;

public class UpdateMessagesTests
{
    [Fact]
    public void UpToDate_SaysItIsTheLatestVersion()
    {
        Assert.Equal("Ya está instalada la última versión.", UpdateMessages.For(UpdateCheck.UpToDate));
    }

    [Fact]
    public void Ready_NamesTheVersion_AndWhenItInstalls()
    {
        Assert.Equal("La versión 1.1.0 está lista. Se instala al salir de la app.",
            UpdateMessages.For(UpdateCheck.Ready("1.1.0")));
    }

    [Fact]
    public void Failed_SaysItWillTryAgainLater()
    {
        Assert.Equal("No se pudieron buscar actualizaciones. Se vuelve a intentar más tarde.",
            UpdateMessages.For(UpdateCheck.Failed));
    }

    [Fact]
    public void Unavailable_ShowsNothing()
    {
        Assert.Null(UpdateMessages.For(UpdateCheck.Unavailable));
    }

    [Fact]
    public void Title_IsUpdates()
    {
        Assert.Equal("Actualizaciones", UpdateMessages.Title);
    }

    [Fact]
    public void MenuText_WithNothingPending_OffersToCheck()
    {
        Assert.Equal("Buscar actualizaciones", UpdateMessages.MenuText(null));
    }

    [Fact]
    public void MenuText_WithAPendingRelease_OffersToRestart()
    {
        Assert.Equal("Reiniciar para actualizar a 1.1.0", UpdateMessages.MenuText("1.1.0"));
    }
}
