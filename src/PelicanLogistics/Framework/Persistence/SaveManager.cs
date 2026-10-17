using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Items;
using PelicanLogistics.Core.Machines;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Persistence;
using PelicanLogistics.Core.Simulation;
using PelicanLogistics.Framework.World;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Inventories;

namespace PelicanLogistics.Framework.Persistence;

/// <summary>
/// Salva e carrega o estado em tempo de execução (itens em trânsito, mãos dos Inseridores, slots virtuais).
/// O estado vai em JSON (<c>WriteSaveData</c>); os itens vão num inventário global do jogo, que o jogo salva com
/// segurança, e o JSON guarda o índice de cada um. Fora do salvamento, o inventário fica vazio.
/// </summary>
internal sealed class SaveManager
{
    /// <summary>Inventário global que leva os itens para o arquivo de save.</summary>
    public const string InventoryId = "spirandev.PelicanLogistics/Transit";

    /// <summary>Chave do estado nos dados do mod no save.</summary>
    public const string SaveDataKey = "runtime-state";

    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly WorldRegistry registry;

    public SaveManager(IModHelper helper, IMonitor monitor, WorldRegistry registry)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.registry = registry;
    }

    /// <summary>
    /// <c>GameLoop.Saving</c>: grava o estado e coloca os itens no inventário global. Os itens continuam no Core;
    /// o inventário é só o "veículo" para o arquivo de save. Nunca impede o jogo de salvar.
    /// </summary>
    public void OnSaving()
    {
        if (!Context.IsMainPlayer)
            return;

        try
        {
            var items = new List<Item>();
            WorldRuntimeState state = RuntimeStateSerializer.Export(this.registry.World, item => Register(items, item));
            this.FillInventory(items);
            this.helper.Data.WriteSaveData(SaveDataKey, state);
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Falha ao salvar os itens em trânsito; eles vão para perto da casa da fazenda ao carregar.\n{ex}", LogLevel.Error);
            this.SaveFallback();
        }
    }

    /// <summary><c>GameLoop.Saved</c>: esvazia o inventário global, para nenhuma função do jogo enxergar cópias.</summary>
    public void OnSaved()
    {
        if (!Context.IsMainPlayer)
            return;

        try
        {
            GetInventory().Clear();
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Falha ao esvaziar o inventário de trânsito depois de salvar.\n{ex}", LogLevel.Error);
        }
    }

    /// <summary>
    /// Ao carregar o save, logo depois de <see cref="WorldRegistry.Rebuild"/>: devolve os itens aos nós e aos
    /// slots virtuais. Todo item sem destino é solto no chão; nenhum é perdido em silêncio.
    /// </summary>
    public void Load()
    {
        if (!Context.IsMainPlayer)
            return;

        List<Item?> snapshot;
        try
        {
            Inventory inventory = GetInventory();
            snapshot = new List<Item?>(inventory);
            inventory.Clear();
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Falha ao ler o inventário de trânsito do save.\n{ex}", LogLevel.Error);
            return;
        }

        var used = new HashSet<int>();
        var orphans = new List<OrphanItem>();
        try
        {
            WorldRuntimeState? state = this.helper.Data.ReadSaveData<WorldRuntimeState>(SaveDataKey);
            if (state == null)
            {
                this.DropLeftovers(snapshot, used, "o save tem itens em trânsito, mas não tem o estado deles");
                return;
            }

            RuntimeStateSerializer.Import(this.registry.World, state, index => Resolve(snapshot, used, index), orphans);
        }
        catch (Exception ex)
        {
            // Descarta o que já foi restaurado (recriando os nós vazios) e devolve tudo ao chão: nada duplica.
            this.monitor.Log($"Falha ao restaurar os itens em trânsito; eles foram soltos perto da casa da fazenda.\n{ex}", LogLevel.Error);
            this.registry.Rebuild();
            this.DropNearFarmhouse(snapshot.Where(item => item != null).Cast<Item>());
            return;
        }

        this.DropOrphans(orphans);
        this.RemoveOrphanMachineSlots();
        this.DropLeftovers(snapshot, used, "itens do save não pertencem a nenhum bloco");
    }

    private static Inventory GetInventory()
    {
        return Game1.player.team.GetOrCreateGlobalInventory(InventoryId);
    }

    private static int Register(List<Item> items, ILogisticsItem item)
    {
        if (item is not GameItem gameItem)
            throw new InvalidOperationException($"Item em trânsito que não é do jogo: {item.DisplayName}.");

        items.Add(gameItem.Item);
        return items.Count - 1;
    }

    /// <summary>O item do índice, ou null se não existe ou está vazio. Marca o índice como usado.</summary>
    private static ILogisticsItem? Resolve(List<Item?> snapshot, HashSet<int> used, int index)
    {
        if (index < 0 || index >= snapshot.Count || snapshot[index] is not { Stack: > 0 } item)
            return null;

        used.Add(index);
        return new GameItem(item);
    }

    private void FillInventory(IEnumerable<Item> items)
    {
        Inventory inventory = GetInventory();
        inventory.Clear();
        foreach (Item item in items)
            inventory.Add(item);
    }

    /// <summary>
    /// Se a exportação falhar: todos os itens do Core vão para o inventário, sem estado. Ao carregar, eles são
    /// soltos perto da casa da fazenda.
    /// </summary>
    private void SaveFallback()
    {
        try
        {
            this.FillInventory(AllItems(this.registry.World));
            this.helper.Data.WriteSaveData<WorldRuntimeState>(SaveDataKey, null);
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Falha também ao guardar os itens em trânsito sem o estado.\n{ex}", LogLevel.Error);
        }
    }

    /// <summary>Todos os itens do Core: espaços dos nós e slots virtuais.</summary>
    private static IEnumerable<Item> AllItems(LogisticsWorld world)
    {
        foreach (LogisticsGrid grid in world.Grids.Select(pair => pair.Value))
        {
            foreach (LogisticsNode node in grid.Nodes)
            {
                foreach (TransitSlot slot in node.Slots)
                {
                    if (slot.Item is GameItem item)
                        yield return item.Item;
                }
            }

            foreach (VirtualSlots slots in grid.MachineSlots.Entries.Select(entry => entry.Value))
            {
                foreach (ILogisticsItem stored in slots.AllItems())
                {
                    if (stored is GameItem item)
                        yield return item.Item;
                }
            }
        }
    }

    /// <summary>Itens sem destino (nó removido, tipo diferente...) caem no chão onde estavam.</summary>
    private void DropOrphans(List<OrphanItem> orphans)
    {
        if (orphans.Count == 0)
            return;

        this.monitor.Log($"{orphans.Count} pilha(s) em trânsito sem bloco ao carregar; soltas no chão onde estavam.", LogLevel.Warn);
        foreach (IGrouping<string, OrphanItem> group in orphans.GroupBy(o => o.LocationKey))
        {
            foreach (IGrouping<TilePos, OrphanItem> tile in group.GroupBy(o => o.Position))
                this.Drop(group.Key, tile.Key, tile.Select(o => o.Item));
        }
    }

    /// <summary>B3: slots virtuais de máquinas que não existem mais são removidos e os itens caem no chão.</summary>
    private void RemoveOrphanMachineSlots()
    {
        foreach ((string key, LogisticsGrid grid) in this.registry.World.Grids.ToList())
        {
            List<TilePos> positions = grid.MachineSlots.Entries.Select(e => e.Key).ToList();
            if (positions.Count == 0)
                continue;

            GameLocation? location = this.registry.FindLocation(key);
            var endpoints = location != null ? new GameEndpointProvider(location) : null;
            foreach (TilePos pos in positions)
            {
                if (endpoints?.GetEndpoint(pos) is IMachineEndpoint)
                    continue;

                this.monitor.Log($"Slots virtuais em {key} {pos} sem máquina ao carregar; itens soltos no chão.", LogLevel.Warn);
                this.Drop(key, pos, grid.MachineSlots.RemoveMachine(pos));
            }
        }
    }

    /// <summary>Itens do inventário que nenhum slot usou (ou todos, sem estado): perto da casa da fazenda.</summary>
    private void DropLeftovers(List<Item?> snapshot, HashSet<int> used, string reason)
    {
        var leftovers = new List<Item>();
        for (int i = 0; i < snapshot.Count; i++)
        {
            if (!used.Contains(i) && snapshot[i] is { Stack: > 0 } item)
                leftovers.Add(item);
        }

        if (leftovers.Count == 0)
            return;

        this.monitor.Log($"{leftovers.Count} pilha(s) soltas perto da casa da fazenda: {reason}.", LogLevel.Warn);
        this.DropNearFarmhouse(leftovers);
    }

    /// <summary>Solta no tile do local; se o local não existe mais, perto da casa da fazenda.</summary>
    private void Drop(string locationKey, TilePos pos, IEnumerable<ILogisticsItem> items)
    {
        GameLocation? location = this.registry.FindLocation(locationKey);
        if (location != null)
        {
            WorldRegistry.DropItems(location, WorldRegistry.ToVector(pos), items);
            return;
        }

        this.monitor.Log($"Local {locationKey} não existe mais; itens soltos perto da casa da fazenda.", LogLevel.Warn);
        this.DropNearFarmhouse(items.OfType<GameItem>().Select(i => i.Item));
    }

    private void DropNearFarmhouse(IEnumerable<Item> items)
    {
        Farm farm = Game1.getFarm();
        Point entry = farm.GetMainFarmHouseEntry();
        WorldRegistry.DropItems(farm, new Vector2(entry.X, entry.Y), items);
    }
}
