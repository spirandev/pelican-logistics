using StardewModdingAPI.Utilities;

namespace PelicanLogistics;

/// <summary>Configuração do mod, lida de config.json.</summary>
public sealed class ModConfig
{
    /// <summary>Alpha: todos os itens à venda por 0g na loja do Clint.</summary>
    public bool UnlockAll { get; set; } = true;

    /// <summary>Tecla para girar a direção de colocação ou o bloco sob o cursor.</summary>
    public KeybindList RotateKey { get; set; } = KeybindList.Parse("R");

    /// <summary>Mostra informações de depuração (usado a partir da Fase 6).</summary>
    public bool DebugMode { get; set; } = false;
}
