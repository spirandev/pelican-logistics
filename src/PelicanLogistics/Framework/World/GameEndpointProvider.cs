using Microsoft.Xna.Framework;
using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Geometry;
using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.Objects;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework.World;

/// <summary>Endpoints (baús e máquinas) de um local do jogo.</summary>
internal sealed class GameEndpointProvider : IEndpointProvider
{
    private readonly GameLocation location;

    public GameEndpointProvider(GameLocation location)
    {
        this.location = location;
    }

    /// <summary>Baú suportado → baú; bloco nosso, Covo ou objeto sem <c>Data/Machines</c> → nada; senão → máquina.</summary>
    public IEndpoint? GetEndpoint(TilePos position)
    {
        if (!this.location.objects.TryGetValue(new Vector2(position.X, position.Y), out SObject? obj) || obj == null)
            return null;

        if (obj is Chest chest)
            return ChestEndpoint.IsSupported(chest) ? new ChestEndpoint(chest) : null;

        if (!MachineEndpoint.IsSupported(obj))
            return null;

        MachineData? data = obj.GetMachineData();
        return data != null ? new MachineEndpoint(obj, this.location, data) : null;
    }
}
