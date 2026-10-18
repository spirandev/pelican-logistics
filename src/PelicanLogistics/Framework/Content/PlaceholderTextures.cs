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
/// | Índice | Item            |
/// |--------|-----------------|
/// | 0      | Esteira         |
/// | 1      | Inseridor       |
/// | 2      | Filtro Comum    |
/// | 3      | Filtro Completo |
/// | 4      | Divisor         |
/// | 5      | Esteira Rápida  |
/// | 6      | Esteira Expressa |
/// | 7      | Inseridor de Pilha |
/// | 8      | Inseridor Configurável |
/// | 9      | Esteira Subterrânea (ícone da entrada) |
/// </code>
/// Novos blocos acrescentam índices no fim de <see cref="Blocks"/>. A folha dos acessórios (<see cref="ObjectSheetAssetName"/>,
/// <c>Data/Objects</c>) tem células de 16×16: 0 = Ponte, 1 = Regulador.
/// </remarks>
internal static class PlaceholderTextures
{
    /// <summary>Nome do asset da folha de ícones usada em <c>Data/BigCraftables</c>.</summary>
    public const string IconSheetAssetName = "Mods/spirandev.PelicanLogistics/BigCraftables";

    /// <summary>Nome do asset da folha de ícones dos acessórios, usada em <c>Data/Objects</c>.</summary>
    public const string ObjectSheetAssetName = "Mods/spirandev.PelicanLogistics/Objects";

    private const int SheetColumns = 8;
    private const int CellWidth = PlaceholderPainter.SpriteSize;
    private const int CellHeight = PlaceholderPainter.SpriteSize * 2;

    /// <summary>Blocos na ordem dos índices da folha de ícones, com o sprite de mundo (16×16, apontando para o norte).</summary>
    private static readonly (string ItemId, Func<PixelBuffer> Paint)[] Blocks =
    {
        (ItemIds.Belt, () => PlaceholderPainter.PaintBelt(BeltTier.Basic)),
        (ItemIds.Inserter, () => PlaceholderPainter.PaintInserter(TransferTier.Single)),
        (ItemIds.FilterCommon, () => PlaceholderPainter.PaintFilter(false)),
        (ItemIds.FilterComplete, () => PlaceholderPainter.PaintFilter(true)),
        (ItemIds.Splitter, () => PlaceholderPainter.PaintSplitter()),
        (ItemIds.BeltFast, () => PlaceholderPainter.PaintBelt(BeltTier.Fast)),
        (ItemIds.BeltExpress, () => PlaceholderPainter.PaintBelt(BeltTier.Express)),
        (ItemIds.InserterStack, () => PlaceholderPainter.PaintInserter(TransferTier.Stack)),
        (ItemIds.InserterConfigurable, () => PlaceholderPainter.PaintInserter(TransferTier.Configurable)),
        (ItemIds.UndergroundBelt, () => PlaceholderPainter.PaintUnderground(UndergroundRole.Entrance)),
    };

    /// <summary>Acessórios na ordem dos índices da folha dos acessórios.</summary>
    private static readonly (string ItemId, AccessoryKind Kind)[] Accessories =
    {
        (ItemIds.Bridge, AccessoryKind.Bridge),
        (ItemIds.Regulator, AccessoryKind.Regulator),
    };

    private static readonly Dictionary<string, Texture2D> WorldSprites = new();
    private static readonly Dictionary<(BeltTier Tier, bool FromLeft), Texture2D> BeltCurveSprites = new();
    private static readonly Dictionary<UndergroundRole, Texture2D> UndergroundSprites = new();
    private static Texture2D? ghostArrow;
    private static Texture2D? pixel;
    private static Texture2D? bridgeOverlay;
    private static Texture2D? regulatorOverlay;

    /// <summary>Seta da colocação (16×16, apontando para o norte).</summary>
    public static Texture2D GhostArrow => ghostArrow ??= ToTexture(PlaceholderPainter.PaintGhostArrow());

    /// <summary>1×1 pixel branco, esticado e colorido ao desenhar (braço e mão do Inseridor).</summary>
    public static Texture2D Pixel => pixel ??= CreatePixel();

    /// <summary>Ponte desenhada sobre a esteira (16×16, faixa horizontal, sem girar).</summary>
    public static Texture2D BridgeOverlay => bridgeOverlay ??= ToTexture(PlaceholderPainter.PaintBridge());

    /// <summary>Regulador desenhado sobre o Inseridor (16×16, quadrado pequeno no canto, sem girar).</summary>
    public static Texture2D RegulatorOverlay => regulatorOverlay ??= ToTexture(PlaceholderPainter.PaintRegulator());

    /// <summary>Índice do acessório na folha dos acessórios.</summary>
    public static int ObjectSpriteIndexOf(string itemId)
    {
        int index = Array.FindIndex(Accessories, a => a.ItemId == itemId);
        if (index < 0)
            throw new ArgumentException($"Acessório sem sprite: {itemId}.", nameof(itemId));
        return index;
    }

    /// <summary>Cria a folha dos acessórios (uma linha de células 16×16). Como a folha de ícones, sempre uma textura nova.</summary>
    public static Texture2D BuildObjectSheet()
    {
        var sheet = new PixelBuffer(Accessories.Length * PlaceholderPainter.SpriteSize, PlaceholderPainter.SpriteSize);
        for (int i = 0; i < Accessories.Length; i++)
            sheet.Blit(PlaceholderPainter.PaintAccessoryIcon(Accessories[i].Kind), i * PlaceholderPainter.SpriteSize, 0);

        return ToTexture(sheet);
    }

    /// <summary>Sprite de mundo da Esteira Subterrânea (16×16, apontando para o norte) pelo papel. Guardado em cache.</summary>
    public static Texture2D GetUndergroundSprite(UndergroundRole role)
    {
        if (!UndergroundSprites.TryGetValue(role, out Texture2D? texture))
        {
            texture = ToTexture(PlaceholderPainter.PaintUnderground(role));
            UndergroundSprites[role] = texture;
        }

        return texture;
    }

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

    /// <summary>
    /// Sprite da esteira em curva (16×16, apontando para o norte, recebendo pelo oeste se 'fromLeft', senão pelo leste).
    /// Criado no primeiro uso e guardado em cache.
    /// </summary>
    public static Texture2D GetBeltCurveSprite(BeltTier tier, bool fromLeft)
    {
        if (!BeltCurveSprites.TryGetValue((tier, fromLeft), out Texture2D? texture))
        {
            texture = ToTexture(PlaceholderPainter.PaintBeltCurve(tier, fromLeft));
            BeltCurveSprites[(tier, fromLeft)] = texture;
        }

        return texture;
    }

    private static Texture2D CreatePixel()
    {
        var texture = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
        texture.SetData(new[] { Color.White });
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
