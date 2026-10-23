using StardewValley;

namespace PelicanLogistics.Framework.World;

/// <summary>
/// Capacete de Engenheiro (README "Capacete de Engenheiro"; plano mestre D18): na cabeça ou na mão, deixa <b>ver</b> o que
/// é escondido; só na cabeça, deixa <b>construir</b> embaixo da terra (a mão segura o item).
/// </summary>
internal static class EngineerHelmet
{
    /// <summary>Capacete na cabeça.</summary>
    public static bool IsWorn => ItemIds.IsEngineerHelmet(Game1.player?.hat.Value);

    /// <summary>Capacete na cabeça ou selecionado na barra de itens.</summary>
    public static bool IsActive => IsWorn || ItemIds.IsEngineerHelmet(Game1.player?.CurrentItem);
}
