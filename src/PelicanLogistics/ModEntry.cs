using System;
using HarmonyLib;
using PelicanLogistics.Framework;
using PelicanLogistics.Framework.Content;
using PelicanLogistics.Framework.Input;
using PelicanLogistics.Framework.Patches;
using PelicanLogistics.Framework.Rendering;
using PelicanLogistics.Framework.World;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace PelicanLogistics;

/// <summary>Ponto de entrada do mod.</summary>
public sealed class ModEntry : Mod
{
    private ModConfig config = null!;
    private WorldRegistry registry = null!;
    private PlacementController placement = null!;
    private BlockMenuController menus = null!;

    public override void Entry(IModHelper helper)
    {
        this.config = helper.ReadConfig<ModConfig>();

        this.registry = new WorldRegistry(this.Monitor, () => this.placement.CurrentDirection);
        this.placement = new PlacementController(helper, this.config, this.registry);
        this.menus = new BlockMenuController(helper, this.registry);
        var renderer = new NodeRenderer(this.registry, this.placement);

        new ContentRegistrar(helper, this.config).Register();
        new ConsoleCommands(helper, this.Monitor, this.registry).Register();
        this.ApplyPatches(renderer);

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.World.ObjectListChanged += this.OnObjectListChanged;
        helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        helper.Events.Display.RenderedWorld += this.OnRenderedWorld;
    }

    private void ApplyPatches(NodeRenderer renderer)
    {
        try
        {
            ObjectDrawPatch.Apply(new Harmony(this.ModManifest.UniqueID), renderer, this.Monitor);
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Falha ao aplicar os patches Harmony; os blocos não serão desenhados.\n{ex}", LogLevel.Error);
        }
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.Monitor.Log(
            this.Helper.Translation.Get("mod.loaded", new { version = this.ModManifest.Version }),
            LogLevel.Info);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        this.registry.Rebuild();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        this.registry.Clear();
    }

    /// <summary>Um tick de lógica em todos os locais, só com o tempo do jogo passando (README: pausado e à noite, as esteiras param).</summary>
    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer || !Game1.shouldTimePass())
            return;

        this.registry.World.Tick(key => this.registry.TryGetLocation(key, out GameLocation? location) ? new GameEndpointProvider(location) : null);
    }

    private void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
    {
        this.registry.OnObjectListChanged(e);
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        this.placement.OnButtonsChanged(e);
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        this.menus.OnButtonPressed(e);
    }

    private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        this.placement.OnRenderedWorld(e);
    }
}
