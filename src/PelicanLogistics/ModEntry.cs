using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace PelicanLogistics;

/// <summary>Ponto de entrada do mod.</summary>
public sealed class ModEntry : Mod
{
    private ModConfig config = null!;

    public override void Entry(IModHelper helper)
    {
        this.config = helper.ReadConfig<ModConfig>();
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.Monitor.Log(
            this.Helper.Translation.Get("mod.loaded", new { version = this.ModManifest.Version }),
            LogLevel.Info);
    }
}
