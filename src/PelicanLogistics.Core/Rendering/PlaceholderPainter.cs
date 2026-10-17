using System;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;

namespace PelicanLogistics.Core.Rendering;

/// <summary>
/// Pinta os placeholders da alpha (README "Placeholders visuais"). Os sprites de bloco têm 16×16 e
/// apontam para o NORTE; o adaptador gira ao desenhar.
/// </summary>
public static class PlaceholderPainter
{
    public const int SpriteSize = 16;

    /// <summary>Quadrado cinza (claro/médio/escuro conforme o tier) com 1, 2 ou 3 setas brancas para cima.</summary>
    public static PixelBuffer PaintBelt(BeltTier tier)
    {
        (Rgba color, int[] arrowRows) = tier switch
        {
            BeltTier.Basic => (PlaceholderColors.BeltBasic, new[] { 8 }),
            BeltTier.Fast => (PlaceholderColors.BeltFast, new[] { 5, 11 }),
            BeltTier.Express => (PlaceholderColors.BeltExpress, new[] { 3, 8, 13 }),
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Tier de esteira inválido."),
        };

        var buffer = new PixelBuffer(SpriteSize, SpriteSize);
        buffer.FillRect(0, 0, SpriteSize, SpriteSize, color);
        buffer.DrawRect(0, 0, SpriteSize, SpriteSize, PlaceholderColors.Outline);
        foreach (int row in arrowRows)
            buffer.DrawArrow(SpriteSize / 2, row, Direction.North, PlaceholderColors.Mark);
        return buffer;
    }

    /// <summary>Fundo transparente, triângulo laranja apontando para cima; 2 ou 3 pontos no canto para Pilha/Configurável.</summary>
    public static PixelBuffer PaintExtractor(TransferTier tier)
    {
        return PaintTransfer(PlaceholderColors.Extractor, tier);
    }

    /// <summary>Fundo transparente, triângulo azul apontando para cima; 2 ou 3 pontos no canto para Pilha/Configurável.</summary>
    public static PixelBuffer PaintInserter(TransferTier tier)
    {
        return PaintTransfer(PlaceholderColors.Inserter, tier);
    }

    /// <summary>Seta grande branca apontando para cima, em fundo transparente (seta fantasma de colocação).</summary>
    public static PixelBuffer PaintGhostArrow()
    {
        var buffer = new PixelBuffer(SpriteSize, SpriteSize);
        buffer.FillTriangle(8, 1, 2, 8, 14, 8, PlaceholderColors.Mark);
        buffer.FillRect(6, 8, 4, 7, PlaceholderColors.Mark);
        return buffer;
    }

    /// <summary>Ícone de Big Craftable (16×32) com o sprite centralizado na metade de baixo.</summary>
    public static PixelBuffer PaintIcon(PixelBuffer sprite16)
    {
        var icon = new PixelBuffer(SpriteSize, SpriteSize * 2);
        icon.Blit(sprite16, (SpriteSize - sprite16.Width) / 2, SpriteSize + ((SpriteSize - sprite16.Height) / 2));
        return icon;
    }

    private static PixelBuffer PaintTransfer(Rgba color, TransferTier tier)
    {
        int dots = tier switch
        {
            TransferTier.Single => 0,
            TransferTier.Stack => 2,
            TransferTier.Configurable => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Tier inválido."),
        };

        var buffer = new PixelBuffer(SpriteSize, SpriteSize);
        buffer.FillTriangle(8, 0, 0, 15, 16, 15, PlaceholderColors.Outline);
        buffer.FillTriangle(8, 2, 2, 14, 14, 14, color);

        // Pontos 2×2 no canto superior esquerdo, que o triângulo não cobre.
        int[][] dotPositions = { new[] { 1, 1 }, new[] { 4, 1 }, new[] { 1, 4 } };
        for (int i = 0; i < dots; i++)
            buffer.FillRect(dotPositions[i][0], dotPositions[i][1], 2, 2, PlaceholderColors.Mark);
        return buffer;
    }
}
