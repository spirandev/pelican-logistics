using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Simulation;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework.World;

/// <summary>Mantém o <see cref="LogisticsWorld"/> do Core sincronizado com os objetos do jogo.</summary>
internal sealed class WorldRegistry
{
    private readonly IMonitor monitor;
    private readonly Func<Direction> placementDirection;
    private readonly Dictionary<string, GameLocation> locations = new(StringComparer.Ordinal);

    /// <param name="monitor">Log do SMAPI.</param>
    /// <param name="placementDirection">Direção de colocação escolhida pelo jogador (seta fantasma).</param>
    public WorldRegistry(IMonitor monitor, Func<Direction> placementDirection)
    {
        this.monitor = monitor;
        this.placementDirection = placementDirection;
    }

    public LogisticsWorld World { get; } = new();

    /// <summary>Chave do local no <see cref="LogisticsWorld"/>.</summary>
    public static string KeyOf(GameLocation location)
    {
        return location.NameOrUniqueName;
    }

    public bool TryGetLocation(string key, [NotNullWhen(true)] out GameLocation? location)
    {
        return this.locations.TryGetValue(key, out location);
    }

    /// <summary>Recria todos os nós a partir dos objetos dos locais (ao carregar o save).</summary>
    public void Rebuild()
    {
        this.Clear();

        int count = 0;
        Utility.ForEachLocation(
            location =>
            {
                foreach (KeyValuePair<Vector2, SObject> pair in location.objects.Pairs)
                {
                    if (this.TryAddNode(location, pair.Key, pair.Value))
                        count++;
                }

                return true;
            },
            true,   // includeInteriors: interiores de construções (celeiros, galpões, estufa...)
            false); // includeGenerated: minas e outros níveis temporários não guardam objetos

        this.monitor.Log($"{count} bloco(s) registrado(s) em {this.locations.Count} local(is).", LogLevel.Trace);
    }

    /// <summary>Esquece tudo (ao voltar para a tela inicial).</summary>
    public void Clear()
    {
        this.World.Clear();
        this.locations.Clear();
    }

    public void OnObjectListChanged(ObjectListChangedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        GameLocation location = e.Location;
        foreach ((Vector2 tile, SObject obj) in e.Removed)
        {
            if (ItemIds.IsOurBlock(obj))
                this.RemoveNode(location, tile);
            else
                this.RemoveMachineSlots(location, tile);
        }

        foreach ((Vector2 tile, SObject obj) in e.Added)
        {
            if (!ItemIds.IsOurBlock(obj))
                continue;

            // Sempre a direção da seta e a configuração padrão: ao colocar, o jogo copia o modData do item
            // segurado (getOne), então um bloco recolhido e colocado de novo traria a direção e a configuração antigas.
            NodeFactory.WriteFacing(obj, this.placementDirection());
            NodeFactory.ResetConfig(obj);
            this.TryAddNode(location, tile, obj);
        }
    }

    public LogisticsNode? GetNode(GameLocation location, TilePos pos)
    {
        return this.World.Get(KeyOf(location))?.Get(pos);
    }

    /// <summary>Slots virtuais da máquina no tile, ou null se não há nada guardado.</summary>
    public VirtualSlots? GetMachineSlots(GameLocation location, TilePos pos)
    {
        return this.World.Get(KeyOf(location))?.MachineSlots.Get(pos);
    }

    /// <summary>Gira o nó no sentido horário e grava a direção no <c>modData</c>. Retorna false se não há nó.</summary>
    public bool Rotate(GameLocation location, TilePos pos)
    {
        LogisticsGrid? grid = this.World.Get(KeyOf(location));
        LogisticsNode? node = grid?.Get(pos);
        if (grid == null || node == null)
            return false;

        node.Facing = node.Facing.Clockwise();
        if (location.objects.TryGetValue(ToVector(pos), out SObject? obj) && obj != null)
            NodeFactory.WriteFacing(obj, node.Facing);
        grid.MarkDirty();
        return true;
    }

    public static TilePos ToTilePos(Vector2 tile)
    {
        return new TilePos((int)tile.X, (int)tile.Y);
    }

    public static Vector2 ToVector(TilePos pos)
    {
        return new Vector2(pos.X, pos.Y);
    }

    private bool TryAddNode(GameLocation location, Vector2 tile, SObject obj)
    {
        if (!ItemIds.IsOurBlock(obj))
            return false;

        TilePos pos = ToTilePos(tile);
        LogisticsNode? node = NodeFactory.Create(obj, pos);
        if (node == null)
            return false;

        string key = KeyOf(location);
        LogisticsGrid grid = this.World.GetOrCreate(key);
        if (grid.Get(pos) != null)
        {
            this.monitor.Log($"Já existe um bloco registrado em {key} {pos}; ignorando o novo.", LogLevel.Trace);
            return false;
        }

        this.locations[key] = location;
        grid.Add(node);
        return true;
    }

    /// <summary>Remove o nó e solta no chão os itens que ele carregava.</summary>
    private void RemoveNode(GameLocation location, Vector2 tile)
    {
        LogisticsGrid? grid = this.World.Get(KeyOf(location));
        if (grid == null)
            return;

        RemovalResult result = grid.Remove(ToTilePos(tile));
        DropItems(location, tile, result.Items);

        // Acessórios: Fase 5.
    }

    /// <summary>Máquina removida: os itens guardados nos slots virtuais caem no chão (README "Slots virtuais").</summary>
    private void RemoveMachineSlots(GameLocation location, Vector2 tile)
    {
        LogisticsGrid? grid = this.World.Get(KeyOf(location));
        TilePos pos = ToTilePos(tile);
        if (grid == null || grid.MachineSlots.Get(pos) == null)
            return;

        DropItems(location, tile, grid.MachineSlots.RemoveMachine(pos));
    }

    /// <summary>Solta os itens no chão, no centro do tile.</summary>
    private static void DropItems(GameLocation location, Vector2 tile, IEnumerable<ILogisticsItem> items)
    {
        Vector2 pixel = new(tile.X * Game1.tileSize + (Game1.tileSize / 2), tile.Y * Game1.tileSize + (Game1.tileSize / 2));
        foreach (ILogisticsItem item in items)
        {
            if (item is GameItem gameItem && gameItem.Count > 0)
                Game1.createItemDebris(gameItem.Item, pixel, -1, location);
        }
    }
}
