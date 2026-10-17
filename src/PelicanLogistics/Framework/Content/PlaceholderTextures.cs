using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PelicanLogistics.Core.Nodes;
using PelicanLogistics.Core.Rendering;
using StardewValley;

namespace PelicanLogistics.Framework.Content;

/// <summary>
/// Texturas placeholder geradas em tempo de execução a partir do <see cref="PlaceholderPainter"/> do Core.
/// </summary>
/// <remarks>
/// Folha de ícones dos Big Craftables (<see cref="IconSheetAssetName"/>): 8 células por linha, cada uma com
/// 16×32 pixels. Índices:
/// <code>
/// | Índice | Item      |
/// |--------|-----------|
/// | 0      | Esteira   |
/// | 1      | Extrator  |
/// | 2      | Inseridor |
/// </code>
/// As próximas fases acrescentam índices no fim de <see cref="Blocks"/>.
/// </remarks>
internal static class PlaceholderTextures
{
    /// <summary>Nome do asset da folha de ícones usada em <c>Data/BigCraftables</c>.</summary>
    public const string IconSheetAssetName = "Mods/spirandev.PelicanLogistics/BigCraftables";

    private const int SheetColumns = 8;
    private const int CellWidth = PlaceholderPainter.SpriteSize;
    private const int CellHeight = PlaceholderPainter.SpriteSize * 2;

    /// <summary>Blocos na ordem dos índices da folha de ícones, com o sprite de mundo (16×16, apontando para o norte).</summary>
    private static readonly (string ItemId, Func<PixelBuffer> Paint)[] Blocks =
    {
        (ItemIds.Belt, () => PlaceholderPainter.PaintBelt(BeltTier.Basic)),
        (ItemIds.Extractor, () => PlaceholderPainter.PaintExtractor(TransferTier.Single)),
        (ItemIds.Inserter, () => PlaceholderPainter.PaintInserter(TransferTier.Single)),
    };

    private static readonly Dictionary<string, Texture2D> WorldSprites = new();
    private static Texture2D? ghostArrow;

    /// <summary>Seta da colocação (16×16, apontando para o norte).</summary>
    public static Texture2D GhostArrow => ghostArrow ??= ToTexture(PlaceholderPainter.PaintGhostArrow());

    /// <summary>Índice do item na folha de ícones.</summary>
    public static int SpriteIndexOf(string itemId)
    {
        int index = Array.FindIndex(Blocks, b => b.ItemId == itemId);
        if (index < 0)
            throw new ArgumentException($"Item sem sprite: {itemId}.", nameof(itemId));
        return index;
    }

    /// <summary>
    /// Cria a folha de ícones. Chamado pelo carregamento do asset: devolve sempre uma textura nova, porque
    /// a textura carregada pertence ao gerenciador de conteúdo do jogo (que pode descartá-la).
    /// </summary>
    public static Texture2D BuildIconSheet()
    {
        int rows = (Blocks.Length + SheetColumns - 1) / SheetColumns;
        var sheet = new PixelBuffer(SheetColumns * CellWidth, rows * CellHeight);
        for (int i = 0; i < Blocks.Length; i++)
        {
            PixelBuffer icon = PlaceholderPainter.PaintIcon(Blocks[i].Paint());
            sheet.Blit(icon, (i % SheetColumns) * CellWidth, (i / SheetColumns) * CellHeight);
        }

        return ToTexture(sheet);
    }

    /// <summary>Sprite de mundo (16×16, apontando para o norte) do bloco. Criado no primeiro uso e guardado em cache.</summary>
    public static Texture2D GetWorldSprite(string itemId)
    {
        if (!WorldSprites.TryGetValue(itemId, out Texture2D? texture))
        {
            Func<PixelBuffer> paint = Blocks.First(b => b.ItemId == itemId).Paint;
            texture = ToTexture(paint());
            WorldSprites[itemId] = texture;
        }

        return texture;
    }

    /// <summary>Converte um <see cref="PixelBuffer"/> em textura.</summary>
    public static Texture2D ToTexture(PixelBuffer buffer)
    {
        var data = new Color[buffer.Pixels.Length];
        for (int i = 0; i < data.Length; i++)
        {
            Rgba c = buffer.Pixels[i];
            data[i] = Color.FromNonPremultiplied(c.R, c.G, c.B, c.A);
        }

        var texture = new Texture2D(Game1.graphics.GraphicsDevice, buffer.Width, buffer.Height);
        texture.SetData(data);
        return texture;
    }
}
