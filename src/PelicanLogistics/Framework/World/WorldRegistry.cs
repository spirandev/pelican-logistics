using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Xna.Framework;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Persistence;
using PelicanLogistics.Core.Regulators;
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

    /// <summary>
    /// O local da chave, mesmo sem blocos (ex.: só slots virtuais restaurados ao carregar), ou null se ele não
    /// existe mais. O local encontrado passa a ser conhecido (<see cref="TryGetLocation"/>), para a grade dele rodar.
    /// </summary>
    public GameLocation? FindLocation(string key)
    {
        if (this.locations.TryGetValue(key, out GameLocation? known))
            return known;

        GameLocation? found = null;
        Utility.ForEachLocation(
            location =>
            {
                if (KeyOf(location) != key)
                    return true;

                found = location;
                return false;
            },
            true,   // includeInteriors
            false); // includeGenerated

        if (found != null)
            this.locations[key] = found;
        return found;
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

                // Depois dos blocos, para a regra "nada embaixo de um Túnel" valer.
                count += this.LoadSegments(location);
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
                this.RemoveNode(location, tile, obj);
            else
                this.RemoveMachineSlots(location, tile);
        }

        foreach ((Vector2 tile, SObject obj) in e.Added)
        {
            // Tile que já tem nó: proteção contra um aviso atrasado de um objeto já registrado (o SMAPI só informa
            // no tick seguinte). Um bloco quebrado e colocado de novo gera Removed antes, então não cai aqui.
            if (!ItemIds.IsOurBlock(obj) || this.GetNode(location, ToTilePos(tile)) != null)
                continue;

            // Sempre a direção da seta e a configuração padrão: ao colocar, o jogo copia o modData do item
            // segurado (getOne), então um bloco recolhido e colocado de novo traria a direção e a configuração antigas.
            Direction facing = this.placementDirection();
            NodeFactory.WriteFacing(obj, facing);
            NodeFactory.ResetConfig(obj);

            // O Túnel vira entrada ou saída conforme a Esteira Subterrânea atrás dele (plano mestre D21).
            if (obj.ItemId == ItemIds.Tunnel)
                NodeFactory.WriteTunnelRole(obj, TunnelRules.DecideRole(this.World.GetOrCreate(KeyOf(location)), ToTilePos(tile), facing));

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

    /// <summary>A Esteira Subterrânea do tile, ou null.</summary>
    public UndergroundSegmentNode? GetSegment(GameLocation location, TilePos pos)
    {
        return this.World.Get(KeyOf(location))?.GetUnderground(pos);
    }

    /// <summary>
    /// Cria uma Esteira Subterrânea no tile e grava o layout no <c>modData</c> do local. Retorna false se o tile já tem
    /// uma ou tem um Túnel (as regras do mapa ficam com quem chama).
    /// </summary>
    public bool TryPlaceSegment(GameLocation location, TilePos pos, Direction facing)
    {
        string key = KeyOf(location);
        LogisticsGrid grid = this.World.GetOrCreate(key);
        if (!grid.CanPlaceSegment(pos))
            return false;

        grid.AddUnderground(new UndergroundSegmentNode(pos, facing));
        this.locations[key] = location;
        SaveSegments(location, grid);
        return true;
    }

    /// <summary>Remove a Esteira Subterrânea, solta no chão o item que estava nela e grava o layout. Retorna false se não havia.</summary>
    public bool RemoveSegment(GameLocation location, TilePos pos)
    {
        LogisticsGrid? grid = this.World.Get(KeyOf(location));
        if (grid?.GetUnderground(pos) == null)
            return false;

        DropItems(location, ToVector(pos), grid.RemoveUnderground(pos).Items);
        SaveSegments(location, grid);
        return true;
    }

    /// <summary>Gira a Esteira Subterrânea no sentido horário e grava o layout. Retorna false se não havia.</summary>
    public bool RotateSegment(GameLocation location, TilePos pos)
    {
        LogisticsGrid? grid = this.World.Get(KeyOf(location));
        UndergroundSegmentNode? segment = grid?.GetUnderground(pos);
        if (grid == null || segment == null)
            return false;

        segment.Facing = segment.Facing.Clockwise();
        grid.MarkDirty();
        SaveSegments(location, grid);
        return true;
    }

    /// <summary>True se dá para colocar um Túnel no tile (não há Esteira Subterrânea embaixo, plano mestre D21).</summary>
    public bool CanPlaceTunnel(GameLocation location, TilePos pos)
    {
        LogisticsGrid? grid = this.World.Get(KeyOf(location));
        return grid == null || TunnelRules.CanPlaceTunnel(grid, pos);
    }

    /// <summary>O objeto do jogo no tile, ou null.</summary>
    public static SObject? GetObject(GameLocation location, TilePos pos)
    {
        return location.objects.TryGetValue(ToVector(pos), out SObject? obj) ? obj : null;
    }

    /// <summary>
    /// Coloca o acessório sobre o bloco (o Regulador com a regra padrão) e o grava no <c>modData</c>. Retorna false se
    /// não há bloco, se ele já tem um acessório ou se o acessório não serve para ele (README "Acessórios").
    /// </summary>
    public bool TrySetAccessory(GameLocation location, TilePos pos, AccessoryKind kind)
    {
        LogisticsGrid? grid = this.World.Get(KeyOf(location));
        SObject? obj = GetObject(location, pos);
        if (grid == null || obj == null || !grid.TrySetAccessory(pos, kind))
            return false;

        NodeFactory.WriteAccessory(obj, kind, (grid.Get(pos) as InserterNode)?.Regulator);
        return true;
    }

    /// <summary>Tira o acessório do bloco e do <c>modData</c>. Retorna o acessório removido (<see cref="AccessoryKind.None"/> se não havia).</summary>
    public AccessoryKind RemoveAccessory(GameLocation location, TilePos pos)
    {
        AccessoryKind removed = this.World.Get(KeyOf(location))?.RemoveAccessory(pos) ?? AccessoryKind.None;
        if (removed != AccessoryKind.None && GetObject(location, pos) is { } obj)
            NodeFactory.ClearAccessory(obj);
        return removed;
    }

    /// <summary>Grava no <c>modData</c> a regra do Regulador do Inseridor (depois de a interface alterá-la).</summary>
    public void SaveRegulatorRule(GameLocation location, TilePos pos)
    {
        if (this.GetNode(location, pos) is InserterNode { Regulator: { } rule } && GetObject(location, pos) is { } obj)
            NodeFactory.WriteRegulatorRule(obj, rule);
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
        this.RestoreAccessory(location, tile, obj, grid, pos);
        return true;
    }

    /// <summary>
    /// Recria as Esteiras Subterrâneas do local a partir do <c>modData</c> dele (plano mestre D19). Uma posição que não
    /// aceita mais Esteira Subterrânea (ex.: um Túnel em cima) é descartada, com aviso no log. Retorna quantas recriou.
    /// </summary>
    private int LoadSegments(GameLocation location)
    {
        if (!location.modData.TryGetValue(ModDataKeys.UndergroundSegments, out string? json))
            return 0;

        string key = KeyOf(location);
        LogisticsGrid grid = this.World.GetOrCreate(key);
        int created = 0;
        int skipped = 0;
        foreach ((TilePos pos, Direction facing) in UndergroundLayoutSerializer.Deserialize(json))
        {
            if (!grid.CanPlaceSegment(pos))
            {
                skipped++;
                continue;
            }

            grid.AddUnderground(new UndergroundSegmentNode(pos, facing));
            created++;
        }

        if (created > 0)
            this.locations[key] = location;
        if (skipped > 0)
        {
            this.monitor.Log($"{skipped} Esteira(s) Subterrânea(s) inválida(s) em {key} foram descartadas.", LogLevel.Warn);
            SaveSegments(location, grid);
        }

        return created;
    }

    /// <summary>Grava as Esteiras Subterrâneas da grade no <c>modData</c> do local (sem nenhuma, apaga a chave).</summary>
    private static void SaveSegments(GameLocation location, LogisticsGrid grid)
    {
        if (grid.UndergroundNodes.Any())
            location.modData[ModDataKeys.UndergroundSegments] = UndergroundLayoutSerializer.Serialize(grid);
        else
            location.modData.Remove(ModDataKeys.UndergroundSegments);
    }

    /// <summary>Coloca no nó recém-criado o acessório gravado no <c>modData</c> (ao carregar o save).</summary>
    private void RestoreAccessory(GameLocation location, Vector2 tile, SObject obj, LogisticsGrid grid, TilePos pos)
    {
        AccessoryKind kind = NodeFactory.ReadAccessory(obj);
        if (kind == AccessoryKind.None)
            return;

        RegulatorRule? rule = kind == AccessoryKind.Regulator ? NodeFactory.ReadRegulatorRule(obj) : null;
        if (grid.TrySetAccessory(pos, kind, rule))
            return;

        // Não deveria acontecer (o modData só é gravado quando a grade aceita o acessório): o acessório cai no chão.
        this.monitor.Log($"Acessório {kind} inválido no bloco {obj.ItemId} em {KeyOf(location)} {pos}; ele foi solto no chão.", LogLevel.Warn);
        NodeFactory.ClearAccessory(obj);
        DropItems(location, tile, new[] { ItemIds.CreateAccessory(kind) });
    }

    /// <summary>Remove o nó e solta no chão os itens que ele carregava e o acessório (plano mestre D9).</summary>
    private void RemoveNode(GameLocation location, Vector2 tile, SObject obj)
    {
        LogisticsGrid? grid = this.World.Get(KeyOf(location));
        if (grid == null)
            return;

        RemovalResult result = grid.Remove(ToTilePos(tile));
        DropItems(location, tile, result.Items);
        if (result.Accessory != AccessoryKind.None)
            DropItems(location, tile, new[] { ItemIds.CreateAccessory(result.Accessory) });

        // O bloco recolhido não leva o acessório (que já caiu no chão) nem a regra.
        NodeFactory.ClearAccessory(obj);
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
    public static void DropItems(GameLocation location, Vector2 tile, IEnumerable<ILogisticsItem> items)
    {
        var gameItems = new List<Item>();
        foreach (ILogisticsItem item in items)
        {
            if (item is GameItem gameItem)
                gameItems.Add(gameItem.Item);
        }

        DropItems(location, tile, gameItems);
    }

    /// <summary>Solta os itens do jogo no chão, no centro do tile. Pilhas vazias são ignoradas.</summary>
    public static void DropItems(GameLocation location, Vector2 tile, IEnumerable<Item> items)
    {
        Vector2 pixel = new(tile.X * Game1.tileSize + (Game1.tileSize / 2), tile.Y * Game1.tileSize + (Game1.tileSize / 2));
        foreach (Item item in items)
        {
            if (item.Stack > 0)
                Game1.createItemDebris(item, pixel, -1, location);
        }
    }
}
