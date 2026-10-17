using PelicanLogistics.Core.Geometry;

namespace PelicanLogistics.Core.Endpoints;

/// <summary>Dá acesso ao que está ao redor dos nós (baús, máquinas) em um local do jogo.</summary>
public interface IEndpointProvider
{
    /// <summary>Endpoint no tile, ou null. Nunca retorna nós do próprio mod.</summary>
    IEndpoint? GetEndpoint(TilePos position);
}
