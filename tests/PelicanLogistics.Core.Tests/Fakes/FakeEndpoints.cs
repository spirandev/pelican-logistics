using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Geometry;

namespace PelicanLogistics.Core.Tests.Fakes;

/// <summary>Endpoints para testes: um dicionário de posição para endpoint.</summary>
public sealed class FakeEndpoints : IEndpointProvider
{
    public Dictionary<TilePos, IEndpoint> Endpoints { get; } = new();

    public IEndpoint? GetEndpoint(TilePos position)
    {
        return this.Endpoints.TryGetValue(position, out IEndpoint? endpoint) ? endpoint : null;
    }
}
