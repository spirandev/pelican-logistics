using Microsoft.Xna.Framework;
using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Geometry;
using StardewValley;
using StardewValley.Objects;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework.World;

/// <summary>Endpoints (baús) de um local do jogo.</summary>
internal sealed class GameEndpointProvider : IEndpointProvider
{
    private readonly GameLocation location;

    public GameEndpointProvider(GameLocation location)
    {
        this.location = location;
    }

    public IEndpoint? GetEndpoint(TilePos position)
    {
        if (!this.location.objects.TryGetValue(new Vector2(position.X, position.Y), out SObject? obj) || obj == null)
            return null;

        if (obj is Chest chest && ChestEndpoint.IsSupported(chest))
            return new ChestEndpoint(chest);

        // Blocos do mod nunca são endpoints; máquinas entram na Fase 2.
        return null;
    }
}
