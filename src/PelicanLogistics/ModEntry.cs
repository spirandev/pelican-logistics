using System;
using HarmonyLib;
using PelicanLogistics.Framework;
using PelicanLogistics.Framework.Content;
using PelicanLogistics.Framework.Input;
using PelicanLogistics.Framework.Patches;
using PelicanLogistics.Framework.Persistence;
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
    private AccessoryController accessories = null!;
    private UndergroundController underground = null!;
    private UndergroundRenderer undergroundRenderer = null!;
    private SaveManager saves = null!;

    public override void Entry(IModHelper helper)
    {
        this.config = helper.ReadConfig<ModConfig>();

        this.registry = new WorldRegistry(this.Monitor, () => this.placement.CurrentDirection);
        this.placement = new PlacementController(helper, this.config, this.registry);
        this.menus = new BlockMenuController(helper, this.registry);
        this.accessories = new AccessoryController(helper, this.registry);
        this.underground = new UndergroundController(helper, this.registry, this.placement);
        this.undergroundRenderer = new UndergroundRenderer(this.registry);
        this.saves = new SaveManager(helper, this.Monitor, this.registry);
        var renderer = new NodeRenderer(this.registry, this.placement);

        new ContentRegistrar(helper, this.config).Register();
        new ConsoleCommands(helper, this.Monitor, this.registry).Register();
        this.ApplyPatches(renderer);

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.Saving += this.OnSaving;
        helper.Events.GameLoop.Saved += this.OnSaved;
        helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.World.ObjectListChanged += this.OnObjectListChanged;
        helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        helper.Events.Display.RenderedWorld += this.OnRenderedWorld;
    }

    private void ApplyPatches(NodeRenderer renderer)
    {
        var harmony = new Harmony(this.ModManifest.UniqueID);
        try
        {
            ObjectDrawPatch.Apply(harmony, renderer, this.Monitor);
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Falha ao aplicar os patches Harmony; os blocos não serão desenhados.\n{ex}", LogLevel.Error);
        }

        try
        {
            PassablePatch.Apply(harmony);
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Falha ao aplicar o patch da Ponte; não será possível passar por cima das esteiras.\n{ex}", LogLevel.Error);
        }

        try
        {
            TunnelPlacementPatch.Apply(harmony, this.registry);
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Falha ao aplicar o patch do Túnel; ele poderá ser colocado sem o Capacete de Engenheiro.\n{ex}", LogLevel.Error);
        }

        try
        {
            DescriptionPatch.Apply(harmony, this.Helper.Translation, this.Monitor);
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Falha ao aplicar o patch das descrições; as linhas técnicas do Capacete de Engenheiro não aparecerão.\n{ex}", LogLevel.Error);
        }
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.Monitor.Log(
            this.Helper.Translation.Get("mod.loaded", new { version = this.ModManifest.Version }),
            LogLevel.Info);
    }

    /// <summary>Recria os nós a partir dos objetos e, em seguida (nessa ordem), devolve a eles os itens salvos.</summary>
    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        this.registry.Rebuild();
        this.saves.Load();
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        this.saves.OnSaving();
    }

    private void OnSaved(object? sender, SavedEventArgs e)
    {
        this.saves.OnSaved();
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

    /// <summary>
    /// Esteiras Subterrâneas e acessórios primeiro: segurando uma Esteira Subterrânea, uma Ponte ou um Regulador, o clique
    /// constrói ou aplica em vez de abrir a interface do bloco.
    /// </summary>
    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!this.underground.OnButtonPressed(e) && !this.accessories.OnButtonPressed(e))
            this.menus.OnButtonPressed(e);
    }

    private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        this.undergroundRenderer.OnRenderedWorld(e);
        this.placement.OnRenderedWorld(e);
    }
}
